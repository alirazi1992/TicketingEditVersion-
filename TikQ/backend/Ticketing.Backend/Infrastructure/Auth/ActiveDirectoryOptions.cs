namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Options for LDAP-based AD user lookup. Bound from configuration section "ActiveDirectory".
/// </summary>
public class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    /// <summary>
    /// Optional LDAP path (e.g. LDAP://DC=company,DC=local). If empty, default DirectoryEntry() is used.
    /// </summary>
    public string LdapPath { get; set; } = string.Empty;
}
