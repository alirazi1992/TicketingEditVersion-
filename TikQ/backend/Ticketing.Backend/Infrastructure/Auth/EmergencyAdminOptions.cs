namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Break-glass admin: emergency login when server/directory is unavailable.
/// Enabled only when explicitly configured; in Production, Password and Key must be set via environment.
/// </summary>
public class EmergencyAdminOptions
{
    public const string SectionName = "EmergencyAdmin";

    /// <summary>When true, /api/auth/emergency-login is enabled. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Emergency admin email. Must match login request.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name for the emergency admin user in TikQ (e.g. "Break-Glass Admin").</summary>
    public string FullName { get; set; } = "Emergency Admin";

    /// <summary>Emergency admin password. In Production must be set via env (e.g. EmergencyAdmin__Password).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Extra secret key required in request. In Production set via env only (e.g. EmergencyAdmin__Key).</summary>
    public string Key { get; set; } = string.Empty;
}
