using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<IEnumerable<User>> GetByRoleAsync(string role);
    /// <summary>Get users by role (enum). Use this for reliable query when DB stores Role as integer.</summary>
    Task<IEnumerable<User>> GetByRoleAsync(UserRole role);
    Task<bool> ExistsByEmailAsync(string email);
    Task<bool> ExistsByEmailExcludingIdAsync(string email, Guid excludeId);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> AnyAsync();
}

