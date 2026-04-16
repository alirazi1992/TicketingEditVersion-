using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int? SubcategoryId { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? TechnicianId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? ResponsibleTechnicianId { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public Guid? ResponsibleSetByUserId { get; set; }
    public DateTime? ResponsibleSetAt { get; set; }

    public Category? Category { get; set; }
    public Subcategory? Subcategory { get; set; }
    public User? CreatedByUser { get; set; }
    public User? AssignedToUser { get; set; }
    public Technician? Technician { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<TicketFieldValue> FieldValues { get; set; } = new List<TicketFieldValue>();
    public ICollection<TicketTechnician> AssignedTechnicians { get; set; } = new List<TicketTechnician>();
    public ICollection<TicketActivity> Activities { get; set; } = new List<TicketActivity>();
}
