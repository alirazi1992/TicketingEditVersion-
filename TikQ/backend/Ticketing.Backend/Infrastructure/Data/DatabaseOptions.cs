namespace Ticketing.Backend.Infrastructure.Data;

/// <summary>
/// Strongly-typed options for database provider selection and startup behavior.
/// Section: "Database". Env: Database__Provider, Database__AutoMigrateOnStartup.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Database provider: "Sqlite" or "SqlServer" (case-insensitive).
    /// Unknown values cause startup to throw.
    /// </summary>
    public string Provider { get; set; } = "Sqlite";

    /// <summary>
    /// When true, run EF migrations on startup. Default: true in Development, false in Production
    /// unless explicitly enabled via config (Database:AutoMigrateOnStartup or Database__AutoMigrateOnStartup).
    /// </summary>
    public bool AutoMigrateOnStartup { get; set; }

    /// <summary>
    /// Normalized provider for comparison: "SqlServer" or "Sqlite". Throws if invalid.
    /// </summary>
    public string NormalizedProvider
    {
        get
        {
            var p = (Provider ?? "Sqlite").Trim();
            if (string.IsNullOrEmpty(p)) return "Sqlite";
            if (string.Equals(p, "SqlServer", StringComparison.OrdinalIgnoreCase)) return "SqlServer";
            if (string.Equals(p, "Sqlite", StringComparison.OrdinalIgnoreCase)) return "Sqlite";
            throw new InvalidOperationException(
                $"Database:Provider must be 'Sqlite' or 'SqlServer' (case-insensitive). Current value: '{Provider}'.");
        }
    }

    public bool IsSqlServer => string.Equals(NormalizedProvider, "SqlServer", StringComparison.Ordinal);
    public bool IsSqlite => string.Equals(NormalizedProvider, "Sqlite", StringComparison.Ordinal);
}
