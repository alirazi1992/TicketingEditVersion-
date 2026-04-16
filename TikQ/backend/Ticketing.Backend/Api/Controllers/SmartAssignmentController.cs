using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/assignment")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class SmartAssignmentController : ControllerBase
{
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ISmartAssignmentService _smartAssignmentService;

    public SmartAssignmentController(
        ISystemSettingsService systemSettingsService,
        ISmartAssignmentService smartAssignmentService)
    {
        _systemSettingsService = systemSettingsService;
        _smartAssignmentService = smartAssignmentService;
    }

    /// <summary>
    /// Get smart assignment status
    /// </summary>
    [HttpGet("smart")]
    public async Task<ActionResult<SmartAssignmentStatusResponse>> GetSmartAssignmentStatus()
    {
        var settings = await _systemSettingsService.GetSystemSettingsAsync();
        return Ok(new SmartAssignmentStatusResponse
        {
            Enabled = settings.AutoAssignEnabled
        });
    }

    /// <summary>
    /// Update smart assignment status
    /// </summary>
    [HttpPut("smart")]
    public async Task<ActionResult<SmartAssignmentStatusResponse>> UpdateSmartAssignmentStatus(
        [FromBody] SmartAssignmentUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Get current settings
            var currentSettings = await _systemSettingsService.GetSystemSettingsAsync();
            
            // Update only the AutoAssignEnabled field
            var updateRequest = new SystemSettingsUpdateRequest
            {
                AppName = currentSettings.AppName,
                SupportEmail = currentSettings.SupportEmail,
                SupportPhone = currentSettings.SupportPhone,
                DefaultLanguage = currentSettings.DefaultLanguage,
                DefaultTheme = currentSettings.DefaultTheme,
                Timezone = currentSettings.Timezone,
                DefaultPriority = currentSettings.DefaultPriority,
                DefaultStatus = currentSettings.DefaultStatus,
                ResponseSlaHours = currentSettings.ResponseSlaHours,
                AutoAssignEnabled = request.Enabled, // Update this field
                AllowClientAttachments = currentSettings.AllowClientAttachments,
                MaxAttachmentSizeMB = currentSettings.MaxAttachmentSizeMB,
                EmailNotificationsEnabled = currentSettings.EmailNotificationsEnabled,
                SmsNotificationsEnabled = currentSettings.SmsNotificationsEnabled,
                NotifyOnTicketCreated = currentSettings.NotifyOnTicketCreated,
                NotifyOnTicketAssigned = currentSettings.NotifyOnTicketAssigned,
                NotifyOnTicketReplied = currentSettings.NotifyOnTicketReplied,
                NotifyOnTicketClosed = currentSettings.NotifyOnTicketClosed,
                PasswordMinLength = currentSettings.PasswordMinLength,
                Require2FA = currentSettings.Require2FA,
                SessionTimeoutMinutes = currentSettings.SessionTimeoutMinutes,
                AllowedEmailDomains = currentSettings.AllowedEmailDomains
            };

            await _systemSettingsService.UpdateSystemSettingsAsync(updateRequest);

            return Ok(new SmartAssignmentStatusResponse
            {
                Enabled = request.Enabled
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "خطا در به‌روزرسانی وضعیت تعیین هوشمند", error = ex.Message });
        }
    }

    /// <summary>
    /// Manually run smart assignment for unassigned tickets
    /// </summary>
    [HttpPost("smart/run")]
    public async Task<ActionResult<SmartAssignmentRunResponse>> RunSmartAssignment(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? scope = "unassigned")
    {
        try
        {
            // Check if smart assignment is enabled
            var settings = await _systemSettingsService.GetSystemSettingsAsync();
            if (!settings.AutoAssignEnabled)
            {
                return BadRequest(new { message = "سیستم تعیین هوشمند غیرفعال است. لطفاً ابتدا آن را فعال کنید." });
            }

            int assignedCount = await _smartAssignmentService.AssignUnassignedTicketsAsync(start, end);

            return Ok(new SmartAssignmentRunResponse
            {
                AssignedCount = assignedCount,
                Message = $"{assignedCount} تیکت به صورت خودکار تخصیص داده شد."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "خطا در اجرای تعیین هوشمند", error = ex.Message });
        }
    }
}

