using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class SupervisorTechnicianLinkRepository : ISupervisorTechnicianLinkRepository
{
    private readonly AppDbContext _context;

    public SupervisorTechnicianLinkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SupervisorTechnicianLink>> GetLinksForSupervisorAsync(Guid supervisorUserId)
    {
        return await _context.SupervisorTechnicianLinks
            .Include(link => link.TechnicianUser)
            .Where(link => link.SupervisorUserId == supervisorUserId)
            .OrderBy(link => link.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupervisorTechnicianLink>> GetLinksForTechnicianAsync(Guid technicianUserId)
    {
        return await _context.SupervisorTechnicianLinks
            .Include(link => link.SupervisorUser)
            .Where(link => link.TechnicianUserId == technicianUserId)
            .OrderBy(link => link.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsLinkedAsync(Guid supervisorUserId, Guid technicianUserId)
    {
        return await _context.SupervisorTechnicianLinks
            .AnyAsync(link => link.SupervisorUserId == supervisorUserId && link.TechnicianUserId == technicianUserId);
    }

    public async Task<SupervisorTechnicianLink> AddAsync(SupervisorTechnicianLink link)
    {
        link.Id = Guid.NewGuid();
        _context.SupervisorTechnicianLinks.Add(link);
        await _context.SaveChangesAsync();
        return link;
    }

    public async Task<bool> RemoveAsync(Guid supervisorUserId, Guid technicianUserId)
    {
        var link = await _context.SupervisorTechnicianLinks
            .FirstOrDefaultAsync(l => l.SupervisorUserId == supervisorUserId && l.TechnicianUserId == technicianUserId);
        if (link == null) return false;
        _context.SupervisorTechnicianLinks.Remove(link);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.SupervisorTechnicianLinks.CountAsync();
    }

    public async Task<int> GetCountForSupervisorAsync(Guid supervisorUserId)
    {
        return await _context.SupervisorTechnicianLinks.CountAsync(l => l.SupervisorUserId == supervisorUserId);
    }
}

