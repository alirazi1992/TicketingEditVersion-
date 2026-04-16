using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.Common.Interfaces;

namespace Ticketing.Backend.Infrastructure.CompanyDirectory;

/// <summary>
/// Read-only Company DB directory using ADO.NET (no EF). Executes only SELECT by email.
/// TikQ roles are authoritative. External directory is identity-only. Do not SELECT or map
/// Role (or any role column) from the Company DB; if the schema has role fields, ignore them.
/// Do not log PasswordHash or any credential data.
/// Belt-and-suspenders: rejects any command text containing write/DDL keywords.
/// </summary>
public sealed class SqlServerCompanyUserDirectory : ICompanyUserDirectory
{
    private static readonly string[] ReadOnlyRejectedKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "CREATE", "ALTER", "DROP", "EXEC ", "EXECUTE "
    };

    private readonly CompanyDirectoryOptions _options;
    private readonly ILogger<SqlServerCompanyUserDirectory>? _logger;

    public SqlServerCompanyUserDirectory(CompanyDirectoryOptions options, ILogger<SqlServerCompanyUserDirectory>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    private static void EnsureReadOnlyCommand(string commandText)
    {
        foreach (var keyword in ReadOnlyRejectedKeywords)
        {
            if (commandText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException($"[CompanyDirectory] Read-only enforced: command must not contain '{keyword}'.");
        }
    }

    /// <summary>
    /// Table/column mapping is isolated here. Adjust if your Company DB schema differs.
    /// Only identity fields: Email, FullName, PasswordHash, IsActive, IsDisabled. Do NOT add Role.
    /// </summary>
    private const string Sql = @"
SELECT Email, FullName, PasswordHash, IsActive, IsDisabled
FROM dbo.Users
WHERE LOWER(LTRIM(RTRIM(Email))) = @email;";

    public async Task<CompanyDirectoryUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return null;

        EnsureReadOnlyCommand(Sql);
        _logger?.LogInformation("[CompanyDirectory] Read-only enforced");

        await using var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = new SqlCommand(Sql, connection);
        cmd.Parameters.Add("@email", System.Data.SqlDbType.NVarChar, 256).Value = normalizedEmail;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        // Map row to CompanyDirectoryUser (isolated here; change if column names differ)
        var idxEmail = reader.GetOrdinal("Email");
        var idxFullName = reader.GetOrdinal("FullName");
        var idxPasswordHash = reader.GetOrdinal("PasswordHash");
        var idxIsActive = reader.GetOrdinal("IsActive");
        var idxIsDisabled = reader.GetOrdinal("IsDisabled");
        var dbEmail = reader.GetString(idxEmail);
        var fullName = reader.IsDBNull(idxFullName) ? null : reader.GetString(idxFullName);
        var passwordHash = reader.IsDBNull(idxPasswordHash) ? null : reader.GetString(idxPasswordHash);
        var isActive = reader.IsDBNull(idxIsActive)
            ? true
            : Convert.ToBoolean(reader.GetValue(idxIsActive));
        var isDisabled = !reader.IsDBNull(idxIsDisabled)
            && Convert.ToBoolean(reader.GetValue(idxIsDisabled));

        return new CompanyDirectoryUser(dbEmail, fullName, passwordHash, isActive, isDisabled);
    }
}
