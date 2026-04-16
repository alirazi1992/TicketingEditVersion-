using Ticketing.Backend.Application.Common.Interfaces;

namespace Ticketing.Backend.Infrastructure.CompanyDirectory;

public sealed class FakeCompanyUserDirectory : ICompanyUserDirectory
{
    public Task<CompanyDirectoryUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult<CompanyDirectoryUser?>(null);
    }
}
