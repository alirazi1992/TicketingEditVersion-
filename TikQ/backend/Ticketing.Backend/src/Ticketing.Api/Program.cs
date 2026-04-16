using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Enums;
using Ticketing.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Database: SqlServer or Sqlite from config (Database:Provider / Database__Provider, ConnectionStrings:DefaultConnection)
var providerRaw = (Environment.GetEnvironmentVariable("Database__Provider") ?? builder.Configuration["Database:Provider"] ?? "Sqlite").Trim();
var isSqlServer = string.Equals(providerRaw, "SqlServer", StringComparison.OrdinalIgnoreCase);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/ticketing.db";

if (isSqlServer)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
}
else
{
    var sqlitePath = ResolveSqliteDbPath(builder.Configuration, builder.Environment.ContentRootPath);
    var sqliteConnectionString = $"Data Source={sqlitePath}";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(sqliteConnectionString));
    var dir = Path.GetDirectoryName(sqlitePath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        try { Directory.CreateDirectory(dir); } catch { /* best effort */ }
    }
}

builder.Services.AddControllers();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", p => p.RequireRole(nameof(UserRole.Admin))));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

const string BuildStamp = "tikq-build-2026-02-25-diag-v1";
app.MapGet("/diag/build", () => Results.Json(new { build = BuildStamp }))
    .RequireAuthorization("AdminOnly");
app.MapGet("/api/diag/build", () => Results.Json(new { build = BuildStamp }))
    .RequireAuthorization("AdminOnly");
app.Logger.LogInformation("BuildStamp: {BuildStamp}", BuildStamp);

// Stable health schema for handoff and verify-prod.ps1 (unauthenticated). See docs/01_Runbook/HEALTH_SCHEMA.md
app.MapGet("/api/health", async (AppDbContext dbContext, IConfiguration configuration, IWebHostEnvironment env) =>
{
    var connectionStringForHealth = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/ticketing.db";
    string connectionInfoRedacted = "unknown";
    string? pathValue = null;
    var canConnect = false;
    string? dbError = null;
    var categoryCount = 0;
    var ticketCount = 0;
    var userCount = 0;
    int? pendingMigrationsCount = null;
    string? lastMigrationId = null;

    // Provider from DbContext.Database.ProviderName (required for verification)
    var providerName = dbContext.Database.ProviderName ?? "";
    var providerDisplay = providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ? "SqlServer"
        : providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ? "Sqlite"
        : "Unknown";

    try
    {
        if (providerDisplay == "SqlServer")
        {
            try
            {
                var csb = new SqlConnectionStringBuilder(connectionStringForHealth);
                connectionInfoRedacted = $"Server={csb.DataSource};Database={csb.InitialCatalog}";
            }
            catch
            {
                connectionInfoRedacted = "SqlServer";
            }
            pathValue = null;
        }
        else
        {
            if (connectionStringForHealth.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                var extracted = connectionStringForHealth.Substring("Data Source=".Length).Trim();
                if (extracted.IndexOf(';') >= 0) extracted = extracted.Substring(0, extracted.IndexOf(';')).Trim();
                pathValue = Path.IsPathRooted(extracted)
                    ? extracted
                    : Path.Combine(env.ContentRootPath, extracted);
            }
            connectionInfoRedacted = pathValue ?? "Sqlite";
        }

        canConnect = await dbContext.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        dbError = ex.Message;
    }

    if (canConnect)
    {
        try
        {
            categoryCount = await dbContext.Categories.CountAsync();
        }
        catch (Exception ex)
        {
            dbError = (dbError != null ? dbError + "; " : null) + "Categories: " + ex.Message;
        }
        try
        {
            ticketCount = await dbContext.Tickets.CountAsync();
        }
        catch (Exception ex)
        {
            dbError = (dbError != null ? dbError + "; " : null) + "Tickets: " + ex.Message;
        }
        try
        {
            userCount = await dbContext.Users.CountAsync();
        }
        catch (Exception ex)
        {
            dbError = (dbError != null ? dbError + "; " : null) + "Users: " + ex.Message;
        }

        try
        {
            var pending = await dbContext.Database.GetPendingMigrationsAsync();
            pendingMigrationsCount = pending.Count();
            var applied = await dbContext.Database.GetAppliedMigrationsAsync();
            lastMigrationId = applied.OrderBy(x => x).LastOrDefault();
        }
        catch (Exception ex)
        {
            dbError = (dbError != null ? dbError + "; " : null) + "Migrations: " + ex.Message;
        }
    }

    var isHealthy = canConnect;
    var status = isHealthy ? "healthy" : "degraded";

    return Results.Ok(new
    {
        ok = isHealthy,
        status,
        timestamp = DateTime.UtcNow,
        environment = env.EnvironmentName,
        contentRoot = env.ContentRootPath,
        database = new
        {
            provider = providerDisplay,
            connectionInfoRedacted,
            path = pathValue,
            canConnect,
            error = (string?)dbError,
            dataCounts = new
            {
                categories = categoryCount,
                tickets = ticketCount,
                users = userCount
            },
            pendingMigrationsCount,
            lastMigrationId
        }
    });
});

app.Logger.LogInformation("[STARTUP] Routes mapped: Controllers=ON, Health=/api/health, Diag=/diag/build, /api/diag/build");

app.Run();

static string ResolveSqliteDbPath(IConfiguration config, string contentRoot)
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    var relativePath = "App_Data/ticketing.db";
    if (!string.IsNullOrWhiteSpace(connectionString) &&
        connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
    {
        var extracted = connectionString.Substring("Data Source=".Length).Trim();
        if (extracted.IndexOf(';') >= 0) extracted = extracted.Substring(0, extracted.IndexOf(';')).Trim();
        if (!string.IsNullOrWhiteSpace(extracted)) relativePath = extracted;
    }
    if (Path.IsPathRooted(relativePath))
        return relativePath;
    return Path.Combine(contentRoot, relativePath);
}
