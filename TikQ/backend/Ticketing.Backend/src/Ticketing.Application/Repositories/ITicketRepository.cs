using Ticketing.Domain.Entities;
using Ticketing.Domain.Enums;

namespace Ticketing.Application.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByIdWithIncludesAsync(Guid id); // Includes Category, Subcategory, Users, Technicians, FieldValues
    Task<IEnumerable<Ticket>> GetTicketsAsync(
        UserRole role, 
        Guid userId, 
        TicketStatus? status = null, 
        TicketPriority? priority = null, 
        Guid? assignedTo = null, 
        Guid? createdBy = null, 
        string? search = null);
    Task<Ticket> AddAsync(Ticket ticket);
    Task<Ticket> UpdateAsync(Ticket ticket);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<Ticket>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate);
    Task<int> GetUnassignedCountAsync();
    Task<Ticket?> GetBasicByIdAsync(Guid id); // Returns ticket with minimal includes (just Id, Title, Status, CreatedByUserId)
}
