namespace Ticketing.Backend.Application.DTOs;

/// <summary>
/// Response for GET /api/admin/reports/technician-work?from=&to=
/// </summary>
public class TechnicianWorkReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<TechnicianWorkReportUserDto> Users { get; set; } = new();
}

public class TechnicianWorkReportUserDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Technician | Supervisor (when IsSupervisor)</summary>
    public string Role { get; set; } = "Technician";
    public bool IsSupervisor { get; set; }
    public int TicketsOwned { get; set; }
    public int TicketsCollaborated { get; set; }
    public int TicketsTotalInvolved { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int RepliesCount { get; set; }
    public int StatusChangesCount { get; set; }
    public int AttachmentsCount { get; set; }
    public int GrantsCount { get; set; }
    public int RevokesCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public List<TechnicianWorkReportTicketSummaryDto> TopTickets { get; set; } = new();
}

public class TechnicianWorkReportTicketSummaryDto
{
    public Guid TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? LastActionAt { get; set; }
    public int ActionsCount { get; set; }
}

/// <summary>
/// Response for GET /api/admin/reports/technician-work/{userId}/activities?from=&to=
/// Drilldown: activities grouped by ticket.
/// </summary>
public class TechnicianWorkReportDetailDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<TechnicianWorkReportTicketActivityDto> ByTicket { get; set; } = new();
}

public class TechnicianWorkReportTicketActivityDto
{
    public Guid TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<TechnicianWorkReportActivityItemDto> Actions { get; set; } = new();
}

public class TechnicianWorkReportActivityItemDto
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
