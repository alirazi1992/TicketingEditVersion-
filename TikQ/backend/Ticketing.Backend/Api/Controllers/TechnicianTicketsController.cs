using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Api.Extensions;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/technician")]
[Authorize]
public class TechnicianTicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ITechnicianService _technicianService;
    private readonly ILogger<TechnicianTicketsController> _logger;

    public TechnicianTicketsController(
        ITicketService ticketService, 
        ITechnicianService technicianService,
        ILogger<TechnicianTicketsController> logger)
    {
        _ticketService = ticketService;
        _technicianService = technicianService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idValue, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Get tickets assigned to the current technician
    /// </summary>
    [HttpGet("tickets")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> GetMyTickets()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var tickets = await _ticketService.GetTicketsAsync(
                userId.Value,
                UserRole.Technician,
                status: null,
                priority: null,
                assignedTo: null,
                createdBy: null,
                search: null,
                unseen: null
            );
            return Ok(tickets);
        }
        catch (Exception ex)
        {
            var traceId = HttpContext.TraceIdentifier;
            _logger.LogError(ex,
                "GET /api/technician/tickets failed. TraceId={TraceId}, UserId={UserId}, ExceptionType={ExceptionType}, Message={Message}",
                traceId, userId.Value, ex.GetType().Name, ex.Message);
#if DEBUG
            _logger.LogDebug(ex, "StackTrace: {StackTrace}", ex.StackTrace);
#endif
            // In Development, include detail to help diagnose schema/DB issues (e.g. no such column)
            if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            {
                return StatusCode(500, new
                {
                    message = "Failed to retrieve technician tickets.",
                    traceId,
                    detail = ex.Message,
                    type = ex.GetType().Name
                });
            }
            return StatusCode(500, new { message = "Failed to retrieve technician tickets.", traceId });
        }
    }

    /// <summary>
    /// Get current technician's profile information
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> GetMyTechnicianProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var technician = await _technicianService.GetTechnicianByUserIdAsync(userId.Value);
        if (technician == null)
        {
            return NotFound(new { message = "Technician profile not found for current user" });
        }

        return Ok(technician);
    }

    /// <summary>
    /// Get list of assignable technicians (for supervisor delegation)
    /// Returns only active, non-supervisor technicians
    /// </summary>
    [HttpGet("available")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> GetAssignableTechnicians()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        // Verify current user is a supervisor
        var currentTechnician = await _technicianService.GetTechnicianByUserIdAsync(userId.Value);
        if (currentTechnician == null || !currentTechnician.IsSupervisor)
        {
            return this.ForbiddenProblem("Only supervisors can view assignable technicians");
        }

        var technicians = await _technicianService.GetAssignableTechniciansAsync();
        return Ok(technicians);
    }
}

