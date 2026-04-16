namespace Ticketing.Backend.Domain.Entities;

/// <summary>
/// Audit log for ticket activities visible to Admin/Technician/Client.
/// Tracks who did what and when.
/// </summary>
public class TicketActivityEvent
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty; // "Admin", "Technician", "Client"
    public string EventType { get; set; } = string.Empty; // "AssignedTechnicians", "TechnicianOpened", "StartWork", "ReplyAdded", "StatusChanged", "Handoff", "Closed", "Revision"
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? MetadataJson { get; set; } // JSON for additional context (e.g., technician IDs, message preview)
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Ticket? Ticket { get; set; }
    public User? ActorUser { get; set; }
}


































