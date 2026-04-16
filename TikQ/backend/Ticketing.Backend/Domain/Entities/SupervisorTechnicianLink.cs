namespace Ticketing.Backend.Domain.Entities;

/// <summary>
/// Links a supervisor technician (User) to a managed technician (User).
/// </summary>
public class SupervisorTechnicianLink
{
    public Guid Id { get; set; }
    public Guid SupervisorUserId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? SupervisorUser { get; set; }
    public User? TechnicianUser { get; set; }
}
