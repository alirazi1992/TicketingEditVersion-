using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TicketTechnicianAssignmentRepository : ITicketTechnicianAssignmentRepository
{
    private readonly AppDbContext _context;

    public TicketTechnicianAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketTechnicianAssignment?> GetByIdAsync(Guid id)
    {
        return await _context.TicketTechnicianAssignments
            .Include(ta => ta.TechnicianUser)
            .Include(ta => ta.AssignedByUser)
            .FirstOrDefaultAsync(ta => ta.Id == id);
    }

    public async Task<IEnumerable<TicketTechnicianAssignment>> GetAssignmentsForTicketAsync(Guid ticketId)
    {
        return await _context.TicketTechnicianAssignments
            .Include(ta => ta.TechnicianUser)
            .Include(ta => ta.AssignedByUser)
            .Where(ta => ta.TicketId == ticketId)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketTechnicianAssignment>> GetActiveAssignmentsForTicketAsync(Guid ticketId)
    {
        return await _context.TicketTechnicianAssignments
            .Include(ta => ta.TechnicianUser)
            .Include(ta => ta.AssignedByUser)
            .Where(ta => ta.TicketId == ticketId && ta.IsActive)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketTechnicianAssignment>> GetTicketsForTechnicianAsync(Guid technicianUserId)
    {
        return await _context.TicketTechnicianAssignments
            .Include(ta => ta.Ticket)
            .Where(ta => ta.TechnicianUserId == technicianUserId)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketTechnicianAssignment>> GetActiveTicketsForTechnicianAsync(Guid technicianUserId)
    {
        return await _context.TicketTechnicianAssignments
            .Include(ta => ta.Ticket)
            .Where(ta => ta.TechnicianUserId == technicianUserId && ta.IsActive)
            .OrderByDescending(ta => ta.AssignedAt)
            .ToListAsync();
    }

    public async Task<TicketTechnicianAssignment?> GetActiveAssignmentAsync(Guid ticketId, Guid technicianUserId)
    {
        return await _context.TicketTechnicianAssignments
            .FirstOrDefaultAsync(ta => ta.TicketId == ticketId && ta.TechnicianUserId == technicianUserId && ta.IsActive);
    }

    public async Task<TicketTechnicianAssignment> AddAsync(TicketTechnicianAssignment assignment)
    {
        assignment.Id = Guid.NewGuid();
        _context.TicketTechnicianAssignments.Add(assignment);
        await _context.SaveChangesAsync();
        return assignment;
    }

    public async Task<TicketTechnicianAssignment> UpdateAsync(TicketTechnicianAssignment assignment)
    {
        assignment.UpdatedAt = DateTime.UtcNow;
        _context.TicketTechnicianAssignments.Update(assignment);
        await _context.SaveChangesAsync();
        return assignment;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var assignment = await _context.TicketTechnicianAssignments.FindAsync(id);
        if (assignment == null) return false;
        _context.TicketTechnicianAssignments.Remove(assignment);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsTechnicianAssignedAsync(Guid ticketId, Guid technicianUserId)
    {
        return await _context.TicketTechnicianAssignments
            .AnyAsync(ta => ta.TicketId == ticketId && ta.TechnicianUserId == technicianUserId && ta.IsActive);
    }

    public async Task SetAssignmentsAsync(Guid ticketId, IEnumerable<Guid> technicianUserIds, Guid assignedByUserId, string? leadTechnicianUserId = null)
    {
        var technicianIdsList = technicianUserIds.ToList();
        var existingAssignments = await _context.TicketTechnicianAssignments
            .Where(ta => ta.TicketId == ticketId)
            .ToListAsync();

        // Deactivate assignments not in the new list
        foreach (var existing in existingAssignments)
        {
            if (!technicianIdsList.Contains(existing.TechnicianUserId))
            {
                existing.IsActive = false;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Add or reactivate assignments
        foreach (var technicianUserId in technicianIdsList)
        {
            var existing = existingAssignments.FirstOrDefault(ta => ta.TechnicianUserId == technicianUserId);
            if (existing != null)
            {
                // Reactivate if needed
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.AssignedAt = DateTime.UtcNow; // Update assignment time
                    existing.AssignedByUserId = assignedByUserId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                // Update role
                existing.Role = leadTechnicianUserId == technicianUserId.ToString() ? "Lead" : "Collaborator";
            }
            else
            {
                // Create new assignment
                var newAssignment = new TicketTechnicianAssignment
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticketId,
                    TechnicianUserId = technicianUserId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = assignedByUserId,
                    IsActive = true,
                    Role = leadTechnicianUserId == technicianUserId.ToString() ? "Lead" : "Collaborator"
                };
                _context.TicketTechnicianAssignments.Add(newAssignment);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<(Guid TicketId, DateTime AcceptedAt, Guid AcceptedByUserId)>> GetFirstAcceptedByTicketIdsAsync(IEnumerable<Guid> ticketIds)
    {
        var idList = ticketIds.Distinct().ToList();
        if (idList.Count == 0) return Array.Empty<(Guid, DateTime, Guid)>();

        var allAccepted = await _context.TicketTechnicianAssignments
            .Where(ta => ta.AcceptedAt != null && idList.Contains(ta.TicketId))
            .Select(ta => new { ta.TicketId, ta.AcceptedAt, ta.TechnicianUserId })
            .ToListAsync();

        var firstPerTicket = allAccepted
            .Where(x => x.AcceptedAt.HasValue)
            .GroupBy(x => x.TicketId)
            .Select(g => g.OrderBy(x => x.AcceptedAt).First())
            .Select(x => (x.TicketId, x.AcceptedAt!.Value, x.TechnicianUserId))
            .ToList();

        return firstPerTicket;
    }
}



