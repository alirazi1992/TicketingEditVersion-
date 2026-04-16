namespace Ticketing.Backend.Application.Common.Interfaces;

/// <summary>
/// Resolves a Windows sAMAccountName to an email address via Active Directory (LDAP).
/// Used for Windows Integrated Auth when no password is stored; no credentials are passed.
/// </summary>
public interface IAdUserLookup
{
    /// <summary>
    /// Looks up the user in AD by sAMAccountName and returns mail or userPrincipalName.
    /// Returns null if not found or on error.
    /// </summary>
    Task<string?> GetEmailBySamAccountNameAsync(string samAccountName, CancellationToken ct = default);
}
