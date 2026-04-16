using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TechnicianSubcategoryPermissionRepository : ITechnicianSubcategoryPermissionRepository
{
    private readonly AppDbContext _context;

    public TechnicianSubcategoryPermissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TechnicianSubcategoryPermission>> GetByTechnicianIdAsync(Guid technicianId)
    {
        return await _context.TechnicianSubcategoryPermissions
            .Include(p => p.Subcategory)
                .ThenInclude(s => s!.Category)
            .Where(p => p.TechnicianId == technicianId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Guid>> GetTechnicianUserIdsBySubcategoryIdAsync(int subcategoryId)
    {
        return await _context.TechnicianSubcategoryPermissions
            .Where(p => p.SubcategoryId == subcategoryId)
            .Join(_context.Technicians,
                permission => permission.TechnicianId,
                technician => technician.Id,
                (permission, technician) => technician)
            .Where(t => t.IsActive && !t.IsDeleted && t.UserId.HasValue)
            .Select(t => t.UserId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<Guid>> GetTechnicianUserIdsByCategoryIdAsync(int categoryId)
    {
        return await _context.TechnicianSubcategoryPermissions
            .Where(p => p.Subcategory != null && p.Subcategory.CategoryId == categoryId)
            .Join(_context.Technicians,
                permission => permission.TechnicianId,
                technician => technician.Id,
                (permission, technician) => technician)
            .Where(t => t.IsActive && !t.IsDeleted && t.UserId.HasValue)
            .Select(t => t.UserId!.Value)
            .Distinct()
            .ToListAsync();
    }

    public async Task ReplacePermissionsAsync(Guid technicianId, IEnumerable<int> subcategoryIds)
    {
        var subcategoryIdsList = subcategoryIds.ToList();

        // Get existing permissions
        var existing = await _context.TechnicianSubcategoryPermissions
            .Where(p => p.TechnicianId == technicianId)
            .ToListAsync();

        // Remove permissions not in the new list
        var toRemove = existing.Where(e => !subcategoryIdsList.Contains(e.SubcategoryId)).ToList();
        foreach (var permission in toRemove)
        {
            _context.TechnicianSubcategoryPermissions.Remove(permission);
        }

        // Add new permissions
        var existingSubcategoryIds = existing.Select(e => e.SubcategoryId).ToHashSet();
        var toAdd = subcategoryIdsList.Where(id => !existingSubcategoryIds.Contains(id)).ToList();

        foreach (var subcategoryId in toAdd)
        {
            var permission = new TechnicianSubcategoryPermission
            {
                Id = Guid.NewGuid(),
                TechnicianId = technicianId,
                SubcategoryId = subcategoryId,
                CreatedAt = DateTime.UtcNow
            };
            await _context.TechnicianSubcategoryPermissions.AddAsync(permission);
        }

        // Update UpdatedAt for existing permissions that remain
        var toUpdate = existing.Where(e => subcategoryIdsList.Contains(e.SubcategoryId)).ToList();
        foreach (var permission in toUpdate)
        {
            permission.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<TechnicianSubcategoryPermission> AddAsync(TechnicianSubcategoryPermission permission)
    {
        await _context.TechnicianSubcategoryPermissions.AddAsync(permission);
        return permission;
    }

    public async Task DeleteAsync(Guid permissionId)
    {
        var permission = await _context.TechnicianSubcategoryPermissions.FindAsync(permissionId);
        if (permission != null)
        {
            _context.TechnicianSubcategoryPermissions.Remove(permission);
        }
    }

    public async Task DeleteByTechnicianIdAsync(Guid technicianId)
    {
        var permissions = await _context.TechnicianSubcategoryPermissions
            .Where(p => p.TechnicianId == technicianId)
            .ToListAsync();
        _context.TechnicianSubcategoryPermissions.RemoveRange(permissions);
    }
}

