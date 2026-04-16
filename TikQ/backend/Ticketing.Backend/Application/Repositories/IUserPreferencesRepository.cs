using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetByUserIdAsync(Guid userId);
    Task<UserPreferences> AddAsync(UserPreferences preferences);
    Task UpdateAsync(UserPreferences preferences);
}

