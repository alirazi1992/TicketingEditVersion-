using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/supervisor")]
[Authorize(Policy = "SupervisorOrAdmin")]
public class SupervisorController : ControllerBase
{
    private readonly ISupervisorService _supervisorService;
    private readonly ILogger<SupervisorController> _logger;
    private readonly IWebHostEnvironment _env;

    public SupervisorController(
        ISupervisorService supervisorService,
        ILogger<SupervisorController> logger,
        IWebHostEnvironment env)
    {
        _supervisorService = supervisorService;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Resolves current user's User.Id (GUID) from claims. Use this for SupervisorUserId/TechnicianUserId (table columns are User.Id).
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token (expect NameIdentifier or sub claim with GUID)");
        }
        return userId;
    }

    /// <summary>
    /// Get list of technicians managed by the current supervisor
    /// </summary>
    [HttpGet("technicians")]
    public async Task<ActionResult<IEnumerable<SupervisorTechnicianListItemDto>>> GetTechnicians()
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            if (_env.IsDevelopment())
            {
                var sub = User.FindFirst("sub")?.Value;
                var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                _logger.LogInformation(
                    "[SUPERVISOR_DEV] GET technicians: sub={Sub}, NameIdentifier={NameId}, email={Email}, resolvedSupervisorUserId={SupervisorUserId}",
                    sub, nameId, email, supervisorUserId);
            }
            var technicians = await _supervisorService.GetTechniciansAsync(supervisorUserId);
            return Ok(technicians);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to supervisor technicians");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supervisor technicians");
            return StatusCode(500, new { message = "An error occurred while retrieving technicians" });
        }
    }

    /// <summary>
    /// Get available technicians that can be linked to the supervisor
    /// </summary>
    [HttpGet("technicians/available")]
    public async Task<ActionResult<IEnumerable<TechnicianResponse>>> GetAvailableTechnicians()
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            if (_env.IsDevelopment())
            {
                var sub = User.FindFirst("sub")?.Value;
                var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
                _logger.LogInformation(
                    "[SUPERVISOR_DEV] GET technicians/available: sub={Sub}, NameIdentifier={NameId}, email={Email}, resolvedSupervisorUserId={SupervisorUserId}",
                    sub, nameId, email, supervisorUserId);
            }
            var technicians = await _supervisorService.GetAvailableTechniciansAsync(supervisorUserId);
            return Ok(technicians);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to available technicians");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available technicians");
            return StatusCode(500, new { message = "An error occurred while retrieving available technicians" });
        }
    }

    /// <summary>
    /// Get summary for a specific technician
    /// </summary>
    [HttpGet("technicians/{technicianUserId}/summary")]
    public async Task<ActionResult<SupervisorTechnicianSummaryDto>> GetTechnicianSummary(Guid technicianUserId)
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var summary = await _supervisorService.GetTechnicianSummaryAsync(supervisorUserId, technicianUserId);
            
            if (summary == null)
            {
                return NotFound(new { message = "Technician not found or not managed by this supervisor" });
            }
            
            return Ok(summary);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to technician summary");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician summary for {TechnicianUserId}", technicianUserId);
            return StatusCode(500, new { message = "An error occurred while retrieving technician summary" });
        }
    }

    /// <summary>
    /// Get tickets available for assignment
    /// </summary>
    [HttpGet("tickets/available-to-assign")]
    public async Task<ActionResult<List<TicketSummaryDto>>> GetAvailableTickets()
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var tickets = await _supervisorService.GetAvailableTicketsAsync(supervisorUserId);
            return Ok(tickets);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to available tickets");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tickets");
            return StatusCode(500, new { message = "An error occurred while retrieving available tickets" });
        }
    }

    /// <summary>
    /// Link a technician to the supervisor
    /// </summary>
    [HttpPost("technicians/{technicianUserId}/link")]
    public async Task<ActionResult> LinkTechnician(Guid technicianUserId)
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var success = await _supervisorService.LinkTechnicianAsync(supervisorUserId, technicianUserId);
            
            if (!success)
            {
                return BadRequest(new { message = "Failed to link technician" });
            }
            
            return Ok(new { message = "Technician linked successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to link technician");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking technician {TechnicianUserId}", technicianUserId);
            return StatusCode(500, new { message = "An error occurred while linking technician" });
        }
    }

    /// <summary>
    /// Unlink a technician from the supervisor
    /// </summary>
    [HttpDelete("technicians/{technicianUserId}/link")]
    public async Task<ActionResult> UnlinkTechnician(Guid technicianUserId)
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var success = await _supervisorService.UnlinkTechnicianAsync(supervisorUserId, technicianUserId);
            
            if (!success)
            {
                return BadRequest(new { message = "Failed to unlink technician" });
            }
            
            return Ok(new { message = "Technician unlinked successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to unlink technician");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking technician {TechnicianUserId}", technicianUserId);
            return StatusCode(500, new { message = "An error occurred while unlinking technician" });
        }
    }

    /// <summary>
    /// Assign a ticket to a technician
    /// </summary>
    [HttpPost("technicians/{technicianUserId}/assignments")]
    public async Task<ActionResult> AssignTicket(Guid technicianUserId, [FromBody] SupervisorTicketAssignmentRequest request)
    {
        try
        {
            if (request.TicketId == Guid.Empty)
            {
                return BadRequest(new { message = "TicketId is required", field = "ticketId" });
            }

            var supervisorUserId = GetCurrentUserId();
            
            // Check if technician is linked
            var isLinked = await _supervisorService.GetTechniciansAsync(supervisorUserId);
            if (!isLinked.Any(t => t.TechnicianUserId == technicianUserId))
            {
                return BadRequest(new { 
                    message = "Technician is not linked to this supervisor", 
                    field = "technicianUserId",
                    details = "You must link this technician before assigning tickets to them"
                });
            }
            
            var success = await _supervisorService.AssignTicketAsync(supervisorUserId, technicianUserId, request.TicketId);
            
            if (!success)
            {
                return BadRequest(new { 
                    message = "Cannot assign ticket. Either the ticket is not assigned to you, or it doesn't exist.",
                    details = "As a supervisor, you can only delegate tickets that are currently assigned to you."
                });
            }
            
            return Ok(new { message = "Ticket assigned successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to assign ticket");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning ticket {TicketId} to technician {TechnicianUserId}", 
                request.TicketId, technicianUserId);
            return StatusCode(500, new { message = "An error occurred while assigning ticket", error = ex.Message });
        }
    }

    /// <summary>
    /// Remove ticket assignment from a technician
    /// </summary>
    [HttpDelete("technicians/{technicianUserId}/assignments/{ticketId}")]
    public async Task<ActionResult> RemoveAssignment(Guid technicianUserId, Guid ticketId)
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var success = await _supervisorService.RemoveAssignmentAsync(supervisorUserId, technicianUserId, ticketId);
            
            if (!success)
            {
                return BadRequest(new { message = "Failed to remove assignment" });
            }
            
            return Ok(new { message = "Assignment removed successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to remove assignment");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing assignment of ticket {TicketId} from technician {TechnicianUserId}", 
                ticketId, technicianUserId);
            return StatusCode(500, new { message = "An error occurred while removing assignment" });
        }
    }

    /// <summary>
    /// Get technician report (CSV format) - FULL PERSIAN with Persian calendar and digits
    /// </summary>
    [HttpGet("technicians/{technicianUserId}/report")]
    public async Task<ActionResult> GetTechnicianReport(Guid technicianUserId, [FromQuery] string format = "csv")
    {
        try
        {
            var supervisorUserId = GetCurrentUserId();
            var summary = await _supervisorService.GetTechnicianSummaryAsync(supervisorUserId, technicianUserId);
            
            if (summary == null)
            {
                return NotFound(new { message = "تکنسین یافت نشد یا تحت مدیریت این سرپرست نیست" });
            }

            if (format.ToLower() == "csv")
            {
                var csv = GeneratePersianCsvReport(summary);
                var bytes = Application.Common.PersianFormat.GetCsvBytes(csv); // Now includes UTF-8 BOM
                
                // Generate Persian-safe filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
                var techName = Application.Common.PersianFormat.SafeFileName(summary.TechnicianName);
                var fileName = $"technician-report-{techName}-{timestamp}.csv";
                
                return File(bytes, "text/csv; charset=utf-8", fileName);
            }

            return BadRequest(new { message = "فرمت پشتیبانی نمی‌شود. از 'csv' استفاده کنید." });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to technician report");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report for technician {TechnicianUserId}", technicianUserId);
            return StatusCode(500, new { message = "خطا در تولید گزارش رخ داد" });
        }
    }

    /// <summary>
    /// Generate CSV report with FULL Persian formatting:
    /// - Persian calendar dates (۱۴۰۳/۱۱/۱۲)
    /// - Persian digits (۰۱۲۳۴۵۶۷۸۹)
    /// - Persian headers
    /// - Persian status labels
    /// </summary>
    private string GeneratePersianCsvReport(SupervisorTechnicianSummaryDto summary)
    {
        var sb = new System.Text.StringBuilder();
        
        // Persian Headers
        sb.AppendLine("شناسه تیکت,عنوان,وضعیت,نام مشتری,تاریخ ایجاد,آخرین بروزرسانی,نوع");
        
        // Active tickets
        foreach (var ticket in summary.ActiveTickets)
        {
            var persianCreatedAt = Application.Common.PersianFormat.ToPersianDateTime(ticket.CreatedAt);
            var persianUpdatedAt = Application.Common.PersianFormat.ToPersianDateTime(ticket.UpdatedAt);
            var persianStatus = Application.Common.PersianFormat.GetPersianStatus(ticket.DisplayStatus);
            
            sb.AppendLine(string.Join(",",
                Application.Common.PersianFormat.EscapeCsv(ticket.Id.ToString()),
                Application.Common.PersianFormat.EscapeCsv(ticket.Title),
                Application.Common.PersianFormat.EscapeCsv(persianStatus),
                Application.Common.PersianFormat.EscapeCsv(ticket.ClientName),
                Application.Common.PersianFormat.EscapeCsv(persianCreatedAt),
                Application.Common.PersianFormat.EscapeCsv(persianUpdatedAt),
                Application.Common.PersianFormat.EscapeCsv("فعال")
            ));
        }
        
        // Archive tickets
        foreach (var ticket in summary.ArchiveTickets)
        {
            var persianCreatedAt = Application.Common.PersianFormat.ToPersianDateTime(ticket.CreatedAt);
            var persianUpdatedAt = Application.Common.PersianFormat.ToPersianDateTime(ticket.UpdatedAt);
            var persianStatus = Application.Common.PersianFormat.GetPersianStatus(ticket.DisplayStatus);
            
            sb.AppendLine(string.Join(",",
                Application.Common.PersianFormat.EscapeCsv(ticket.Id.ToString()),
                Application.Common.PersianFormat.EscapeCsv(ticket.Title),
                Application.Common.PersianFormat.EscapeCsv(persianStatus),
                Application.Common.PersianFormat.EscapeCsv(ticket.ClientName),
                Application.Common.PersianFormat.EscapeCsv(persianCreatedAt),
                Application.Common.PersianFormat.EscapeCsv(persianUpdatedAt),
                Application.Common.PersianFormat.EscapeCsv("آرشیو")
            ));
        }
        
        return sb.ToString();
    }
}
