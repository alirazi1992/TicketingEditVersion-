using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.DTOs;

public class TicketDynamicFieldRequest
{
    public int FieldDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

public class FileAttachmentRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public long FileSize { get; set; }
}

public class TicketCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public TicketPriority Priority { get; set; }
    public List<TicketDynamicFieldRequest>? DynamicFields { get; set; }
}

public class TicketUpdateRequest
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
}

public class TicketListItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public TicketPriority Priority { get; set; }

    /// <summary>
    /// The canonical status stored in database. Use for internal logic only.
    /// For UI display, always use DisplayStatus.
    /// </summary>
    public TicketStatus CanonicalStatus { get; set; }

    /// <summary>
    /// The status to display in UI, mapped based on the requester's role.
    /// </summary>
    public TicketStatus DisplayStatus { get; set; }

    /// <summary>
    /// Legacy property for backward compatibility. Returns DisplayStatus.
    /// </summary>
    [Obsolete("Use DisplayStatus for UI rendering or CanonicalStatus for logic")]
    public TicketStatus Status => DisplayStatus;

    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string? CreatedByPhoneNumber { get; set; }
    public string? CreatedByDepartment { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? AssignedToPhoneNumber { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool? IsUnseen { get; set; }
    public bool? IsUnread { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessageAuthorName { get; set; }

    /// <summary>Owner | Collaborator | Candidate | None (for technician list: faded/read-only)</summary>
    public string? AccessMode { get; set; }
    public bool CanAct { get; set; }
    public bool IsFaded { get; set; }

    /// <summary>True when at least one assignment has AcceptedAt set (technician has accepted responsibility). Do not treat Assigned as Accepted.</summary>
    public bool IsAccepted { get; set; }
    /// <summary>First acceptance time, if any.</summary>
    public DateTime? AcceptedAt { get; set; }
    /// <summary>User ID of the technician who first accepted, if any.</summary>
    public Guid? AcceptedByUserId { get; set; }
}

public class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public TicketPriority Priority { get; set; }
    
    /// <summary>
    /// The canonical status stored in database. Use for internal logic only.
    /// For UI display, always use DisplayStatus.
    /// </summary>
    public TicketStatus CanonicalStatus { get; set; }
    
    /// <summary>
    /// The status to display in UI, mapped based on the requester's role.
    /// For clients: Redo appears as InProgress.
    /// For all other roles: Same as CanonicalStatus.
    /// </summary>
    public TicketStatus DisplayStatus { get; set; }
    
    /// <summary>
    /// Legacy property for backward compatibility. Returns DisplayStatus.
    /// New code should use DisplayStatus or CanonicalStatus explicitly.
    /// </summary>
    [Obsolete("Use DisplayStatus for UI rendering or CanonicalStatus for logic")]
    public TicketStatus Status => DisplayStatus;
    
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string? CreatedByPhoneNumber { get; set; }
    public string? CreatedByDepartment { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignedToEmail { get; set; }
    public string? AssignedToPhoneNumber { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public List<TicketDynamicFieldResponse>? DynamicFields { get; set; }
    public List<AssignedTechnicianDto>? AssignedTechnicians { get; set; }
    public List<TicketActivityEventDto>? ActivityEvents { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool? IsUnseen { get; set; }
    public bool? IsUnread { get; set; } // True if UpdatedAt > LastReadAt for current user
    public TicketLatestActivityDto? LatestActivity { get; set; }
    public bool CanClaim { get; set; }
    public string? ClaimDisabledReason { get; set; }

    // ==========================================================
    // Access flags (computed server-side; used to enforce read-only)
    // ==========================================================
    public bool CanView { get; set; }
    public bool CanReply { get; set; }
    public bool CanEdit { get; set; }
    public bool IsReadOnly { get; set; }
    public string? ReadOnlyReason { get; set; }
    public bool CanGrantAccess { get; set; }

    /// <summary>Owner | Collaborator | Candidate | None</summary>
    public string? AccessMode { get; set; }
    public bool CanAct { get; set; }
    public bool IsFaded { get; set; }

    /// <summary>True when at least one assignment has AcceptedAt set. Do not treat Assigned as Accepted.</summary>
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
}

public class TicketLatestActivityDto
{
    public string ActionType { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Summary { get; set; }
}

public class TicketDynamicFieldResponse
{
    public int FieldDefinitionId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}

public class TicketMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public TicketStatus? Status { get; set; }
}

/// <summary>Optional body for POST /api/tickets/{id}/messages/seen</summary>
public class MarkMessagesSeenRequest
{
    public Guid? LastSeenMessageId { get; set; }
}

public class TicketCollaboratorRequest
{
    public Guid TechnicianUserId { get; set; }
    /// <summary>"grant" | "revoke"</summary>
    public string Action { get; set; } = "grant";
}

/// <summary>Request body for POST /api/tickets/{id}/access/grant or /access/revoke</summary>
public class TicketAccessGrantRequest
{
    public Guid TechnicianUserId { get; set; }
}

public class TicketMessageDto
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    /// <summary>Role of the message author for display (e.g. Client, Technician, Admin, Supervisor).</summary>
    public string AuthorRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TicketStatus? Status { get; set; }
}

public class TicketCalendarResponse
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Canonical status from database</summary>
    public TicketStatus CanonicalStatus { get; set; }
    
    /// <summary>Display status mapped for the requester's role</summary>
    public TicketStatus DisplayStatus { get; set; }
    
    /// <summary>Legacy property for backward compatibility</summary>
    [Obsolete("Use DisplayStatus for UI")]
    public TicketStatus Status => DisplayStatus;
    
    public TicketPriority Priority { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? AssignedTechnicianName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AssignTechnicianRequest
{
    public Guid TechnicianId { get; set; }
}

public class AssignTechniciansRequest
{
    public List<Guid> TechnicianUserIds { get; set; } = new();
    public Guid? LeadTechnicianUserId { get; set; }
}

public class HandoffTicketRequest
{
    public Guid ToTechnicianUserId { get; set; }
    public bool DeactivateCurrent { get; set; } = true;
}

public class AssignedTechnicianDto
{
    public Guid Id { get; set; }
    public Guid TechnicianUserId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public string? TechnicianEmail { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
    public string? Role { get; set; } // "Lead", "Collaborator", or "Candidate"
    /// <summary>Owner | Collaborator | Candidate — for UI section "تکنسین‌های واگذار شده".</summary>
    public string? AccessMode { get; set; }
    /// <summary>True when this technician can act (Owner or Collaborator). Used for active count.</summary>
    public bool CanAct { get; set; }
}

public class TicketActivityEventDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketActivityDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public TicketActivityType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}