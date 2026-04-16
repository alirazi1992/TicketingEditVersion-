using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class UserPreferencesRepository : IUserPreferencesRepository
{
    private readonly AppDbContext _context;

    public UserPreferencesRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserPreferences?> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<UserPreferences> AddAsync(UserPreferences preferences)
    {
        await _context.UserPreferences.AddAsync(preferences);
        return preferences;
    }

    public Task UpdateAsync(UserPreferences preferences)
    {
        _context.UserPreferences.Update(preferences);
        return Task.CompletedTask;
    }
}

