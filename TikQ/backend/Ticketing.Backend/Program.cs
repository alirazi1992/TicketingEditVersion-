using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Security.Principal;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Api.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using Ticketing.Backend.Application.Common;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Auth;
using Ticketing.Backend.Infrastructure.Data;
using Ticketing.Backend.Infrastructure.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// =======================
// IIS-safe startup logging: ensure logs dir exists and helper to write to logs/startup.log
// =======================
var contentRoot = builder.Environment.ContentRootPath;
var logsDir = Path.Combine(contentRoot, "logs");
var startupLogPath = Path.Combine(logsDir, "startup.log");
if (!Directory.Exists(logsDir))
{
    try { Directory.CreateDirectory(logsDir); } catch { /* best effort */ }
}
void LogStartup(string message)
{
    var line = $"[{DateTime.UtcNow:O}] {message}";
    Console.WriteLine(line);
    try
    {
        File.AppendAllText(startupLogPath, line + Environment.NewLine);
    }
    catch { /* do not crash on file write */ }
}

// Log any unhandled exception so we do not crash silently (IIS 500.30 diagnosis)
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    try
    {
        var ex = e.ExceptionObject as Exception;
        var text = ex != null ? ex.ToString() : e.ExceptionObject?.ToString() ?? "Unknown";
        LogStartup($"[STARTUP] Unhandled exception: {text}");
    }
    catch { /* best effort */ }
};

// =======================
// Configure URL/Port for local dev
// =======================
// Ensure backend listens on http://localhost:5000 for local development
// This can be overridden via ASPNETCORE_URLS environment variable
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]) && 
    builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

// =======================
// JWT configuration (IIS/production-safe: env → appsettings.json → appsettings.Production.json)
// =======================
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("Jwt").Bind(jwtSettings);

// Resolve secret: env vars (Jwt__Secret, TikQ_JWT_SECRET) then config. No hardcoded production secret.
string? resolvedSecret =
    Environment.GetEnvironmentVariable("Jwt__Secret")
    ?? Environment.GetEnvironmentVariable("TikQ_JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"];

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(resolvedSecret) || resolvedSecret.Length < 32)
    {
        throw new InvalidOperationException(
            "JWT secret is not configured for Production. Set Jwt__Secret environment variable."
        );
    }
}
else
{
    if (string.IsNullOrWhiteSpace(resolvedSecret))
    {
        LogStartup("[STARTUP] Using development-only JWT secret.");
        resolvedSecret = "DEV_ONLY_SECRET_CHANGE_BEFORE_PRODUCTION_123456";
    }
}

LogStartup($"[STARTUP] JWT secret configured (Production: require env Jwt__Secret; Development: env or dev fallback)");

jwtSettings.Secret = resolvedSecret;
builder.Services.AddSingleton(jwtSettings);

// =======================
// Company directory (read-only identity from Company DB)
// =======================
var companyDirectoryOptions = new Ticketing.Backend.Infrastructure.CompanyDirectory.CompanyDirectoryOptions();
builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.CompanyDirectory.CompanyDirectoryOptions.SectionName).Bind(companyDirectoryOptions);
builder.Services.AddSingleton(companyDirectoryOptions);

// =======================
// Windows Auth: enable Negotiate only when explicitly configured (default false)
// =======================
builder.Services.Configure<Ticketing.Backend.Infrastructure.Auth.WindowsAuthOptions>(
    builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Auth.WindowsAuthOptions.SectionName));

// =======================
// Break-glass Emergency Admin (separate login route; only when Enabled)
// =======================
builder.Services.Configure<Ticketing.Backend.Infrastructure.Auth.EmergencyAdminOptions>(
    builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Auth.EmergencyAdminOptions.SectionName));

// =======================
// Auth cookies (SameSite, Secure) for IIS/reverse proxy
// =======================
builder.Services.Configure<Ticketing.Backend.Infrastructure.Auth.AuthCookiesOptions>(
    builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Auth.AuthCookiesOptions.SectionName));

// =======================
// Windows user map (DOMAIN\username -> email; config-only, no LDAP yet)
// =======================
var windowsUserMapOptions = new Ticketing.Backend.Infrastructure.Auth.WindowsUserMapOptions();
var windowsUserMapSection = builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Auth.WindowsUserMapOptions.SectionName);
foreach (var child in windowsUserMapSection.GetChildren())
{
    var value = child.Value;
    if (!string.IsNullOrEmpty(value))
        windowsUserMapOptions.Map[child.Key] = value;
}
builder.Services.AddSingleton(windowsUserMapOptions);
builder.Services.AddSingleton<Ticketing.Backend.Infrastructure.Auth.IWindowsUserMapResolver, Ticketing.Backend.Infrastructure.Auth.WindowsUserMapResolver>();

// Active Directory (LDAP) user lookup for Windows Integrated Auth (Windows only; elsewhere uses NullAdUserLookup)
var activeDirectoryOptions = new Ticketing.Backend.Infrastructure.Auth.ActiveDirectoryOptions();
builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Auth.ActiveDirectoryOptions.SectionName).Bind(activeDirectoryOptions);
builder.Services.AddSingleton(activeDirectoryOptions);
if (OperatingSystem.IsWindows())
    builder.Services.AddScoped<Ticketing.Backend.Application.Common.Interfaces.IAdUserLookup, Ticketing.Backend.Infrastructure.Auth.LdapAdUserLookup>();
else
    builder.Services.AddScoped<Ticketing.Backend.Application.Common.Interfaces.IAdUserLookup, Ticketing.Backend.Infrastructure.Auth.NullAdUserLookup>();

// =======================
// DbContext: strongly-typed DatabaseOptions (Sqlite | SqlServer), fail-fast in Production
// =======================
var databaseOptions = new Ticketing.Backend.Infrastructure.Data.DatabaseOptions();
builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Data.DatabaseOptions.SectionName).Bind(databaseOptions);
var dbSection = builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Data.DatabaseOptions.SectionName);
if (!dbSection.GetSection("AutoMigrateOnStartup").Exists())
    databaseOptions.AutoMigrateOnStartup = builder.Environment.IsDevelopment();

var bootstrapOptions = new Ticketing.Backend.Infrastructure.Data.BootstrapOptions();
builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Data.BootstrapOptions.SectionName).Bind(bootstrapOptions);
var bootstrapSection = builder.Configuration.GetSection(Ticketing.Backend.Infrastructure.Data.BootstrapOptions.SectionName);
if (builder.Environment.IsProduction() && !bootstrapSection.GetSection("Enabled").Exists())
    bootstrapOptions.Enabled = false;

var providerConfigSource = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Database__Provider"))
    ? "Environment (Database__Provider)"
    : "appsettings (Database:Provider)";
LogStartup($"[STARTUP] Database provider: {databaseOptions.Provider} (source: {providerConfigSource}), AutoMigrateOnStartup: {databaseOptions.AutoMigrateOnStartup}");

// Fail-fast: unknown provider (NormalizedProvider throws)
try
{
    _ = databaseOptions.NormalizedProvider;
}
catch (InvalidOperationException ex)
{
    throw new InvalidOperationException(ex.Message);
}

// Fail-fast: Production requires explicit provider (env Database__Provider) so IIS never accidentally uses default Sqlite
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Database__Provider")))
{
    throw new InvalidOperationException(
        "Production requires explicit database provider. Set Database__Provider environment variable to 'SqlServer' or 'Sqlite' (e.g. via deploy-iis.ps1 or IIS Application Pool environment variables).");
}

// Fail-fast: Production + SqlServer + missing/empty connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsProduction() && databaseOptions.IsSqlServer && string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Production requires a valid database connection. Database:Provider is SqlServer but ConnectionStrings:DefaultConnection is missing or empty. " +
        "Set ConnectionStrings__DefaultConnection (e.g. via IIS environment variable or appsettings.Production.json) or set Database__Provider=Sqlite to use SQLite.");
}

var isSqlServer = databaseOptions.IsSqlServer;
string? sqliteDbPath = null;
string databasePathOrSummary;

if (isSqlServer)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Database:Provider is SqlServer but ConnectionStrings:DefaultConnection is missing or empty. " +
            "Set ConnectionStrings__DefaultConnection (e.g. via environment variable) or set Provider=Sqlite to use SQLite.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure();
            sql.UseQuerySplittingBehavior(Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery);
        }));
    try
    {
        var csb = new SqlConnectionStringBuilder(connectionString);
        databasePathOrSummary = $"Server={csb.DataSource};Database={csb.InitialCatalog}";
    }
    catch
    {
        databasePathOrSummary = "SqlServer (connection string not parsed)";
    }
    LogStartup($"[STARTUP] Database provider: SqlServer. Connection summary (no secrets): {databasePathOrSummary}");
}
else
{
    sqliteDbPath = ResolveSqliteDbPath(builder.Configuration, builder.Environment.ContentRootPath);
    var sqliteConnectionString = $"Data Source={sqliteDbPath}";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));
    databasePathOrSummary = Path.GetFullPath(sqliteDbPath);
    var sqliteDir = Path.GetDirectoryName(sqliteDbPath);
    if (!string.IsNullOrEmpty(sqliteDir) && !Directory.Exists(sqliteDir))
    {
        try { Directory.CreateDirectory(sqliteDir); } catch { /* best effort */ }
    }
    LogStartup($"[STARTUP] Database provider: Sqlite. Resolved SQLite DB path (absolute): {databasePathOrSummary}");
}

builder.Services.AddSingleton(databaseOptions);
builder.Services.AddSingleton(bootstrapOptions);

// =======================
// DataProtection: persist keys to file system (IIS-safe; avoid EphemeralXmlRepository)
// =======================
var dataProtectionKeysPath = Path.Combine(contentRoot, "App_Data", "keys");
if (!Directory.Exists(dataProtectionKeysPath))
{
    try { Directory.CreateDirectory(dataProtectionKeysPath); } catch { /* best effort */ }
}
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
if (OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

// Helper: Resolve SQLite DB path to absolute path under ContentRoot
static string ResolveSqliteDbPath(IConfiguration config, string contentRoot)
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    
    // Default relative path if not configured
    var relativePath = "App_Data/ticketing.db";
    
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        // Extract file path from "Data Source=<path>" format
        var dataSourcePrefix = "Data Source=";
        if (connectionString.StartsWith(dataSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var extractedPath = connectionString.Substring(dataSourcePrefix.Length).Trim();
            if (!string.IsNullOrWhiteSpace(extractedPath))
            {
                relativePath = extractedPath;
            }
        }
    }
    
    // If already absolute, use as-is
    if (Path.IsPathRooted(relativePath))
    {
        var directory = Path.GetDirectoryName(relativePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return relativePath;
    }
    
    // Convert relative path to absolute under ContentRoot
    var absolutePath = Path.Combine(contentRoot, relativePath);
    var absoluteDirectory = Path.GetDirectoryName(absolutePath);
    
    // Ensure directory exists
    if (!string.IsNullOrEmpty(absoluteDirectory) && !Directory.Exists(absoluteDirectory))
    {
        Directory.CreateDirectory(absoluteDirectory);
    }
    
    return absolutePath;
}

// =======================
// Schema Guard: Ensures SubcategoryFieldDefinitions table has all required columns
// =======================
static async Task EnsureSubcategoryFieldDefinitionsSchemaAsync(
    AppDbContext context,
    ILogger logger,
    string dbPath)
{
    try
    {
        logger.LogInformation("[SCHEMA_GUARD] Verifying SubcategoryFieldDefinitions table schema...");
        
        // Use PRAGMA table_info to check existing columns
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }
        
        // First, check if table exists
        bool tableExists = false;
        using (var checkTableCommand = connection.CreateCommand())
        {
            checkTableCommand.CommandText = @"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name='SubcategoryFieldDefinitions';
            ";
            var result = await checkTableCommand.ExecuteScalarAsync();
            tableExists = result != null;
        }
        
        if (!tableExists)
        {
            logger.LogWarning("[SCHEMA_GUARD] Table SubcategoryFieldDefinitions does not exist. Migrations should have created it.");
            logger.LogWarning("[SCHEMA_GUARD] Attempting to create table with correct schema...");
            
            // Create the table with all required columns
            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS SubcategoryFieldDefinitions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SubcategoryId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Label TEXT NOT NULL,
                    Key TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    IsRequired INTEGER NOT NULL DEFAULT 0,
                    DefaultValue TEXT,
                    OptionsJson TEXT,
                    Min REAL,
                    Max REAL
                );
            ";
            
            try
            {
                await context.Database.ExecuteSqlRawAsync(createTableSql);
                logger.LogInformation("[SCHEMA_GUARD] Successfully created SubcategoryFieldDefinitions table");
                
                // Create indexes
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE INDEX IF NOT EXISTS IX_SubcategoryFieldDefinitions_SubcategoryId 
                    ON SubcategoryFieldDefinitions(SubcategoryId);
                ");
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_SubcategoryFieldDefinitions_SubcategoryId_Key 
                    ON SubcategoryFieldDefinitions(SubcategoryId, Key);
                ");
                
                logger.LogInformation("[SCHEMA_GUARD] Created indexes for SubcategoryFieldDefinitions table");
            }
            catch (Exception createEx)
            {
                logger.LogError(createEx, "[SCHEMA_GUARD] Failed to create SubcategoryFieldDefinitions table: {Error}", createEx.Message);
            }
            
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
            return;
        }
        
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(SubcategoryFieldDefinitions)";
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(1); // column name is at index 1
                    columns.Add(columnName);
                }
            }
        }

        logger.LogInformation("[SCHEMA_GUARD] Existing columns: {Columns}", string.Join(", ", columns));

        // Check for critical required columns and add them additively (NO table recreation)
        // Critical columns that must exist for the table to function (column name FieldKey for SQL Server reserved keyword compatibility)
        var criticalColumns = new Dictionary<string, string>
        {
            { "Name", "TEXT" },
            { "Label", "TEXT" },
            { "FieldKey", "TEXT" },
            { "Type", "TEXT" },
            { "IsRequired", "INTEGER" },
            { "SubcategoryId", "INTEGER" }
        };
        
        var missingCritical = criticalColumns.Where(c => !columns.Contains(c.Key)).ToList();
        if (missingCritical.Any())
        {
            logger.LogWarning("[SCHEMA_GUARD] Missing critical columns detected: {MissingColumns}", 
                string.Join(", ", missingCritical.Select(c => c.Key)));
            logger.LogWarning("[SCHEMA_GUARD] Applying additive schema fixes (ALTER TABLE ADD COLUMN)...");
            
            // Backup database before making schema changes
            if (File.Exists(dbPath))
            {
                var backupPath = $"{dbPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(dbPath, backupPath, overwrite: true);
                    logger.LogInformation("[SCHEMA_GUARD] Database backed up to: {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    logger.LogWarning(backupEx, "[SCHEMA_GUARD] Failed to create backup: {Error}", backupEx.Message);
                }
            }
            
            // Add missing critical columns additively
            foreach (var missing in missingCritical)
            {
                try
                {
                    var columnDef = missing.Value;
                    var columnName = missing.Key;
                    
                    // For NOT NULL columns, we need to add as nullable first, then backfill
                    var isNotNull = columnName == "Name" || columnName == "Label" || columnName == "FieldKey" || 
                                   columnName == "Type" || columnName == "SubcategoryId" || columnName == "IsRequired";
                    
                    if (isNotNull && (columnName == "Name" || columnName == "Label" || columnName == "FieldKey"))
                    {
                        // Add as nullable first
                        var addColumnSql = $"ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN {columnName} {columnDef};";
                        using (var addCommand = connection.CreateCommand())
                        {
                            addCommand.CommandText = addColumnSql;
                            await addCommand.ExecuteNonQueryAsync();
                        }
                        logger.LogInformation("[SCHEMA_GUARD] Added column {Column} as nullable", columnName);
                        
                        // Backfill: for Name/Label, use FieldKey as default; for FieldKey, use empty string
                        string backfillValue = columnName == "FieldKey" ? "''" : "FieldKey";
                        var backfillSql = $"UPDATE SubcategoryFieldDefinitions SET {columnName} = {backfillValue} WHERE {columnName} IS NULL;";
                        using (var backfillCommand = connection.CreateCommand())
                        {
                            backfillCommand.CommandText = backfillSql;
                            var rowsAffected = await backfillCommand.ExecuteNonQueryAsync();
                            logger.LogInformation("[SCHEMA_GUARD] Backfilled {Column}: {RowsAffected} rows updated", columnName, rowsAffected);
                        }
                    }
                    else if (isNotNull && columnName == "Type")
                    {
                        // Add Type column with default 'Text'
                        var addColumnSql = $"ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN {columnName} {columnDef} DEFAULT 'Text';";
                        using (var addCommand = connection.CreateCommand())
                        {
                            addCommand.CommandText = addColumnSql;
                            await addCommand.ExecuteNonQueryAsync();
                        }
                        logger.LogInformation("[SCHEMA_GUARD] Added column {Column} with default value", columnName);
                    }
                    else if (isNotNull && columnName == "IsRequired")
                    {
                        // Check if column already exists
                        bool columnExists = columns.Contains(columnName);
                        
                        if (!columnExists)
                        {
                            // Column doesn't exist - add with default
                            // Note: SQLite doesn't support NOT NULL in ALTER TABLE ADD COLUMN for existing tables with data
                            // So we add as nullable first, then backfill, then rely on application-level defaults
                            var addColumnSql = $"ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN {columnName} {columnDef} DEFAULT 0;";
                            using (var addCommand = connection.CreateCommand())
                            {
                                addCommand.CommandText = addColumnSql;
                                await addCommand.ExecuteNonQueryAsync();
                            }
                            logger.LogInformation("[SCHEMA_GUARD] Added column {Column} with DEFAULT 0", columnName);
                            
                            // Backfill any existing NULL values (shouldn't be any if table is empty, but be safe)
                            var backfillSql = $"UPDATE SubcategoryFieldDefinitions SET {columnName} = 0 WHERE {columnName} IS NULL;";
                            using (var backfillCommand = connection.CreateCommand())
                            {
                                backfillCommand.CommandText = backfillSql;
                                var rowsAffected = await backfillCommand.ExecuteNonQueryAsync();
                                if (rowsAffected > 0)
                                {
                                    logger.LogInformation("[SCHEMA_GUARD] Backfilled {Column}: {RowsAffected} rows updated", columnName, rowsAffected);
                                }
                            }
                        }
                        else
                        {
                            // Column exists - ensure it has no NULLs
                            logger.LogInformation("[SCHEMA_GUARD] Column {Column} already exists, verifying no NULL values...", columnName);
                            
                            // Backfill any NULL values
                            var backfillSql = $"UPDATE SubcategoryFieldDefinitions SET {columnName} = 0 WHERE {columnName} IS NULL;";
                            using (var backfillCommand = connection.CreateCommand())
                            {
                                backfillCommand.CommandText = backfillSql;
                                var rowsAffected = await backfillCommand.ExecuteNonQueryAsync();
                                if (rowsAffected > 0)
                                {
                                    logger.LogInformation("[SCHEMA_GUARD] Backfilled {Column}: {RowsAffected} rows updated", columnName, rowsAffected);
                                }
                                else
                                {
                                    logger.LogInformation("[SCHEMA_GUARD] Column {Column} has no NULL values", columnName);
                                }
                            }
                            
                            // Note: SQLite doesn't support ALTER COLUMN to add/modify DEFAULT
                            // EF Core's HasDefaultValue(false) configuration should ensure new inserts have a value
                            // If the column was created without a default, EF Core will use the configured default
                        }
                    }
                    else if (isNotNull && columnName == "SubcategoryId")
                    {
                        // SubcategoryId should already exist, but if missing, we can't add it safely
                        logger.LogError("[SCHEMA_GUARD] Cannot add SubcategoryId column - table structure is severely broken");
                        throw new InvalidOperationException("SubcategoryId column is missing - table structure is broken beyond repair");
                    }
                    else
                    {
                        // Nullable column - add directly
                        var addColumnSql = $"ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN {columnName} {columnDef};";
                        using (var addCommand = connection.CreateCommand())
                        {
                            addCommand.CommandText = addColumnSql;
                            await addCommand.ExecuteNonQueryAsync();
                        }
                        logger.LogInformation("[SCHEMA_GUARD] Added nullable column {Column}", columnName);
                    }
                }
                catch (Exception addEx)
                {
                    if (addEx.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
                        addEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation("[SCHEMA_GUARD] Column {Column} already exists (possibly added concurrently)", missing.Key);
                    }
                    else
                    {
                        logger.LogError(addEx, "[SCHEMA_GUARD] Failed to add column {Column}: {Error}", missing.Key, addEx.Message);
                        throw; // Re-throw for critical columns
                    }
                }
            }
            
            logger.LogInformation("[SCHEMA_GUARD] Successfully added all missing critical columns");
            
            // Re-read columns after adding to verify
            columns.Clear();
            using (var verifyCommand = connection.CreateCommand())
            {
                verifyCommand.CommandText = "PRAGMA table_info(SubcategoryFieldDefinitions)";
                using (var reader = await verifyCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader.GetString(1);
                        columns.Add(columnName);
                    }
                }
            }
            logger.LogInformation("[SCHEMA_GUARD] After fixes, table has columns: {Columns}", string.Join(", ", columns));
        }

        // After fixing critical columns, ensure IsRequired column is properly configured
        // SQLite doesn't support ALTER COLUMN to modify defaults, so we need to ensure
        // the column exists with a default, and backfill any NULLs
        if (columns.Contains("IsRequired"))
        {
            try
            {
                // Check for NULL values and backfill
                var nullCheckSql = "SELECT COUNT(*) FROM SubcategoryFieldDefinitions WHERE IsRequired IS NULL;";
                using (var nullCheckCommand = connection.CreateCommand())
                {
                    nullCheckCommand.CommandText = nullCheckSql;
                    var nullCount = Convert.ToInt32(await nullCheckCommand.ExecuteScalarAsync());
                    if (nullCount > 0)
                    {
                        logger.LogWarning("[SCHEMA_GUARD] Found {Count} rows with NULL IsRequired, backfilling...", nullCount);
                        var backfillSql = "UPDATE SubcategoryFieldDefinitions SET IsRequired = 0 WHERE IsRequired IS NULL;";
                        using (var backfillCommand = connection.CreateCommand())
                        {
                            backfillCommand.CommandText = backfillSql;
                            var rowsAffected = await backfillCommand.ExecuteNonQueryAsync();
                            logger.LogInformation("[SCHEMA_GUARD] Backfilled IsRequired: {RowsAffected} rows updated", rowsAffected);
                        }
                    }
                }
                
                // Verify column definition - check if it has a default
                // Note: SQLite PRAGMA table_info doesn't show defaults reliably, but we can check constraints
                logger.LogInformation("[SCHEMA_GUARD] IsRequired column exists and has been verified/backfilled");
            }
            catch (Exception backfillEx)
            {
                logger.LogWarning(backfillEx, "[SCHEMA_GUARD] Error checking/backfilling IsRequired: {Error}", backfillEx.Message);
            }
        }

        // Required columns with their SQL definitions (only nullable ones can be safely added)
        var requiredColumns = new Dictionary<string, string>
        {
            { "DefaultValue", "TEXT" },
            { "OptionsJson", "TEXT" },
            { "Min", "REAL" },
            { "Max", "REAL" }
        };

        var missingColumns = new List<string>();
        foreach (var required in requiredColumns)
        {
            if (!columns.Contains(required.Key))
            {
                missingColumns.Add(required.Key);
            }
        }

        if (missingColumns.Count == 0)
        {
            logger.LogInformation("[SCHEMA_GUARD] All required columns exist - schema is valid");
            
            // Close connection if we opened it
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
            return;
        }

        logger.LogWarning("[SCHEMA_GUARD] Missing columns detected: {MissingColumns}", string.Join(", ", missingColumns));

        // Backup database before making schema changes
        if (File.Exists(dbPath))
        {
            var backupPath = $"{dbPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
            try
            {
                File.Copy(dbPath, backupPath, overwrite: true);
                logger.LogInformation("[SCHEMA_GUARD] Database backed up to: {BackupPath}", backupPath);
            }
            catch (Exception backupEx)
            {
                logger.LogWarning(backupEx, "[SCHEMA_GUARD] Failed to create backup: {Error}", backupEx.Message);
                // Continue anyway - schema fix is important
            }
        }

        // Add missing columns one by one
        foreach (var missingColumn in missingColumns)
        {
            try
            {
                var columnDef = requiredColumns[missingColumn];
                var addColumnSql = $"ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN {missingColumn} {columnDef};";
                
                logger.LogInformation("[SCHEMA_GUARD] Adding missing column: {Column} with definition: {Definition}", 
                    missingColumn, columnDef);
                
                await context.Database.ExecuteSqlRawAsync(addColumnSql);
                
                logger.LogInformation("[SCHEMA_GUARD] Successfully added column: {Column}", missingColumn);
            }
            catch (Exception addEx)
            {
                // Check if column was added by another process or already exists
                if (addEx.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
                    addEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("[SCHEMA_GUARD] Column {Column} already exists (possibly added concurrently)", missingColumn);
                }
                else
                {
                    logger.LogError(addEx, "[SCHEMA_GUARD] Failed to add column {Column}: {Error}", 
                        missingColumn, addEx.Message);
                    // Continue with other columns
                }
            }
        }

        logger.LogInformation("[SCHEMA_GUARD] Schema guard completed");
    }
    catch (Exception ex)
    {
        // Log but don't fail startup - let runtime handle errors
        logger.LogWarning(ex, "[SCHEMA_GUARD] Schema guard encountered an error: {Error}", ex.Message);
    }
}

// =======================
// Schema Guard: Ensures Subcategories table exists (required for foreign key)
// =======================
static async Task EnsureSubcategoriesTableExistsAsync(
    AppDbContext context,
    ILogger logger,
    string dbPath)
{
    try
    {
        logger.LogInformation("[SCHEMA_GUARD] Verifying Subcategories table exists...");
        
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }
        
        // Check if Subcategories table exists
        bool tableExists = false;
        using (var checkTableCommand = connection.CreateCommand())
        {
            checkTableCommand.CommandText = @"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name='Subcategories';
            ";
            var result = await checkTableCommand.ExecuteScalarAsync();
            tableExists = result != null;
        }
        
        if (!tableExists)
        {
            logger.LogWarning("[SCHEMA_GUARD] Subcategories table does not exist. This table is required for SubcategoryFieldDefinitions foreign key.");
            logger.LogWarning("[SCHEMA_GUARD] The table should be created by the InitialCreate migration.");
            logger.LogWarning("[SCHEMA_GUARD] Please ensure all migrations are applied correctly.");
        }
        else
        {
            // Verify Name column exists
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(Subcategories)";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader.GetString(1);
                        columns.Add(columnName);
                    }
                }
            }
            
            if (!columns.Contains("Name"))
            {
                logger.LogError("[SCHEMA_GUARD] Subcategories table exists but is missing 'Name' column!");
                logger.LogError("[SCHEMA_GUARD] This will cause foreign key validation to fail.");
            }
            else
            {
                logger.LogInformation("[SCHEMA_GUARD] Subcategories table exists with required columns");
            }
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking Subcategories table: {Error}", ex.Message);
    }
}

// =======================
// Schema Guard: Ensures Categories.NormalizedName column exists (fixes GET /api/categories on existing DBs)
// =======================
static async Task EnsureCategoriesNormalizedNameColumnExistsAsync(
    AppDbContext context,
    ILogger logger)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        // Check if Categories table exists
        bool tableExists = false;
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Categories';";
            tableExists = (await checkCmd.ExecuteScalarAsync()) != null;
        }

        if (!tableExists)
        {
            logger.LogInformation("[SCHEMA_GUARD] Categories table does not exist. Skipping NormalizedName guard.");
            if (!wasOpen) await connection.CloseAsync();
            return;
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Categories');";
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));
            }
        }

        if (columns.Contains("NormalizedName"))
        {
            logger.LogInformation("[SCHEMA_GUARD] Categories.NormalizedName exists.");
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_NormalizedName ON Categories(NormalizedName);");
            }
            catch { /* index may already exist with different definition */ }
            if (!wasOpen) await connection.CloseAsync();
            return;
        }

        logger.LogWarning("[SCHEMA_GUARD] Categories.NormalizedName missing. Adding column...");
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Categories\" ADD COLUMN \"NormalizedName\" TEXT NULL;");
        logger.LogInformation("[SCHEMA_GUARD] Categories.NormalizedName created.");

        // Backfill: same normalization as CategoryService (trim + lower; matches SQL Server migration)
        var backfillCmd = connection.CreateCommand();
        backfillCmd.CommandText = @"
            UPDATE ""Categories"" SET ""NormalizedName"" = lower(trim(""Name""))
            WHERE ""NormalizedName"" IS NULL OR ""NormalizedName"" = '';
        ";
        var rowsAffected = await backfillCmd.ExecuteNonQueryAsync();
        if (rowsAffected > 0)
            logger.LogInformation("[SCHEMA_GUARD] Backfilled Categories.NormalizedName: {RowsAffected} rows updated.", rowsAffected);

        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_NormalizedName ON Categories(NormalizedName);");

        if (!wasOpen) await connection.CloseAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error ensuring Categories.NormalizedName: {Error}", ex.Message);
    }
}

// =======================
// Schema Guard: Ensures TicketTechnicianAssignments table exists
// =======================
static async Task EnsureTicketTechnicianAssignmentsTableExistsAsync(
    AppDbContext context,
    ILogger logger,
    string dbPath)
{
    try
    {
        logger.LogInformation("[SCHEMA_GUARD] Verifying TicketTechnicianAssignments table exists...");
        
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }
        
        // Check if TicketTechnicianAssignments table exists
        bool tableExists = false;
        using (var checkTableCommand = connection.CreateCommand())
        {
            checkTableCommand.CommandText = @"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name='TicketTechnicianAssignments';
            ";
            var result = await checkTableCommand.ExecuteScalarAsync();
            tableExists = result != null;
        }
        
        if (!tableExists)
        {
            logger.LogWarning("[SCHEMA_GUARD] TicketTechnicianAssignments table does not exist. Creating it...");
            
            // Backup database before making schema changes
            if (File.Exists(dbPath))
            {
                var backupPath = $"{dbPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(dbPath, backupPath, overwrite: true);
                    logger.LogInformation("[SCHEMA_GUARD] Database backed up to: {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    logger.LogWarning(backupEx, "[SCHEMA_GUARD] Failed to create backup: {Error}", backupEx.Message);
                }
            }
            
            // Create the table with the exact schema from the migration (including AcceptedAt)
            var createTableSql = @"
                CREATE TABLE TicketTechnicianAssignments (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TicketId TEXT NOT NULL,
                    TechnicianUserId TEXT NOT NULL,
                    TechnicianId TEXT,
                    AssignedAt TEXT NOT NULL,
                    AssignedByUserId TEXT NOT NULL,
                    AcceptedAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    Role TEXT,
                    UpdatedAt TEXT,
                    FOREIGN KEY (TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE,
                    FOREIGN KEY (TechnicianUserId) REFERENCES Users(Id) ON DELETE RESTRICT,
                    FOREIGN KEY (AssignedByUserId) REFERENCES Users(Id) ON DELETE RESTRICT,
                    FOREIGN KEY (TechnicianId) REFERENCES Technicians(Id) ON DELETE SET NULL
                );
                
                CREATE INDEX IX_TicketTechnicianAssignments_TicketId ON TicketTechnicianAssignments(TicketId);
                CREATE INDEX IX_TicketTechnicianAssignments_TechnicianUserId ON TicketTechnicianAssignments(TechnicianUserId);
                CREATE INDEX IX_TicketTechnicianAssignments_TicketId_TechnicianUserId_IsActive ON TicketTechnicianAssignments(TicketId, TechnicianUserId, IsActive);
            ";
            
            try
            {
                await context.Database.ExecuteSqlRawAsync(createTableSql);
                logger.LogInformation("[SCHEMA_GUARD] Successfully created TicketTechnicianAssignments table");
            }
            catch (Exception createEx)
            {
                logger.LogError(createEx, "[SCHEMA_GUARD] Failed to create TicketTechnicianAssignments table: {Error}", createEx.Message);
                throw;
            }
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] TicketTechnicianAssignments table exists");
            // Ensure AcceptedAt column exists (migration 20260210000000 or schema drift)
            bool hasAcceptedAt = false;
            using (var pragmaCmd = connection.CreateCommand())
            {
                pragmaCmd.CommandText = "PRAGMA table_info(TicketTechnicianAssignments);";
                using var reader = await pragmaCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "AcceptedAt", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAcceptedAt = true;
                        break;
                    }
                }
            }
            if (!hasAcceptedAt)
            {
                logger.LogWarning("[SCHEMA_GUARD] TicketTechnicianAssignments missing AcceptedAt column. Adding it (ALTER TABLE).");
                using (var alterCmd = connection.CreateCommand())
                {
                    alterCmd.CommandText = "ALTER TABLE TicketTechnicianAssignments ADD COLUMN AcceptedAt TEXT;";
                    await alterCmd.ExecuteNonQueryAsync();
                }
                logger.LogInformation("[SCHEMA_GUARD] Successfully added AcceptedAt column to TicketTechnicianAssignments");
            }
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking TicketTechnicianAssignments table: {Error}", ex.Message);
    }
}

// =======================
// Schema Guard: CHECK only — RBAC table must exist (fail fast at startup if missing).
// Do NOT create the table here; use migrations only.
// =======================
static async Task EnsureTechnicianSubcategoryPermissionsTableExistsAsync(AppDbContext context, ILogger logger)
{
    logger.LogInformation("[SCHEMA_GUARD] Verifying TechnicianSubcategoryPermissions table exists...");

    var connection = context.Database.GetDbConnection();
    var wasOpen = connection.State == System.Data.ConnectionState.Open;

    if (!wasOpen)
    {
        await connection.OpenAsync();
    }

    try
    {
        bool tableExists = false;
        using (var checkTableCommand = connection.CreateCommand())
        {
            checkTableCommand.CommandText = @"
                SELECT name FROM sqlite_master
                WHERE type='table' AND name='TechnicianSubcategoryPermissions';
            ";
            var result = await checkTableCommand.ExecuteScalarAsync();
            tableExists = result != null;
        }

        if (!tableExists)
        {
            logger.LogError("[SCHEMA_GUARD] TechnicianSubcategoryPermissions table is missing. Run database migrations.");
            throw new InvalidOperationException("RBAC permissions table missing. Run database migrations.");
        }

        logger.LogInformation("[SCHEMA_GUARD] TechnicianSubcategoryPermissions table exists");
    }
    finally
    {
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureTicketActivityEventsTableExistsAsync(AppDbContext context, ILogger logger)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='TicketActivityEvents';";
        var result = await command.ExecuteScalarAsync();
        var tableExists = result != null && result.ToString() == "TicketActivityEvents";

        if (!tableExists)
        {
            logger.LogWarning("[SCHEMA_GUARD] TicketActivityEvents table does not exist. Creating it...");
            
            var dbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "").Trim();
            if (string.IsNullOrEmpty(dbPath))
            {
                dbPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ticketing.db");
            }
            
            // Backup database before making schema changes
            if (File.Exists(dbPath))
            {
                var backupPath = $"{dbPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(dbPath, backupPath, overwrite: true);
                    logger.LogInformation("[SCHEMA_GUARD] Database backed up to: {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    logger.LogWarning(backupEx, "[SCHEMA_GUARD] Failed to create backup: {Error}", backupEx.Message);
                }
            }
            
            // Create the table with the exact schema from the migration
            var createTableSql = @"
                CREATE TABLE TicketActivityEvents (
                    Id TEXT NOT NULL PRIMARY KEY,
                    TicketId TEXT NOT NULL,
                    ActorUserId TEXT NOT NULL,
                    ActorRole TEXT NOT NULL,
                    EventType TEXT NOT NULL,
                    OldStatus TEXT,
                    NewStatus TEXT,
                    MetadataJson TEXT,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ActorUserId) REFERENCES Users(Id) ON DELETE RESTRICT
                );
                
                CREATE INDEX IX_TicketActivityEvents_TicketId ON TicketActivityEvents(TicketId);
                CREATE INDEX IX_TicketActivityEvents_CreatedAt ON TicketActivityEvents(CreatedAt);
                CREATE INDEX IX_TicketActivityEvents_TicketId_CreatedAt ON TicketActivityEvents(TicketId, CreatedAt);
            ";
            
            try
            {
                await context.Database.ExecuteSqlRawAsync(createTableSql);
                logger.LogInformation("[SCHEMA_GUARD] Successfully created TicketActivityEvents table");
            }
            catch (Exception createEx)
            {
                logger.LogError(createEx, "[SCHEMA_GUARD] Failed to create TicketActivityEvents table: {Error}", createEx.Message);
                throw;
            }
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] TicketActivityEvents table exists");
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking TicketActivityEvents table: {Error}", ex.Message);
    }
}

// =======================
// Schema Guard: Ensure ClaimedAtUtc columns exist (dev + SQLite only)
// =======================
static async Task EnsureClaimedAtUtcColumnsExistAsync(
    AppDbContext context,
    ILogger logger)
{
    try
    {
        logger.LogInformation("[SCHEMA_GUARD] Checking ClaimedAtUtc columns on Tickets and TicketTechnicianAssignments...");

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        async Task<bool> TableExistsAsync(string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT name FROM sqlite_master 
                WHERE type='table' AND name=$tableName;
            ";
            var param = command.CreateParameter();
            param.ParameterName = "$tableName";
            param.Value = tableName;
            command.Parameters.Add(param);
            var result = await command.ExecuteScalarAsync();
            return result != null;
        }

        async Task<HashSet<string>> GetColumnsAsync(string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}')";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                columns.Add(columnName);
            }
            return columns;
        }

        async Task EnsureColumnAsync(string tableName)
        {
            if (!await TableExistsAsync(tableName))
            {
                logger.LogWarning("[SCHEMA_GUARD] Table {Table} does not exist. Skipping ClaimedAtUtc guard.", tableName);
                return;
            }

            var columns = await GetColumnsAsync(tableName);
            if (columns.Contains("ClaimedAtUtc"))
            {
                logger.LogInformation("[SCHEMA_GUARD] {Table}.ClaimedAtUtc already exists.", tableName);
                return;
            }

            logger.LogWarning("[SCHEMA_GUARD] {Table}.ClaimedAtUtc missing. Adding column...", tableName);
            try
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN ClaimedAtUtc TEXT NULL;";
                await alter.ExecuteNonQueryAsync();
                logger.LogInformation("[SCHEMA_GUARD] Added {Table}.ClaimedAtUtc column.", tableName);
            }
            catch (Exception addEx)
            {
                if (addEx.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase) ||
                    addEx.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("[SCHEMA_GUARD] {Table}.ClaimedAtUtc already exists (race condition).", tableName);
                }
                else
                {
                    logger.LogWarning(addEx, "[SCHEMA_GUARD] Failed to add {Table}.ClaimedAtUtc: {Error}", tableName, addEx.Message);
                }
            }
        }

        await EnsureColumnAsync("Tickets");
        await EnsureColumnAsync("TicketTechnicianAssignments");

        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] ClaimedAtUtc guard encountered an error: {Error}", ex.Message);
    }
}

static async Task EnsureTicketFieldValuesUpdatedAtColumnExistsAsync(AppDbContext context, ILogger logger)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        // Check if TicketFieldValues table exists
        var tableCheckCommand = connection.CreateCommand();
        tableCheckCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='TicketFieldValues';";
        var tableResult = await tableCheckCommand.ExecuteScalarAsync();
        var tableExists = tableResult != null && tableResult.ToString() == "TicketFieldValues";

        if (!tableExists)
        {
            logger.LogWarning("[SCHEMA_GUARD] TicketFieldValues table does not exist. Skipping UpdatedAt column check.");
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
            return;
        }

        // Check if UpdatedAt column exists
        var columnCheckCommand = connection.CreateCommand();
        columnCheckCommand.CommandText = "PRAGMA table_info(TicketFieldValues);";
        using var reader = await columnCheckCommand.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1); // Column name is at index 1
            columns.Add(columnName);
        }

        if (!columns.Contains("UpdatedAt", StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("[SCHEMA_GUARD] TicketFieldValues.UpdatedAt column does not exist. Adding it...");
            
            var dbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "").Trim();
            if (string.IsNullOrEmpty(dbPath))
            {
                dbPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ticketing.db");
            }
            
            // Backup database before making schema changes
            if (File.Exists(dbPath))
            {
                var backupPath = $"{dbPath}.backup.{DateTime.UtcNow:yyyyMMddHHmmss}";
                try
                {
                    File.Copy(dbPath, backupPath, overwrite: true);
                    logger.LogInformation("[SCHEMA_GUARD] Database backed up to: {BackupPath}", backupPath);
                }
                catch (Exception backupEx)
                {
                    logger.LogWarning(backupEx, "[SCHEMA_GUARD] Failed to create backup: {Error}", backupEx.Message);
                }
            }
            
            // Add the UpdatedAt column
            var addColumnSql = "ALTER TABLE TicketFieldValues ADD COLUMN UpdatedAt TEXT;";
            
            try
            {
                await context.Database.ExecuteSqlRawAsync(addColumnSql);
                logger.LogInformation("[SCHEMA_GUARD] Successfully added UpdatedAt column to TicketFieldValues table");
            }
            catch (Exception createEx)
            {
                logger.LogError(createEx, "[SCHEMA_GUARD] Failed to add UpdatedAt column to TicketFieldValues table: {Error}", createEx.Message);
                throw;
            }
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] TicketFieldValues.UpdatedAt column exists");
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking TicketFieldValues.UpdatedAt column: {Error}", ex.Message);
    }
}


static async Task EnsureIsSupervisorColumnExistsAsync(AppDbContext context, ILogger logger, string dbPath)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Technicians)";
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                }
            }
        }

        if (!columns.Contains("IsSupervisor"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Technicians.IsSupervisor column does not exist. Adding it...");
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Copy(dbPath, "${dbPath}.backup.${DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: true);
                }
                catch { }
            }
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Technicians ADD COLUMN IsSupervisor INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added IsSupervisor column to Technicians table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Technicians.IsSupervisor column exists");
        }
        
        if (!wasOpen) await connection.CloseAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking Technicians.IsSupervisor column: {Error}", ex.Message);
    }
}

static async Task EnsureUserLockoutColumnsExistAsync(AppDbContext context, ILogger logger, string dbPath)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        // Get existing columns
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Users);";
        var columns = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        // Add LockoutEnabled if missing
        if (!columns.Contains("LockoutEnabled"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Users.LockoutEnabled column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN LockoutEnabled INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added LockoutEnabled column to Users table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Users.LockoutEnabled column exists");
        }

        // Add LockoutEnd if missing
        if (!columns.Contains("LockoutEnd"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Users.LockoutEnd column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN LockoutEnd TEXT NULL;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added LockoutEnd column to Users table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Users.LockoutEnd column exists");
        }

        // Add SecurityStamp if missing
        if (!columns.Contains("SecurityStamp"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Users.SecurityStamp column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN SecurityStamp TEXT NULL;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added SecurityStamp column to Users table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Users.SecurityStamp column exists");
        }

        if (!wasOpen) await connection.CloseAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking Users lockout columns: {Error}", ex.Message);
    }
}

static async Task EnsureTechnicianSoftDeleteColumnsExistAsync(AppDbContext context, ILogger logger, string dbPath)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        // Get existing columns
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Technicians);";
        var columns = new List<string>();
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        // Add IsDeleted if missing
        if (!columns.Contains("IsDeleted"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Technicians.IsDeleted column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Technicians ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added IsDeleted column to Technicians table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Technicians.IsDeleted column exists");
        }

        // Add DeletedAt if missing
        if (!columns.Contains("DeletedAt"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Technicians.DeletedAt column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Technicians ADD COLUMN DeletedAt TEXT NULL;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added DeletedAt column to Technicians table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Technicians.DeletedAt column exists");
        }

        // Add DeletedByUserId if missing
        if (!columns.Contains("DeletedByUserId"))
        {
            logger.LogWarning("[SCHEMA_GUARD] Technicians.DeletedByUserId column does not exist. Adding it...");
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Technicians ADD COLUMN DeletedByUserId TEXT NULL;");
            logger.LogInformation("[SCHEMA_GUARD] Successfully added DeletedByUserId column to Technicians table");
        }
        else
        {
            logger.LogInformation("[SCHEMA_GUARD] Technicians.DeletedByUserId column exists");
        }

        if (!wasOpen) await connection.CloseAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[SCHEMA_GUARD] Error checking Technicians soft delete columns: {Error}", ex.Message);
    }
}

static async Task CleanupInvalidTicketFieldValuesAsync(AppDbContext context, ILogger logger)
{
    try
    {
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        // Check if TicketFieldValues table exists
        var tableCheckCommand = connection.CreateCommand();
        tableCheckCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='TicketFieldValues';";
        var tableResult = await tableCheckCommand.ExecuteScalarAsync();
        var tableExists = tableResult != null && tableResult.ToString() == "TicketFieldValues";

        if (!tableExists)
        {
            logger.LogInformation("[CLEANUP] TicketFieldValues table does not exist. Skipping cleanup.");
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
            return;
        }

        // Count rows with invalid GUIDs (empty strings, null, or invalid format)
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = @"
            SELECT COUNT(*) FROM TicketFieldValues 
            WHERE Id = '' OR Id IS NULL OR TicketId = '' OR TicketId IS NULL
            OR length(Id) != 36 OR length(TicketId) != 36;";
        var invalidCount = await countCommand.ExecuteScalarAsync();
        var count = Convert.ToInt32(invalidCount ?? 0);

        if (count > 0)
        {
            logger.LogWarning("[CLEANUP] Found {Count} TicketFieldValues rows with invalid GUIDs. Cleaning up...", count);
            
            // Delete rows with invalid GUIDs
            var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = @"
                DELETE FROM TicketFieldValues 
                WHERE Id = '' OR Id IS NULL OR TicketId = '' OR TicketId IS NULL
                OR length(Id) != 36 OR length(TicketId) != 36;";
            
            var deleted = await deleteCommand.ExecuteNonQueryAsync();
            logger.LogInformation("[CLEANUP] Deleted {Deleted} rows with invalid GUIDs from TicketFieldValues", deleted);
        }
        else
        {
            logger.LogInformation("[CLEANUP] No invalid GUIDs found in TicketFieldValues");
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[CLEANUP] Error cleaning up invalid TicketFieldValues: {Error}", ex.Message);
    }
}

/// <summary>
/// One-time bootstrap: create Admin, Client, Technician, Supervisor users when DB has no users (Production only).
/// Each role uses env TikQ_BOOTSTRAP_&lt;Role&gt;_PASSWORD (required, min 8) and optional TikQ_BOOTSTRAP_&lt;Role&gt;_EMAIL.
/// Supervisor = UserRole.Technician + Technician.IsSupervisor=true. Never crashes startup; logs [BOOTSTRAP] only.
/// </summary>
static async Task BootstrapUsersIfEmptyAsync(AppDbContext context, IPasswordHasher<User> passwordHasher, ILogger logger)
{
    const int MinPasswordLength = 8;
    try
    {
        if (await context.Users.AnyAsync())
        {
            logger.LogInformation("[BOOTSTRAP] Users exist; skipping bootstrap.");
            return;
        }

        var addedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasDuplicate(string email) => addedEmails.Contains(email) || context.Users.Local.Any(u => u.Email == email);

        // Admin
        var adminPassword = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_ADMIN_PASSWORD")?.Trim();
        var adminEmail = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_ADMIN_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = "admin@local";
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < MinPasswordLength)
            logger.LogWarning("[BOOTSTRAP] Admin password missing/too short; skipping.");
        else if (await context.Users.AnyAsync(u => u.Email == adminEmail) || hasDuplicate(adminEmail))
            { /* duplicate */ }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Admin",
                Email = adminEmail,
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = passwordHasher.HashPassword(user, adminPassword);
            context.Users.Add(user);
            addedEmails.Add(adminEmail);
            logger.LogInformation("[BOOTSTRAP] Created Admin user: {Email}", adminEmail);
        }

        // Client
        var clientPassword = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_CLIENT_PASSWORD")?.Trim();
        var clientEmail = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_CLIENT_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(clientEmail)) clientEmail = "client@local";
        if (string.IsNullOrWhiteSpace(clientPassword) || clientPassword.Length < MinPasswordLength)
            logger.LogWarning("[BOOTSTRAP] Client password missing/too short; skipping.");
        else if (await context.Users.AnyAsync(u => u.Email == clientEmail) || hasDuplicate(clientEmail))
            { /* duplicate */ }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Client",
                Email = clientEmail,
                Role = UserRole.Client,
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = passwordHasher.HashPassword(user, clientPassword);
            context.Users.Add(user);
            addedEmails.Add(clientEmail);
            logger.LogInformation("[BOOTSTRAP] Created Client user: {Email}", clientEmail);
        }

        // Technician (Role=Technician, IsSupervisor=false)
        var techPassword = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_TECH_PASSWORD")?.Trim();
        var techEmail = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_TECH_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(techEmail)) techEmail = "tech@local";
        if (string.IsNullOrWhiteSpace(techPassword) || techPassword.Length < MinPasswordLength)
            logger.LogWarning("[BOOTSTRAP] Technician password missing/too short; skipping.");
        else if (await context.Users.AnyAsync(u => u.Email == techEmail) || hasDuplicate(techEmail))
            { /* duplicate */ }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Technician",
                Email = techEmail,
                Role = UserRole.Technician,
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = passwordHasher.HashPassword(user, techPassword);
            context.Users.Add(user);
            context.Technicians.Add(new Technician
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Technician",
                Email = techEmail,
                IsActive = true,
                IsSupervisor = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UserId = user.Id
            });
            addedEmails.Add(techEmail);
            logger.LogInformation("[BOOTSTRAP] Created Technician user: {Email}", techEmail);
        }

        // Supervisor (Role=Technician, Technician.IsSupervisor=true)
        var supervisorPassword = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_SUPERVISOR_PASSWORD")?.Trim();
        var supervisorEmail = Environment.GetEnvironmentVariable("TikQ_BOOTSTRAP_SUPERVISOR_EMAIL")?.Trim();
        if (string.IsNullOrWhiteSpace(supervisorEmail)) supervisorEmail = "supervisor@local";
        if (string.IsNullOrWhiteSpace(supervisorPassword) || supervisorPassword.Length < MinPasswordLength)
            logger.LogWarning("[BOOTSTRAP] Supervisor password missing/too short; skipping.");
        else if (await context.Users.AnyAsync(u => u.Email == supervisorEmail) || hasDuplicate(supervisorEmail))
            { /* duplicate */ }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Supervisor",
                Email = supervisorEmail,
                Role = UserRole.Technician,
                CreatedAt = DateTime.UtcNow,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = passwordHasher.HashPassword(user, supervisorPassword);
            context.Users.Add(user);
            context.Technicians.Add(new Technician
            {
                Id = Guid.NewGuid(),
                FullName = "Bootstrap Supervisor",
                Email = supervisorEmail,
                IsActive = true,
                IsSupervisor = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UserId = user.Id
            });
            addedEmails.Add(supervisorEmail);
            logger.LogInformation("[BOOTSTRAP] Created Supervisor user: {Email}", supervisorEmail);
        }

        await context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[BOOTSTRAP] Bootstrap failed (startup continues): {Error}", ex.Message);
    }
}

/// <summary>
/// One-time bootstrap: when Users table is empty, create a single Admin from config (BootstrapAdmin:Email, :Password, :FullName).
/// In Production or ProductionHandoffMode: no default passwords; BootstrapAdmin:Password must be set and strong (min 8 chars) or bootstrap is skipped and startup fails if no users exist.
/// </summary>
static async Task BootstrapAdminOnceIfNoUsersAsync(
    AppDbContext context,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    ILogger logger,
    bool requireStrongBootstrapPassword)
{
    try
    {
        if (await context.Users.AnyAsync())
            return;

        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            email = "admin@test.com";
        var password = configuration["BootstrapAdmin:Password"]?.Trim();
        var fullName = configuration["BootstrapAdmin:FullName"]?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "System Admin";

        if (requireStrongBootstrapPassword)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                throw new InvalidOperationException(
                    "Bootstrap admin is required (no users in DB) but BootstrapAdmin:Password is missing or too short (min 8 characters). " +
                    "Set BootstrapAdmin:Email, BootstrapAdmin:Password, and BootstrapAdmin:FullName in config or environment.");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(password))
                password = "Admin123!";
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = normalizedEmail,
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("[BOOTSTRAP] Admin created");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[BOOTSTRAP] One-time admin bootstrap failed (startup continues): {Error}", ex.Message);
    }
}

// =======================
// Repositories
// =======================
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.IFieldDefinitionRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.FieldDefinitionRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ICategoryRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.CategoryRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ISystemSettingsRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.SystemSettingsRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.IUserPreferencesRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.UserPreferencesRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITechnicianRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TechnicianRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.IUserRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.UserRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITicketRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TicketRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITicketMessageRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TicketMessageRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITicketTechnicianAssignmentRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TicketTechnicianAssignmentRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITicketActivityEventRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TicketActivityEventRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITechnicianSubcategoryPermissionRepository, 
    Ticketing.Backend.Infrastructure.Data.Repositories.TechnicianSubcategoryPermissionRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ITicketUserStateRepository,
    Ticketing.Backend.Infrastructure.Data.Repositories.TicketUserStateRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.ISupervisorTechnicianLinkRepository,
    Ticketing.Backend.Infrastructure.Data.Repositories.SupervisorTechnicianLinkRepository>();
builder.Services.AddScoped<Ticketing.Backend.Application.Repositories.IUnitOfWork, 
    Ticketing.Backend.Infrastructure.Data.Repositories.UnitOfWork>();

// Company directory: read-only identity (Company DB). If Enabled use SQL Server; else fake.
builder.Services.AddScoped<Ticketing.Backend.Application.Common.Interfaces.ICompanyUserDirectory>(sp =>
{
    var opts = sp.GetRequiredService<Ticketing.Backend.Infrastructure.CompanyDirectory.CompanyDirectoryOptions>();
    if (opts.Enabled && !string.IsNullOrWhiteSpace(opts.ConnectionString))
    {
        var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<Ticketing.Backend.Infrastructure.CompanyDirectory.SqlServerCompanyUserDirectory>>();
        return new Ticketing.Backend.Infrastructure.CompanyDirectory.SqlServerCompanyUserDirectory(opts, logger);
    }
    return new Ticketing.Backend.Infrastructure.CompanyDirectory.FakeCompanyUserDirectory();
});

// =======================
// Application services
// =======================
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IFieldDefinitionService, FieldDefinitionService>();
builder.Services.AddScoped<Ticketing.Backend.Application.Services.ITicketService, Ticketing.Backend.Application.Services.TicketService>();
builder.Services.AddScoped<Ticketing.Backend.Application.Services.ISupervisorService, Ticketing.Backend.Application.Services.SupervisorService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddScoped<ISmartAssignmentService, SmartAssignmentService>();
builder.Services.AddScoped<ITechnicianService, TechnicianService>();
builder.Services.AddScoped<ISupervisorService, SupervisorService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAutomationCoverageService, AutomationCoverageService>();

// =======================
// Authentication / JWT + Windows Negotiate (Smart scheme)
// =======================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Smart";
    options.DefaultChallengeScheme = "Smart";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret))
    };
    // Accept JWT from: (A) Authorization: Bearer, (B) cookie "tikq_access", (C) query access_token (SignalR)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // A) Authorization header (e.g. curl, Postman)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader["Bearer ".Length..].Trim();
                return Task.CompletedTask;
            }
            // B) HttpOnly cookie (browser)
            if (context.Request.Cookies.TryGetValue("tikq_access", out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
                return Task.CompletedTask;
            }
            // C) SignalR query string
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/tickets"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnForbidden = async context =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/api/supervisor", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "Forbidden",
                    detail = "Supervisor or admin access required."
                });
                await context.Response.WriteAsync(payload);
            }
        },
        OnChallenge = async context =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/api/supervisor", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (context.Response.HasStarted)
            {
                return;
            }

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                title = "Unauthorized",
                detail = "Authentication required."
            });
            await context.Response.WriteAsync(payload);
        }
    };
})
.AddNegotiate()
.AddPolicyScheme("Smart", "Smart (JWT or Negotiate)", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return JwtBearerDefaults.AuthenticationScheme;
        var windowsAuthOpts = context.Request.HttpContext.RequestServices
            .GetService<Microsoft.Extensions.Options.IOptions<Ticketing.Backend.Infrastructure.Auth.WindowsAuthOptions>>();
        if (windowsAuthOpts?.Value?.IsWindowsAuthAvailable == true)
            return NegotiateDefaults.AuthenticationScheme;
        return JwtBearerDefaults.AuthenticationScheme;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(nameof(UserRole.Admin)));

    options.AddPolicy("SupervisorOnly", policy =>
        policy.RequireClaim("isSupervisor", "true"));

    options.AddPolicy("SupervisorOrAdmin", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole(UserRole.Admin.ToString()) ||
            (context.User.IsInRole(UserRole.Technician.ToString()) &&
             string.Equals(context.User.FindFirstValue("isSupervisor"), "true", StringComparison.OrdinalIgnoreCase))));
});

// =======================
// SignalR for real-time updates
// =======================
builder.Services.AddSignalR(options =>
{
    // Enable detailed errors in development
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    // Keep connections alive
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Register TicketHubService for broadcasting ticket updates
builder.Services.AddScoped<Ticketing.Backend.Application.Services.ITicketHubService, 
    Ticketing.Backend.Infrastructure.Services.TicketHubService>();

// =======================
// CORS
// =======================
var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

// In Development, add common dev origins if not already configured
if (builder.Environment.IsDevelopment())
{
    var devOrigins = new[]
    {
        "http://localhost:3000",
        "https://localhost:3000",
        "http://127.0.0.1:3000",
        "https://127.0.0.1:3000",
        "http://localhost:3001",
        "https://localhost:3001",
        "http://127.0.0.1:3001",
        "https://127.0.0.1:3001",
        "http://localhost:5173", // Vite default
        "https://localhost:5173"
    };
    
    if (allowedCorsOrigins == null || allowedCorsOrigins.Length == 0)
    {
        allowedCorsOrigins = devOrigins;
    }
    else
    {
        // Merge configured origins with dev origins, removing duplicates
        var merged = new HashSet<string>(allowedCorsOrigins, StringComparer.OrdinalIgnoreCase);
        foreach (var origin in devOrigins)
        {
            merged.Add(origin);
        }
        allowedCorsOrigins = merged.ToArray();
    }
}
else
{
    // Production: use only configured origins, or default safe list
    allowedCorsOrigins ??= Array.Empty<string>();
}

// [HANDOFF] In Production or ProductionHandoffMode, require CORS origins (fail fast)
var isProductionOrHandoff = builder.Environment.IsProduction()
    || Ticketing.Backend.Infrastructure.StartupValidation.IsProductionHandoffMode(builder.Configuration);
var hasNoOrigins = allowedCorsOrigins == null
    || allowedCorsOrigins.Length == 0
    || !allowedCorsOrigins.Any(o => !string.IsNullOrWhiteSpace(o));
if (isProductionOrHandoff && hasNoOrigins)
{
    throw new InvalidOperationException("Cors:AllowedOrigins must be configured in production.");
}

builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Development: Allow specific dev origins + credentials for SignalR
        options.AddPolicy("DevCors", policy =>
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    }
    else
    {
        // Production: Strict CORS - only configured origins
        options.AddPolicy("DevCors", policy =>
        {
            if (allowedCorsOrigins.Length > 0)
            {
                policy
                    .WithOrigins(allowedCorsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
            // If no origins configured in production, deny all (security)
        });
    }
});

// =======================
// MVC / JSON
// =======================
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeOffsetJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetJsonConverter());
});

// Minimal APIs (e.g. /api/health) use HTTP JSON options; ensure camelCase for consistent responses
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();

// =======================
// Swagger + JWT Configuration (SECURITY-CRITICAL)
// =======================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ticketing.Backend",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter ONLY the JWT token. Swagger will add 'Bearer ' automatically."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =======================
// Defensive startup logging (before Build): do not crash on missing JWT, only log
// =======================
try
{
    var envName = builder.Environment.EnvironmentName;
    var connSource = builder.Configuration.GetConnectionString("DefaultConnection") != null ? "DefaultConnection from config" : "not set";
    var hasJwtSecret = !string.IsNullOrWhiteSpace(resolvedSecret);
    LogStartup($"[STARTUP] Environment: {envName}");
    LogStartup($"[STARTUP] Connection string source: {connSource}");
    LogStartup(hasJwtSecret ? "[STARTUP] JWT secret configured." : "[STARTUP] JWT secret configured: False");
    if (!hasJwtSecret && builder.Environment.IsProduction())
        LogStartup("[STARTUP] WARNING: JWT secret is missing. Production will fail at startup.");
}
catch (Exception ex)
{
    LogStartup($"[STARTUP] Defensive logging failed: {ex}");
}

// =======================
// [HANDOFF] Production config validation (fail fast when Production or ProductionHandoffMode)
// =======================
var isProductionEnv = builder.Environment.IsProduction();
if (isProductionEnv || Ticketing.Backend.Infrastructure.StartupValidation.IsProductionHandoffMode(builder.Configuration))
{
    Ticketing.Backend.Infrastructure.StartupValidation.ValidateProductionConfig(
        builder.Configuration,
        isProductionEnv,
        resolvedSecret,
        LogStartup);
}

// Enable console logging for IIS (stdout is captured by ASP.NET Core Module)
builder.Logging.AddConsole();

WebApplication app;
try
{
    app = builder.Build();
}
catch (Exception ex)
{
    LogStartup($"[STARTUP] Application build failed: {ex}");
    throw;
}
if (!app.Environment.IsDevelopment())
    app.Logger.LogWarning("[HARDEN] DebugBlocker middleware ENABLED. Env={Env}", app.Environment.EnvironmentName);

// =======================
// Apply migrations & seed (only when Database:AutoMigrateOnStartup is true; default true in Dev, false in Prod)
// =======================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    var config = services.GetRequiredService<IConfiguration>();
    var dbOptions = services.GetRequiredService<Ticketing.Backend.Infrastructure.Data.DatabaseOptions>();

    if (!dbOptions.AutoMigrateOnStartup)
    {
        LogStartup("[STARTUP] Migrations skipped (Database:AutoMigrateOnStartup is false).");
    }

    await StartupMigrationRunner.RunAsync(
        context,
        dbOptions,
        logger,
        app.Environment,
        databasePathOrSummary,
        sqliteFileExists: isSqlServer ? null : (bool?)(sqliteDbPath != null && File.Exists(sqliteDbPath)));

    if (dbOptions.AutoMigrateOnStartup)
    {
        // Lightweight schema diagnostic: SubcategoryFieldDefinitions Key vs FieldKey (SQL Server + SQLite)
        try
        {
            var keyColumn = await SchemaInspector.GetSubcategoryFieldDefinitionKeyColumnNameAsync(context);
            if (keyColumn != null)
                logger.LogInformation("[SCHEMA] SubcategoryFieldDefinitions key column: {Column} (prefer FieldKey for SQL Server)", keyColumn);
            else
                logger.LogInformation("[SCHEMA] SubcategoryFieldDefinitions key column: could not determine (table may not exist yet)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SCHEMA] SubcategoryFieldDefinitions key column check failed (non-fatal)");
        }

        // Post-migration schema guards (SQLite only; migrations are source of truth for SqlServer)
        if (!isSqlServer && sqliteDbPath != null)
        {
        await EnsureSubcategoryFieldDefinitionsSchemaAsync(context, logger, sqliteDbPath);
        await EnsureCategoriesNormalizedNameColumnExistsAsync(context, logger);
        await EnsureSubcategoriesTableExistsAsync(context, logger, sqliteDbPath);
        await EnsureIsSupervisorColumnExistsAsync(context, logger, sqliteDbPath);
        await EnsureUserLockoutColumnsExistAsync(context, logger, sqliteDbPath);
        await EnsureTechnicianSoftDeleteColumnsExistAsync(context, logger, sqliteDbPath);
        await EnsureTicketTechnicianAssignmentsTableExistsAsync(context, logger, sqliteDbPath);
        }

        // SQLite-only schema guards (migrations are source of truth for SqlServer)
        if (!isSqlServer)
        {
        await EnsureTechnicianSubcategoryPermissionsTableExistsAsync(context, logger);
        if (app.Environment.IsDevelopment() &&
            context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            await EnsureClaimedAtUtcColumnsExistAsync(context, logger);
        }
        await EnsureTicketActivityEventsTableExistsAsync(context, logger);
        await EnsureTicketFieldValuesUpdatedAtColumnExistsAsync(context, logger);
        await CleanupInvalidTicketFieldValuesAsync(context, logger);
        }

        // Bootstrap (Production): handled below via BootstrapSeederService when Bootstrap:Enabled and users empty.
    }

    {
        if (enableDevSeeding && !app.Environment.IsDevelopment())
            logger.LogWarning("[SEED] EnableDevSeeding=true in non-Development environment; running dev seed (Test123! passwords).");
        logger.LogInformation("[SEED] Running seed data initialization...");
        await SeedData.InitializeAsync(context, passwordHasher, logger);

        // Sync technicians to identity users: ensure each technician has a User, set password Test123!
        logger.LogInformation("[SEED] Syncing technician users (Technicians → Users)...");
        var technicianSyncReport = await SeedData.SyncTechnicianUsersAsync(context, passwordHasher);
        logger.LogInformation("[SEED] Technician user sync report (Email | Roles | Status):");
        foreach (var row in technicianSyncReport)
            logger.LogInformation("[SEED]   {Email} | {Roles} | {Status}", row.Email, row.Roles, row.Status);
        if (technicianSyncReport.Count == 0)
            logger.LogInformation("[SEED]   (no technicians in table)");
        
        // Verify seed data was applied
        var userCount = await context.Users.CountAsync();
        var categoryCount = await context.Categories.CountAsync();
        var technicianCount = await context.Technicians.CountAsync();
        
        logger.LogInformation("[SEED] Seed data verification:");
        logger.LogInformation("[SEED]   Users: {UserCount}", userCount);
        logger.LogInformation("[SEED]   Categories: {CategoryCount}", categoryCount);
        logger.LogInformation("[SEED]   Technicians: {TechnicianCount}", technicianCount);
        
        if (userCount == 0)
        {
            logger.LogWarning("[SEED] ⚠️  WARNING: No users found after seeding! Check SeedData.cs for issues.");
        }
        if (categoryCount == 0)
        {
            logger.LogWarning("[SEED] ⚠️  WARNING: No categories found after seeding! Check SeedData.cs for issues.");
        }

        var totalLinks = await context.SupervisorTechnicianLinks.CountAsync();
        logger.LogInformation("[SEED]   SupervisorTechnicianLinks total: {TotalLinks}", totalLinks);
        if (totalLinks == 0)
        {
            logger.LogWarning("[SEED] ⚠️  WARNING: No SupervisorTechnicianLinks! Supervisor list will be empty. Check EnsureSupervisorTechnicianLinksAsync and User emails.");
        }
        else
        {
            var supervisorEmails = new[] { "supervisor@test.com", "techsuper@email.com" };
            foreach (var email in supervisorEmails)
            {
                var emailLower = email.ToLowerInvariant();
                var supUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == emailLower);
                if (supUser == null) continue;
                var linksForSup = await context.SupervisorTechnicianLinks.CountAsync(l => l.SupervisorUserId == supUser.Id);
                logger.LogInformation("[SEED]   Links for {Email} (UserId={UserId}): {Count}", email, supUser.Id, linksForSup);
                if (linksForSup == 0)
                    logger.LogWarning("[SEED] ⚠️  No links for {Email} — supervisor list will be empty for this user.", email);
            }
        }
    }
    else
    {
        logger.LogInformation("[SEED] Skipped in Production");
            logger.LogInformation("[BOOTSTRAP] Seed applied: {Count} user(s).", bootstrapResult.UsersCreated);
        else
            logger.LogInformation("[BOOTSTRAP] Seed skipped: {Reason}", bootstrapResult.Message);

        // Minimal categories when table is empty or first category has no subcategories (no deletes, no migrations, INSERT only)
        var categoryCount = await context.Categories.CountAsync();
        if (categoryCount == 0)
        {
            var category = new Category
            {
                Id = 1,
                Name = "مالی",
                NormalizedName = "مالی".Trim().ToLowerInvariant(),
                Description = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Subcategories = new List<Subcategory>
                {
                    new Subcategory { Id = 1, Name = "حقوق و دستمزد", IsActive = true, CreatedAt = DateTime.UtcNow }
                }
            };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            logger.LogInformation("[SEED_MIN] Inserted default category+subcategory");
        }
        else
        {
            var firstCategory = await context.Categories
                .Include(c => c.Subcategories)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();
            if (firstCategory != null && firstCategory.Subcategories.Count == 0)
            {
                var nextSubId = (await context.Subcategories.MaxAsync(s => (int?)s.Id) ?? 0) + 1;
                firstCategory.Subcategories.Add(new Subcategory
                {
                    Id = nextSubId,
                    Name = "حقوق و دستمزد",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
                logger.LogInformation("[SEED_MIN] Added missing default subcategory for existing category");
            }
            else
            {
                logger.LogInformation("[SEED_MIN] Skipped (already has subcategories)");
            }
        }
    }

    logger.LogInformation("═══════════════════════════════════════════════════════════════");
}

// =======================
// Middleware pipeline
// =======================
app.UseRouting();

// [HARDEN] Block debug/maintenance routes in Production or ProductionHandoffMode (404)
var isHandoff = Ticketing.Backend.Infrastructure.StartupValidation.IsProductionHandoffMode(app.Configuration);
if (!app.Environment.IsDevelopment() || isHandoff)
{
    app.Logger.LogWarning("[HARDEN] DebugBlocker middleware ENABLED. Env={Env}, Handoff={Handoff}", app.Environment.EnvironmentName, isHandoff);

    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/api/debug", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/admin/cleanup", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/diag", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/debug-users", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

app.UseCors("DevCors");

// Enable WebSockets for SignalR
app.UseWebSockets();

// Global exception handler
app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (exception != null)
        {
            logger.LogError(exception,
                "Unhandled exception: {ExceptionType}, Message: {Message}, Path: {Path}, StackTrace: {StackTrace}",
                exception.GetType().Name, exception.Message, context.Request.Path, exception.StackTrace);

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var isDevelopment = app.Environment.IsDevelopment();
            var response = new
            {
                status = 500,
                title = "An error occurred while processing your request.",
                detail = isDevelopment ? exception.Message : "An internal server error occurred.",
                type = exception.GetType().Name,
                traceId = context.TraceIdentifier
            };

            if (isDevelopment)
            {
                var responseWithStackTrace = new
                {
                    status = 500,
                    title = "An error occurred while processing your request.",
                    detail = exception.Message,
                    type = exception.GetType().Name,
                    traceId = context.TraceIdentifier,
                    stackTrace = exception.StackTrace,
                    innerException = exception.InnerException != null ? new
                    {
                        message = exception.InnerException.Message,
                        type = exception.InnerException.GetType().Name,
                        stackTrace = exception.InnerException.StackTrace
                    } : null
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(responseWithStackTrace, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            }
            else
            {
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            }
        }
    });
});

// Request logging middleware (Development only) - to identify spam
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("[REQ] {Method} {Path}", context.Request.Method, context.Request.Path);
        await next();
    });
}

// Always enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ticketing.Backend v1");
    c.RoutePrefix = "swagger";
});

// Enable request buffering for POST/PUT/PATCH requests so body can be re-read after model binding
// This is critical for endpoints that accept both FormData and JSON (like CreateTicket)
app.Use(async (context, next) =>
{
    if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH")
    {
        context.Request.EnableBuffering();
    }
    await next();
});

// Forwarded headers for IIS/reverse proxy: X-Forwarded-Proto (scheme) and X-Forwarded-For (client IP).
// MUST run before UseAuthentication so Request.IsHttps and cookie Secure/SameSite are correct when TLS is terminated at IIS.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedFor
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Ticketing.Backend.Infrastructure.Auth.WindowsAuthModeMiddleware>();

app.MapGet("/api/ping", () => Results.Ok(new { message = "pong" }));

// Deploy verification: anonymous so we get 200 to confirm runtime (no 404). Paths as-is, no route groups.
const string DiagBuildStamp = "tikq-runtime-diag-v1";
app.MapGet("/diag/build", () => Results.Json(new { build = DiagBuildStamp }));
app.MapGet("/api/diag/build", () => Results.Json(new { build = DiagBuildStamp }));

// Health endpoint for connectivity checks
// CRITICAL: This endpoint is used by frontend and verify-prod.ps1; schema must match docs (provider, database.*, migration indicators).
app.MapGet("/api/health", async (AppDbContext dbContext, IConfiguration configuration, Ticketing.Backend.Infrastructure.Data.DatabaseOptions databaseOptions) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var isSqlServerHealth = databaseOptions.IsSqlServer;

    var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/ticketing.db";
    string dbPath = "unknown";
    string connectionInfoRedacted = "unknown";
    bool canConnectToDb = false;
    bool dbFileExists = false;
    int categoryCount = 0;
    int ticketCount = 0;
    int userCount = 0;
    string? dbError = null;
    int? pendingMigrationsCount = null;
    string? lastMigrationId = null;

    // Derive provider from DbContext.Database.ProviderName (stable; no config drift)
    string providerDisplay = databaseOptions.NormalizedProvider;
    try
    {
        var pn = dbContext.Database.ProviderName ?? "";
        if (pn.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            providerDisplay = "SqlServer";
        else if (pn.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            providerDisplay = "Sqlite";
    }
    catch { /* fallback to databaseOptions already set */ }

    try
    {
        if (isSqlServerHealth)
        {
            try
            {
                var csb = new SqlConnectionStringBuilder(connectionString);
                dbPath = $"Server={csb.DataSource};Database={csb.InitialCatalog}";
                connectionInfoRedacted = dbPath;
            }
            catch { dbPath = "SqlServer"; connectionInfoRedacted = "SqlServer"; }
            canConnectToDb = await dbContext.Database.CanConnectAsync();
            dbFileExists = canConnectToDb;
        }
        else
        {
            if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var extractedPath = connectionString.Substring("Data Source=".Length).Trim();
                if (extractedPath.IndexOf(';') >= 0)
                    extractedPath = extractedPath.Substring(0, extractedPath.IndexOf(';')).Trim();
                if (Path.IsPathRooted(extractedPath))
                    dbPath = extractedPath;
                else
                    dbPath = Path.Combine(app.Environment.ContentRootPath, extractedPath);
            }
            connectionInfoRedacted = dbPath;
            dbFileExists = File.Exists(dbPath);
            canConnectToDb = await dbContext.Database.CanConnectAsync();
        }

        if (canConnectToDb)
        {
            try
            {
                categoryCount = await dbContext.Categories.CountAsync();
                ticketCount = await dbContext.Tickets.CountAsync();
                userCount = await dbContext.Users.CountAsync();
                logger.LogInformation("[HEALTH] DB Stats: Categories={Categories}, Tickets={Tickets}, Users={Users}",
                    categoryCount, ticketCount, userCount);
            }
            catch (Exception countEx)
            {
                dbError = $"Count query failed: {countEx.Message}";
                logger.LogWarning(countEx, "[HEALTH] Failed to query data counts");
            }

            try
            {
                var pending = await dbContext.Database.GetPendingMigrationsAsync();
                pendingMigrationsCount = pending.Count();
                var applied = await dbContext.Database.GetAppliedMigrationsAsync();
                lastMigrationId = applied.OrderBy(x => x).LastOrDefault();
            }
            catch (Exception migEx)
            {
                logger.LogWarning(migEx, "[HEALTH] Could not get migration status (pending/last): {Message}", migEx.Message);
            }
        }
    }
    catch (Exception ex)
    {
        dbError = ex.Message;
        logger.LogWarning(ex, "[HEALTH] Could not verify DB connection: {Error}", ex.Message);
    }

    var isHealthy = isSqlServerHealth ? canConnectToDb : (canConnectToDb && dbFileExists);
    var hasData = categoryCount > 0 || userCount > 0;
    // path: sqlite file path when Sqlite, null when SqlServer (do not expose server path as "path")
    string? pathValue = string.Equals(providerDisplay, "Sqlite", StringComparison.Ordinal) ? dbPath : null;

    logger.LogInformation("[HEALTH] Check complete: Provider={Provider}, Connected={Connected}, HasData={HasData}",
        providerDisplay, canConnectToDb, hasData);

    // Safe provider/env diagnostics: env presence flags only (no secrets)
    var effectiveEnvVarsPresent = new
    {
        Jwt__Secret = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Jwt__Secret")),
        ConnectionStrings__DefaultConnection = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")),
        Database__Provider = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Database__Provider")),
        Database__AutoMigrateOnStartup = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Database__AutoMigrateOnStartup")),
        Bootstrap__Enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Bootstrap__Enabled")),
        CompanyDirectory__Enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CompanyDirectory__Enabled")),
        AuthCookies__SecurePolicy = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AuthCookies__SecurePolicy")),
        AuthCookies__SameSite = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AuthCookies__SameSite"))
    };

    // Stable response shape for verify-prod.ps1 and load balancers (see docs/01_Runbook/HEALTH_SCHEMA.md)
    return Results.Ok(new
    {
        ok = isHealthy,
        status = isHealthy ? "healthy" : "degraded",
        environment = app.Environment.EnvironmentName,
        contentRoot = app.Environment.ContentRootPath,
        database = new
        {
            provider = providerDisplay,
            connectionInfoRedacted,
            path = pathValue,
            canConnect = canConnectToDb,
            error = (string?)dbError,
            dataCounts = new
            {
                categories = categoryCount,
                tickets = ticketCount,
                users = userCount
            },
            pendingMigrationsCount,
            lastMigrationId
        },
        effectiveEnvVarsPresent
    });
});

// Technician work report (explicit route so 404 does not occur if controller routing fails)
// GET .../technician-work?from=YYYY-MM-DD&to=YYYY-MM-DD&userId=...&format=json|xlsx
app.MapGet("/api/admin/reports/technician-work", async (
    HttpContext httpContext,
    string? from,
    string? to,
    string? userId,
    string? format,
    Ticketing.Backend.Application.Services.IReportService reportService,
    ILogger<Program> logger,
    IWebHostEnvironment env) =>
{
    Guid? userIdGuid = null;
    if (!string.IsNullOrWhiteSpace(userId))
    {
        if (string.Equals(userId.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            userIdGuid = null;
        else if (!Guid.TryParse(userId, out var parsed))
            return Results.BadRequest(new { title = "Invalid userId", detail = "userId must be a valid GUID." });
        else
            userIdGuid = parsed;
    }

    try
    {
        var (startDate, endDate) = ReportsDateRange.ParseForTechnicianWork(from, to);
        if (env.IsDevelopment() && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            logger.LogInformation("[Dev] Parsed report range: from={From} to={To} => {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}", from, to, startDate, endDate);
        logger.LogInformation("[REPORT] technician-work from={From} to={To} => {Start:yyyy-MM-dd}..{End:yyyy-MM-dd} userId={UserId} format={Format}",
            from ?? "(null)", to ?? "(null)", startDate, endDate, userIdGuid, format ?? "json");

        var fmt = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim().ToLowerInvariant();
        if (fmt == "xlsx")
        {
            var excelBytes = await reportService.GenerateTechnicianWorkReportExcelAsync(startDate, endDate, userIdGuid);
            var fileName = $"technician-work-report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
            return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        var report = await reportService.GetTechnicianWorkReportAsync(startDate, endDate, userIdGuid);
        httpContext.Response.Headers.Append("X-Report-Row-Count", report.Users.Count.ToString());
        return Results.Ok(report);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to generate technician work report. TraceId: {TraceId}", httpContext.TraceIdentifier);
        if (env.IsDevelopment())
        {
            return Results.Json(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "Report generation failed",
                status = 500,
                detail = ex.Message,
                traceId = httpContext.TraceIdentifier,
                innerMessage = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            }, statusCode: 500);
        }
        return Results.Json(new { message = "Failed to generate report." }, statusCode: 500);
    }
})
.RequireAuthorization("AdminOnly");

// Backward-compatible health endpoint (also at /health for compatibility)
app.MapGet("/health", async (AppDbContext dbContext, IConfiguration configuration, Ticketing.Backend.Infrastructure.Data.DatabaseOptions databaseOptions) =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var isSqlServerHealth = databaseOptions.IsSqlServer;

    var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/ticketing.db";
    string dbPath = "unknown";
    bool canConnectToDb = false;
    bool dbFileExists = false;
    int userCount = 0;
    int categoryCount = 0;
    try
    {
        if (isSqlServerHealth)
        {
            try { var csb = new SqlConnectionStringBuilder(connectionString); dbPath = $"Server={csb.DataSource};Database={csb.InitialCatalog}"; }
            catch { dbPath = "SqlServer"; }
            canConnectToDb = await dbContext.Database.CanConnectAsync();
            dbFileExists = canConnectToDb;
        }
        else
        {
            if (connectionString.Contains("Data Source="))
            {
                var extractedPath = connectionString.Split(new[] { "Data Source=" }, StringSplitOptions.None)[1].Split(';')[0].Trim();
                dbPath = Path.IsPathRooted(extractedPath) ? extractedPath : Path.Combine(app.Environment.ContentRootPath, extractedPath);
            }
            dbFileExists = File.Exists(dbPath);
            canConnectToDb = await dbContext.Database.CanConnectAsync();
        }
        if (canConnectToDb)
        {
            try
            {
                userCount = await dbContext.Users.CountAsync();
                categoryCount = await dbContext.Categories.CountAsync();
            }
            catch { /* non-fatal */ }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "[HEALTH] Could not verify DB connection: {Error}", ex.Message);
    }
    var hasData = categoryCount > 0 || userCount > 0;
    var isHealthy = isSqlServerHealth ? canConnectToDb : (canConnectToDb && dbFileExists);
    return Results.Ok(new
    {
        ok = isHealthy,
        status = isHealthy ? "healthy" : "degraded",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        hasData,
        database = new
        {
            provider = databaseOptions.NormalizedProvider,
            path = dbPath,
            fileExists = dbFileExists,
            connected = canConnectToDb,
            usersCount = userCount
        }
    });
});

// DEBUG endpoint: Detailed data visibility diagnostics (Development only, Admin only)
// IMPORTANT: Use this endpoint when "no data showing" to diagnose root cause
app.MapGet("/api/debug/data-status", async (
    AppDbContext dbContext,
    IConfiguration configuration,
    IWebHostEnvironment env,
    Ticketing.Backend.Infrastructure.Data.DatabaseOptions databaseOptions) =>
{
    if (!env.IsDevelopment())
    {
        return Results.NotFound();
    }

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("[DEBUG] Data status check requested");
    
    var provider = databaseOptions.NormalizedProvider;
    var isSqlServerDebug = databaseOptions.IsSqlServer;
    var resolvedPathOrSummary = isSqlServerDebug ? databasePathOrSummary : (sqliteDbPath ?? "unknown");
    var fileExists = !isSqlServerDebug && sqliteDbPath != null && File.Exists(sqliteDbPath);
    var fileSizeBytes = fileExists && sqliteDbPath != null ? new FileInfo(sqliteDbPath).Length : 0L;
    
    var result = new
    {
        timestamp = DateTime.UtcNow,
        database = new
        {
            provider = provider,
            resolvedPathOrSummary = resolvedPathOrSummary,
            fileExists = fileExists,
            fileSizeBytes = fileSizeBytes,
            contentRoot = app.Environment.ContentRootPath,
            currentDirectory = Directory.GetCurrentDirectory()
        },
        dataCounts = new
        {
            users = 0,
            categories = 0,
            subcategories = 0,
            tickets = 0,
            technicians = 0
        },
        sampleData = new
        {
            firstUser = (object?)null,
            firstCategory = (object?)null,
            firstTicket = (object?)null
        },
        errors = new List<string>()
    };
    
    var errors = new List<string>();
    int userCount = 0, categoryCount = 0, subcategoryCount = 0, ticketCount = 0, technicianCount = 0;
    object? firstUser = null, firstCategory = null, firstTicket = null;
    
    try
    {
        userCount = await dbContext.Users.CountAsync();
        categoryCount = await dbContext.Categories.CountAsync();
        subcategoryCount = await dbContext.Subcategories.CountAsync();
        ticketCount = await dbContext.Tickets.CountAsync();
        technicianCount = await dbContext.Technicians.CountAsync();
        
        // Get sample data for debugging
        var user = await dbContext.Users.FirstOrDefaultAsync();
        if (user != null)
        {
            firstUser = new { user.Id, user.Email, user.FullName, user.Role };
        }
        
        var category = await dbContext.Categories.Include(c => c.Subcategories).FirstOrDefaultAsync();
        if (category != null)
        {
            firstCategory = new { category.Id, category.Name, category.IsActive, SubcategoryCount = category.Subcategories.Count };
        }
        
        var ticket = await dbContext.Tickets.Include(t => t.CreatedByUser).FirstOrDefaultAsync();
        if (ticket != null)
        {
            firstTicket = new { ticket.Id, ticket.Title, ticket.Status, CreatedBy = ticket.CreatedByUser?.Email };
        }
        
        logger.LogInformation("[DEBUG] Data counts: Users={Users}, Categories={Categories}, Tickets={Tickets}, Technicians={Technicians}",
            userCount, categoryCount, ticketCount, technicianCount);
    }
    catch (Exception ex)
    {
        errors.Add($"Query error: {ex.Message}");
        logger.LogError(ex, "[DEBUG] Failed to query data: {Error}", ex.Message);
    }
    
    return Results.Ok(new
    {
        timestamp = DateTime.UtcNow,
        database = new
        {
            provider = provider,
            resolvedPathOrSummary = resolvedPathOrSummary,
            fileExists = fileExists,
            fileSizeBytes = fileSizeBytes,
            contentRoot = app.Environment.ContentRootPath,
            currentDirectory = Directory.GetCurrentDirectory()
        },
        dataCounts = new
        {
            users = userCount,
            categories = categoryCount,
            subcategories = subcategoryCount,
            tickets = ticketCount,
            technicians = technicianCount
        },
        sampleData = new
        {
            firstUser = firstUser,
            firstCategory = firstCategory,
            firstTicket = firstTicket
        },
        diagnosis = userCount == 0 && categoryCount == 0 
            ? "DATABASE_EMPTY: No seed data found. Either migrations didn't run or wrong database file is being used."
            : ticketCount == 0 && userCount > 0
                ? "NO_TICKETS: Users exist but no tickets. Try creating a ticket or check if data was seeded."
                : "DATA_EXISTS: Database has data. If UI shows empty, check frontend API calls or authorization.",
        errors = errors
    });
})
.RequireAuthorization("AdminOnly");

// DEBUG: Supervisor config (Development only, Admin only)
app.MapGet("/api/debug/config/supervisor-mode", (IConfiguration configuration, IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment())
        return Results.NotFound();

    var modeRaw = configuration["SupervisorTechnicians:Mode"]?.Trim();
    var modeResolved = !string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "LinkedOnly", StringComparison.OrdinalIgnoreCase)
        ? "LinkedOnly"
        : (!string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "AllByDefault", StringComparison.OrdinalIgnoreCase))
            ? "AllByDefault"
            : "AllByDefault";
    var section = configuration.GetSection("SupervisorTechnicians");
    var rawSection = new Dictionary<string, string?>();
    if (section.Exists())
    {
        foreach (var child in section.GetChildren())
            rawSection[child.Key] = section[child.Key];
    }
    return Results.Ok(new
    {
        environmentName = env.EnvironmentName,
        modeResolved,
        supervisorTechniciansModeRaw = modeRaw ?? "(null/empty)",
        sourcesLoaded = new[] { "appsettings.json", env.EnvironmentName == "Development" ? "appsettings.Development.json" : null }.Where(x => x != null).ToArray(),
        supervisorTechnicians = rawSection
    });
})
.RequireAuthorization("AdminOnly");

// DEBUG: Supervisor technicians diagnose (Development only, Admin only)
app.MapGet("/api/debug/supervisor/technicians/diagnose", async (
    Ticketing.Backend.Application.Services.ISupervisorService supervisorService,
    IConfiguration configuration,
    IWebHostEnvironment env) =>
{
    if (!env.IsDevelopment())
        return Results.NotFound();

    var modeRaw = configuration["SupervisorTechnicians:Mode"]?.Trim();
    var modeResolved = !string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "LinkedOnly", StringComparison.OrdinalIgnoreCase)
        ? "LinkedOnly"
        : (!string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "AllByDefault", StringComparison.OrdinalIgnoreCase))
            ? "AllByDefault"
            : "AllByDefault";
    var diag = await supervisorService.GetSupervisorTechniciansDiagnosticAsync(null);
    return Results.Ok(new
    {
        environmentName = env.EnvironmentName,
        modeResolved,
        currentUserId = (Guid?)null,
        activeTechCount = diag.ActiveTechCount,
        linkedCount = diag.LinkedCount,
        sampleActiveTechEmails = diag.SampleActiveTechEmails,
        sampleLinkedTechIds = diag.SampleLinkedTechIds
    });
})
.RequireAuthorization("AdminOnly");

// Serve static files from App_Data/uploads
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads");
if (Directory.Exists(uploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
}

// Route mapping is unconditional (all environments). Do not gate MapControllers or /api/health on IsDevelopment().
app.MapControllers();

// =======================
// SignalR Hub Endpoints
// =======================
// Map the TicketHub for real-time ticket status synchronization
// Frontend clients connect to /hubs/tickets to receive instant updates
app.MapHub<Ticketing.Backend.Infrastructure.Hubs.TicketHub>("/hubs/tickets").RequireCors("DevCors");
app.Logger.LogInformation("[SignalR] Hub mapped at {HubRoute}", "/hubs/tickets");

app.Logger.LogInformation("[STARTUP] Routes mapped: Controllers=ON, Health=/api/health");
app.Logger.LogInformation("DIAG BUILD ACTIVE: tikq-runtime-diag-v1");

// =======================
// Port 5000 Preflight Check (Development only)
// =======================
if (app.Environment.IsDevelopment())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    const int defaultPort = 5000;
    
    try
    {
        // Try to detect if port 5000 is already in use
        using var testSocket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, defaultPort);
        testSocket.Start();
        testSocket.Stop();
    }
    catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
    {
        logger.LogError("=");
        logger.LogError("PORT CONFLICT DETECTED");
        logger.LogError("=");
        logger.LogError("Port {Port} is already in use. Backend cannot start.", defaultPort);
        logger.LogError("");
        logger.LogError("DIAGNOSTICS:");
        logger.LogError("  Run this command to find the process using port {Port}:", defaultPort);
        logger.LogError("    netstat -ano | findstr :{Port}", defaultPort);
        logger.LogError("");
        logger.LogError("SOLUTION:");
        logger.LogError("  1. Run the safe backend runner script:");
        logger.LogError("     .\\tools\\run-backend.ps1");
        logger.LogError("");
        logger.LogError("  2. Or manually stop the process:");
        logger.LogError("     - Find PID using: netstat -ano | findstr :{Port}", defaultPort);
        logger.LogError("     - Stop it: taskkill /PID <pid> /F");
        logger.LogError("");
        
        // Try to get more info about what's using the port (Windows-specific)
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "netstat",
                        Arguments = "-ano",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains($":{defaultPort}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && int.TryParse(parts.Last(), out var pid))
                        {
                            try
                            {
                                var blockingProcess = System.Diagnostics.Process.GetProcessById(pid);
                                logger.LogError("  Process using port {Port}:", defaultPort);
                                logger.LogError("    PID: {Pid}", pid);
                                logger.LogError("    Name: {Name}", blockingProcess.ProcessName);
                                logger.LogError("    Path: {Path}", blockingProcess.MainModule?.FileName ?? "N/A");
                                logger.LogError("");
                            }
                            catch
                            {
                                logger.LogError("  Process using port {Port}: PID {Pid} (could not get details)", defaultPort, pid);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in diagnostics
            }
        }
        
        logger.LogError("=");
        logger.LogError("");
        
        // Exit gracefully instead of throwing a stack trace
        Environment.Exit(1);
    }
}

// =======================
// Log listening URLs on startup
// =======================
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var configuredUrls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5000";
var baseUrl = configuredUrls.Split(';')[0].Trim();

// Extract port from URL for clear display
var port = "unknown";
try
{
    var uri = new Uri(baseUrl);
    port = uri.Port.ToString();
}
catch
{
    // If parsing fails, try to extract port manually
    var portMatch = System.Text.RegularExpressions.Regex.Match(baseUrl, @":(\d+)");
    if (portMatch.Success)
    {
        port = portMatch.Groups[1].Value;
    }
}

startupLogger.LogInformation("=");
startupLogger.LogInformation("Backend Server Starting");
startupLogger.LogInformation("TikQ build stamp: {BuildStamp}", DiagBuildStamp);
startupLogger.LogInformation("=");
startupLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
var supervisorModeRaw = app.Configuration["SupervisorTechnicians:Mode"]?.Trim();
var supervisorModeResolved = !string.IsNullOrEmpty(supervisorModeRaw) && string.Equals(supervisorModeRaw, "LinkedOnly", StringComparison.OrdinalIgnoreCase)
    ? "LinkedOnly"
    : (!string.IsNullOrEmpty(supervisorModeRaw) && string.Equals(supervisorModeRaw, "AllByDefault", StringComparison.OrdinalIgnoreCase))
        ? "AllByDefault"
        : (app.Environment.IsDevelopment() ? "AllByDefault" : "LinkedOnly");
startupLogger.LogInformation("[SUPERVISOR_MODE] Environment={Env}, ModeResolved={ModeResolved}, SupervisorTechnicians:Mode(raw)={ModeRaw}", app.Environment.EnvironmentName, supervisorModeResolved, supervisorModeRaw ?? "(null/empty)");
startupLogger.LogInformation("Listening on: {Urls}", configuredUrls);
startupLogger.LogInformation("Base URL: {BaseUrl}", baseUrl);
startupLogger.LogInformation("Port: {Port}", port);
startupLogger.LogInformation("Swagger UI: {SwaggerUrl}", $"{baseUrl}/swagger");
startupLogger.LogInformation("Health Check: {HealthUrl}", $"{baseUrl}/api/health");
startupLogger.LogInformation("CORS Origins: {Origins}", string.Join(", ", allowedCorsOrigins));
startupLogger.LogInformation("=");
startupLogger.LogInformation("IMPORTANT: Frontend should use NEXT_PUBLIC_API_BASE_URL={BaseUrl}", baseUrl);
startupLogger.LogInformation("=");

app.Run();

