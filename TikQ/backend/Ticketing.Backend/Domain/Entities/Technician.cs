namespace Ticketing.Backend.Domain.Entities;

public class Technician
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSupervisor { get; set; } = false; // سرپرست role
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; } // Link to User for authentication

    // Soft delete fields
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public User? DeletedByUser { get; set; }
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TechnicianSubcategoryPermission> SubcategoryPermissions { get; set; } = new List<TechnicianSubcategoryPermission>();
}