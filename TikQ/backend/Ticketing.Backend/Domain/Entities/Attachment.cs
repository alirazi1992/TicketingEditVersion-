namespace Ticketing.Backend.Domain.Entities;

public class Attachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;

    public Ticket? Ticket { get; set; }
}
