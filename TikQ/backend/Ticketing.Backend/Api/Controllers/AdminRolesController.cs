using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// Admin-only endpoints for role assignment (Company Directory handoff).
/// Writes only to TikQ DB (Users/Technicians). Company DB remains read-only.
/// </summary>
[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminRolesController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminRolesController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Assign role for an org user by email. Creates minimal TikQ user (no password) if not exists.
    /// POST /api/admin/roles/assign
    /// Body: { "email": "...", "role": 0|1|2, "isSupervisor": true|false (optional, for Technician only) }
    /// </summary>
    [HttpPost("assign")]
    [Consumes("application/json")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required.", error = "VALIDATION" });
        }

        var role = request.Role;
        if (role != UserRole.Client && role != UserRole.Technician && role != UserRole.Admin)
        {
            return BadRequest(new { message = "Role must be Client (0), Technician (1), or Admin (2).", error = "VALIDATION" });
        }

        var result = await _userService.AssignRoleForOrgUserAsync(request.Email.Trim(), role, request.IsSupervisor);
        if (result == null)
        {
            return BadRequest(new { message = "Invalid request.", error = "VALIDATION" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Admin-only: set password for a user by email (pre-provision for server/shadow users). No current password required.
    /// POST /api/admin/roles/set-password
    /// Body: { "email": "...", "newPassword": "..." }
    /// </summary>
    [HttpPost("set-password")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required.", error = "VALIDATION" });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "NewPassword must be at least 8 characters.", error = "VALIDATION" });
        }

        var (success, errorMessage) = await _userService.SetPasswordForUserAsync(request.Email.Trim(), request.NewPassword);
        if (!success)
        {
            if (errorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(new { message = errorMessage, error = "NOT_FOUND" });
            return BadRequest(new { message = errorMessage ?? "Failed to set password.", error = "VALIDATION" });
        }

        return Ok(new { ok = true, message = "Password set successfully." });
    }

    /// <summary>
    /// Get current TikQ role and isSupervisor by email.
    /// GET /api/admin/roles/by-email?email=...
    /// </summary>
    [HttpGet("by-email")]
    public async Task<IActionResult> GetRoleByEmail([FromQuery] string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Query parameter 'email' is required.", error = "VALIDATION" });
        }

        var result = await _userService.GetRoleByEmailAsync(email!);
        if (result == null)
        {
            return NotFound(new { message = "No TikQ user found for this email.", error = "NOT_FOUND" });
        }

        return Ok(result);
    }
}
