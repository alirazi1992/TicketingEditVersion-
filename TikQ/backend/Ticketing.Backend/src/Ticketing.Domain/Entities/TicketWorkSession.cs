using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class TicketWorkSession
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public Guid TechnicianId { get; set; }
    public string WorkingOn { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public TicketTechnicianState? State { get; set; }
    public Ticket? Ticket { get; set; }
}
