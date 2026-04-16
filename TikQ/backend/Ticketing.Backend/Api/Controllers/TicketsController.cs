using System.Security.Claims;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Api.Extensions;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<TicketsController> _logger;

    public TicketsController(ITicketService ticketService, IFieldDefinitionService fieldDefinitionService, ILogger<TicketsController> logger)
    {
        _ticketService = ticketService;
        _fieldDefinitionService = fieldDefinitionService;
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

    [HttpGet("test-no-service")]
    [Authorize]
    public IActionResult GetTicketsTestNoService()
    {
        // Test endpoint that doesn't use any service dependencies
        return Ok(new { message = "Test without service works", timestamp = DateTime.UtcNow });
    }

    [HttpGet("test-with-service")]
    [Authorize]
    public IActionResult GetTicketsTestWithService()
    {
        // Test endpoint that accesses service but doesn't call methods
        try
        {
            var serviceType = _ticketService?.GetType().Name ?? "null";
            return Ok(new { 
                message = "Test with service access works",
                serviceType = serviceType,
                serviceNotNull = _ticketService != null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.GetType().Name, message = ex.Message });
        }
    }

    [HttpGet("test-simple")]
    [Authorize]
    public IActionResult GetTicketsTestSimple()
    {
        try
        {
            _logger?.LogInformation("GetTicketsTestSimple: Called - Logger works");
            
            // Test if service is accessible
            var serviceType = _ticketService?.GetType().Name ?? "null";
            _logger?.LogInformation("GetTicketsTestSimple: Service type = {ServiceType}", serviceType);
            
            return Ok(new { 
                message = "Simple test works",
                serviceType = serviceType,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GetTicketsTestSimple failed: {Type} - {Message}", ex.GetType().Name, ex.Message);
            
            // Try to return error without using logger (in case logger is the issue)
            try
            {
                return StatusCode(500, new { 
                    error = ex.GetType().Name, 
                    message = ex.Message,
                    stackTrace = ex.StackTrace != null ? ex.StackTrace.Substring(0, Math.Min(200, ex.StackTrace.Length)) : null
                });
            }
            catch
            {
                // Ultimate fallback
                Response.ContentType = "text/plain";
                return StatusCode(500, $"Error: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }

    [HttpGet("field-definitions")]
    public async Task<IActionResult> GetFieldDefinitions([FromQuery] int categoryId, [FromQuery] int? subcategoryId)
    {
        if (categoryId <= 0)
        {
            return BadRequest(new { message = "categoryId is required." });
        }

        try
        {
            var fields = await _fieldDefinitionService.GetMergedFieldDefinitionsAsync(categoryId, subcategoryId);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load field definitions for CategoryId={CategoryId}, SubcategoryId={SubcategoryId}", categoryId, subcategoryId);
            return StatusCode(500, new { message = "Failed to load field definitions." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, [FromQuery] Guid? assignedTo, [FromQuery] Guid? createdBy, [FromQuery] string? search, [FromQuery] bool? unseen)
    {
        _logger.LogInformation("GetTickets: Method called");
        
        var context = GetUserContext();
        if (context == null)
        {
            _logger.LogWarning("GetTickets: GetUserContext returned null");
            return Unauthorized();
        }
        
        _logger.LogInformation("GetTickets: Context obtained - UserId={UserId}, Role={Role}", context.Value.userId, context.Value.role);

        try
        {
            _logger.LogInformation("GetTickets: UserId={UserId}, Role={Role}, Status={Status}, Priority={Priority}",
                context.Value.userId, context.Value.role, status, priority);
            
            // Service method applies role-based filtering (clients see their own tickets, technicians see assignments)
            _logger.LogInformation("GetTickets: Calling GetTicketsAsync...");
            
            IEnumerable<TicketListItemResponse> tickets;
            try
            {
                tickets = await _ticketService.GetTicketsAsync(context.Value.userId, context.Value.role, status, priority, assignedTo, createdBy, search, unseen);
                var ticketCount = tickets != null ? tickets.Count() : 0;
            _logger.LogInformation("GetTickets: GetTicketsAsync completed, got {Count} tickets", ticketCount);
            }
            catch (Exception serviceEx)
            {
                _logger.LogError(serviceEx, "GetTickets: GetTicketsAsync threw exception: {Type} - {Message}", 
                    serviceEx.GetType().Name, serviceEx.Message);
                throw;
            }
            
            // Materialize to catch serialization issues early
            var ticketList = tickets?.ToList() ?? new List<TicketListItemResponse>();
            
            _logger.LogInformation("GetTickets: Materialized {Count} tickets", ticketList.Count);
            
            // Try returning just the count first to test if basic response works
            if (ticketList.Count == 0)
            {
                _logger.LogInformation("GetTickets: No tickets, returning empty list");
                return Ok(new List<TicketListItemResponse>());
            }
            
            _logger.LogInformation("GetTickets: Attempting to serialize {Count} tickets", ticketList.Count);
            
            // Try returning a simplified version first to test serialization
            try
            {
                // Test 1: Return just count
                _logger.LogInformation("GetTickets: Test 1 - Returning count only");
                var testResponse1 = Ok(new { count = ticketList.Count });
                _logger.LogInformation("GetTickets: Test 1 response created");
                
                // Test 2: Return first ticket only (simplified)
                if (ticketList.Count > 0)
                {
                    _logger.LogInformation("GetTickets: Test 2 - Returning first ticket only");
                    var firstTicket = ticketList[0];
                    var simplifiedTicket = new
                    {
                        id = firstTicket.Id,
                        title = firstTicket.Title,
                        status = firstTicket.DisplayStatus.ToString(),
                        priority = firstTicket.Priority.ToString()
                    };
                    var testResponse2 = Ok(new { tickets = new[] { simplifiedTicket } });
                    _logger.LogInformation("GetTickets: Test 2 response created");
                }
                
                // Test 3: Return full list
                _logger.LogInformation("GetTickets: Test 3 - Returning full ticket list");
                return Ok(ticketList);
            }
            catch (Exception serializationEx)
            {
                _logger.LogError(serializationEx, "GetTickets: Serialization failed: {Type} - {Message}, StackTrace: {StackTrace}", 
                    serializationEx.GetType().Name, serializationEx.Message, serializationEx.StackTrace);
                
                // Last resort: return plain text
                Response.ContentType = "text/plain";
                return StatusCode(500, $"Serialization error: {serializationEx.GetType().Name} - {serializationEx.Message}");
            }
        }
        catch (Exception ex)
        {
            // Production-safe guard: missing RBAC table must not cause HTTP 500
            var sqlEx = ex as SqliteException ?? ex.InnerException as SqliteException;
            if (sqlEx != null &&
                sqlEx.Message?.Contains("TechnicianSubcategoryPermissions", StringComparison.OrdinalIgnoreCase) == true &&
                (sqlEx.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase) || sqlEx.SqliteErrorCode == 1))
            {
                _logger.LogError(sqlEx, "GetTickets: RBAC permissions table missing. Run database migrations. UserId={UserId}, Role={Role}",
                    context.Value.userId, context.Value.role);
                return StatusCode(503, new ProblemDetails
                {
                    Status = 503,
                    Title = "Service Unavailable",
                    Detail = "RBAC permissions table missing. Run database migrations.",
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogError(ex, "GetTickets FAILED: UserId={UserId}, Role={Role}, ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}, InnerException={InnerException}",
                context.Value.userId, context.Value.role, ex.GetType().Name, ex.Message, ex.StackTrace, ex.InnerException?.Message);
            
            // Try multiple approaches to return error
            try
            {
                // Approach 1: Simple dictionary
                var simpleError = new Dictionary<string, string>
                {
                    ["error"] = "INTERNAL_ERROR",
                    ["message"] = ex.Message ?? "An error occurred",
                    ["type"] = ex.GetType().Name
                };
                
                if (ex.InnerException != null)
                {
                    simpleError["innerException"] = ex.InnerException.Message ?? "None";
                }
                
                _logger.LogInformation("GetTickets: Attempting to return StatusCode(500, simpleError)");
                var result = StatusCode(500, simpleError);
                _logger.LogInformation("GetTickets: StatusCode result created");
                return result;
            }
            catch (Exception serializationEx1)
            {
                _logger.LogError(serializationEx1, "GetTickets: Approach 1 failed: {Type} - {Message}", serializationEx1.GetType().Name, serializationEx1.Message);
                
                try
                {
                    // Approach 2: Plain object
                    _logger.LogInformation("GetTickets: Attempting Approach 2 - plain object");
                    Response.ContentType = "application/json";
                    return StatusCode(500, new { error = "INTERNAL_ERROR", message = ex.Message });
                }
                catch (Exception serializationEx2)
                {
                    _logger.LogError(serializationEx2, "GetTickets: Approach 2 failed: {Type} - {Message}", serializationEx2.GetType().Name, serializationEx2.Message);
                    
                    try
                    {
                        // Approach 3: Plain text
                        _logger.LogInformation("GetTickets: Attempting Approach 3 - plain text");
                        Response.ContentType = "text/plain";
                        return StatusCode(500, $"Error: {ex.GetType().Name} - {ex.Message}");
                    }
                    catch (Exception serializationEx3)
                    {
                        _logger.LogError(serializationEx3, "GetTickets: All approaches failed. Last error: {Type} - {Message}", 
                            serializationEx3.GetType().Name, serializationEx3.Message);
                        
                        // Ultimate fallback - rethrow to let middleware handle it
                        throw;
                    }
                }
            }
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

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] TicketUpdateRequest request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            var updated = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, request);
            if (updated == null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (StatusChangeForbiddenException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateTicket failed for {TicketId}", id);
            return StatusCode(500, new { message = "Failed to update ticket", error = ex.Message });
        }
    }

    [HttpPost("{id}/seen")]
    public async Task<IActionResult> MarkTicketSeen(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        var marked = await _ticketService.MarkTicketSeenAsync(id, context.Value.userId, context.Value.role);
        if (!marked)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Client))]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateTicket([FromForm] string? ticketData, [FromForm] List<IFormFile>? attachments)
    {
        var traceId = HttpContext.TraceIdentifier;
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        _logger.LogInformation("CreateTicket: ContentType={ContentType}, HasFormContentType={HasFormContentType}, ticketData={HasTicketData}",
            Request.ContentType, Request.HasFormContentType, !string.IsNullOrWhiteSpace(ticketData));

        TicketCreateRequest? request = null;
        try
        {
            // Configure JsonSerializerOptions with enum converter for both form-data and JSON body parsing
            var jsonOptions = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            if (Request.HasFormContentType)
            {
                // Request is multipart/form-data (with attachments)
                if (!string.IsNullOrWhiteSpace(ticketData))
                {
                    _logger.LogInformation("CreateTicket: Parsing from FormData ticketData field");
                    request = JsonSerializer.Deserialize<TicketCreateRequest>(ticketData, jsonOptions);
                }
            }
            else
            {
                // Request is JSON body (no attachments)
                // Enable buffering so we can read the body even if model binding attempted to read it
                Request.EnableBuffering();
                Request.Body.Position = 0;
                
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                _logger.LogInformation("CreateTicket: Read JSON body, length={BodyLength}, preview={Preview}",
                    body?.Length ?? 0, body?.Substring(0, Math.Min(100, body?.Length ?? 0)));
                    
                if (!string.IsNullOrWhiteSpace(body))
                {
                    request = JsonSerializer.Deserialize<TicketCreateRequest>(body, jsonOptions);
                    _logger.LogInformation("CreateTicket: Parsed JSON successfully - CategoryId={CategoryId}, SubcategoryId={SubcategoryId}, Title={Title}",
                        request?.CategoryId, request?.SubcategoryId, request?.Title);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse ticket create request body: {Message}. TraceId={TraceId}", ex.Message, traceId);
            return BadRequest(new { message = "Invalid ticket payload", error = "INVALID_PAYLOAD", details = ex.Message, traceId });
        }

        if (request == null)
        {
            _logger.LogWarning("CreateTicket: request is null after parsing. ContentType={ContentType}, HasFormContentType={HasFormContentType}",
                Request.ContentType, Request.HasFormContentType);
            return BadRequest(new { message = "Ticket payload is required", error = "MISSING_PAYLOAD", traceId });
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid ticket create request: {@ModelState}", ModelState);
            var modelErrors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            return BadRequest(new { message = "Validation failed", error = "VALIDATION_ERROR", fieldErrors = modelErrors, traceId });
        }

        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            fieldErrors["title"] = new[] { "Title is required." };
        }
        if (request.CategoryId <= 0)
        {
            fieldErrors["categoryId"] = new[] { "CategoryId is required." };
        }
        if (!request.SubcategoryId.HasValue || request.SubcategoryId <= 0)
        {
            fieldErrors["subcategoryId"] = new[] { "SubcategoryId is required." };
        }
        if (!Enum.IsDefined(typeof(TicketPriority), request.Priority))
        {
            fieldErrors["priority"] = new[] { "Priority is invalid." };
        }

        if (fieldErrors.Any())
        {
            return BadRequest(new { message = "Validation failed", error = "VALIDATION_ERROR", fieldErrors, traceId });
        }

        try
        {
            // Create a new ticket as the current client user
            _logger.LogInformation("POST /api/tickets: REQUEST RECEIVED - UserId={UserId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}, Title={Title}, Priority={Priority}",
                context.Value.userId, request.CategoryId, request.SubcategoryId, request.Title, request.Priority);
            
            if (System.Diagnostics.Debugger.IsAttached || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                _logger.LogInformation("POST /api/tickets: Full request payload - {@Request}", request);
            }
            
            // Convert attachments if provided (optional)
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

            var ticket = await _ticketService.CreateTicketAsync(context.Value.userId, request, fileAttachments);
            
            if (ticket == null)
            {
                _logger.LogError("CreateTicketAsync returned null for UserId={UserId}", context.Value.userId);
                return StatusCode(500, new { message = "Ticket creation returned null", error = "INTERNAL_ERROR" });
            }
            
            _logger.LogInformation("POST /api/tickets: Ticket created successfully. TicketId={TicketId}, UserId={UserId}, Status={Status}",
                ticket.Id, context.Value.userId, ticket.CanonicalStatus);
            
            return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, ticket);
        }
        catch (InvalidOperationException ex)
        {
            // Validation errors from service
            _logger.LogWarning(ex, "Ticket creation validation failed for UserId={UserId}: {Message}. TraceId={TraceId}", context.Value.userId, ex.Message, traceId);
            return BadRequest(new { message = ex.Message, error = "VALIDATION_ERROR", traceId });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Ticket creation failed due to missing entity for UserId={UserId}. TraceId={TraceId}", context.Value.userId, traceId);
            return NotFound(new { message = ex.Message, error = "NOT_FOUND", traceId });
        }
        catch (DbUpdateException ex)
        {
            var sqliteError = ex.InnerException as SqliteException;
            var isConstraintViolation = sqliteError?.SqliteErrorCode == 19 || ex.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase);
            _logger.LogError(ex, "Ticket creation failed due to database update error for UserId={UserId}. TraceId={TraceId} ConstraintViolation={ConstraintViolation}",
                context.Value.userId, traceId, isConstraintViolation);

            if (isConstraintViolation)
            {
                return BadRequest(new
                {
                    message = "Ticket request violates a database constraint. Verify required fields and references.",
                    error = "CONSTRAINT_VIOLATION",
                    traceId
                });
            }

            return StatusCode(500, new
            {
                message = "An error occurred while creating the ticket",
                error = "INTERNAL_ERROR",
                traceId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ticket for user {UserId}. TraceId={TraceId} ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
                context.Value.userId, traceId, ex.GetType().Name, ex.Message, ex.StackTrace);
            
            return StatusCode(500, new { 
                message = "An error occurred while creating the ticket",
                error = "INTERNAL_ERROR",
                traceId
            });
        }
    }

    [HttpPut("{id}/assign-technician")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> AssignTechnician(Guid id, [FromBody] AssignTechnicianRequest request)
    {
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

    /// <summary>
    /// Assign multiple technicians to a ticket (Admin or Supervisor Technician)
    /// </summary>
    [HttpPost("{id}/assign-technicians")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Technician)}")]
    public async Task<IActionResult> AssignTechnicians(Guid id, [FromBody] AssignTechniciansRequest request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var ticket = await _ticketService.AssignTechniciansAsync(
                id, 
                request.TechnicianUserIds, 
                context.Value.userId, 
                request.LeadTechnicianUserId);
            if (ticket == null)
            {
                return NotFound("Ticket not found");
            }
            return Ok(ticket);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign technicians to ticket {TicketId}", id);
            return StatusCode(500, "Failed to assign technicians");
        }
    }

    /// <summary>
    /// Handoff ticket from one technician to another (Technician or Admin)
    /// </summary>
    [HttpPost("{id}/handoff")]
    [Authorize(Roles = $"{nameof(UserRole.Technician)},{nameof(UserRole.Admin)}")]
    public async Task<IActionResult> HandoffTicket(Guid id, [FromBody] HandoffTicketRequest request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var ticket = await _ticketService.HandoffTicketAsync(
                id,
                context.Value.userId,
                request.ToTechnicianUserId,
                request.DeactivateCurrent,
                context.Value.role);
            if (ticket == null)
            {
                return NotFound("Ticket not found or you are not assigned to this ticket");
            }
            return Ok(ticket);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handoff ticket {TicketId}", id);
            return StatusCode(500, "Failed to handoff ticket");
        }
    }

    [HttpPost("{id}/assign")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [Obsolete("Use PUT /api/tickets/{id}/assign-technician instead")]
    public async Task<IActionResult> AssignTicket(Guid id, [FromBody] Guid technicianId)
    {
        var ticket = await _ticketService.AssignTicketAsync(id, technicianId);
        if (ticket == null)
        {
            return NotFound();
        }
        return Ok(ticket);
    }

    [HttpPost("{id}/claim")]
    [Authorize(Roles = nameof(UserRole.Technician))]
    public async Task<IActionResult> ClaimTicket(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            var ticket = await _ticketService.ClaimTicketAsync(id, context.Value.userId);
            if (ticket == null)
            {
                return NotFound(new { message = "Ticket not found" });
            }
            return Ok(ticket);
        }
        catch (InvalidOperationException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaimTicket failed for {TicketId}", id);
            return StatusCode(500, new { message = "Failed to claim ticket", error = ex.Message });
        }
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            var messages = await _ticketService.GetMessagesAsync(id, context.Value.userId, context.Value.role);
#if DEBUG
            _logger.LogInformation("GET messages: ticketId={TicketId} (read-only, no SaveChanges)", id);
#endif
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ticket messages. TicketId={TicketId}, UserId={UserId}, Role={Role}",
                id, context.Value.userId, context.Value.role);

            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            return Problem(
                title: "Failed to load ticket messages.",
                detail: env.IsDevelopment() ? ex.Message : "An internal server error occurred.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Mark ticket messages as seen for the current user (writes to DB). Use this instead of side effects in GET messages.
    /// POST /api/tickets/{id}/messages/seen (optional body: { "lastSeenMessageId": "guid" }).
    /// </summary>
    [HttpPost("{id}/messages/seen")]
    public async Task<IActionResult> MarkMessagesSeen(Guid id, [FromBody] MarkMessagesSeenRequest? request = null)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        try
        {
            var ok = await _ticketService.MarkTicketSeenAsync(id, context.Value.userId, context.Value.role);
            return ok ? Ok() : NotFound(new { message = "Ticket not found or access denied." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark messages seen. TicketId={TicketId}, UserId={UserId}", id, context.Value.userId);
            return StatusCode(500, new { message = "Failed to update read state.", error = ex.Message });
        }
    }

    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] TicketMessageRequest request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message is required." });
        }

        try
        {
            var message = await _ticketService.AddMessageAsync(id, context.Value.userId, request.Message, request.Status);
            return CreatedAtAction(nameof(GetMessages), new { id }, message);
        }
        catch (StatusChangeForbiddenException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to add ticket message. TicketId={TicketId}, UserId={UserId}, Role={Role}, Status={Status}, MessageLength={MessageLength}",
                id, context.Value.userId, context.Value.role, request.Status, request.Message?.Length ?? 0);

            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            return Problem(
                title: "Failed to add ticket message.",
                detail: env.IsDevelopment() ? ex.Message : "An internal server error occurred.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Grant collaborator access to a technician on a ticket (Admin or TechSupervisor only).
    /// POST /api/tickets/{ticketId}/access/grant body: { technicianUserId: "GUID" }
    /// </summary>
    [HttpPost("{id}/access/grant")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Technician)}")]
    public async Task<IActionResult> GrantAccess(Guid id, [FromBody] TicketAccessGrantRequest request)
    {
        var context = GetUserContext();
        if (context == null) return Unauthorized();
        if (request == null || request.TechnicianUserId == Guid.Empty)
            return BadRequest(new { message = "technicianUserId is required." });
        try
        {
            var updated = await _ticketService.UpdateCollaboratorAsync(id, context.Value.userId, context.Value.role, request.TechnicianUserId, "grant");
            if (updated == null) return NotFound(new { message = "Ticket not found" });
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GrantAccess failed for TicketId={TicketId}", id);
            return StatusCode(500, new { message = "Failed to grant access", error = ex.Message });
        }
    }

    /// <summary>
    /// Revoke collaborator access from a technician on a ticket (Admin or TechSupervisor only).
    /// POST /api/tickets/{ticketId}/access/revoke body: { technicianUserId: "GUID" }
    /// </summary>
    [HttpPost("{id}/access/revoke")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Technician)}")]
    public async Task<IActionResult> RevokeAccess(Guid id, [FromBody] TicketAccessGrantRequest request)
    {
        var context = GetUserContext();
        if (context == null) return Unauthorized();
        if (request == null || request.TechnicianUserId == Guid.Empty)
            return BadRequest(new { message = "technicianUserId is required." });
        try
        {
            var updated = await _ticketService.UpdateCollaboratorAsync(id, context.Value.userId, context.Value.role, request.TechnicianUserId, "revoke");
            if (updated == null) return NotFound(new { message = "Ticket not found" });
            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RevokeAccess failed for TicketId={TicketId}", id);
            return StatusCode(500, new { message = "Failed to revoke access", error = ex.Message });
        }
    }

    /// <summary>
    /// Grant/revoke collaborator access for a technician on a ticket (Admin or Supervisor Technician)
    /// </summary>
    [HttpPost("{id}/collaborators")]
    [Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Technician)}")]
    public async Task<IActionResult> UpdateCollaborator(Guid id, [FromBody] TicketCollaboratorRequest request)
    {
        var context = GetUserContext();
        if (context == null)
        {
            return Unauthorized();
        }

        if (request == null || request.TechnicianUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new { message = "Invalid request. technicianUserId and action are required." });
        }

        try
        {
            var updated = await _ticketService.UpdateCollaboratorAsync(
                id,
                context.Value.userId,
                context.Value.role,
                request.TechnicianUserId,
                request.Action);

            if (updated == null)
            {
                return NotFound(new { message = "Ticket not found" });
            }

            return Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ForbiddenProblem(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateCollaborator failed for {TicketId}", id);
            return StatusCode(500, new { message = "Failed to update collaborator", error = ex.Message });
        }
    }

    /// <summary>
    /// Get tickets for calendar view (Admin only). Filter by UpdatedAt (آخرین بروزرسانی). Day boundaries in Asia/Tehran.
    /// </summary>
    [HttpGet("calendar")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetCalendarTickets([FromQuery] string start, [FromQuery] string end, [FromQuery] TicketStatus? status)
    {
        if (!DateTime.TryParse(start, out var startDate) || !DateTime.TryParse(end, out var endDate))
        {
            return BadRequest("Invalid date format. Use YYYY-MM-DD");
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var localStart = DateTime.SpecifyKind(new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0), DateTimeKind.Unspecified);
        var localEndNext = DateTime.SpecifyKind(new DateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0), DateTimeKind.Unspecified).AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var endUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(localEndNext, tz);

        var tickets = await _ticketService.GetCalendarTicketsAsync(startUtc, endUtcExclusive, status);
        return Ok(tickets);
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

        try
        {
            // Use UpdateTicketAsync with status change
            var updateRequest = new TicketUpdateRequest
            {
                Status = TicketStatus.Solved
            };
            var ticket = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, updateRequest);
            if (ticket == null)
            {
                _logger.LogWarning("Solve: UpdateTicketAsync returned null for TicketId={TicketId}, UserId={UserId}, Role={Role}", 
                    id, context.Value.userId, context.Value.role);
                return NotFound(new { message = "Ticket not found or you do not have permission to update this ticket" });
            }
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solve: Error updating ticket {TicketId} to Solved status. UserId={UserId}, Role={Role}", 
                id, context.Value.userId, context.Value.role);
            return StatusCode(500, new { message = "An error occurred while updating the ticket status", error = ex.Message });
        }
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

        try
        {
            var updateRequest = new TicketUpdateRequest
            {
                Status = TicketStatus.SeenRead
            };
            var ticket = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, updateRequest);
            if (ticket == null)
            {
                _logger.LogWarning("MarkAsSeenRead: UpdateTicketAsync returned null for TicketId={TicketId}, UserId={UserId}, Role={Role}", 
                    id, context.Value.userId, context.Value.role);
                return NotFound(new { message = "Ticket not found or you do not have permission to update this ticket" });
            }
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAsSeenRead: Error updating ticket {TicketId} to SeenRead status. UserId={UserId}, Role={Role}", 
                id, context.Value.userId, context.Value.role);
            return StatusCode(500, new { message = "An error occurred while updating the ticket status", error = ex.Message });
        }
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

        try
        {
            var updateRequest = new TicketUpdateRequest
            {
                Status = TicketStatus.InProgress
            };
            var ticket = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, updateRequest);
            if (ticket == null)
            {
                _logger.LogWarning("StartWork: UpdateTicketAsync returned null for TicketId={TicketId}, UserId={UserId}, Role={Role}", 
                    id, context.Value.userId, context.Value.role);
                return NotFound(new { message = "Ticket not found or you do not have permission to update this ticket" });
            }
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartWork: Error updating ticket {TicketId} to InProgress status. UserId={UserId}, Role={Role}", 
                id, context.Value.userId, context.Value.role);
            return StatusCode(500, new { message = "An error occurred while updating the ticket status", error = ex.Message });
        }
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

        try
        {
            var updateRequest = new TicketUpdateRequest
            {
                Status = TicketStatus.Redo
            };
            var ticket = await _ticketService.UpdateTicketAsync(id, context.Value.userId, context.Value.role, updateRequest);
            if (ticket == null)
            {
                _logger.LogWarning("Redo: UpdateTicketAsync returned null for TicketId={TicketId}, UserId={UserId}, Role={Role}", 
                    id, context.Value.userId, context.Value.role);
                return NotFound(new { message = "Ticket not found or you do not have permission to update this ticket" });
            }
            return Ok(ticket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redo: Error updating ticket {TicketId} to Redo status. UserId={UserId}, Role={Role}", 
                id, context.Value.userId, context.Value.role);
            return StatusCode(500, new { message = "An error occurred while updating the ticket status", error = ex.Message });
        }
    }
}