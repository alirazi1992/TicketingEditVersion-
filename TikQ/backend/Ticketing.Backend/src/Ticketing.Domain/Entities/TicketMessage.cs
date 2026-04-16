using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class TicketMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TicketStatus? Status { get; set; }

    public Ticket? Ticket { get; set; }
    public User? AuthorUser { get; set; }
}
