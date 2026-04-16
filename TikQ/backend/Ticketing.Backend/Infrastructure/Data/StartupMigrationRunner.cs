using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Ticketing.Backend.Infrastructure.Data;

/// <summary>
/// Runs EF Core migrations on startup when <see cref="DatabaseOptions.AutoMigrateOnStartup"/> is true.
/// Logs target provider, pending migrations, and result. In Production with SqlServer, rethrows on failure to fail fast.
/// Safe to run under IIS (uses app pool identity; runs after host build, not during publish).
/// </summary>
public static class StartupMigrationRunner
{
    /// <summary>
    /// Runs migrations if enabled; otherwise logs skip and returns.
    /// </summary>
    /// <param name="context">The application DbContext.</param>
    /// <param name="databaseOptions">Database section options (Provider, AutoMigrateOnStartup).</param>
    /// <param name="logger">Logger for migration messages.</param>
    /// <param name="environment">Host environment (used to decide fail-fast on error).</param>
    /// <param name="databasePathOrSummary">Redacted connection summary or path for logs (e.g. "Server=X;Database=Y" or absolute SQLite path).</param>
    /// <param name="sqliteFileExists">If provider is SQLite, whether the database file already exists (optional, for logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RunAsync(
        AppDbContext context,
        DatabaseOptions databaseOptions,
        ILogger logger,
        IWebHostEnvironment environment,
        string databasePathOrSummary,
        bool? sqliteFileExists = null,
        CancellationToken cancellationToken = default)
    {
        if (!databaseOptions.AutoMigrateOnStartup)
        {
            logger.LogInformation("[MIGRATION] Skipped (AutoMigrateOnStartup is false). Apply migrations manually if needed (see docs/01_Runbook/MIGRATIONS.md).");
            return;
        }

        var provider = databaseOptions.NormalizedProvider;
        logger.LogInformation("[MIGRATION] AutoMigrateOnStartup=true; running migration check. Target provider: {Provider}", provider);
        logger.LogInformation("[MIGRATION] Database: {DbSummary}", databasePathOrSummary);
        if (databaseOptions.IsSqlite && sqliteFileExists.HasValue)
            logger.LogInformation("[MIGRATION] SQLite file exists: {Exists}", sqliteFileExists.Value);

        try
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            var appliedBefore = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            logger.LogInformation("[MIGRATION] Applied migrations (before): {Applied}", appliedBefore.Count > 0 ? string.Join(", ", appliedBefore) : "(none)");
            logger.LogInformation("[MIGRATION] Pending migrations: {Pending}", pending.Count > 0 ? string.Join(", ", pending) : "(none)");

            if (pending.Count == 0)
            {
                logger.LogInformation("[MIGRATION] No pending migrations; database is up to date.");
                return;
            }

            foreach (var m in pending)
                logger.LogInformation("[MIGRATION] Applying: {MigrationName}", m);
            await context.Database.MigrateAsync(cancellationToken);

            var appliedAfter = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            logger.LogInformation("[MIGRATION] Applied migrations (after): {Applied}", string.Join(", ", appliedAfter));
            logger.LogInformation("[MIGRATION] Result: applied {Count} migration(s). Migrations completed successfully.", pending.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MIGRATION] Error applying migrations: {Message}. Full exception: {FullException}", ex.Message, ex.ToString());

            if (environment.IsProduction() && databaseOptions.IsSqlServer)
            {
                logger.LogError("[MIGRATION] Production + SqlServer: failing fast. Fix the database or connection and restart.");
                throw;
            }

            logger.LogWarning("[MIGRATION] Continuing without rethrow (non-Production or SQLite). Fix migrations or apply manually (see docs/01_Runbook/MIGRATIONS.md).");
        }
    }
}
