using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ISystemSettingsRepository
{
    Task<SystemSettings?> GetByIdAsync(int id);
    Task<SystemSettings?> GetByNameAsync(string name);
    Task<IEnumerable<SystemSettings>> GetAllAsync();
    Task<SystemSettings> AddAsync(SystemSettings settings);
    Task<SystemSettings> UpdateAsync(SystemSettings settings);
    Task<bool> DeleteAsync(int id);
}
