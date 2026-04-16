using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class TicketTechnician
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public bool IsLead { get; set; }
    public TicketTechnicianState State { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Ticket? Ticket { get; set; }
    public Technician? Technician { get; set; }
}
