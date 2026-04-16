using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/tickets")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminTicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<AdminTicketsController> _logger;

    public AdminTicketsController(ITicketService ticketService, ILogger<AdminTicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Tickets updated on a single day (Admin only). For calendar day click. Based on UpdatedAt (آخرین بروزرسانی).
    /// Single source of truth: Jalali day in Asia/Tehran. Day boundaries: startUtc inclusive, endUtc exclusive.
    /// GET /api/admin/tickets/by-date?dayJalali=1404/11/16 (preferred) or ?date=YYYY-MM-DD (Gregorian in Tehran).
    /// </summary>
    [HttpGet("by-date")]
    public async Task<IActionResult> GetTicketsByDate([FromQuery] string? date, [FromQuery] string? dayJalali)
    {
        DateTime startUtc;
        DateTime endUtc;
        string? dayLabel = null;

        if (!string.IsNullOrWhiteSpace(dayJalali))
        {
            if (!TryParseJalaliDayToTehranRange(dayJalali.Trim().Replace('-', '/'), out startUtc, out endUtc))
            {
                return BadRequest(new { message = "Query 'dayJalali' must be a valid Jalali date (YYYY/MM/DD or YYYY-MM-DD)." });
            }
            dayLabel = dayJalali;
        }
        else if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            var localStart = DateTime.SpecifyKind(
                new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 0, 0, 0),
                DateTimeKind.Unspecified);
            startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
            endUtc = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), tz);
            dayLabel = date;
        }
        else
        {
            return BadRequest(new { message = "Provide either 'dayJalali' (e.g. 1404/11/16) or 'date' (YYYY-MM-DD)." });
        }

        try
        {
            var result = await _ticketService.GetAdminTicketsByUpdatedRangeAsync(startUtc, endUtc, page: 1, pageSize: 500);
            var items = result.Items.Select(t => new AdminTicketByDateItemDto
            {
                TicketId = t.Id,
                Title = t.Title,
                Status = t.DisplayStatus,
                Priority = TicketPriority.Medium,
                UpdatedAt = t.UpdatedAt ?? t.CreatedAt,
                AssignedToName = t.AssignedTechnicians?.FirstOrDefault()?.Name,
                Code = $"T-{t.Id.ToString("N")[..8].ToUpperInvariant()}",
            }).ToList();

#if DEBUG
            _logger.LogInformation(
                "GetTicketsByDate: dayLabel={DayLabel}, startUtc={StartUtc:O}, endUtc={EndUtc:O}, count={Count}",
                dayLabel ?? string.Empty, startUtc, endUtc, items.Count.ToString());
#endif
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tickets by date {DayLabel}", dayLabel);
            return StatusCode(500, new { message = "Failed to retrieve tickets", error = ex.Message });
        }
    }

    /// <summary>Parse Jalali day (YYYY/MM/DD) to Tehran local day range in UTC. End-exclusive.</summary>
    private static bool TryParseJalaliDayToTehranRange(string jalaliDay, out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = default;
        endUtc = default;
        var parts = jalaliDay.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var jy) || !int.TryParse(parts[1], out var jm) || !int.TryParse(parts[2], out var jd))
            return false;
        try
        {
            var pc = new PersianCalendar();
            var localStart = pc.ToDateTime(jy, jm, jd, 0, 0, 0, 0);
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
            startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
            endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart.AddDays(1), DateTimeKind.Unspecified), tz);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recent tickets (default: last 30 days)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentTickets([FromQuery] int days = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (days <= 0) days = 30;
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        try
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-days);
            var result = await _ticketService.GetAdminTicketsAsync(start, end, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent tickets");
            return StatusCode(500, new { message = "Failed to retrieve tickets", error = ex.Message });
        }
    }

    /// <summary>
    /// Archive tickets older than a threshold (default: older than 30 days)
    /// </summary>
    [HttpGet("archive")]
    public async Task<IActionResult> GetArchiveTickets([FromQuery] int olderThanDays = 30, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (olderThanDays <= 0) olderThanDays = 30;
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        try
        {
            var before = DateTime.UtcNow.AddDays(-olderThanDays);
            var result = await _ticketService.GetAdminArchiveTicketsAsync(before, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive tickets");
            return StatusCode(500, new { message = "Failed to retrieve archive tickets", error = ex.Message });
        }
    }

    /// <summary>
    /// Admin ticket details with analytics
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetTicketDetails(Guid id)
    {
        try
        {
            var details = await _ticketService.GetAdminTicketDetailsAsync(id);
            if (details == null)
            {
                return NotFound();
            }
            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ticket details {TicketId}", id);
            return StatusCode(500, new { message = "Failed to retrieve ticket details", error = ex.Message });
        }
    }

    /// <summary>
    /// Auto-assign matching technicians by ticket coverage (Admin only)
    /// </summary>
    [HttpPost("{id}/assign/auto")]
    public async Task<IActionResult> AutoAssignTechnicians(Guid id)
    {
        var adminUserId = GetUserId();
        if (!adminUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _ticketService.AutoAssignTechniciansByCoverageAsync(id, adminUserId.Value);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign technicians for ticket {TicketId}", id);
            return StatusCode(500, new { message = "Failed to auto-assign technicians", error = ex.Message });
        }
    }

    /// <summary>
    /// Manually assign technicians to a ticket (Admin only)
    /// </summary>
    [HttpPost("{id}/assign/manual")]
    public async Task<IActionResult> ManualAssignTechnicians(Guid id, [FromBody] AdminTicketManualAssignRequest request)
    {
        var adminUserId = GetUserId();
        if (!adminUserId.HasValue)
        {
            return Unauthorized();
        }

        if (request == null || request.TechnicianUserIds == null)
        {
            return BadRequest(new { message = "TechnicianUserIds are required." });
        }

        try
        {
            var result = await _ticketService.ManualAssignTechniciansAsync(id, adminUserId.Value, request.TechnicianUserIds);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manually assign technicians for ticket {TicketId}", id);
            return StatusCode(500, new { message = "Failed to assign technicians", error = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

