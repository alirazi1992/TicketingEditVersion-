namespace Ticketing.Backend.Domain.Entities;

public class TicketUserState
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? LastSeenAt { get; set; }

    public Ticket? Ticket { get; set; }
    public User? User { get; set; }
}

