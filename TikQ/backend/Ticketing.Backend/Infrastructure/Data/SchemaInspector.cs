using Microsoft.EntityFrameworkCore;

namespace Ticketing.Backend.Infrastructure.Data;

/// <summary>
/// Lightweight schema inspection for SQL Server and SQLite (e.g. detect Key vs FieldKey column).
/// </summary>
public static class SchemaInspector
{
    /// <summary>
    /// Returns "FieldKey", "Key", or null if table/column cannot be determined.
    /// </summary>
    public static async Task<string?> GetSubcategoryFieldDefinitionKeyColumnNameAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var providerName = context.Database.ProviderName ?? "";
            if (providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'SubcategoryFieldDefinitions' AND COLUMN_NAME IN (N'FieldKey', N'Key')
ORDER BY CASE COLUMN_NAME WHEN N'FieldKey' THEN 0 ELSE 1 END";
                var name = await cmd.ExecuteScalarAsync(cancellationToken);
                return name?.ToString();
            }

            if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM pragma_table_info('SubcategoryFieldDefinitions') WHERE name IN ('FieldKey', 'Key') ORDER BY CASE name WHEN 'FieldKey' THEN 0 ELSE 1 END";
                var name = await cmd.ExecuteScalarAsync(cancellationToken);
                return name?.ToString();
            }
        }
        catch
        {
            // Ignore; return null
        }

        return null;
    }
}
