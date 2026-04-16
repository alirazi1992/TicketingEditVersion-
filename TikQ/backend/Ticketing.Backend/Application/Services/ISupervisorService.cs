using Ticketing.Backend.Application.DTOs;

namespace Ticketing.Backend.Application.Services;

public interface ISupervisorService
{
    /// <summary>Diagnostic: activeTechCount, linkedCount, sample emails/ids. supervisorUserId null when called as Admin.</summary>
    Task<SupervisorTechniciansDiagnosticDto> GetSupervisorTechniciansDiagnosticAsync(Guid? supervisorUserId);
    Task<IEnumerable<SupervisorTechnicianListItemDto>> GetTechniciansAsync(Guid supervisorUserId);
    Task<IEnumerable<TechnicianResponse>> GetAvailableTechniciansAsync(Guid supervisorUserId);
    Task<SupervisorTechnicianSummaryDto?> GetTechnicianSummaryAsync(Guid supervisorUserId, Guid technicianUserId);
    Task<List<TicketSummaryDto>> GetAvailableTicketsAsync(Guid supervisorUserId);
    Task<bool> LinkTechnicianAsync(Guid supervisorUserId, Guid technicianUserId);
    Task<bool> UnlinkTechnicianAsync(Guid supervisorUserId, Guid technicianUserId);
    Task<bool> AssignTicketAsync(Guid supervisorUserId, Guid technicianUserId, Guid ticketId);
    Task<bool> RemoveAssignmentAsync(Guid supervisorUserId, Guid technicianUserId, Guid ticketId);
}

