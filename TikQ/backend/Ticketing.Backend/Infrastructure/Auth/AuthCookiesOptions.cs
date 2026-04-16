namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Options for auth cookie behavior (e.g. behind IIS reverse proxy / HTTPS).
/// SameSite and SecurePolicy are configurable so dev (HTTP) and production (HTTPS) work correctly.
/// </summary>
public class AuthCookiesOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// SameSite cookie attribute: "Lax" (default), "Strict", or "None".
    /// Use "None" only when cross-site requests must send the cookie (requires Secure).
    /// </summary>
    public string SameSite { get; set; } = "Lax";

    /// <summary>
    /// When to set Secure on the cookie: "SameAsRequest" (secure only when request is HTTPS, default)
    /// or "Always" (always set Secure; use when behind HTTPS reverse proxy).
    /// </summary>
    public string SecurePolicy { get; set; } = "SameAsRequest";
}
