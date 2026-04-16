using Ticketing.Application.DTOs;
using Ticketing.Domain.Enums;

namespace Ticketing.Application.Services;

public interface ITicketService
{
    Task<IEnumerable<TicketResponse>> GetTicketsAsync(Guid userId, UserRole role, TicketStatus? status, TicketPriority? priority, Guid? assignedTo, Guid? createdBy, string? search);
    Task<IEnumerable<TicketResponse>> GetTechnicianTicketsAsync(Guid technicianUserId, string? mode = null); // mode: "assigned" | "responsible" | null (all)
    Task<TicketResponse?> GetTicketAsync(Guid id, Guid userId, UserRole role);
    Task<TicketResponse?> CreateTicketAsync(Guid userId, TicketCreateRequest request, List<DTOs.FileAttachmentRequest>? attachments = null);
    Task<TicketResponse?> UpdateTicketAsync(Guid id, Guid userId, UserRole role, TicketUpdateRequest request);
    Task<TicketResponse?> AssignTicketAsync(Guid id, Guid technicianId);
    Task<IEnumerable<TicketMessageDto>> GetMessagesAsync(Guid ticketId, Guid userId, UserRole role);
    Task<TicketMessageDto?> AddMessageAsync(Guid ticketId, Guid authorId, string message, TicketStatus? status = null);
    Task<IEnumerable<TicketCalendarResponse>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate);
    Task<AssignmentQueueResponse> GetAssignmentQueueAsync(string? type = null, TicketStatus? status = null, int? page = null, int? pageSize = null);
    
    // Multi-technician assignment methods
    Task<List<TicketTechnicianDto>> AssignTechniciansAsync(Guid ticketId, List<Guid> technicianIds, Guid? leadTechnicianId, Guid actorUserId);
    Task<bool> RemoveTechnicianAsync(Guid ticketId, Guid technicianId, Guid actorUserId);
    Task<List<TicketTechnicianDto>> GetTicketTechniciansAsync(Guid ticketId, Guid userId, UserRole role);
    Task<TicketTechnicianDto?> UpdateTechnicianStateAsync(Guid ticketId, Guid technicianUserId, TicketTechnicianState newState);
    Task<List<TicketActivityDto>> GetTicketActivitiesAsync(Guid ticketId, Guid userId, UserRole role);
    
    // Collaboration/work session methods
    Task UpdateWorkSessionAsync(Guid ticketId, Guid technicianUserId, UpdateWorkSessionRequest request);
    Task<TicketCollaborationResponse?> GetCollaborationDataAsync(Guid ticketId, Guid userId, UserRole role);
    
    // Responsible technician delegation
    Task<bool> SetResponsibleTechnicianAsync(Guid ticketId, Guid responsibleTechnicianId, Guid actorUserId, UserRole actorRole);
    
    // Status transition methods
    Task<TicketResponse?> UpdateTicketStatusAsync(Guid ticketId, Guid userId, UserRole role, TicketStatus newStatus);
    
    // Handoff methods
    Task<TicketResponse?> HandoffTicketAsync(Guid ticketId, Guid fromTechnicianUserId, Guid toTechnicianId, string? reason, string? note, Guid actorUserId, UserRole actorRole);
}