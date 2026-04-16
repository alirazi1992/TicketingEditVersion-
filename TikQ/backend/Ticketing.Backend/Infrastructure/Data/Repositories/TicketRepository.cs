using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _context;

    public TicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByIdWithIncludesAsync(Guid id)
    {
        return await _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            .Include(t => t.FieldValues)
                .ThenInclude(fv => fv.FieldDefinition)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.TechnicianUser)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.AssignedByUser)
            .Include(t => t.ActivityEvents)
                .ThenInclude(ae => ae.ActorUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Ticket>> QueryAsync(
        UserRole role,
        Guid userId,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? assignedTo = null,
        Guid? createdBy = null,
        string? search = null)
    {
        // Build query with all necessary includes
        // NOTE: Order matters - include collections before filtering with .Any()
        var query = _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            // Include AssignedTechnicians with all navigation properties BEFORE using .Any() in filter
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.TechnicianUser)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.AssignedByUser)
            .Include(t => t.FieldValues)
                .ThenInclude(fv => fv.FieldDefinition)
            .Include(t => t.ActivityEvents)
                .ThenInclude(ae => ae.ActorUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(t => t.Attachments)
            .AsQueryable();

        // Restrict tickets based on role
        // NOTE: For Technician role, we split the query to avoid potential EF Core issues with .Any() on included collections
        if (role == UserRole.Client)
        {
            query = query.Where(t => t.CreatedByUserId == userId);
        }
        else if (role == UserRole.Technician)
        {
            // For technicians, check AssignedToUserId (the UserId of assigned technician)
            // and the new multi-technician assignment system
            // NOTE: TechnicianId is FK to Technician entity, NOT UserId - so we don't use it here
            query = query.Where(t => 
                t.AssignedToUserId == userId || 
                t.AssignedTechnicians.Any(ta => ta.TechnicianUserId == userId && ta.IsActive));
        }
        // Admin sees all tickets - no filter needed

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }
        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }
        if (assignedTo.HasValue)
        {
            query = query.Where(t => t.AssignedToUserId == assignedTo.Value);
        }
        if (createdBy.HasValue)
        {
            query = query.Where(t => t.CreatedByUserId == createdBy.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search));
        }

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<List<TicketListItemResponse>> QueryListItemsAsync(
        UserRole role,
        Guid userId,
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? assignedTo = null,
        Guid? createdBy = null,
        string? search = null,
        bool? unseen = null)
    {
        var baseQuery = _context.Tickets.AsNoTracking().AsQueryable();

        if (role == UserRole.Client)
        {
            baseQuery = baseQuery.Where(t => t.CreatedByUserId == userId);
        }
        else if (role == UserRole.Technician)
        {
            baseQuery = baseQuery.Where(t =>
                t.AssignedToUserId == userId ||
                t.AssignedTechnicians.Any(ta =>
                    ta.TechnicianUserId == userId &&
                    ta.IsActive) ||
                // Supervisor scope: include tickets assigned to linked technicians
                (_context.SupervisorTechnicianLinks.Any(link => link.SupervisorUserId == userId) &&
                 (
                     (t.AssignedToUserId != null &&
                      _context.SupervisorTechnicianLinks.Any(link =>
                          link.SupervisorUserId == userId &&
                          link.TechnicianUserId == t.AssignedToUserId)) ||
                     t.AssignedTechnicians.Any(ta =>
                         ta.IsActive &&
                         _context.SupervisorTechnicianLinks.Any(link =>
                             link.SupervisorUserId == userId &&
                             link.TechnicianUserId == ta.TechnicianUserId))
                 )) ||
                (t.AssignedToUserId == null &&
                 _context.TechnicianSubcategoryPermissions.Any(p =>
                     p.Technician != null &&
                     p.Technician.UserId == userId &&
                     p.Technician.IsActive &&
                     !p.Technician.IsDeleted &&
                     ((t.SubcategoryId != null && p.SubcategoryId == t.SubcategoryId) ||
                      (t.SubcategoryId == null && p.Subcategory != null && p.Subcategory.CategoryId == t.CategoryId))
                 )));
        }

        if (status.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Status == status.Value);
        }
        if (priority.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Priority == priority.Value);
        }
        if (assignedTo.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.AssignedToUserId == assignedTo.Value);
        }
        if (createdBy.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.CreatedByUserId == createdBy.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            baseQuery = baseQuery.Where(t => t.Title.Contains(search) || t.Description.Contains(search));
        }

        var query =
            from t in baseQuery
            let lastMessage = _context.TicketMessages
                .Where(m => m.TicketId == t.Id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Message,
                    m.CreatedAt,
                    AuthorName = m.AuthorUser != null ? m.AuthorUser.FullName : null
                })
                .FirstOrDefault()
            let lastActivityAt = _context.TicketActivityEvents
                .Where(ae => ae.TicketId == t.Id)
                .OrderByDescending(ae => ae.CreatedAt)
                .Select(ae => (DateTime?)ae.CreatedAt)
                .FirstOrDefault()
            let lastSeenAt = _context.TicketUserStates
                .Where(state => state.TicketId == t.Id && state.UserId == userId)
                .Select(state => (DateTime?)state.LastSeenAt)
                .FirstOrDefault()
            let computedLastActivityAt = (DateTime?)(lastActivityAt ?? t.UpdatedAt ?? t.CreatedAt)
            let isUnseen = lastSeenAt == null ? (bool?)true : computedLastActivityAt > lastSeenAt
            where !unseen.HasValue || (isUnseen ?? false) == unseen.Value
            orderby t.CreatedAt descending
            select new TicketListItemResponse
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category!.Name : string.Empty,
                SubcategoryId = t.SubcategoryId,
                SubcategoryName = t.Subcategory != null ? t.Subcategory!.Name : null,
                Priority = t.Priority,
                CanonicalStatus = t.Status,
                DisplayStatus = t.Status,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = t.CreatedByUser != null ? t.CreatedByUser!.FullName : string.Empty,
                CreatedByEmail = t.CreatedByUser != null ? t.CreatedByUser!.Email : string.Empty,
                CreatedByPhoneNumber = t.CreatedByUser != null ? t.CreatedByUser!.PhoneNumber : null,
                CreatedByDepartment = t.CreatedByUser != null ? t.CreatedByUser!.Department : null,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null ? t.AssignedToUser!.FullName : null,
                AssignedToEmail = t.AssignedToUser != null ? t.AssignedToUser!.Email : null,
                AssignedToPhoneNumber = t.AssignedToUser != null ? t.AssignedToUser!.PhoneNumber : null,
                AssignedTechnicianName = t.AssignedToUserId != null
                    ? (t.Technician != null ? t.Technician!.FullName : (t.AssignedToUser != null ? t.AssignedToUser!.FullName : null))
                    : null,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                DueDate = t.DueDate,
                LastActivityAt = computedLastActivityAt,
                LastSeenAt = lastSeenAt,
                IsUnseen = isUnseen,
                IsUnread = isUnseen,
                LastMessagePreview = lastMessage != null ? lastMessage.Message : null,
                LastMessageAt = lastMessage != null ? (DateTime?)lastMessage.CreatedAt : null,
                LastMessageAuthorName = lastMessage != null ? lastMessage.AuthorName : null
            };

        return await query.ToListAsync();
    }

    /// <summary>Calendar month view: filter by UpdatedAt (or CreatedAt if null) in [startUtc, endUtcExclusive), Asia/Tehran day boundaries.</summary>
    public async Task<IEnumerable<Ticket>> GetCalendarTicketsAsync(DateTime startUtc, DateTime endUtcExclusive, TicketStatus? status = null)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Technician)
            .Where(t => (t.UpdatedAt ?? t.CreatedAt) >= startUtc && (t.UpdatedAt ?? t.CreatedAt) < endUtcExclusive);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>By-date modal: filter by UpdatedAt (or CreatedAt if null), range [startUtc, endUtcExclusive). Sort by UpdatedAt desc.</summary>
    public async Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsByUpdatedRangeAsync(
        DateTime startUtc,
        DateTime endUtcExclusive,
        int page,
        int pageSize)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.TechnicianUser)
            .Where(t => (t.UpdatedAt ?? t.CreatedAt) >= startUtc && (t.UpdatedAt ?? t.CreatedAt) < endUtcExclusive);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsByCreatedRangeAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.TechnicianUser)
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(IEnumerable<Ticket> Items, int TotalCount)> GetAdminTicketsBeforeAsync(
        DateTime beforeDate,
        int page,
        int pageSize)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians)
                .ThenInclude(ta => ta.TechnicianUser)
            .Where(t => t.CreatedAt < beforeDate);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int> CountByTechnicianIdAndStatusAsync(Guid technicianId, IEnumerable<TicketStatus> statuses)
    {
        return await _context.Tickets
            .CountAsync(t => 
                t.TechnicianId == technicianId && 
                statuses.Contains(t.Status));
    }

    public async Task<IEnumerable<Ticket>> GetUnassignedTicketsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Tickets
            .Where(t => t.TechnicianId == null)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= endDate.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Ticket> AddAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
        return ticket;
    }

    public Task UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }
}