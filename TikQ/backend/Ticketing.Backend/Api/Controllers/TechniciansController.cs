using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/technicians")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianService _technicianService;
    private readonly ILogger<TechniciansController> _logger;

    public TechniciansController(ITechnicianService technicianService, ILogger<TechniciansController> logger)
    {
        _technicianService = technicianService;
        _logger = logger;
    }

    /// <summary>
    /// Get all technicians
    /// </summary>
    /// <param name="page">Page number (optional)</param>
    /// <param name="pageSize">Page size (optional)</param>
    /// <param name="search">Search query (optional)</param>
    /// <param name="includeDeleted">Include soft-deleted technicians (optional, default false)</param>
    [HttpGet]
    public async Task<IActionResult> GetAllTechnicians(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        [FromQuery] bool includeDeleted = false)
    {
        try
        {
            var technicians = await _technicianService.GetAllTechniciansAsync(includeDeleted);
            var filtered = technicians;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalized = search.Trim().ToLowerInvariant();
                filtered = technicians.Where(t =>
                    t.FullName.ToLowerInvariant().Contains(normalized) ||
                    t.Email.ToLowerInvariant().Contains(normalized) ||
                    (!string.IsNullOrWhiteSpace(t.Department) && t.Department.ToLowerInvariant().Contains(normalized)) ||
                    (!string.IsNullOrWhiteSpace(t.Phone) && t.Phone.ToLowerInvariant().Contains(normalized)));
            }

            if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
            {
                var totalCount = filtered.Count();
                var items = filtered
                    .Skip((page.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value)
                    .ToList();

                return Ok(new
                {
                    items,
                    totalCount,
                    page = page.Value,
                    pageSize = pageSize.Value
                });
            }

            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all technicians: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = "Failed to retrieve technicians", error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Admin technician directory for assignment picker
    /// </summary>
    [HttpGet("directory")]
    public async Task<IActionResult> GetTechnicianDirectory(
        [FromQuery] string? search,
        [FromQuery] string? availability,
        [FromQuery] int? categoryId,
        [FromQuery] int? subcategoryId)
    {
        try
        {
            var technicians = await _technicianService.GetTechnicianDirectoryAsync(search, availability, categoryId, subcategoryId);
            return Ok(technicians);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get technician directory");
            return StatusCode(500, new { message = "Failed to retrieve technician directory", error = ex.Message });
        }
    }

    /// <summary>
    /// Get technician by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTechnician([FromRoute] Guid id)
    {
        try
        {
            _logger.LogInformation("GetTechnician: Fetching technician with Id={Id}", id);
            
            var technician = await _technicianService.GetTechnicianByIdAsync(id);
            if (technician == null)
            {
                _logger.LogWarning("GetTechnician: Technician with Id={Id} not found", id);
                return NotFound(new { message = "Technician not found", error = "TECHNICIAN_NOT_FOUND" });
            }
            
            _logger.LogInformation("GetTechnician: Successfully retrieved technician {TechnicianId}", technician.Id);
            return Ok(technician);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTechnician: Error fetching technician with Id={Id}. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                id, ex.GetType().Name, ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while retrieving the technician", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new technician
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTechnician([FromBody] TechnicianCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var technician = await _technicianService.CreateTechnicianAsync(request);
            _logger.LogInformation("Technician created: {TechnicianId}", technician.Id);
            return CreatedAtAction(nameof(GetTechnician), new { id = technician.Id }, technician);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new { message = ex.Message, error = "EMAIL_EXISTS" });
        }
        catch (DuplicatePhoneException ex)
        {
            return Conflict(new { message = ex.Message, error = "PHONE_EXISTS" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, error = "VALIDATION_ERROR" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create technician");
            return StatusCode(500, "Failed to create technician");
        }
    }

    /// <summary>
    /// Update technician
    /// </summary>
    /// <param name="id">Technician ID</param>
    /// <param name="request">Technician update request including fullName, email, phone, department, and isActive</param>
    /// <returns>Updated technician</returns>
    /// <response code="200">Technician updated successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="404">Technician not found</response>
    /// <response code="401">Unauthorized</response>
    /// <response code="403">Forbidden - Admin role required</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TechnicianResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> UpdateTechnician(Guid id, [FromBody] TechnicianUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var technician = await _technicianService.UpdateTechnicianAsync(id, request);
        if (technician == null)
        {
            return NotFound();
        }

        return Ok(technician);
    }

    /// <summary>
    /// Update technician status (active/inactive)
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateTechnicianStatus(Guid id, [FromBody] TechnicianStatusUpdateRequest request)
    {
        var success = await _technicianService.UpdateTechnicianStatusAsync(id, request.IsActive);
        if (!success)
        {
            return NotFound();
        }

        return Ok(new { message = "Technician status updated successfully" });
    }

    /// <summary>
    /// Link a Technician record to a User account (required for Smart Assignment)
    /// </summary>
    /// <remarks>
    /// A Technician MUST be linked to a User account (with Role=Technician) to be eligible for ticket assignment.
    /// Without this link, Smart Assignment cannot set Ticket.AssignedToUserId correctly.
    /// </remarks>
    /// <param name="id">Technician ID</param>
    /// <param name="request">User ID to link</param>
    /// <returns>Updated technician with userId populated</returns>
    /// <response code="200">Technician linked successfully</response>
    /// <response code="404">Technician or User not found</response>
    /// <response code="400">User does not have Technician role</response>
    /// <response code="409">Technician is already linked to a User</response>
    [HttpPatch("{id}/link-user")]
    [ProducesResponseType(typeof(TechnicianResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> LinkUser(Guid id, [FromBody] TechnicianLinkUserRequest request)
    {
        var (result, technician) = await _technicianService.LinkUserAsync(id, request.UserId);

        return result switch
        {
            LinkUserResult.Success => Ok(technician),
            LinkUserResult.TechnicianNotFound => NotFound(new { message = "Technician not found", error = "TECHNICIAN_NOT_FOUND" }),
            LinkUserResult.UserNotFound => NotFound(new { message = "User not found", error = "USER_NOT_FOUND" }),
            LinkUserResult.UserNotTechnicianRole => BadRequest(new { message = "User must have Technician role", error = "USER_NOT_TECHNICIAN_ROLE" }),
            LinkUserResult.AlreadyLinked => Conflict(new { message = "Technician is already linked to a User account", error = "ALREADY_LINKED" }),
            _ => StatusCode(500, new { message = "Unexpected error" })
        };
    }

    /// <summary>
    /// Update technician expertise (subcategory permissions)
    /// </summary>
    /// <param name="id">Technician ID</param>
    /// <param name="request">List of subcategory IDs this technician has expertise in</param>
    /// <returns>Updated technician with subcategoryIds populated</returns>
    /// <response code="200">Technician expertise updated successfully</response>
    /// <response code="400">Invalid subcategory IDs</response>
    /// <response code="404">Technician not found</response>
    [HttpPut("{id}/expertise")]
    [ProducesResponseType(typeof(TechnicianResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateExpertise(Guid id, [FromBody] UpdateTechnicianExpertiseRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }
        if (request.SubcategoryIds == null)
        {
            return BadRequest(new { message = "SubcategoryIds are required." });
        }

        try
        {
            var updated = await _technicianService.UpdateTechnicianExpertiseAsync(id, request.SubcategoryIds);
            if (updated == null)
            {
                return NotFound(new { message = "Technician not found", error = "TECHNICIAN_NOT_FOUND" });
            }

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Soft delete a technician (Admin only)
    /// </summary>
    /// <remarks>
    /// This performs a soft delete:
    /// - Sets IsDeleted=true, DeletedAt=UtcNow
    /// - Sets IsActive=false
    /// - Locks out the linked user account (prevents login)
    /// 
    /// Soft-deleted technicians:
    /// - Will not appear in technician lists or assignment pickers
    /// - Will not be auto-assigned to new tickets
    /// - Historical ticket data remains intact for audit purposes
    /// </remarks>
    /// <param name="id">Technician ID to delete</param>
    /// <returns>Confirmation with deleted technician info</returns>
    /// <response code="200">Technician soft deleted successfully</response>
    /// <response code="404">Technician not found</response>
    /// <response code="500">Failed to delete technician</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteTechnician(Guid id)
    {
        // Get current admin user ID from claims
        var adminUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminUserIdStr, out var adminUserId))
        {
            return Unauthorized(new { message = "Invalid user ID in token", error = "INVALID_USER_ID" });
        }

        try
        {
            var (result, technician) = await _technicianService.SoftDeleteTechnicianAsync(id, adminUserId);

            return result switch
            {
                SoftDeleteResult.Success => Ok(new 
                { 
                    message = "Technician deleted successfully",
                    technicianId = id,
                    isDeleted = true,
                    technician
                }),
                SoftDeleteResult.AlreadyDeleted => Ok(new 
                { 
                    message = "Technician was already deleted",
                    technicianId = id,
                    isDeleted = true,
                    technician
                }),
                SoftDeleteResult.TechnicianNotFound => NotFound(new 
                { 
                    message = "Technician not found", 
                    error = "TECHNICIAN_NOT_FOUND" 
                }),
                SoftDeleteResult.Failed => StatusCode(500, new 
                { 
                    message = "Failed to delete technician", 
                    error = "DELETE_FAILED" 
                }),
                _ => StatusCode(500, new { message = "Unexpected error" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteTechnician: Error deleting technician {TechnicianId}", id);
            return StatusCode(500, new { message = "Failed to delete technician", error = ex.Message });
        }
    }
}

