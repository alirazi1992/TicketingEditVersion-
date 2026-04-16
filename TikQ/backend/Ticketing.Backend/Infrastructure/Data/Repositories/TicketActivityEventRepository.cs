using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TicketActivityEventRepository : ITicketActivityEventRepository
{
    private readonly AppDbContext _context;

    public TicketActivityEventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketActivityEvent?> GetByIdAsync(Guid id)
    {
        return await _context.TicketActivityEvents
            .Include(tae => tae.ActorUser)
            .FirstOrDefaultAsync(tae => tae.Id == id);
    }

    public async Task<IEnumerable<TicketActivityEvent>> GetEventsForTicketAsync(Guid ticketId)
    {
        return await _context.TicketActivityEvents
            .Include(tae => tae.ActorUser)
            .Where(tae => tae.TicketId == ticketId)
            .OrderByDescending(tae => tae.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TicketActivityEvent>> GetRecentEventsForTicketAsync(Guid ticketId, int count = 50)
    {
        return await _context.TicketActivityEvents
            .Include(tae => tae.ActorUser)
            .Where(tae => tae.TicketId == ticketId)
            .OrderByDescending(tae => tae.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<TicketActivityEvent> AddEventAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorRole,
        string eventType,
        string? oldStatus = null,
        string? newStatus = null,
        string? metadataJson = null)
    {
        var activityEvent = new TicketActivityEvent
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            EventType = eventType,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.TicketActivityEvents.Add(activityEvent);
        await _context.SaveChangesAsync();
        return activityEvent;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var activityEvent = await _context.TicketActivityEvents.FindAsync(id);
        if (activityEvent == null) return false;
        _context.TicketActivityEvents.Remove(activityEvent);
        await _context.SaveChangesAsync();
        return true;
    }
}


































