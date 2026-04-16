using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    // Lockout fields for account security
    public bool LockoutEnabled { get; set; } = false;
    public DateTimeOffset? LockoutEnd { get; set; }
    public string? SecurityStamp { get; set; }

    public ICollection<Ticket> TicketsCreated { get; set; } = new List<Ticket>();
    public ICollection<Ticket> TicketsAssigned { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public UserPreferences? Preferences { get; set; }
}
