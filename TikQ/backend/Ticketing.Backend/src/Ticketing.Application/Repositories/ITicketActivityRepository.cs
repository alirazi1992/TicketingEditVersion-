using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ITicketActivityRepository
{
    Task<TicketActivity?> GetByIdAsync(Guid id);
    Task<IEnumerable<TicketActivity>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketActivity> AddAsync(TicketActivity activity);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<TicketActivity>> GetRecentByTicketIdAsync(Guid ticketId, int count = 10);
}
