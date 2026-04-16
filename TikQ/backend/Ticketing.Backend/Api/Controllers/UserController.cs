using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.Services;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// User profile controller (backward compatibility for /api/user route)
/// </summary>
[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Get current user profile (backward compatibility endpoint)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _userService.GetByIdAsync(userId.Value);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving user profile", error = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Email);

        if (string.IsNullOrEmpty(idValue))
        {
            return null;
        }

        if (Guid.TryParse(idValue, out var userId))
        {
            return userId;
        }
        return null;
    }
}

