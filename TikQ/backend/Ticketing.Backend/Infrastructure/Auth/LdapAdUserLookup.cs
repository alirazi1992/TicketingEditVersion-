using System.DirectoryServices;
using System.Runtime.Versioning;
using Ticketing.Backend.Application.Common.Interfaces;

namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Resolves sAMAccountName to email via Active Directory LDAP (mail or userPrincipalName).
/// Uses optional ActiveDirectory:LdapPath; if empty, default DirectoryEntry() is used.
/// </summary>
[SupportedOSPlatform("windows")]
public class LdapAdUserLookup : IAdUserLookup
{
    private readonly ActiveDirectoryOptions _options;

    public LdapAdUserLookup(ActiveDirectoryOptions options)
    {
        _options = options ?? new ActiveDirectoryOptions();
    }

    public Task<string?> GetEmailBySamAccountNameAsync(string samAccountName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(samAccountName))
            return Task.FromResult<string?>(null);

        try
        {
            using var entry = string.IsNullOrWhiteSpace(_options.LdapPath)
                ? new DirectoryEntry()
                : new DirectoryEntry(_options.LdapPath.Trim());

            using var searcher = new DirectorySearcher(entry)
            {
                Filter = $"(sAMAccountName={EscapeLdapValue(samAccountName.Trim())})",
                SearchScope = SearchScope.Subtree,
                PropertiesToLoad = { "mail", "userPrincipalName" }
            };

            var result = searcher.FindOne();
            if (result == null)
                return Task.FromResult<string?>(null);

            var mail = result.Properties["mail"]?[0]?.ToString();
            if (!string.IsNullOrWhiteSpace(mail))
                return Task.FromResult<string?>(mail.Trim());
            var upn = result.Properties["userPrincipalName"]?[0]?.ToString();
            return Task.FromResult(string.IsNullOrWhiteSpace(upn) ? null : upn.Trim());
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static string EscapeLdapValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
}
