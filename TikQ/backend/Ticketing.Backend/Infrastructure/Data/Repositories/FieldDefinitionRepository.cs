using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class FieldDefinitionRepository : IFieldDefinitionRepository
{
    private readonly AppDbContext _context;

    public FieldDefinitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SubcategoryFieldDefinition?> GetByIdAsync(int id)
    {
        // Use AsNoTracking to avoid navigation property loading
        return await _context.SubcategoryFieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<IEnumerable<SubcategoryFieldDefinition>> GetBySubcategoryIdAsync(int subcategoryId, bool includeInactive = true)
    {
        var provider = _context.Database.ProviderName ?? "";
        if (!provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // SQL Server and others: use LINQ so we never run SQLite-style raw SQL.
            var query = _context.SubcategoryFieldDefinitions
                .AsNoTracking()
                .Where(f => f.SubcategoryId == subcategoryId);
            if (!includeInactive)
                query = query.Where(f => f.IsActive);
            return await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();
        }

        var sql = @"
            SELECT Id, SubcategoryId, Name, Label, FieldKey, Type, IsRequired,
                   DefaultValue, OptionsJson, Min, Max, SortOrder, IsActive, CreatedAt, UpdatedAt
            FROM SubcategoryFieldDefinitions
            WHERE SubcategoryId = {0}
            " + (includeInactive ? "" : " AND IsActive = 1 ") + @"
            ORDER BY SortOrder, Id
        ";
        try
        {
            return await _context.SubcategoryFieldDefinitions
                .FromSqlRaw(sql, subcategoryId)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex) when (IsMissingColumnException(ex))
        {
            // Backward compatibility: DB still has [Key] column (migration not applied). Use legacy column, alias as FieldKey.
            string legacySql = @"
            SELECT Id, SubcategoryId, Name, Label, ""Key"" AS FieldKey, Type, IsRequired,
                   DefaultValue, OptionsJson, Min, Max, SortOrder, IsActive, CreatedAt, UpdatedAt
            FROM SubcategoryFieldDefinitions
            WHERE SubcategoryId = {0}
            " + (includeInactive ? "" : " AND IsActive = 1 ") + @"
            ORDER BY SortOrder, Id";
            return await _context.SubcategoryFieldDefinitions
                .FromSqlRaw(legacySql, subcategoryId)
                .AsNoTracking()
                .ToListAsync();
        }
    }

    private static bool IsMissingColumnException(Exception ex)
    {
        if (ex is SqlException sqlEx)
            return sqlEx.Message.Contains("Invalid column name 'FieldKey'", StringComparison.OrdinalIgnoreCase) ||
                   sqlEx.Message.Contains("Invalid column name 'Key'", StringComparison.OrdinalIgnoreCase);
        if (ex is SqliteException sqliteEx)
            return sqliteEx.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase) &&
                   (sqliteEx.Message.Contains("FieldKey", StringComparison.OrdinalIgnoreCase) || sqliteEx.Message.Contains("Key", StringComparison.OrdinalIgnoreCase));
        return false;
    }

    public async Task<IEnumerable<SubcategoryFieldDefinition>> GetByCategoryIdAsync(int categoryId, bool includeInactive = true)
    {
        var query = _context.SubcategoryFieldDefinitions
            .AsNoTracking()
            .Include(f => f.Subcategory)
            .Where(f => f.Subcategory != null && f.Subcategory.CategoryId == categoryId);

        if (!includeInactive)
        {
            query = query.Where(f => f.IsActive);
        }

        return await query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<int> GetNextSubcategoryFieldDefinitionIdAsync()
    {
        var max = await _context.SubcategoryFieldDefinitions.MaxAsync(f => (int?)f.Id);
        return (max ?? 0) + 1;
    }

    public async Task<SubcategoryFieldDefinition> AddAsync(SubcategoryFieldDefinition fieldDefinition)
    {
        // Ensure IsRequired is explicitly set (defensive programming)
        if (fieldDefinition.IsRequired == default(bool))
        {
            fieldDefinition.IsRequired = false;
        }

        var provider = _context.Database.ProviderName ?? "";
        // Use EF Core for any non-SQLite provider (raw path uses SQLite-only: PRAGMA, datetime('now'), last_insert_rowid).
        if (!provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await _context.SubcategoryFieldDefinitions.AddAsync(fieldDefinition);
            return fieldDefinition;
        }

        // SQLite only: use parameterized SQL to safely insert and ensure IsRequired is explicitly set
        // (works around SQLite column definition issues where DEFAULT might not be applied)
        var isRequiredInt = fieldDefinition.IsRequired ? 1 : 0;
        
        // Get all columns from the table to handle legacy columns (SortOrder, IsActive, etc.)
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }
        
        var allColumns = new List<string>();
        var notNullColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "PRAGMA table_info(SubcategoryFieldDefinitions)";
            using (var reader = await checkCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(1);
                    var notNull = reader.GetInt32(3) == 1; // notnull flag is at index 3
                    allColumns.Add(columnName);
                    if (notNull)
                    {
                        notNullColumns.Add(columnName);
                    }
                }
            }
        }
        
        // Build SQL with proper null handling using string interpolation
        // Include all NOT NULL columns that aren't in our entity with safe defaults
        // Escape single quotes in string values
        string EscapeSqlString(string? value) => value == null ? "NULL" : $"'{value.Replace("'", "''")}'";
        string FormatNullableDouble(double? value) => value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL";
        
        // Core columns from entity
        var coreColumns = new List<string>
        {
            "SubcategoryId", "Name", "Label", "FieldKey", "Type", "IsRequired", "DefaultValue", "OptionsJson", "Min", "Max",
            "SortOrder", "IsActive", "CreatedAt", "UpdatedAt"
        };
        var coreValues = new List<string>
        {
            fieldDefinition.SubcategoryId.ToString(),
            EscapeSqlString(fieldDefinition.Name),
            EscapeSqlString(fieldDefinition.Label),
            EscapeSqlString(fieldDefinition.FieldKey),
            $"'{fieldDefinition.Type}'",
            isRequiredInt.ToString(),
            EscapeSqlString(fieldDefinition.DefaultValue),
            EscapeSqlString(fieldDefinition.OptionsJson),
            FormatNullableDouble(fieldDefinition.Min),
            FormatNullableDouble(fieldDefinition.Max),
            fieldDefinition.SortOrder.ToString(),
            fieldDefinition.IsActive ? "1" : "0",
            "datetime('now')",
            "datetime('now')"
        };
        
        // Add legacy columns with safe defaults
        var legacyColumnDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SortOrder", "0" },
            { "IsActive", "1" },
            { "CreatedAt", "datetime('now')" },
            { "UpdatedAt", "datetime('now')" }
        };
        
        foreach (var legacy in legacyColumnDefaults)
        {
            if (allColumns.Contains(legacy.Key) && !coreColumns.Contains(legacy.Key))
            {
                coreColumns.Add(legacy.Key);
                coreValues.Add(legacy.Value);
            }
        }
        
        // Build final SQL
        var columnsList = string.Join(", ", coreColumns);
        var valuesList = string.Join(", ", coreValues);
        
        var sql = $@"
            INSERT INTO SubcategoryFieldDefinitions 
            ({columnsList})
            VALUES 
            ({valuesList});
        ";
        
        await _context.Database.ExecuteSqlRawAsync(sql);
        
        // Get the generated ID (connection is already open from above)
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT last_insert_rowid();";
            var result = await command.ExecuteScalarAsync();
            fieldDefinition.Id = Convert.ToInt32(result);
        }
        
        if (!wasOpen)
        {
            await connection.CloseAsync();
        }
        
        return fieldDefinition;
    }

    public Task<SubcategoryFieldDefinition> UpdateAsync(SubcategoryFieldDefinition fieldDefinition)
    {
        _context.SubcategoryFieldDefinitions.Update(fieldDefinition);
        return Task.FromResult(fieldDefinition);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var field = await _context.SubcategoryFieldDefinitions.FindAsync(id);
        if (field == null)
        {
            return false;
        }

        _context.SubcategoryFieldDefinitions.Remove(field);
        return true;
    }

    public async Task<bool> ExistsAsync(int subcategoryId, string key)
    {
        return await _context.SubcategoryFieldDefinitions
            .AsNoTracking()
            .AnyAsync(f => f.SubcategoryId == subcategoryId && f.FieldKey == key);
    }

    public async Task<bool> ExistsForCategoryAsync(int categoryId, string key)
    {
        return await _context.SubcategoryFieldDefinitions
            .AsNoTracking()
            .Include(f => f.Subcategory)
            .AnyAsync(f => f.Subcategory != null && f.Subcategory.CategoryId == categoryId && f.FieldKey == key);
    }
}


