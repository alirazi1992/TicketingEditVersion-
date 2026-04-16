using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.DTOs;

public class AdminTicketAssigneeDto
{
    public Guid TechnicianUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
}

public class AdminTicketAssignmentResultDto
{
    public List<AdminTicketAssigneeDto> Assignees { get; set; } = new();
    public List<AdminTicketAssigneeDto> AddedTechnicians { get; set; } = new();
}

public class AdminTicketManualAssignRequest
{
    public List<Guid> TechnicianUserIds { get; set; } = new();
}

public class AdminTicketListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? SubcategoryName { get; set; }
    
    /// <summary>Canonical status from database</summary>
    public TicketStatus CanonicalStatus { get; set; }
    
    /// <summary>Display status mapped for the requester's role</summary>
    public TicketStatus DisplayStatus { get; set; }
    
    /// <summary>Legacy property for backward compatibility</summary>
    [Obsolete("Use DisplayStatus for UI")]
    public TicketStatus Status => DisplayStatus;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public List<AdminTicketAssigneeDto> AssignedTechnicians { get; set; } = new();
}

public class AdminTicketListResponse
{
    public List<AdminTicketListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Minimal ticket item for GET /api/admin/tickets/by-date (calendar day list). Based on UpdatedAt (آخرین بروزرسانی).
/// </summary>
public class AdminTicketByDateItemDto
{
    public Guid TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedToName { get; set; }
    /// <summary>Optional short code e.g. T-XXXXXXXX</summary>
    public string? Code { get; set; }
}

public class AdminTicketDurationDto
{
    public double? Seconds { get; set; }
    public string? Display { get; set; }
}

public class AdminTicketResponderDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AdminTicketMessageDto
{
    public Guid Id { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TicketStatus? Status { get; set; }
}

public class AdminTicketDetailsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? SubcategoryName { get; set; }
    
    /// <summary>Canonical status from database</summary>
    public TicketStatus CanonicalStatus { get; set; }
    
    /// <summary>Display status mapped for the requester's role</summary>
    public TicketStatus DisplayStatus { get; set; }
    
    /// <summary>Legacy property for backward compatibility</summary>
    [Obsolete("Use DisplayStatus for UI")]
    public TicketStatus Status => DisplayStatus;
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public AdminTicketDurationDto? TimeToFirstResponse { get; set; }
    public AdminTicketDurationDto? TimeToAnswered { get; set; }
    public AdminTicketDurationDto? TimeToClosed { get; set; }

    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string? ClientPhone { get; set; }
    public string? ClientDepartment { get; set; }

    public List<AdminTicketAssigneeDto> AssignedTechnicians { get; set; } = new();
    public List<AdminTicketResponderDto> Responders { get; set; } = new();
    public List<AdminTicketMessageDto> Messages { get; set; } = new();
    public List<TicketActivityEventDto> ActivityEvents { get; set; } = new();
}

