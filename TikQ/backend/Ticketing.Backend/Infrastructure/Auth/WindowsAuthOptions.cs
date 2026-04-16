namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Options for Windows Integrated Authentication (Negotiate).
/// Mode controls behavior: Off (never use Windows auth), Optional (use if identity present), Enforce (require auth on non-allowlisted routes).
/// </summary>
public class WindowsAuthOptions
{
    public const string SectionName = "WindowsAuth";

    /// <summary>
    /// When true, Windows auth is available (equivalent to Mode Optional if Mode is not set).
    /// When false (default), Windows auth is off unless Mode is explicitly set.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// "Off" | "Optional" | "Enforce". When not set: Off if Enabled is false, Optional if Enabled is true.
    /// Off: app never attempts Windows auth; Windows endpoints return 403 with clear message.
    /// Optional: if Windows identity exists, /api/auth/windows can issue cookie; otherwise 401 with WWW-Authenticate.
    /// Enforce: all routes except /api/health and /api/auth/* require authentication (JWT or Windows).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>Resolved mode: Off, Optional, or Enforce. Uses Mode if set and valid; otherwise derives from Enabled.</summary>
    public string EffectiveMode
    {
        get
        {
            var m = (Mode ?? "").Trim();
            if (string.Equals(m, "Off", StringComparison.OrdinalIgnoreCase)) return "Off";
            if (string.Equals(m, "Optional", StringComparison.OrdinalIgnoreCase)) return "Optional";
            if (string.Equals(m, "Enforce", StringComparison.OrdinalIgnoreCase)) return "Enforce";
            return Enabled ? "Optional" : "Off";
        }
    }

    /// <summary>True when Windows auth is in use (Optional or Enforce).</summary>
    public bool IsWindowsAuthAvailable => EffectiveMode != "Off";
}
