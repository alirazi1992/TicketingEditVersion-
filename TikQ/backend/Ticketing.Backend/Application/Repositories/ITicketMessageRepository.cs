using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITicketMessageRepository
{
    Task<IEnumerable<TicketMessage>> GetByTicketIdAsync(Guid ticketId);
    Task<TicketMessage> AddAsync(TicketMessage message);
    Task<TicketMessage?> GetByIdWithAuthorAsync(Guid id);
}

