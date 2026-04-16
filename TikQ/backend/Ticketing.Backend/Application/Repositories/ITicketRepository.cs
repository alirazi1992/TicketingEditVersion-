using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByIdWithIncludesAsync(Guid id);
    Task<IEnumerable<Ticket>> QueryAsync(
        UserRole role,
        Guid userId,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? assignedTo = null,
        Guid? createdBy = null,
        string? search = null);
    Task<List<TicketListItemResponse>> QueryListItemsAsync(
        UserRole role,
        Guid userId,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? assignedTo = null,
        Guid? createdBy = null,
        string? search = null,
        bool? unseen = null);
    Task<IEnumerable<Ticket>> GetCalendarTicketsAsync(DateTime startUtc, DateTime endUtcExclusive, TicketStatus? status = null);
    Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsByCreatedRangeAsync(DateTime startDate, DateTime endDate, int page, int pageSize);
    /// <summary>For calendar day modal: filter by UpdatedAt (or CreatedAt if null), Tehran local day range [startUtc, endUtcExclusive).</summary>
    Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsByUpdatedRangeAsync(DateTime startUtc, DateTime endUtcExclusive, int page, int pageSize);
    Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsBeforeAsync(DateTime beforeDate, int page, int pageSize);
    Task<int> CountByTechnicianIdAndStatusAsync(Guid technicianId, IEnumerable<TicketStatus> statuses);
    Task<IEnumerable<Ticket>> GetUnassignedTicketsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<Ticket> AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
}

