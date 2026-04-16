namespace Ticketing.Backend.Application.Common.Interfaces;

/// <summary>
/// Read-only view of a user from the Company DB identity directory (identity-only).
/// TikQ roles are authoritative. External directory is identity-only.
/// Do not add Role (or any role-related fields) to this type. If the external directory
/// returns role fields, they must be ignored; role resolution uses only TikQ Users.Role.
/// Do not log PasswordHash or any credential data.
/// PasswordHash is optional: when Company DB has no passwords (read-only directory), use null;
/// server users then authenticate with password stored in TikQ DB only.
/// </summary>
public record CompanyDirectoryUser(
    string Email,
    string? FullName,
    string? PasswordHash,
    bool IsActive,
    bool IsDisabled
);

/// <summary>
/// Read-only identity directory backed by Company DB. Never writes or runs migrations.
/// Used ONLY for identity lookup (email, name, active/disabled). CompanyDirectory MUST NOT
/// provide or override roles; roles always come from TikQ DB (Users.Role).
/// </summary>
public interface ICompanyUserDirectory
{
    Task<CompanyDirectoryUser?> GetByEmailAsync(string email, CancellationToken ct = default);
}
