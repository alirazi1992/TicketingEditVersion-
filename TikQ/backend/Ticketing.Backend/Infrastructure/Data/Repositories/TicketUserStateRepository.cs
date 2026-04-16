using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TicketUserStateRepository : ITicketUserStateRepository
{
    private readonly AppDbContext _context;

    public TicketUserStateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketUserState?> GetStateAsync(Guid ticketId, Guid userId)
    {
        return await _context.TicketUserStates
            .FirstOrDefaultAsync(state => state.TicketId == ticketId && state.UserId == userId);
    }

    public async Task<Dictionary<Guid, TicketUserState>> GetStatesForUserAsync(Guid userId, IEnumerable<Guid> ticketIds)
    {
        var ids = ticketIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TicketUserState>();
        }

        return await _context.TicketUserStates
            .Where(state => state.UserId == userId && ids.Contains(state.TicketId))
            .ToDictionaryAsync(state => state.TicketId);
    }

    public async Task<TicketUserState> UpsertSeenAsync(Guid ticketId, Guid userId, DateTime seenAt)
    {
        var state = await _context.TicketUserStates
            .FirstOrDefaultAsync(s => s.TicketId == ticketId && s.UserId == userId);

        if (state == null)
        {
            state = new TicketUserState
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                UserId = userId,
                LastSeenAt = seenAt
            };
            await _context.TicketUserStates.AddAsync(state);
        }
        else
        {
            state.LastSeenAt = seenAt;
        }

        return state;
    }
}

