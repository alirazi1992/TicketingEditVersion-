using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.Common;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// Admin-only endpoints for generating downloadable reports.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<AdminReportsController> _logger;
    private readonly IWebHostEnvironment _env;

    public AdminReportsController(IReportService reportService, ILogger<AdminReportsController> logger, IWebHostEnvironment env)
    {
        _reportService = reportService;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// GET /api/admin/reports/basic?range=1m&format=csv| xlsx
    /// GET /api/admin/reports/basic?from=2024-01-01&to=2024-01-31&format=xlsx
    /// 
    /// Returns a file: CSV or Excel (.xlsx) with ticket list (ID, Title, Status, Category, Subcategory, Client, Technicians, CreatedAt, LatestUpdateAt).
    /// </summary>
    [HttpGet("basic")]
    public async Task<IActionResult> GetBasicReport(
        [FromQuery] string? range,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string format = "xlsx")
    {
        try
        {
            var (startDate, endDate) = ReportsDateRange.ParseForBasicAnalytic(range, from, to);
            if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                _logger.LogInformation("[Dev] Parsed report range: from={From} to={To} => {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}", from, to, startDate, endDate);
            _logger.LogInformation("Generating basic report: {Start} to {End}, format={Format}", startDate, endDate, format);

            var fmt = format.ToLowerInvariant();
            if (fmt == "xlsx")
            {
                var excelBytes = await _reportService.GenerateBasicReportExcelAsync(startDate, endDate);
                var fileName = $"basic_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            if (fmt == "csv")
            {
                var csvBytes = await _reportService.GenerateBasicReportCsvAsync(startDate, endDate);
                var fileName = $"basic_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
                return File(csvBytes, "text/csv; charset=utf-8", fileName);
            }
            return BadRequest(new { message = "Format must be csv or xlsx for basic reports." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate basic report");
            return StatusCode(500, new { message = "Failed to generate report." });
        }
    }

    /// <summary>
    /// GET /api/admin/reports/analytic?range=1m&format=zip| xlsx
    /// GET /api/admin/reports/analytic?from=2024-01-01&to=2024-01-31&format=xlsx
    /// 
    /// Returns a file: ZIP (multiple CSVs) or Excel (.xlsx with four sheets: تیکت‌ها, بر اساس دسته, بر اساس مشتری, تغییر وضعیت).
    /// </summary>
    [HttpGet("analytic")]
    public async Task<IActionResult> GetAnalyticReport(
        [FromQuery] string? range,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string format = "xlsx")
    {
        try
        {
            var (startDate, endDate) = ReportsDateRange.ParseForBasicAnalytic(range, from, to);
            if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                _logger.LogInformation("[Dev] Parsed report range: from={From} to={To} => {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}", from, to, startDate, endDate);
            _logger.LogInformation("Generating analytic report: {Start} to {End}, format={Format}", startDate, endDate, format);

            var fmt = format.ToLowerInvariant();
            if (fmt == "xlsx")
            {
                var excelBytes = await _reportService.GenerateAnalyticReportExcelAsync(startDate, endDate);
                var fileName = $"analytic_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            if (fmt == "zip")
            {
                var zipBytes = await _reportService.GenerateAnalyticReportZipAsync(startDate, endDate);
                var fileName = $"analytic_report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.zip";
                return File(zipBytes, "application/zip", fileName);
            }
            return BadRequest(new { message = "Format must be zip or xlsx for analytic reports." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate analytic report");
            return StatusCode(500, new { message = "Failed to generate report." });
        }
    }

    // GET /api/admin/reports/technician-work is handled by minimal API in Program.cs to avoid 404 routing issues.

    /// <summary>
    /// GET /api/admin/reports/technician-work/{userId}/activities?from=YYYY-MM-DD&to=YYYY-MM-DD
    /// Drilldown: activities by ticket for one technician (Admin only).
    /// </summary>
    [HttpGet("technician-work/{userId:guid}/activities")]
    public async Task<IActionResult> GetTechnicianWorkReportDetail(
        [FromRoute] Guid userId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        try
        {
            var (startDate, endDate) = ReportsDateRange.ParseForTechnicianWork(from, to);
            if (_env.IsDevelopment() && !string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                _logger.LogInformation("[Dev] Parsed report range: from={From} to={To} => {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}", from, to, startDate, endDate);
            var detail = await _reportService.GetTechnicianWorkReportDetailAsync(userId, startDate, endDate);
            if (detail == null)
                return NotFound(new { message = "User not found." });
            return Ok(detail);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get technician work detail for user {UserId}", userId);
            return StatusCode(500, new { message = "Failed to get report detail." });
        }
    }

}















