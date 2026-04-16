namespace Ticketing.Backend.Domain.Entities;

/// <summary>
/// Represents an assignment of a technician to a ticket.
/// Supports multiple technicians per ticket with handoff capability.
/// </summary>
public class TicketTechnicianAssignment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianUserId { get; set; } // User ID of the technician
    public Guid? TechnicianId { get; set; } // Optional: Technician entity ID if exists
    public DateTime AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; } // Admin or technician who made the assignment
    /// <summary>When the technician accepted responsibility (e.g. first reply or StartWork). Null = assigned but not yet accepted.</summary>
    public DateTime? AcceptedAt { get; set; }
    public bool IsActive { get; set; } = true; // For handoff: inactive = previous assignment
    public string? Role { get; set; } // "Lead" or "Collaborator" or null
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Ticket? Ticket { get; set; }
    public User? TechnicianUser { get; set; }
    public User? AssignedByUser { get; set; }
    public Technician? Technician { get; set; }
}


































