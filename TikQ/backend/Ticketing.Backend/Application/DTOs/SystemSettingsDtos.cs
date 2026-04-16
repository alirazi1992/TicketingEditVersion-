using System.ComponentModel.DataAnnotations;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.DTOs;

public class SystemSettingsResponse
{
    // App / General
    public string AppName { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = "fa";
    public string DefaultTheme { get; set; } = "system";
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
    public List<string> AllowedEmailDomains { get; set; } = new();
}

public class SystemSettingsUpdateRequest
{
    // App / General
    [Required(ErrorMessage = "نام سامانه الزامی است")]
    [StringLength(200, ErrorMessage = "نام سامانه نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
    public string AppName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل پشتیبانی الزامی است")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر وارد کنید")]
    public string SupportEmail { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "شماره تماس نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
    public string SupportPhone { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(fa|en)$", ErrorMessage = "زبان باید fa یا en باشد")]
    public string DefaultLanguage { get; set; } = "fa";

    [Required]
    [RegularExpression("^(light|dark|system)$", ErrorMessage = "تم باید light، dark یا system باشد")]
    public string DefaultTheme { get; set; } = "system";

    [Required]
    [StringLength(100)]
    public string Timezone { get; set; } = "Asia/Tehran";

    // Ticketing Defaults
    [Required]
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

    [Required]
    public TicketStatus DefaultStatus { get; set; } = TicketStatus.Submitted;

    [Range(1, 168, ErrorMessage = "زمان SLA باید بین ۱ تا ۱۶۸ ساعت باشد")]
    public int ResponseSlaHours { get; set; } = 24;

    public bool AutoAssignEnabled { get; set; } = false;
    public bool AllowClientAttachments { get; set; } = true;

    [Range(1, 100, ErrorMessage = "حداکثر حجم فایل باید بین ۱ تا ۱۰۰ مگابایت باشد")]
    public int MaxAttachmentSizeMB { get; set; } = 10;

    // Notifications
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SmsNotificationsEnabled { get; set; } = false;
    public bool NotifyOnTicketCreated { get; set; } = true;
    public bool NotifyOnTicketAssigned { get; set; } = true;
    public bool NotifyOnTicketReplied { get; set; } = true;
    public bool NotifyOnTicketClosed { get; set; } = true;

    // Security
    [Range(4, 32, ErrorMessage = "حداقل طول رمز عبور باید بین ۴ تا ۳۲ کاراکتر باشد")]
    public int PasswordMinLength { get; set; } = 6;

    public bool Require2FA { get; set; } = false;

    [Range(5, 1440, ErrorMessage = "زمان انقضای نشست باید بین ۵ تا ۱۴۴۰ دقیقه باشد")]
    public int SessionTimeoutMinutes { get; set; } = 60;

    public List<string> AllowedEmailDomains { get; set; } = new();
}

