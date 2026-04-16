using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// Admin-only endpoints for automation coverage visualization.
/// </summary>
[ApiController]
[Route("api/admin/automation")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminAutomationController : ControllerBase
{
    private readonly IAutomationCoverageService _coverageService;
    private readonly ILogger<AdminAutomationController> _logger;

    public AdminAutomationController(
        IAutomationCoverageService coverageService,
        ILogger<AdminAutomationController> logger)
    {
        _coverageService = coverageService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/automation/coverage/summary
    /// Returns high-level KPIs: category counts, coverage %, ticket stats.
    /// </summary>
    [HttpGet("coverage/summary")]
    public async Task<IActionResult> GetCoverageSummary()
    {
        try
        {
            var summary = await _coverageService.GetCoverageSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coverage summary");
            return StatusCode(500, new { message = "Failed to get coverage summary." });
        }
    }

    /// <summary>
    /// GET /api/admin/automation/coverage/breakdown
    /// Returns detailed lists: uncovered pairs, covered pairs, technician coverage.
    /// </summary>
    [HttpGet("coverage/breakdown")]
    public async Task<IActionResult> GetCoverageBreakdown()
    {
        try
        {
            var breakdown = await _coverageService.GetCoverageBreakdownAsync();
            return Ok(breakdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coverage breakdown");
            return StatusCode(500, new { message = "Failed to get coverage breakdown." });
        }
    }

    /// <summary>
    /// GET /api/admin/automation/coverage/graph
    /// Returns node/edge structure for visualization.
    /// Nodes: categories, subcategories, technicians
    /// Edges: category->subcategory (contains), subcategory->technician (covers)
    /// </summary>
    [HttpGet("coverage/graph")]
    public async Task<IActionResult> GetCoverageGraph()
    {
        try
        {
            var graph = await _coverageService.GetCoverageGraphAsync();
            return Ok(graph);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coverage graph");
            return StatusCode(500, new { message = "Failed to get coverage graph." });
        }
    }
}














