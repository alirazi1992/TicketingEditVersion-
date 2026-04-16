using System.ComponentModel.DataAnnotations;

namespace Ticketing.Backend.Application.DTOs;

public class UserPreferencesResponse
{
    public string Theme { get; set; } = "system";
    public string FontSize { get; set; } = "md";
    public string Language { get; set; } = "fa";
    public string Direction { get; set; } = "rtl"; // Derived from language: "fa" -> "rtl", "en" -> "ltr"
    public string Timezone { get; set; } = "Asia/Tehran";
    public NotificationPreferencesResponse Notifications { get; set; } = new();
}

public class NotificationPreferencesResponse
{
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
    public bool DesktopEnabled { get; set; } = true;
}

public class NotificationPreferencesUpdateRequest
{
    [Required]
    public bool EmailEnabled { get; set; } = true;

    [Required]
    public bool PushEnabled { get; set; } = true;

    [Required]
    public bool SmsEnabled { get; set; } = false;

    [Required]
    public bool DesktopEnabled { get; set; } = true;
}

public class UserPreferencesUpdateRequest
{
    [Required]
    [RegularExpression("^(light|dark|system)$", ErrorMessage = "Theme must be light, dark, or system")]
    public string Theme { get; set; } = "system";

    [Required]
    [RegularExpression("^(sm|md|lg)$", ErrorMessage = "FontSize must be sm, md, or lg")]
    public string FontSize { get; set; } = "md";

    [Required]
    [RegularExpression("^fa$", ErrorMessage = "Language must be fa (system is Farsi-only)")]
    public string Language { get; set; } = "fa";

    [Required]
    [MaxLength(100)]
    public string Timezone { get; set; } = "Asia/Tehran";
}

