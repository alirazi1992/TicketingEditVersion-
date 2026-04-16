using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ITicketTechnicianRepository
{
    Task<TicketTechnician?> GetByIdAsync(Guid id);
    Task<TicketTechnician?> GetByTicketAndTechnicianAsync(Guid ticketId, Guid technicianUserId);
    Task<TicketTechnician?> GetByTicketAndTechnicianIdAsync(Guid ticketId, Guid technicianId);
    Task<IEnumerable<TicketTechnician>> GetByTicketIdAsync(Guid ticketId);
    Task<IEnumerable<TicketTechnician>> GetByTechnicianUserIdAsync(Guid technicianUserId);
    Task<IEnumerable<TicketTechnician>> GetByTechnicianIdAsync(Guid technicianId);
    Task<TicketTechnician> AddAsync(TicketTechnician ticketTechnician);
    Task<TicketTechnician> UpdateAsync(TicketTechnician ticketTechnician);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> DeleteByTicketAndTechnicianAsync(Guid ticketId, Guid technicianUserId);
}
