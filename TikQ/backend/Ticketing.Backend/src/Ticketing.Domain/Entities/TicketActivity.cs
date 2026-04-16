using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class TicketActivity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorUserId { get; set; }
    public TicketActivityType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Ticket? Ticket { get; set; }
    public User? ActorUser { get; set; }
}
