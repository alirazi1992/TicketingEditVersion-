namespace Ticketing.Backend.Infrastructure.CompanyDirectory;

/// <summary>
/// Options for the read-only Company DB identity directory.
/// TikQ roles are authoritative. External directory is identity-only (email, name, active/disabled).
/// Bound from configuration section "CompanyDirectory".
/// </summary>
public class CompanyDirectoryOptions
{
    public const string SectionName = "CompanyDirectory";

    /// <summary>When true, use SQL Server directory; when false, use fake (no lookup).</summary>
    public bool Enabled { get; set; }

    /// <summary>Connection string for the Company DB. Used only for SELECT; never write or migrate.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Mode: "Enforce" = login fails with 403 ROLE_NOT_ASSIGNED if no TikQ role; "Friendly" = default to Client when not assigned (no password stored). Default Enforce for production safety.</summary>
    public string Mode { get; set; } = "Enforce";
}
