using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ISystemSettingsRepository
{
    Task<SystemSettings?> GetByIdAsync(int id);
    Task<SystemSettings> GetOrCreateDefaultAsync(int id);
    Task<SystemSettings> AddAsync(SystemSettings settings);
    Task UpdateAsync(SystemSettings settings);
}

