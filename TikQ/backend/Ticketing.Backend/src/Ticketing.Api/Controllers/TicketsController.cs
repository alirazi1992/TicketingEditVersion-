using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ticketing.Application.DTOs;
using Ticketing.Application.Exceptions;
using Ticketing.Application.Services;
using Ticketing.Domain.Enums;
using System.Text.Json;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ITicketService ticketService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    private (Guid userId, UserRole role)? GetUserContext()
    {
        // Read the user id and role that were embedded into the JWT at login time
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleValue = User.FindFirstValue(ClaimTypes.Role);
        if (Guid.TryParse(idValue, out var userId) && Enum.TryParse<UserRole>(roleValue, out var role))
        {
            return (userId, role);
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, [FromQuery] Guid? assignedTo, [FromQuery] Guid? createdBy, [FromQuery] string? search)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            _logger.LogInformation("GetTickets Controller: UserId={UserId}, Role={Role}, Status={Status}, Priority={Priority}",
                context.Value.userId, context.Value.role, status, priority);
            
            // Service method applies role-based filtering (clients see their own tickets, technicians see assignments)
            var tickets = await _ticketService.GetTicketsAsync(context.Value.userId, context.Value.role, status, priority, assignedTo, createdBy, search);
            return Ok(tickets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTickets Controller FAILED: UserId={UserId}, Role={Role}, ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
                context.Value.userId, context.Value.role, ex.GetType().Name, ex.Message, ex.StackTrace);
            throw; // Re-throw to let exception handler middleware format the response
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.GetTicketAsync(id, context.Value.userId, context.Value.role);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Client))]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB limit
    public async Task<IActionResult> CreateTicket([FromForm] string? ticketData, [FromForm] List<IFormFile>? attachments)
    {
        TicketCreateRequest? request = null;
        
        // Parse ticket data from form
        if (!string.IsNullOrWhiteSpace(ticketData))
        {
            try
            {
                request = JsonSerializer.Deserialize<TicketCreateRequest>(ticketData, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("Failed to parse ticket data from form: {Error}", ex.Message);
                return BadRequest(new { message = "Invalid ticket data format.", error = "INVALID_JSON" });
            }
        }
        
        // If no form data, try to get from body (for backward compatibility with JSON-only requests)
        if (request == null)
        {
            // Try reading from body as JSON
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    request = JsonSerializer.Deserialize<TicketCreateRequest>(body, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });
                }
            }
            catch
            {
                // Ignore - will return error below
            }
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        // Log received request for debugging
        _logger.LogInformation("CreateTicket called with CategoryId={CategoryId}, SubcategoryId={SubcategoryId}, Priority={Priority}, AttachmentCount={AttachmentCount}", 
            request.CategoryId, request.SubcategoryId, request.Priority, attachments?.Count ?? 0);
        
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid ticket create request: {@ModelState}. Request: {@Request}", ModelState, request);
            
            // In Development, return detailed validation errors
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (env == "Development")
            {
                return ValidationProblem(ModelState);
            }
            
            return BadRequest(ModelState);
        }

        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            // DEBUG: Log JWT context
            _logger.LogInformation("DEBUG JWT CONTEXT: UserId={UserId}, Role={Role}, NameIdentifier={NameIdentifier}, RoleClaim={RoleClaim}",
                context.Value.userId,
                context.Value.role,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindFirstValue(ClaimTypes.Role));
            
            // Convert IFormFile to FileAttachmentRequest (Application layer abstraction)
            List<FileAttachmentRequest>? fileAttachments = null;
            if (attachments != null && attachments.Any())
            {
                fileAttachments = new List<FileAttachmentRequest>();
                foreach (var file in attachments)
                {
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    fileAttachments.Add(new FileAttachmentRequest
                    {
                        FileName = file.FileName,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        Content = memoryStream.ToArray(),
                        FileSize = file.Length
                    });
                }
            }
            
            // Create a new ticket as the current client user (with optional attachments)
            var ticket = await _ticketService.CreateTicketAsync(context.Value.userId, request, fileAttachments);
            _logger.LogInformation("Ticket created {@TicketId} by user {@UserId}", ticket?.Id, context.Value.userId);
            return CreatedAtAction(nameof(GetTicket), new { id = ticket!.Id }, ticket);
        }
        catch (TicketValidationException ex)
        {
            // Category/Subcategory validation errors - return 400 with clear error message and code
            // Parse CategoryId and SubcategoryId from Field and Value if they exist
            int? categoryId = null;
            int? subcategoryId = null;
            
            if (int.TryParse(ex.Field, out var catId))
            {
                categoryId = catId;
            }
            if (int.TryParse(ex.Value, out var subId))
            {
                subcategoryId = subId;
            }
            
            _logger.LogWarning(
                "Ticket creation failed - validation error: {Message} (Code={Code}, UserId={UserId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId})",
                ex.Message, ex.Code, context.Value.userId, categoryId, subcategoryId);

            return BadRequest(new
            {
                message = ex.Message,
                error = ex.Code,
                categoryId = categoryId,
                subcategoryId = subcategoryId
            });
        }
        catch (ArgumentException ex)
        {
            // User ID or other argument validation errors
            _logger.LogWarning("Ticket creation failed - argument error: {Message} (UserId={UserId})",
                ex.Message, context.Value.userId);
            
            return BadRequest(new
            {
                message = ex.Message,
                error = "INVALID_ARGUMENT"
            });
        }
        catch (InvalidOperationException ex)
        {
            // Fallback for other InvalidOperationException cases
            _logger.LogWarning(
                "Ticket creation failed - operation error: {Message} (UserId={UserId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId})",
                ex.Message, context.Value.userId, request?.CategoryId, request?.SubcategoryId);

            return BadRequest(new
            {
                message = ex.Message,
                error = "VALIDATION_ERROR",
                categoryId = request?.CategoryId,
                subcategoryId = request?.SubcategoryId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ticket for user {UserId} (CategoryId={CategoryId}, SubcategoryId={SubcategoryId})",
                context.Value.userId, request?.CategoryId, request?.SubcategoryId);
            return StatusCode(500, new
            {
                message = "An unexpected error occurred while creating the ticket",
                error = "INTERNAL_ERROR"
            });
        }
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] TicketUpdateRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        // Validate that request body is not empty (all fields are null)
        if (request.Status == null &&
            request.Priority == null &&
            request.AssignedToUserId == null &&
            request.DueDate == null &&
            string.IsNullOrWhiteSpace(request.Description))
        {
            var problemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["request"] = new[] { "Request body cannot be empty for update operations." }
            })
            {
                Title = "Validation Error",
                Status = 400,
                Instance = HttpContext.Request.Path,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };
            problemDetails.Extensions.Add("traceId", HttpContext.TraceIdentifier);
            return BadRequest(problemDetails);
        }

        var ticket = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, request);
        if (ticket == null)
        {
            return Forbid();
        }
        return Ok(ticket);
    }

    [HttpPut("{id}/assign-technician")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> AssignTechnician(Guid id, [FromBody] AssignTechnicianRequest? request)
    {
        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ticket = await _ticketService.AssignTicketAsync(id, request.TechnicianId);
        if (ticket == null)
        {
            return BadRequest("Ticket not found or technician is inactive");
        }
        return Ok(ticket);
    }

    [HttpPost("{id}/assign")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [Obsolete("Use PUT /api/tickets/{id}/assign-technician instead")]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] Guid? technicianId)
    {
        // Guard clause: technician ID is required
        if (!technicianId.HasValue)
        {
            return BadRequest(new { message = "Technician ID is required." });
        }

        var ticket = await _ticketService.AssignTicketAsync(id, technicianId.Value);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var messages = await _ticketService.GetMessagesAsync(id, context.Value.userId, context.Value.role);
        return Ok(messages);
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] TicketMessageRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        try
        {
            var message = await _ticketService.AddMessageAsync(id, context.Value.userId, request.Message, request.Status);
            if (message == null)
            {
                return NotFound();
            }
            return Ok(message);
        }
        catch (StatusChangeForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message, error = "STATUS_CHANGE_FORBIDDEN" });
        }
    }

    /// <summary>
    /// Get tickets for calendar view (Admin only)
    /// </summary>
    [HttpGet("calendar")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetCalendarTickets([FromQuery] string start, [FromQuery] string end)
    {
        if (!DateTime.TryParse(start, out var startDate) || !DateTime.TryParse(end, out var endDate))
        {
            return BadRequest("Invalid date format. Use YYYY-MM-DD");
        }

        // Ensure end date includes the full day
        endDate = endDate.Date.AddDays(1).AddTicks(-1);

        var tickets = await _ticketService.GetCalendarTicketsAsync(startDate, endDate);
        return Ok(tickets);
    }

    /// <summary>
    /// Assign multiple technicians to a ticket (Admin only)
    /// </summary>
    [HttpPost("{ticketId}/assign-technicians")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> AssignTechnicians(Guid ticketId, [FromBody] AssignTechniciansRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        try
        {
            var result = await _ticketService.AssignTechniciansAsync(ticketId, request.TechnicianIds, request.LeadTechnicianId, context.Value.userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign technicians to ticket {TicketId}", ticketId);
            return Problem(
                title: "Failed to assign technicians",
                detail: "An error occurred while assigning technicians to the ticket",
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Remove a technician from a ticket (Admin only)
    /// </summary>
    [HttpDelete("{ticketId}/technicians/{technicianId}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> RemoveTechnician(Guid ticketId, Guid technicianId)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var removed = await _ticketService.RemoveTechnicianAsync(ticketId, technicianId, context.Value.userId);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Get technicians assigned to a ticket (Admin + assigned Technicians)
    /// </summary>
    [HttpGet("{ticketId}/technicians")]
    [Authorize]
    public async Task<IActionResult> GetTicketTechnicians(Guid ticketId)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var technicians = await _ticketService.GetTicketTechniciansAsync(ticketId, context.Value.userId, context.Value.role);
        return Ok(technicians);
    }

    /// <summary>
    /// Update technician's own state on a ticket (Technician only)
    /// </summary>
    [HttpPut("{ticketId}/technicians/me/state")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> UpdateMyState(Guid ticketId, [FromBody] UpdateTechnicianStateRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        var result = await _ticketService.UpdateTechnicianStateAsync(ticketId, context.Value.userId, request.State);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get ticket activity timeline (Admin + assigned Technicians + ticket owner Client)
    /// </summary>
    [HttpGet("{ticketId}/activities")]
    [Authorize]
    public async Task<IActionResult> GetTicketActivities(Guid ticketId)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var activities = await _ticketService.GetTicketActivitiesAsync(ticketId, context.Value.userId, context.Value.role);
        return Ok(activities);
    }

    /// <summary>
    /// Update technician's work focus/section on a ticket (Technician only)
    /// </summary>
    [HttpPut("{ticketId}/work/me")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> UpdateMyWork(Guid ticketId, [FromBody] UpdateWorkSessionRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        try
        {
            await _ticketService.UpdateWorkSessionAsync(ticketId, context.Value.userId, request);
            return Ok(new { message = "Work session updated successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update work session for ticket {TicketId}", ticketId);
            return Problem(
                title: "Failed to update work session",
                detail: "An error occurred while updating the work session",
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Get collaboration data for a ticket (Admin + assigned Technicians)
    /// </summary>
    [HttpGet("{ticketId}/collaboration")]
    [Authorize]
    public async Task<IActionResult> GetCollaboration(Guid ticketId)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var collaboration = await _ticketService.GetCollaborationDataAsync(ticketId, context.Value.userId, context.Value.role);
        if (collaboration == null)
        {
            return NotFound();
        }

        return Ok(collaboration);
    }

    /// <summary>
    /// Set responsible technician for a ticket (Admin OR Lead/Assigned Technician only)
    /// </summary>
    [HttpPut("{ticketId}/responsible")]
    [Authorize]
    public async Task<IActionResult> SetResponsibleTechnician(Guid ticketId, [FromBody] SetResponsibleTechnicianRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var success = await _ticketService.SetResponsibleTechnicianAsync(
            ticketId, 
            request.ResponsibleTechnicianId, 
            context.Value.userId, 
            context.Value.role);

        if (!success)
        {
            return BadRequest(new { message = "Unable to set responsible technician. Ticket not found, technician not assigned, or insufficient permissions." });
        }

        return Ok(new { success = true, message = "Responsible technician set successfully." });
    }

    /// <summary>
    /// Mark ticket as seen/read (when technician first opens it)
    /// </summary>
    [HttpPost("{id}/seen-read")]
    [Authorize(Roles = nameof(UserRole.Technician) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> MarkAsSeenRead(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.UpdateTicketStatusAsync(id, context.Value.userId, context.Value.role, TicketStatus.SeenRead);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    /// <summary>
    /// Mark ticket as in progress (when technician starts working)
    /// </summary>
    [HttpPost("{id}/start-work")]
    [Authorize(Roles = nameof(UserRole.Technician) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> StartWork(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.UpdateTicketStatusAsync(id, context.Value.userId, context.Value.role, TicketStatus.InProgress);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    /// <summary>
    /// Mark ticket as solved (when technician finishes work)
    /// </summary>
    [HttpPost("{id}/solve")]
    [Authorize(Roles = nameof(UserRole.Technician) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> Solve(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.UpdateTicketStatusAsync(id, context.Value.userId, context.Value.role, TicketStatus.Solved);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    /// <summary>
    /// Mark ticket as redo (when technician needs to redo work)
    /// </summary>
    [HttpPost("{id}/redo")]
    [Authorize(Roles = nameof(UserRole.Technician) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> Redo(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.UpdateTicketStatusAsync(id, context.Value.userId, context.Value.role, TicketStatus.Redo);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    /// <summary>
    /// Handoff ticket from one technician to another (Technician or Admin)
    /// </summary>
    [HttpPost("{id}/handoff")]
    [Authorize(Roles = nameof(UserRole.Technician) + "," + nameof(UserRole.Admin))]
    public async Task<IActionResult> HandoffTicket(Guid id, [FromBody] HandoffTicketRequest? request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        // Guard clause: request body is required
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        try
        {
            // For technicians, they can only handoff from themselves
            var fromTechnicianUserId = context.Value.role == UserRole.Technician 
                ? context.Value.userId 
                : (await _ticketService.GetTicketAsync(id, context.Value.userId, context.Value.role))?.AssignedToUserId ?? context.Value.userId;

            var ticket = await _ticketService.HandoffTicketAsync(
                id, 
                fromTechnicianUserId, 
                request.ToTechnicianId, 
                request.Reason, 
                request.Note,
                context.Value.userId,
                context.Value.role);

            if (ticket == null)
            {
                return NotFound();
            }

            _logger.LogInformation("Ticket {TicketId} handed off from {FromUserId} to {ToTechnicianId} by {ActorUserId}",
                id, fromTechnicianUserId, request.ToTechnicianId, context.Value.userId);

            return Ok(ticket);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized handoff attempt for ticket {TicketId} by user {UserId}", id, context.Value.userId);
            return StatusCode(403, new { message = ex.Message, error = "UNAUTHORIZED" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid handoff request for ticket {TicketId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message, error = "INVALID_ARGUMENT" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handoff ticket {TicketId}", id);
            return StatusCode(500, new { message = "Failed to handoff ticket", error = "INTERNAL_ERROR" });
        }
    }
}

/// <summary>
/// Admin-only endpoints for assignment management
/// </summary>
[ApiController]
[Route("api/admin/assignment")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AssignmentController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ILogger<AssignmentController> _logger;

    public AssignmentController(ITicketService ticketService, ILogger<AssignmentController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    /// <summary>
    /// Get tickets/tasks that need assignment (Admin only)
    /// </summary>
    /// <param name="type">Filter by type: ticket, task, or all (default: all)</param>
    /// <param name="status">Filter by ticket status (optional)</param>
    /// <param name="page">Page number for pagination (optional)</param>
    /// <param name="pageSize">Page size for pagination (optional)</param>
    /// <returns>Assignment queue with tickets and tasks</returns>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(AssignmentQueueResponse), 200)]
    public async Task<IActionResult> GetAssignmentQueue(
        [FromQuery] string? type = null,
        [FromQuery] TicketStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        try
        {
            var queue = await _ticketService.GetAssignmentQueueAsync(type, status, page, pageSize);
            return Ok(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assignment queue");
            return Problem(
                title: "Failed to get assignment queue",
                detail: "An error occurred while retrieving the assignment queue",
                statusCode: 500
            );
        }
    }
}