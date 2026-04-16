namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Resolves Windows identity (e.g. DOMAIN\username) to a TikQ user email via configured map.
/// No LDAP; config-only (Phase 2). Used later for Windows auth (Phase 3).
/// </summary>
public interface IWindowsUserMapResolver
{
    /// <summary>Resolves "DOMAIN\username" to email. Case-insensitive; trims input. Returns null if not found.</summary>
    string? ResolveEmail(string domainUser);
}

public class WindowsUserMapResolver : IWindowsUserMapResolver
{
    private readonly WindowsUserMapOptions _options;

    public WindowsUserMapResolver(WindowsUserMapOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string? ResolveEmail(string domainUser)
    {
        if (string.IsNullOrWhiteSpace(domainUser))
            return null;

        var key = domainUser.Trim();
        if (_options.Map == null || _options.Map.Count == 0)
            return null;

        // Case-insensitive key lookup
        var comparer = StringComparer.OrdinalIgnoreCase;
        foreach (var kv in _options.Map)
        {
            if (comparer.Equals(kv.Key, key))
                return kv.Value;
        }
        return null;
    }
}
