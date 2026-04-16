using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TicketMessageRepository : ITicketMessageRepository
{
    private readonly AppDbContext _context;

    public TicketMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TicketMessage>> GetByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<TicketMessage> AddAsync(TicketMessage message)
    {
        await _context.TicketMessages.AddAsync(message);
        return message;
    }

    public async Task<TicketMessage?> GetByIdWithAuthorAsync(Guid id)
    {
        return await _context.TicketMessages
            .Include(m => m.AuthorUser)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}

