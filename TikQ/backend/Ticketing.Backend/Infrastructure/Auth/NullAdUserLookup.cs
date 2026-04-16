using Ticketing.Backend.Application.Common.Interfaces;

namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// No-op AD lookup for non-Windows platforms. Always returns null; Windows auth then relies on WindowsUserMap only.
/// </summary>
public class NullAdUserLookup : IAdUserLookup
{
    public Task<string?> GetEmailBySamAccountNameAsync(string samAccountName, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);
}
