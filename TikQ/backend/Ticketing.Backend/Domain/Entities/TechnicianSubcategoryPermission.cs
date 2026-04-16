namespace Ticketing.Backend.Domain.Entities;

/// <summary>
/// Represents a permission for a technician to handle tickets in a specific subcategory.
/// This enables expertise-based auto-assignment of tickets.
/// </summary>
public class TechnicianSubcategoryPermission
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }
    public int SubcategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Technician? Technician { get; set; }
    public Subcategory? Subcategory { get; set; }
}


































