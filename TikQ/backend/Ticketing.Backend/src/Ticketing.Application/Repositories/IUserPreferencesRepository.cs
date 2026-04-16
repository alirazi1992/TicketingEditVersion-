using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetByUserIdAsync(Guid userId);
    Task<UserPreferences> AddAsync(UserPreferences preferences);
    Task<UserPreferences> UpdateAsync(UserPreferences preferences);
    Task<bool> DeleteAsync(Guid userId);
}
