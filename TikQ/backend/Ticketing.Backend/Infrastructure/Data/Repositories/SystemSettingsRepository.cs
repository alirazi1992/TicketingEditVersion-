using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly AppDbContext _context;

    public SystemSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettings?> GetByIdAsync(int id)
    {
        return await _context.SystemSettings.FindAsync(id);
    }

    public async Task<SystemSettings> GetOrCreateDefaultAsync(int id)
    {
        var settings = await _context.SystemSettings.FindAsync(id);
        if (settings == null)
        {
            settings = new SystemSettings
            {
                Id = id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.SystemSettings.AddAsync(settings);
        }
        return settings;
    }

    public async Task<SystemSettings> AddAsync(SystemSettings settings)
    {
        await _context.SystemSettings.AddAsync(settings);
        return settings;
    }

    public Task UpdateAsync(SystemSettings settings)
    {
        _context.SystemSettings.Update(settings);
        return Task.CompletedTask;
    }
}

