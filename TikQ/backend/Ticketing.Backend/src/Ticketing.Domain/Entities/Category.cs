namespace Ticketing.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subcategory> Subcategories { get; set; } = new List<Subcategory>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
