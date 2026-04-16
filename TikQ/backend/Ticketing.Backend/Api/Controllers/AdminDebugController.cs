// ⚠️ TEMPORARY DEBUG CONTROLLER - REMOVE BEFORE PRODUCTION ⚠️
// This controller exposes internal data for debugging purposes only.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/debug")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminDebugController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminDebugController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AdminDebugController(AppDbContext context, ILogger<AdminDebugController> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// ⚠️ DEBUG ONLY: List all users in the database
    /// Returns { id, email, role } for each user to verify DB consistency.
    /// REMOVE THIS ENDPOINT BEFORE PRODUCTION.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        _logger.LogWarning("DEBUG ENDPOINT CALLED: GET /api/debug/users - This should be removed before production");

        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                Role = u.Role.ToString(),
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount = users.Count,
            databasePath = _context.Database.GetDbConnection().ConnectionString,
            users
        });
    }

    /// <summary>
    /// ⚠️ DEBUG ONLY: List all technicians with their linked User IDs
    /// REMOVE THIS ENDPOINT BEFORE PRODUCTION.
    /// </summary>
    [HttpGet("technicians")]
    public async Task<IActionResult> GetAllTechniciansDebug()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        _logger.LogWarning("DEBUG ENDPOINT CALLED: GET /api/debug/technicians - This should be removed before production");

        var technicians = await _context.Technicians
            .AsNoTracking()
            .OrderBy(t => t.Email)
            .Select(t => new
            {
                TechnicianId = t.Id,
                t.Email,
                t.FullName,
                LinkedUserId = t.UserId,
                t.IsActive
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount = technicians.Count,
            linkedCount = technicians.Count(t => t.LinkedUserId != null),
            unlinkedCount = technicians.Count(t => t.LinkedUserId == null),
            technicians
        });
    }

    /// <summary>
    /// ⚠️ DEBUG ONLY: Test ticket query with different include levels
    /// REMOVE THIS ENDPOINT BEFORE PRODUCTION.
    /// </summary>
    [HttpGet("tickets/test-query")]
    public async Task<IActionResult> TestTicketQuery()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            // Test 1: Simple count
            var simpleCount = await _context.Tickets.CountAsync();
            
            // Test 2: Query with basic includes
            var withBasicIncludes = await _context.Tickets
                .Include(t => t.Category)
                .Include(t => t.CreatedByUser)
                .Take(1)
                .ToListAsync();
            
            // Test 3: Query with AssignedTechnicians (without AssignedByUser)
            var withAssignments1 = await _context.Tickets
                .Include(t => t.AssignedTechnicians)
                    .ThenInclude(ta => ta.TechnicianUser)
                .Take(1)
                .ToListAsync();
            
            // Test 4: Query with AssignedTechnicians (with AssignedByUser) - THIS IS THE PROBLEMATIC ONE
            var withAssignments2 = await _context.Tickets
                .Include(t => t.AssignedTechnicians)
                    .ThenInclude(ta => ta.TechnicianUser)
                .Include(t => t.AssignedTechnicians)
                    .ThenInclude(ta => ta.AssignedByUser)
                .Take(1)
                .ToListAsync();
            
            return Ok(new
            {
                test1_simpleCount = simpleCount,
                test2_basicIncludes = withBasicIncludes.Count,
                test3_assignmentsWithoutAssignedBy = withAssignments1.Count,
                test4_assignmentsWithAssignedBy = withAssignments2.Count,
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test query failed: {ExceptionType}, {Message}", ex.GetType().Name, ex.Message);
            return StatusCode(500, new
            {
                error = ex.GetType().Name,
                message = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace.Length))
            });
        }
    }

    /// <summary>
    /// ⚠️ DEBUG ONLY: Get ticket count in database
    /// Returns total count of tickets to confirm persistence.
    /// Admin only, Development only.
    /// REMOVE THIS ENDPOINT BEFORE PRODUCTION.
    /// </summary>
    [HttpGet("tickets/count")]
    public async Task<IActionResult> GetTicketCount()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var count = await _context.Tickets.CountAsync();
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTicketCount: Error counting tickets. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            return StatusCode(500, new { message = "An error occurred while counting tickets", error = ex.Message });
        }
    }
}

