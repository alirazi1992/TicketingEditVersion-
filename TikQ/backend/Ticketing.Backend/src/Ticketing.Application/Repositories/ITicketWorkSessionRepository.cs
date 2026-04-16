using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ITicketWorkSessionRepository
{
    Task<TicketWorkSession?> GetByIdAsync(Guid id);
    Task<IEnumerable<TicketWorkSession>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketWorkSession?> GetByTicketAndTechnicianAsync(Guid ticketId, Guid technicianUserId);
    Task<TicketWorkSession> AddAsync(TicketWorkSession session);
    Task<TicketWorkSession> UpdateAsync(TicketWorkSession session);
    Task<bool> DeleteAsync(Guid id);
    Task<TicketWorkSession> AddOrUpdateAsync(TicketWorkSession session);
}
