using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

/// <summary>
/// Result of a status change operation
/// </summary>
public class StatusChangeResult
{
    public bool Success { get; set; }
    public TicketStatus? OldStatus { get; set; }
    public TicketStatus? NewStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public TicketResponse? Ticket { get; set; }
}

public interface ITicketService
{
    Task<IEnumerable<TicketListItemResponse>> GetTicketsAsync(Guid userId, UserRole role, TicketStatus? status, TicketPriority? priority, Guid? assignedTo, Guid? createdBy, string? search, bool? unseen = null);
    Task<TicketResponse?> GetTicketAsync(Guid id, Guid userId, UserRole role);
    Task<TicketResponse?> CreateTicketAsync(Guid userId, TicketCreateRequest request, List<DTOs.FileAttachmentRequest>? attachments = null);
    Task<TicketResponse?> UpdateTicketAsync(Guid id, Guid userId, UserRole role, TicketUpdateRequest request);
    Task<TicketResponse?> AssignTicketAsync(Guid id, Guid technicianId);
    Task<TicketResponse?> ClaimTicketAsync(Guid ticketId, Guid technicianUserId);
    Task<IEnumerable<TicketMessageDto>> GetMessagesAsync(Guid ticketId, Guid userId, UserRole role);
    Task<TicketMessageDto?> AddMessageAsync(Guid ticketId, Guid authorId, string message, TicketStatus? status = null);
    Task<TicketResponse?> UpdateCollaboratorAsync(Guid ticketId, Guid actorUserId, UserRole actorRole, Guid technicianUserId, string action);
    Task<bool> MarkTicketSeenAsync(Guid ticketId, Guid userId, UserRole role);
    Task<IEnumerable<TicketCalendarResponse>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate, TicketStatus? status = null);
    Task<AdminTicketListResponse> GetAdminTicketsAsync(DateTime startDate, DateTime endDate, int page, int pageSize);
    /// <summary>Calendar day modal: tickets by UpdatedAt (آخرین بروزرسانی) in Tehran local day range.</summary>
    Task<AdminTicketListResponse> GetAdminTicketsByUpdatedRangeAsync(DateTime startUtc, DateTime endUtcExclusive, int page, int pageSize);
    Task<AdminTicketListResponse> GetAdminArchiveTicketsAsync(DateTime beforeDate, int page, int pageSize);
    Task<AdminTicketDetailsDto?> GetAdminTicketDetailsAsync(Guid ticketId);
    Task<AdminTicketAssignmentResultDto?> AutoAssignTechniciansByCoverageAsync(Guid ticketId, Guid adminUserId);
    Task<AdminTicketAssignmentResultDto?> ManualAssignTechniciansAsync(Guid ticketId, Guid adminUserId, List<Guid> technicianUserIds);
    Task<TicketResponse?> AssignTechniciansAsync(Guid ticketId, List<Guid> technicianUserIds, Guid assignedByUserId, Guid? leadTechnicianUserId = null);
    Task<TicketResponse?> HandoffTicketAsync(Guid ticketId, Guid fromTechnicianUserId, Guid toTechnicianUserId, bool deactivateCurrent = true, UserRole? requesterRole = null);
    
    /// <summary>
    /// Centralized method to change ticket status with full validation and audit trail.
    /// This is the SINGLE SOURCE OF TRUTH for all status changes.
    /// 
    /// Authorization rules:
    /// - Admin: Can change to any status
    /// - Supervisor: Can change status for tickets assigned to their team
    /// - Technician: Can change status only for tickets assigned to them (Open, InProgress, Solved, Redo)
    /// - Client: Can only change to limited statuses (not InProgress, Solved, Redo)
    /// 
    /// Side effects:
    /// - Updates Ticket.Status (canonical field)
    /// - Updates Ticket.UpdatedAt
    /// - Creates TicketActivityEvent with StatusChanged type
    /// - Broadcasts TicketUpdated event via SignalR (if available)
    /// - Notifies all participants
    /// </summary>
    Task<StatusChangeResult> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, Guid actorUserId, UserRole actorRole);
}



