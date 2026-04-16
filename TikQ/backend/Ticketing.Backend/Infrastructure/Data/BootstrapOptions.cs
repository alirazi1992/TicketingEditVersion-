namespace Ticketing.Backend.Infrastructure.Data;

/// <summary>
/// Options for one-time bootstrap user seeding (first run only).
/// Section: "Bootstrap". In Production, Enabled defaults to false; set explicitly to true for first deployment.
/// Env: Bootstrap__Enabled, Bootstrap__AdminEmail, Bootstrap__AdminPassword, etc.
/// </summary>
public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// When true and Users table is empty, seed admin (and optional test users). Default false in Production.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Admin account email (required when Enabled and seeding).
    /// </summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>
    /// Admin account password (required when Enabled and seeding; min 8 characters).
    /// </summary>
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name for admin. Defaults to "Bootstrap Admin" if empty.
    /// </summary>
    public string AdminFullName { get; set; } = string.Empty;

    /// <summary>
    /// Optional test client. If both email and password set, a Client user is seeded.
    /// </summary>
    public string? TestClientEmail { get; set; }

    /// <summary>
    /// Optional test client password (min 8 chars when set).
    /// </summary>
    public string? TestClientPassword { get; set; }

    /// <summary>
    /// Optional test technician. If both email and password set, a Technician user (+ Technician row) is seeded.
    /// </summary>
    public string? TestTechEmail { get; set; }

    /// <summary>
    /// Optional test technician password (min 8 chars when set).
    /// </summary>
    public string? TestTechPassword { get; set; }

    /// <summary>
    /// Optional test supervisor. If both email and password set, a Technician with IsSupervisor=true is seeded.
    /// </summary>
    public string? TestSupervisorEmail { get; set; }

    /// <summary>
    /// Optional test supervisor password (min 8 chars when set).
    /// </summary>
    public string? TestSupervisorPassword { get; set; }
}
