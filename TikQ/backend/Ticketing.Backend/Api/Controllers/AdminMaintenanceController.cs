using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// ⚠️ SECURITY CLEANUP CONTROLLER — DO NOT REUSE
/// This controller exposes one-off administrative maintenance endpoints.
/// These endpoints are intended for manual, supervised use by Admins only.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminMaintenanceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminMaintenanceController> _logger;

    public AdminMaintenanceController(AppDbContext context, ILogger<AdminMaintenanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// ⚠️ SECURITY CLEANUP — DO NOT REUSE
    /// 
    /// One-time cleanup for legacy users that were incorrectly saved as Client
    /// when they likely intended to be Admin (due to the historical registration bug).
    /// 
    /// Deletion criteria:
    /// - Role == Client
    /// - AND (
    ///     Email contains 'admin' (case-insensitive)
    ///     OR (RoleFixBeforeUtc is provided AND CreatedAt < RoleFixBeforeUtc)
    ///   )
    /// 
    /// SAFETY GUARANTEES:
    /// - Does NOT delete any real Admin users (Role == Admin is excluded)
    /// - Does NOT modify roles or auto-promote anyone
    /// - Every deletion is explicitly logged with email, id, role, and timestamp
    /// - Endpoint requires authenticated Admin
    /// - Intended to be used ONCE and then removed/commented out
    /// </summary>
    public class CleanupInvalidAdminUsersRequest
    {
        /// <summary>
        /// Optional cutoff date for legacy users created before the role fix.
        /// If null, only the email-contains-'admin' heuristic is used.
        /// Recommended to set this to the date when the role fix was deployed.
        /// </summary>
        public DateTime? RoleFixBeforeUtc { get; set; }
    }

    [HttpPost("cleanup/invalid-admin-users")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> CleanupInvalidAdminUsers([FromBody] CleanupInvalidAdminUsersRequest request)
    {
        // Determine cutoff date if provided
        DateTime? cutoffUtc = request.RoleFixBeforeUtc?.ToUniversalTime();

        // Build base query: only Client users
        var query = _context.Users
            .Where(u => u.Role == UserRole.Client);

        // Email heuristic: email contains 'admin'
        query = query.Where(u =>
            EF.Functions.Like(u.Email, "%admin%") ||
            (cutoffUtc != null && u.CreatedAt < cutoffUtc.Value));

        // Use a transaction to make cleanup atomic and race-safe
        await using var tx = await _context.Database.BeginTransactionAsync();

        var candidates = await query.ToListAsync();
        if (candidates.Count == 0)
        {
            return Ok(new
            {
                deletedCount = 0,
                deletedEmails = Array.Empty<string>()
            });
        }

        var deletedEmails = new List<string>();

        foreach (var user in candidates)
        {
            // Extra safety: never delete real Admins (should already be filtered out)
            if (user.Role != UserRole.Client)
            {
                continue;
            }

            // Log each deletion explicitly
            _logger.LogWarning(
                "SECURITY_CLEANUP_INVALID_ADMIN_USER_DELETED Email={Email} Id={Id} Role={Role} TimeUtc={TimeUtc}",
                user.Email,
                user.Id,
                user.Role.ToString(),
                DateTime.UtcNow);

            deletedEmails.Add(user.Email);
            _context.Users.Remove(user);
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            deletedCount = deletedEmails.Count,
            deletedEmails
        });
    }
}





