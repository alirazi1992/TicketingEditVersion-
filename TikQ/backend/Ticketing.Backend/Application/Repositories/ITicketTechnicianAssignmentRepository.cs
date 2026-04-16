using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITicketTechnicianAssignmentRepository
{
    Task<TicketTechnicianAssignment?> GetByIdAsync(Guid id);
    Task<IEnumerable<TicketTechnicianAssignment>> GetAssignmentsForTicketAsync(Guid ticketId);
    Task<IEnumerable<TicketTechnicianAssignment>> GetActiveAssignmentsForTicketAsync(Guid ticketId);
    Task<IEnumerable<TicketTechnicianAssignment>> GetTicketsForTechnicianAsync(Guid technicianUserId);
    Task<IEnumerable<TicketTechnicianAssignment>> GetActiveTicketsForTechnicianAsync(Guid technicianUserId);
    Task<TicketTechnicianAssignment?> GetActiveAssignmentAsync(Guid ticketId, Guid technicianUserId);
    Task<TicketTechnicianAssignment> AddAsync(TicketTechnicianAssignment assignment);
    Task<TicketTechnicianAssignment> UpdateAsync(TicketTechnicianAssignment assignment);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> IsTechnicianAssignedAsync(Guid ticketId, Guid technicianUserId);
    Task SetAssignmentsAsync(Guid ticketId, IEnumerable<Guid> technicianUserIds, Guid assignedByUserId, string? leadTechnicianUserId = null);

    /// <summary>Returns (TicketId, AcceptedAt, TechnicianUserId) for the first accepted assignment per ticket. Used to populate IsAccepted/AcceptedAt/AcceptedByUserId on list items.</summary>
    Task<IReadOnlyList<(Guid TicketId, DateTime AcceptedAt, Guid AcceptedByUserId)>> GetFirstAcceptedByTicketIdsAsync(IEnumerable<Guid> ticketIds);
}



