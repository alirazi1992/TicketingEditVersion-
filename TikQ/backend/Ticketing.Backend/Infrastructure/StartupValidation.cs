namespace Ticketing.Backend.Infrastructure;

/// <summary>
/// Centralized production handoff validation. Runs at startup when Production or ProductionHandoffMode is true.
/// Fails fast with clear messages if required config is missing.
/// </summary>
public static class StartupValidation
{
    /// <summary>
    /// When true, run production validation (env ProductionHandoffMode=true or Handoff:ProductionHandoffMode in config).
    /// </summary>
    public static bool IsProductionHandoffMode(IConfiguration config)
    {
        var v = config["ProductionHandoffMode"];
        if (!string.IsNullOrWhiteSpace(v) && bool.TryParse(v, out var b)) return b;
        v = config["Handoff:ProductionHandoffMode"];
        if (!string.IsNullOrWhiteSpace(v) && bool.TryParse(v, out b)) return b;
        return string.Equals(Environment.GetEnvironmentVariable("ProductionHandoffMode"), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Run validation. Throws InvalidOperationException if any required setting is missing.
    /// Call only when env is Production or ProductionHandoffMode is true.
    /// </summary>
    public static void ValidateProductionConfig(
        IConfiguration config,
        bool isProductionEnvironment,
        string? resolvedJwtSecret,
        Action<string> log)
    {
        var isHandoff = IsProductionHandoffMode(config);
        if (!isProductionEnvironment && !isHandoff)
            return;

        // 1) JWT secret required
        if (string.IsNullOrWhiteSpace(resolvedJwtSecret))
        {
            throw new InvalidOperationException(
                "Production validation failed: Jwt:Secret (or JWT_SECRET / Jwt__Secret) is required. " +
                "Set Jwt:Secret in appsettings or JWT_SECRET environment variable.");
        }

        // 2) CompanyDirectory: when Enabled, require ConnectionString and valid Mode
        var companyEnabled = config.GetValue<bool>("CompanyDirectory:Enabled");
        if (companyEnabled)
        {
            var conn = config["CompanyDirectory:ConnectionString"]?.Trim();
            if (string.IsNullOrEmpty(conn))
            {
                throw new InvalidOperationException(
                    "Production validation failed: CompanyDirectory:Enabled is true but CompanyDirectory:ConnectionString is empty. " +
                    "Set CompanyDirectory:ConnectionString or disable CompanyDirectory.");
            }
            var mode = config["CompanyDirectory:Mode"]?.Trim();
            var validModes = new[] { "Enforce", "Optional", "Friendly" };
            if (!string.IsNullOrEmpty(mode) && !validModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production validation failed: CompanyDirectory:Mode must be one of: Enforce, Optional, Friendly. " +
                    "Current value: " + (mode ?? "(empty)"));
            }
        }

        // 2b) EmergencyAdmin: when Enabled in Production/Handoff, require Email, Password (min 8), Key (no default passwords)
        var emergencyEnabled = config.GetValue<bool>("EmergencyAdmin:Enabled");
        if (emergencyEnabled)
        {
            var emEmail = config["EmergencyAdmin:Email"]?.Trim();
            var emPassword = config["EmergencyAdmin:Password"]?.Trim();
            var emKey = config["EmergencyAdmin:Key"]?.Trim();
            if (string.IsNullOrEmpty(emEmail))
            {
                throw new InvalidOperationException(
                    "Production validation failed: EmergencyAdmin:Enabled is true but EmergencyAdmin:Email is empty. " +
                    "Set EmergencyAdmin:Email or disable EmergencyAdmin.");
            }
            if (string.IsNullOrEmpty(emPassword) || emPassword.Length < 8)
            {
                throw new InvalidOperationException(
                    "Production validation failed: EmergencyAdmin:Enabled is true but EmergencyAdmin:Password is missing or shorter than 8 characters. " +
                    "Set EmergencyAdmin:Password via environment (e.g. EmergencyAdmin__Password) in Production.");
            }
            if (string.IsNullOrEmpty(emKey))
            {
                throw new InvalidOperationException(
                    "Production validation failed: EmergencyAdmin:Enabled is true but EmergencyAdmin:Key is empty. " +
                    "Set EmergencyAdmin:Key via environment (e.g. EmergencyAdmin__Key) in Production.");
            }
        }

        // 3) Production environment: reject SQLite as main app DB unless explicitly allowed
        if (isProductionEnvironment)
        {
            var allowSqlite = config.GetValue<bool>("AllowSqliteInProduction");
            if (!allowSqlite)
            {
                var connStr = config.GetConnectionString("DefaultConnection") ?? "";
                var looksLikeSqlite = connStr.IndexOf("sqlite", StringComparison.OrdinalIgnoreCase) >= 0
                    || (connStr.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                        && connStr.IndexOf("Initial Catalog", StringComparison.OrdinalIgnoreCase) < 0
                        && connStr.IndexOf("Database=", StringComparison.OrdinalIgnoreCase) < 0);
                if (looksLikeSqlite)
                {
                    throw new InvalidOperationException(
                        "Production validation failed: SQLite is not allowed as the main app database in Production. " +
                        "Set ConnectionStrings:DefaultConnection to a SQL Server (or other production DB) connection string. " +
                        "To allow SQLite in production (e.g. for testing), set AllowSqliteInProduction=true.");
                }
            }
        }

        log("[HANDOFF] Production validation passed");
    }
}
