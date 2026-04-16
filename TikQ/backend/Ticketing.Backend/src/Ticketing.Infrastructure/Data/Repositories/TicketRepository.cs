using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Data;
using Ticketing.Application.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.Enums;

namespace Ticketing.Infrastructure.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _context;

    public TicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _context.Tickets.FindAsync(id);
    }

    public async Task<Ticket?> GetByIdWithIncludesAsync(Guid id)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(tt => tt.Technician)
            .Include(t => t.FieldValues)
                .ThenInclude(fv => fv.FieldDefinition)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Ticket>> GetTicketsAsync(
        UserRole role,
        Guid userId,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? assignedTo = null,
        Guid? createdBy = null,
        string? search = null)
    {
        var query = _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(tt => tt.Technician)
            .Include(t => t.FieldValues)
                .ThenInclude(fv => fv.FieldDefinition)
            .Include(t => t.Attachments)
            .AsQueryable();

        // Restrict tickets based on role
        query = role switch
        {
            UserRole.Client => query.Where(t => t.CreatedByUserId == userId),
            UserRole.Technician => query.Where(t =>
                t.AssignedToUserId == userId ||
                t.AssignedTechnicians.Any(tt => tt.TechnicianUserId == userId)
            ),
            _ => query // Admin sees all tickets
        };

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);
        if (assignedTo.HasValue)
            query = query.Where(t => t.AssignedToUserId == assignedTo.Value);
        if (createdBy.HasValue)
            query = query.Where(t => t.CreatedByUserId == createdBy.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<Ticket> AddAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
        return ticket;
    }

    public Task<Ticket> UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return Task.FromResult(ticket);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return false;
        _context.Tickets.Remove(ticket);
        return true;
    }

    public async Task<IEnumerable<Ticket>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate)
    {
        // Include all tickets within date range (Admin only - no role filtering)
        // This ensures newly created tickets appear in admin calendar immediately
        return await _context.Tickets
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(tt => tt.Technician)
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnassignedCountAsync()
    {
        return await _context.Tickets
            .CountAsync(t => t.AssignedToUserId == null && 
                           !t.AssignedTechnicians.Any() &&
                           t.Status != TicketStatus.Solved);
    }

    public async Task<Ticket?> GetBasicByIdAsync(Guid id)
    {
        // Returns ticket with only basic properties (no includes) - sufficient for notification title/status checks
        return await _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }
}