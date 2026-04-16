namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Options for mapping Windows identity (DOMAIN\username) to TikQ user email.
/// Bound from configuration section "WindowsUserMap"; keys are "DOMAIN\username", values are email.
/// </summary>
public class WindowsUserMapOptions
{
    public const string SectionName = "WindowsUserMap";

    /// <summary>Maps "DOMAIN\username" -> email. Loaded from config; lookup is case-insensitive.</summary>
    public Dictionary<string, string> Map { get; set; } = new();
}
