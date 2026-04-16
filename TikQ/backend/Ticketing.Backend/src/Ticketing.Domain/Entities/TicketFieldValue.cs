namespace Ticketing.Domain.Entities;

public class TicketFieldValue
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public int FieldDefinitionId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Ticket? Ticket { get; set; }
    public SubcategoryFieldDefinition? FieldDefinition { get; set; }
}
