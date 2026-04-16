using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITicketActivityEventRepository
{
    Task<TicketActivityEvent?> GetByIdAsync(Guid id);
    Task<IEnumerable<TicketActivityEvent>> GetEventsForTicketAsync(Guid ticketId);
    Task<IEnumerable<TicketActivityEvent>> GetRecentEventsForTicketAsync(Guid ticketId, int count = 50);
    Task<TicketActivityEvent> AddEventAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorRole,
        string eventType,
        string? oldStatus = null,
        string? newStatus = null,
        string? metadataJson = null);
    Task<bool> DeleteAsync(Guid id);
}


































