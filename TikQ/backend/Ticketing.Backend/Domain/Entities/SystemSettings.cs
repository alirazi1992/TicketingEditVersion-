using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Domain.Entities;

public class SystemSettings
{
    // Single row - always use Id = 1
    public int Id { get; set; } = 1;

    // App / General
    public string AppName { get; set; } = "سامانه تیکتینگ";
    public string SupportEmail { get; set; } = "support@example.com";
    public string SupportPhone { get; set; } = "";
    public string DefaultLanguage { get; set; } = "fa"; // "fa" | "en"
    public string DefaultTheme { get; set; } = "system"; // "light" | "dark" | "system"
    public string Timezone { get; set; } = "Asia/Tehran";

    // Ticketing Defaults
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;
    public TicketStatus DefaultStatus { get; set; } = TicketStatus.Submitted;
    public int ResponseSlaHours { get; set; } = 24;
    public bool AutoAssignEnabled { get; set; } = false;
    public bool AllowClientAttachments { get; set; } = true;
    public int MaxAttachmentSizeMB { get; set; } = 10;

    // Notifications
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SmsNotificationsEnabled { get; set; } = false;
    public bool NotifyOnTicketCreated { get; set; } = true;
    public bool NotifyOnTicketAssigned { get; set; } = true;
    public bool NotifyOnTicketReplied { get; set; } = true;
    public bool NotifyOnTicketClosed { get; set; } = true;

    // Security
    public int PasswordMinLength { get; set; } = 6;
    public bool Require2FA { get; set; } = false;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public string AllowedEmailDomains { get; set; } = ""; // JSON array as string, e.g., "[\"example.com\",\"company.com\"]"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

