using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ITicketMessageRepository
{
    Task<TicketMessage?> GetByIdAsync(Guid id);
    Task<IEnumerable<TicketMessage>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketMessage> AddAsync(TicketMessage message);
    Task<TicketMessage> UpdateAsync(TicketMessage message);
    Task<bool> DeleteAsync(Guid id);
}
