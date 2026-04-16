using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TechnicianRepository : ITechnicianRepository
{
    private readonly AppDbContext _context;

    public TechnicianRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Technician>> GetAllAsync()
    {
        // Global query filter automatically excludes IsDeleted=true
        return await _context.Technicians
            .OrderBy(t => t.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Technician>> GetAllIncludingDeletedAsync()
    {
        // Bypass the soft delete query filter
        return await _context.Technicians
            .IgnoreQueryFilters()
            .OrderBy(t => t.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Technician>> GetActiveWithUserIdAsync()
    {
        return await _context.Technicians
            .Where(t => t.IsActive && t.UserId != null)
            .OrderBy(t => t.FullName)
            .ToListAsync();
    }

    public async Task<Technician?> GetByIdAsync(Guid id)
    {
        return await _context.Technicians
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Technician?> GetByIdIncludingDeletedAsync(Guid id)
    {
        // Bypass the soft delete query filter
        return await _context.Technicians
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Technician?> GetByIdWithIncludesAsync(Guid id)
    {
        return await _context.Technicians
            .Include(t => t.User)
            .Include(t => t.SubcategoryPermissions)
                .ThenInclude(p => p.Subcategory)
                    .ThenInclude(s => s!.Category)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Technician?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Technicians
            .FirstOrDefaultAsync(t => t.UserId == userId);
    }

    public async Task<IEnumerable<Guid>> GetTechnicianUserIdsBySubcategoryAsync(int subcategoryId)
    {
        return await _context.TechnicianSubcategoryPermissions
            .Where(p => p.SubcategoryId == subcategoryId)
            .Join(_context.Technicians,
                permission => permission.TechnicianId,
                technician => technician.Id,
                (permission, technician) => technician)
            .Where(t => t.IsActive && t.UserId.HasValue)
            .Select(t => t.UserId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task<Technician> AddAsync(Technician technician)
    {
        await _context.Technicians.AddAsync(technician);
        return technician;
    }

    public Task UpdateAsync(Technician technician)
    {
        _context.Technicians.Update(technician);
        return Task.CompletedTask;
    }
}