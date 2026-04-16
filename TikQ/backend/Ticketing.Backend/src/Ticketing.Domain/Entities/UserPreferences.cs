namespace Ticketing.Domain.Entities;

public class UserPreferences
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Appearance settings
    public string Theme { get; set; } = "system"; // "light" | "dark" | "system"
    public string FontSize { get; set; } = "md"; // "sm" | "md" | "lg"
    public string Language { get; set; } = "fa"; // "fa" | "en"
    public string Timezone { get; set; } = "Asia/Tehran";

    // Notification preferences
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
    public bool DesktopEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

