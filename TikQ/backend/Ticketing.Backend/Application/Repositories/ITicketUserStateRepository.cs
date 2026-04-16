using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITicketUserStateRepository
{
    Task<TicketUserState?> GetStateAsync(Guid ticketId, Guid userId);
    Task<Dictionary<Guid, TicketUserState>> GetStatesForUserAsync(Guid userId, IEnumerable<Guid> ticketIds);
    Task<TicketUserState> UpsertSeenAsync(Guid ticketId, Guid userId, DateTime seenAt);
}

