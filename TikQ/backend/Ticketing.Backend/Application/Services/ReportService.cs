using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Ticketing.Backend.Application.Common;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Application.Services;

public interface IReportService
{
    Task<byte[]> GenerateBasicReportCsvAsync(DateTime startDate, DateTime endDate);
    Task<byte[]> GenerateBasicReportExcelAsync(DateTime startDate, DateTime endDate);
    Task<byte[]> GenerateAnalyticReportZipAsync(DateTime startDate, DateTime endDate);
    Task<byte[]> GenerateAnalyticReportExcelAsync(DateTime startDate, DateTime endDate);
    Task<TechnicianWorkReportDto> GetTechnicianWorkReportAsync(DateTime from, DateTime to, Guid? userId = null);
    Task<byte[]> GenerateTechnicianWorkReportExcelAsync(DateTime from, DateTime to, Guid? userId = null);
    Task<TechnicianWorkReportDetailDto?> GetTechnicianWorkReportDetailAsync(Guid userId, DateTime from, DateTime to);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReportService> _logger;
    private readonly IHostEnvironment _env;

    public ReportService(AppDbContext context, ILogger<ReportService> logger, IHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    /// <summary>Log exported row count in Development to diagnose empty reports.</summary>
    private void LogReportRowCountInDevelopment(string reportType, DateTime from, DateTime to, int rowCount)
    {
        if (_env.IsDevelopment())
            _logger.LogInformation("[Dev] Report row count before export: {ReportType}, Range {From:yyyy-MM-dd}..{To:yyyy-MM-dd}, RowCount={RowCount}", reportType, from, to, rowCount);
    }

    /// <summary>Normalize report range to inclusive UTC: from = 00:00:00, to = 23:59:59.9999999. Ensures end date includes all tickets on that day.</summary>
    private static (DateTime startUtc, DateTime endUtc) NormalizeReportRange(DateTime startDate, DateTime endDate)
    {
        var fromDate = startDate.Kind == DateTimeKind.Utc ? startDate : DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var toDate = endDate.Kind == DateTimeKind.Utc ? endDate : DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);
        var startUtc = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
        return (startUtc, endUtc);
    }

    /// <summary>
    /// Generate Basic Report CSV with ticket list - FULL PERSIAN
    /// Persian calendar dates, Persian digits, Persian headers and status labels.
    /// startDate/endDate must be UTC (inclusive range) to match CreatedAt in DB.
    /// </summary>
    public async Task<byte[]> GenerateBasicReportCsvAsync(DateTime startDate, DateTime endDate)
    {
        var (startUtc, endUtc) = NormalizeReportRange(startDate, endDate);
        _logger.LogInformation("Generating PERSIAN basic report for {Start:O} to {End:O} (UTC)", startUtc, endUtc);

        var totalTickets = await _context.Tickets.AsNoTracking().CountAsync();
        var countInRange = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt <= endUtc)
            .CountAsync();
        _logger.LogInformation("Report date range: {CountInRange} tickets in range, {TotalTickets} total in DB", countInRange, totalTickets);

        if (countInRange == 0 && totalTickets > 0)
        {
            _logger.LogWarning(
                "Report has 0 tickets in range. Date range may not overlap ticket CreatedAt (UTC). Start={Start:O}, End={End:O}. Consider widening range or using preset (e.g. 1y).",
                startUtc, endUtc);
        }

        var tickets = await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians.Where(ta => ta.IsActive))
                .ThenInclude(ta => ta.TechnicianUser)
            .Include(t => t.ActivityEvents)
                .ThenInclude(ae => ae.ActorUser)
            .Include(t => t.Messages)
            .Where(t => t.CreatedAt >= startUtc && t.CreatedAt <= endUtc)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        
        // Persian CSV Headers
        sb.AppendLine("شناسه تیکت,عنوان,وضعیت,اولویت,دسته‌بندی,زیردسته,نام مشتری,ایمیل مشتری,تکنسین‌ها,تاریخ ایجاد,آخرین بروزرسانی");

        foreach (var ticket in tickets)
        {
            var technicianNames = GetTechnicianNames(ticket);
            var latestUpdate = ComputeLatestUpdateAt(ticket);
            
            // Convert to Persian
            var persianStatus = PersianFormat.GetPersianStatus(ticket.Status);
            var persianPriority = PersianFormat.GetPersianPriority(ticket.Priority);
            var persianCreatedAt = PersianFormat.ToPersianDateTime(ticket.CreatedAt);
            var persianUpdatedAt = latestUpdate.HasValue ? PersianFormat.ToPersianDateTime(latestUpdate.Value) : "";

            sb.AppendLine(string.Join(",",
                PersianFormat.EscapeCsv(ticket.Id.ToString()),
                PersianFormat.EscapeCsv(ticket.Title),
                PersianFormat.EscapeCsv(persianStatus),
                PersianFormat.EscapeCsv(persianPriority),
                PersianFormat.EscapeCsv(ticket.Category?.Name ?? "نامشخص"),
                PersianFormat.EscapeCsv(ticket.Subcategory?.Name ?? "نامشخص"),
                PersianFormat.EscapeCsv(ticket.CreatedByUser?.FullName ?? "نامشخص"),
                PersianFormat.EscapeCsv(ticket.CreatedByUser?.Email ?? ""),
                PersianFormat.EscapeCsv(technicianNames),
                PersianFormat.EscapeCsv(persianCreatedAt),
                PersianFormat.EscapeCsv(persianUpdatedAt)
            ));
        }

        _logger.LogInformation("Persian basic report generated with {Count} tickets", tickets.Count);
        LogReportRowCountInDevelopment("BasicReportCsv", startUtc, endUtc, tickets.Count);
        return PersianFormat.GetCsvBytes(sb.ToString()); // Now includes UTF-8 BOM
    }

    /// <summary>
    /// Generate Basic Report as Excel (.xlsx) with one sheet: headers + data rows.
    /// Uses same data as CSV; stream position reset before returning bytes.
    /// </summary>
    public async Task<byte[]> GenerateBasicReportExcelAsync(DateTime startDate, DateTime endDate)
    {
        var (startUtc, endUtc) = NormalizeReportRange(startDate, endDate);
        var tickets = await GetTicketsForBasicReportAsync(startUtc, endUtc);
        _logger.LogInformation("Generating basic Excel report with {Count} tickets", tickets.Count);
        LogReportRowCountInDevelopment("BasicReportExcel", startUtc, endUtc, tickets.Count);

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("گزارش پایه");
        ws.RightToLeft = true;

        // Headers (row 1)
        var headers = new[] { "شناسه تیکت", "عنوان", "وضعیت", "اولویت", "دسته‌بندی", "زیردسته", "نام مشتری", "ایمیل مشتری", "تکنسین‌ها", "تاریخ ایجاد", "آخرین بروزرسانی" };
        for (var c = 1; c <= headers.Length; c++)
            ws.Cell(1, c).Value = headers[c - 1];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var ticket in tickets)
        {
            var technicianNames = GetTechnicianNames(ticket);
            var latestUpdate = ComputeLatestUpdateAt(ticket);
            ws.Cell(row, 1).Value = ticket.Id.ToString();
            ws.Cell(row, 2).Value = ticket.Title;
            ws.Cell(row, 3).Value = PersianFormat.GetPersianStatus(ticket.Status);
            ws.Cell(row, 4).Value = PersianFormat.GetPersianPriority(ticket.Priority);
            ws.Cell(row, 5).Value = ticket.Category?.Name ?? "نامشخص";
            ws.Cell(row, 6).Value = ticket.Subcategory?.Name ?? "نامشخص";
            ws.Cell(row, 7).Value = ticket.CreatedByUser?.FullName ?? "نامشخص";
            ws.Cell(row, 8).Value = ticket.CreatedByUser?.Email ?? "";
            ws.Cell(row, 9).Value = technicianNames;
            ws.Cell(row, 10).Value = PersianFormat.ToPersianDateTime(ticket.CreatedAt);
            ws.Cell(row, 11).Value = latestUpdate.HasValue ? PersianFormat.ToPersianDateTime(latestUpdate.Value) : "";
            row++;
        }

        var dataRowsWritten = row - 2;
        if (tickets.Count != dataRowsWritten)
        {
            _logger.LogError(
                "Basic report export row count mismatch: list had {ListCount} tickets but Excel wrote {ExportRows} rows. Start={Start:yyyy-MM-dd}, End={End:yyyy-MM-dd}",
                tickets.Count, dataRowsWritten, startUtc, endUtc);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream, new SaveOptions { ValidatePackage = false });
        stream.Position = 0;
        return stream.ToArray();
    }

    /// <summary>
    /// Generate Analytic Report as ZIP with multiple CSVs.
    /// Uses same dataset as Excel (GetTicketsForAnalyticReportAsync) so UI and export match.
    /// </summary>
    public async Task<byte[]> GenerateAnalyticReportZipAsync(DateTime startDate, DateTime endDate)
    {
        var (startUtc, endUtc) = NormalizeReportRange(startDate, endDate);
        _logger.LogInformation("Generating analytic report for {Start:O} to {End:O} (UTC)", startUtc, endUtc);

        var tickets = await GetTicketsForAnalyticReportAsync(startUtc, endUtc);

        var totalTickets = await _context.Tickets.AsNoTracking().CountAsync();
        if (tickets.Count == 0 && totalTickets > 0)
        {
            _logger.LogWarning(
                "Analytic report has 0 tickets in range. Start={Start:O}, End={End:O}. Consider widening range (e.g. 1y).",
                startUtc, endUtc);
        }
        _logger.LogInformation("Report date range: {CountInRange} tickets in range, {TotalTickets} total in DB", tickets.Count, totalTickets);

        // 1. Generate tickets.csv (per-ticket details + durations)
        var ticketsCsv = GenerateTicketDetailsCsv(tickets);

        // 2. Generate by_category_subcategory.csv (counts)
        var categoryCsv = GenerateCategoryCountsCsv(tickets);

        // 3. Generate by_client_category_subcategory.csv (client frequency)
        var clientFrequencyCsv = GenerateClientFrequencyCsv(tickets);

        // 4. Generate status_transitions.csv (status history)
        var transitionsCsv = GenerateStatusTransitionsCsv(tickets);

        // Create ZIP archive
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            AddCsvToZip(archive, "tickets.csv", ticketsCsv);
            AddCsvToZip(archive, "by_category_subcategory.csv", categoryCsv);
            AddCsvToZip(archive, "by_client_category_subcategory.csv", clientFrequencyCsv);
            AddCsvToZip(archive, "status_transitions.csv", transitionsCsv);
        }

        _logger.LogInformation("Analytic report ZIP generated with {Count} tickets", tickets.Count);
        LogReportRowCountInDevelopment("AnalyticReportZip", startUtc, endUtc, tickets.Count);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate Analytic Report as Excel (.xlsx) with four sheets. Uses same dataset as ZIP (GetTicketsForAnalyticReportAsync).
    /// </summary>
    public async Task<byte[]> GenerateAnalyticReportExcelAsync(DateTime startDate, DateTime endDate)
    {
        var (startUtc, endUtc) = NormalizeReportRange(startDate, endDate);
        var tickets = await GetTicketsForAnalyticReportAsync(startUtc, endUtc);

        _logger.LogInformation("Generating analytic Excel report with {Count} tickets", tickets.Count);
        LogReportRowCountInDevelopment("AnalyticReportExcel", startUtc, endUtc, tickets.Count);

        using var workbook = new XLWorkbook();

        // Sheet 1: Ticket details
        var wsTickets = workbook.AddWorksheet("تیکت‌ها");
        wsTickets.RightToLeft = true;
        var h1 = new[] { "شناسه تیکت", "عنوان", "نام مشتری", "شناسه مشتری", "دسته‌بندی", "زیردسته", "تاریخ ایجاد", "تاریخ حل", "آخرین وضعیت", "تکنسین‌ها", "زمان حل (دقیقه)" };
        for (var c = 1; c <= h1.Length; c++) wsTickets.Cell(1, c).Value = h1[c - 1];
        wsTickets.Row(1).Style.Font.Bold = true;
        var r1 = 2;
        foreach (var t in tickets)
        {
            var techNames = GetTechnicianNames(t);
            var solvedAt = GetFirstStatusChangeTime(t, "Solved");
            var timeToSolved = solvedAt.HasValue ? (int?)(int)(solvedAt.Value - t.CreatedAt).TotalMinutes : null;
            wsTickets.Cell(r1, 1).Value = t.Id.ToString();
            wsTickets.Cell(r1, 2).Value = t.Title;
            wsTickets.Cell(r1, 3).Value = t.CreatedByUser?.FullName ?? "نامشخص";
            wsTickets.Cell(r1, 4).Value = t.CreatedByUserId.ToString();
            wsTickets.Cell(r1, 5).Value = t.Category?.Name ?? "نامشخص";
            wsTickets.Cell(r1, 6).Value = t.Subcategory?.Name ?? "نامشخص";
            wsTickets.Cell(r1, 7).Value = PersianFormat.ToPersianDateTime(t.CreatedAt);
            wsTickets.Cell(r1, 8).Value = solvedAt.HasValue ? PersianFormat.ToPersianDateTime(solvedAt.Value) : "";
            wsTickets.Cell(r1, 9).Value = PersianFormat.GetPersianStatus(t.Status);
            wsTickets.Cell(r1, 10).Value = techNames;
            wsTickets.Cell(r1, 11).Value = timeToSolved.HasValue ? PersianFormat.ToPersianDigits(timeToSolved.Value) : "";
            r1++;
        }

        // Sheet 2: By category/subcategory
        var wsCat = workbook.AddWorksheet("بر اساس دسته");
        wsCat.RightToLeft = true;
        wsCat.Cell(1, 1).Value = "دسته‌بندی";
        wsCat.Cell(1, 2).Value = "زیردسته";
        wsCat.Cell(1, 3).Value = "تعداد تیکت";
        wsCat.Row(1).Style.Font.Bold = true;
        var groupedCat = tickets.GroupBy(t => new { Category = t.Category?.Name ?? "نامشخص", Subcategory = t.Subcategory?.Name ?? "نامشخص" }).OrderBy(g => g.Key.Category).ThenBy(g => g.Key.Subcategory);
        var r2 = 2;
        foreach (var g in groupedCat)
        {
            wsCat.Cell(r2, 1).Value = g.Key.Category;
            wsCat.Cell(r2, 2).Value = g.Key.Subcategory;
            wsCat.Cell(r2, 3).Value = PersianFormat.ToPersianDigits(g.Count());
            r2++;
        }

        // Sheet 3: By client/category/subcategory
        var wsClient = workbook.AddWorksheet("بر اساس مشتری");
        wsClient.RightToLeft = true;
        wsClient.Cell(1, 1).Value = "نام مشتری";
        wsClient.Cell(1, 2).Value = "شناسه مشتری";
        wsClient.Cell(1, 3).Value = "ایمیل مشتری";
        wsClient.Cell(1, 4).Value = "دسته‌بندی";
        wsClient.Cell(1, 5).Value = "زیردسته";
        wsClient.Cell(1, 6).Value = "تعداد تیکت";
        wsClient.Row(1).Style.Font.Bold = true;
        var groupedClient = tickets.GroupBy(t => new { ClientId = t.CreatedByUserId, ClientName = t.CreatedByUser?.FullName ?? "نامشخص", ClientEmail = t.CreatedByUser?.Email ?? "", Category = t.Category?.Name ?? "نامشخص", Subcategory = t.Subcategory?.Name ?? "نامشخص" }).OrderBy(g => g.Key.ClientName).ThenBy(g => g.Key.Category).ThenBy(g => g.Key.Subcategory);
        var r3 = 2;
        foreach (var g in groupedClient)
        {
            wsClient.Cell(r3, 1).Value = g.Key.ClientName;
            wsClient.Cell(r3, 2).Value = g.Key.ClientId.ToString();
            wsClient.Cell(r3, 3).Value = g.Key.ClientEmail;
            wsClient.Cell(r3, 4).Value = g.Key.Category;
            wsClient.Cell(r3, 5).Value = g.Key.Subcategory;
            wsClient.Cell(r3, 6).Value = PersianFormat.ToPersianDigits(g.Count());
            r3++;
        }

        // Sheet 4: Status transitions
        var wsTrans = workbook.AddWorksheet("تغییر وضعیت");
        wsTrans.RightToLeft = true;
        wsTrans.Cell(1, 1).Value = "شناسه تیکت";
        wsTrans.Cell(1, 2).Value = "وضعیت قبلی";
        wsTrans.Cell(1, 3).Value = "وضعیت جدید";
        wsTrans.Cell(1, 4).Value = "زمان تغییر";
        wsTrans.Cell(1, 5).Value = "مدت زمان از قبلی (دقیقه)";
        wsTrans.Cell(1, 6).Value = "نقش";
        wsTrans.Cell(1, 7).Value = "نام کاربر";
        wsTrans.Row(1).Style.Font.Bold = true;
        var r4 = 2;
        foreach (var ticket in tickets)
        {
            var events = ticket.ActivityEvents.Where(e => !string.IsNullOrEmpty(e.NewStatus)).OrderBy(e => e.CreatedAt).ToList();
            DateTime? prevTime = ticket.CreatedAt;
            string? prevStatus = "ایجاد شده";
            foreach (var evt in events)
            {
                var duration = prevTime.HasValue ? (int?)(int)(evt.CreatedAt - prevTime.Value).TotalMinutes : null;
                wsTrans.Cell(r4, 1).Value = ticket.Id.ToString();
                wsTrans.Cell(r4, 2).Value = evt.OldStatus ?? prevStatus ?? "نامشخص";
                wsTrans.Cell(r4, 3).Value = evt.NewStatus ?? "نامشخص";
                wsTrans.Cell(r4, 4).Value = PersianFormat.ToPersianDateTime(evt.CreatedAt);
                wsTrans.Cell(r4, 5).Value = duration.HasValue ? PersianFormat.ToPersianDigits(duration.Value) : "";
                wsTrans.Cell(r4, 6).Value = evt.ActorRole ?? "نامشخص";
                wsTrans.Cell(r4, 7).Value = evt.ActorUser?.FullName ?? "نامشخص";
                r4++;
                prevTime = evt.CreatedAt;
                prevStatus = evt.NewStatus;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream, new SaveOptions { ValidatePackage = false });
        stream.Position = 0;
        return stream.ToArray();
    }

    /// <summary>
    /// Generate ticket details CSV with FULL PERSIAN formatting
    /// </summary>
    private string GenerateTicketDetailsCsv(List<Ticket> tickets)
    {
        var sb = new StringBuilder();
        // Persian headers
        sb.AppendLine("شناسه تیکت,عنوان,نام مشتری,شناسه مشتری,دسته‌بندی,زیردسته,تاریخ ایجاد,تاریخ حل,آخرین وضعیت,تکنسین‌ها,زمان حل (دقیقه)");

        foreach (var ticket in tickets)
        {
            var technicianNames = GetTechnicianNames(ticket);
            var solvedAt = GetFirstStatusChangeTime(ticket, "Solved");

            var timeToSolved = solvedAt.HasValue 
                ? (int)(solvedAt.Value - ticket.CreatedAt).TotalMinutes 
                : (int?)null;
            
            // Convert to Persian
            var persianCreatedAt = PersianFormat.ToPersianDateTime(ticket.CreatedAt);
            var persianSolvedAt = solvedAt.HasValue ? PersianFormat.ToPersianDateTime(solvedAt.Value) : "";
            var persianStatus = PersianFormat.GetPersianStatus(ticket.Status);
            var persianTimeToSolved = timeToSolved.HasValue ? PersianFormat.ToPersianDigits(timeToSolved.Value) : "";

            sb.AppendLine(string.Join(",",
                PersianFormat.EscapeCsv(ticket.Id.ToString()),
                PersianFormat.EscapeCsv(ticket.Title),
                PersianFormat.EscapeCsv(ticket.CreatedByUser?.FullName ?? "نامشخص"),
                PersianFormat.EscapeCsv(ticket.CreatedByUserId.ToString()),
                PersianFormat.EscapeCsv(ticket.Category?.Name ?? "نامشخص"),
                PersianFormat.EscapeCsv(ticket.Subcategory?.Name ?? "نامشخص"),
                PersianFormat.EscapeCsv(persianCreatedAt),
                PersianFormat.EscapeCsv(persianSolvedAt),
                PersianFormat.EscapeCsv(persianStatus),
                PersianFormat.EscapeCsv(technicianNames),
                PersianFormat.EscapeCsv(persianTimeToSolved)
            ));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate category counts CSV with FULL PERSIAN formatting
    /// </summary>
    private string GenerateCategoryCountsCsv(List<Ticket> tickets)
    {
        var sb = new StringBuilder();
        // Persian headers
        sb.AppendLine("دسته‌بندی,زیردسته,تعداد تیکت");

        var grouped = tickets
            .GroupBy(t => new { 
                Category = t.Category?.Name ?? "نامشخص", 
                Subcategory = t.Subcategory?.Name ?? "نامشخص" 
            })
            .OrderBy(g => g.Key.Category)
            .ThenBy(g => g.Key.Subcategory);

        foreach (var group in grouped)
        {
            var persianCount = PersianFormat.ToPersianDigits(group.Count());
            
            sb.AppendLine(string.Join(",",
                PersianFormat.EscapeCsv(group.Key.Category),
                PersianFormat.EscapeCsv(group.Key.Subcategory),
                PersianFormat.EscapeCsv(persianCount)
            ));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate client frequency CSV with FULL PERSIAN formatting
    /// </summary>
    private string GenerateClientFrequencyCsv(List<Ticket> tickets)
    {
        var sb = new StringBuilder();
        // Persian headers
        sb.AppendLine("نام مشتری,شناسه مشتری,ایمیل مشتری,دسته‌بندی,زیردسته,تعداد تیکت");

        var grouped = tickets
            .GroupBy(t => new { 
                ClientId = t.CreatedByUserId,
                ClientName = t.CreatedByUser?.FullName ?? "نامشخص",
                ClientEmail = t.CreatedByUser?.Email ?? "",
                Category = t.Category?.Name ?? "نامشخص", 
                Subcategory = t.Subcategory?.Name ?? "نامشخص" 
            })
            .OrderBy(g => g.Key.ClientName)
            .ThenBy(g => g.Key.Category)
            .ThenBy(g => g.Key.Subcategory);

        foreach (var group in grouped)
        {
            var persianCount = PersianFormat.ToPersianDigits(group.Count());
            
            sb.AppendLine(string.Join(",",
                PersianFormat.EscapeCsv(group.Key.ClientName),
                PersianFormat.EscapeCsv(group.Key.ClientId.ToString()),
                PersianFormat.EscapeCsv(group.Key.ClientEmail),
                PersianFormat.EscapeCsv(group.Key.Category),
                PersianFormat.EscapeCsv(group.Key.Subcategory),
                PersianFormat.EscapeCsv(persianCount)
            ));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate status transitions CSV with FULL PERSIAN formatting
    /// </summary>
    private string GenerateStatusTransitionsCsv(List<Ticket> tickets)
    {
        var sb = new StringBuilder();
        // Persian headers
        sb.AppendLine("شناسه تیکت,وضعیت قبلی,وضعیت جدید,زمان تغییر,مدت زمان از قبلی (دقیقه),نقش,نام کاربر");

        foreach (var ticket in tickets)
        {
            var events = ticket.ActivityEvents
                .Where(e => !string.IsNullOrEmpty(e.NewStatus))
                .OrderBy(e => e.CreatedAt)
                .ToList();

            DateTime? previousTime = ticket.CreatedAt;
            string? previousStatus = "ایجاد شده";

            foreach (var evt in events)
            {
                var duration = previousTime.HasValue 
                    ? (int)(evt.CreatedAt - previousTime.Value).TotalMinutes 
                    : (int?)null;
                
                // Convert to Persian
                var persianTransitionAt = PersianFormat.ToPersianDateTime(evt.CreatedAt);
                var persianDuration = duration.HasValue ? PersianFormat.ToPersianDigits(duration.Value) : "";
                
                // Map status names to Persian if they are enum values
                var persianOldStatus = evt.OldStatus ?? previousStatus ?? "نامشخص";
                var persianNewStatus = evt.NewStatus ?? "نامشخص";

                sb.AppendLine(string.Join(",",
                    PersianFormat.EscapeCsv(ticket.Id.ToString()),
                    PersianFormat.EscapeCsv(persianOldStatus),
                    PersianFormat.EscapeCsv(persianNewStatus),
                    PersianFormat.EscapeCsv(persianTransitionAt),
                    PersianFormat.EscapeCsv(persianDuration),
                    PersianFormat.EscapeCsv(evt.ActorRole ?? "نامشخص"),
                    PersianFormat.EscapeCsv(evt.ActorUser?.FullName ?? "نامشخص")
                ));

                previousTime = evt.CreatedAt;
                previousStatus = evt.NewStatus;
            }
        }

        return sb.ToString();
    }

    private string GetTechnicianNames(Ticket ticket)
    {
        var names = ticket.AssignedTechnicians
            .Where(ta => ta.IsActive && ta.TechnicianUser != null)
            .Select(ta => ta.TechnicianUser!.FullName)
            .Distinct()
            .ToList();

        if (names.Count == 0 && ticket.AssignedToUser != null)
        {
            names.Add(ticket.AssignedToUser.FullName);
        }

        return names.Count > 0 ? string.Join(" | ", names) : "نامشخص";
    }

    private DateTime? ComputeLatestUpdateAt(Ticket ticket)
    {
        var candidates = new List<DateTime?> { ticket.UpdatedAt };

        if (ticket.ActivityEvents.Any())
        {
            candidates.Add(ticket.ActivityEvents.Max(ae => ae.CreatedAt));
        }

        if (ticket.Messages.Any())
        {
            candidates.Add(ticket.Messages.Max(m => m.CreatedAt));
        }

        return candidates.Where(c => c.HasValue).Max();
    }

    private DateTime? GetFirstStatusChangeTime(Ticket ticket, string targetStatus)
    {
        return ticket.ActivityEvents
            .Where(e => e.NewStatus == targetStatus)
            .OrderBy(e => e.CreatedAt)
            .FirstOrDefault()?.CreatedAt;
    }

    /// <summary>Load tickets for basic report (same query as CSV/Excel) with all includes.</summary>
    private async Task<List<Ticket>> GetTicketsForBasicReportAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians.Where(ta => ta.IsActive))
                .ThenInclude(ta => ta.TechnicianUser)
            .Include(t => t.ActivityEvents)
                .ThenInclude(ae => ae.ActorUser)
            .Include(t => t.Messages)
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Load tickets for analytic report (ZIP and Excel use this so export matches).</summary>
    private async Task<List<Ticket>> GetTicketsForAnalyticReportAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedTechnicians.Where(ta => ta.IsActive))
                .ThenInclude(ta => ta.TechnicianUser)
            .Include(t => t.ActivityEvents.OrderBy(ae => ae.CreatedAt))
                .ThenInclude(ae => ae.ActorUser)
            .Include(t => t.Messages)
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    private static void AddCsvToZip(System.IO.Compression.ZipArchive archive, string fileName, string content)
    {
        var entry = archive.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        
        // Write BOM + content bytes directly to ensure BOM is included
        var bytes = PersianFormat.GetCsvBytes(content);
        entryStream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Technician & Supervisor Work Report. Metrics from:
    /// - Tickets (AssignedToUserId, CreatedAt, UpdatedAt) for owned counts and status
    /// - TicketMessages (AuthorUserId, CreatedAt) for reply counts and lastActivity
    /// - TicketTechnicianAssignments (TechnicianUserId, Role, IsActive) for collaboration counts
    /// - TicketActivityEvents (ActorUserId, EventType, CreatedAt) for status changes and lastActivity
    /// When userId is provided, returns only that user's row.
    /// </summary>
    public async Task<TechnicianWorkReportDto> GetTechnicianWorkReportAsync(DateTime from, DateTime to, Guid? userId = null)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var technicianUsersQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Technician || u.Role == UserRole.Supervisor);
        if (userId.HasValue)
            technicianUsersQuery = technicianUsersQuery.Where(u => u.Id == userId.Value);
        var technicianUsers = await technicianUsersQuery
            .Select(u => new { u.Id, u.FullName, u.Email, u.Role })
            .ToListAsync();

        var techIds = technicianUsers.Select(u => u.Id).ToList();
        if (techIds.Count == 0)
            return new TechnicianWorkReportDto { From = fromUtc, To = toUtc, Users = new List<TechnicianWorkReportUserDto>() };

        var technicianRecords = await _context.Technicians
            .AsNoTracking()
            .Where(t => t.UserId != null && !t.IsDeleted)
            .Select(t => new { t.UserId, t.IsSupervisor })
            .ToListAsync();
        // One row per UserId (take first if duplicate technician records exist)
        var techByUserId = technicianRecords
            .Where(t => t.UserId.HasValue)
            .GroupBy(t => t.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Tickets: owned (AssignedToUserId) and status for open/in-progress/resolved
        var allTicketsForCounts = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedToUserId != null && techIds.Contains(t.AssignedToUserId!.Value))
            .Select(t => new { t.Id, t.AssignedToUserId, t.Status, t.CreatedAt, t.UpdatedAt })
            .ToListAsync();

        // TicketMessages: reply counts and last activity by AuthorUserId in range
        var messagesInRange = await _context.TicketMessages
            .AsNoTracking()
            .Where(m => techIds.Contains(m.AuthorUserId) && m.CreatedAt >= fromUtc && m.CreatedAt <= toUtc)
            .Select(m => new { m.AuthorUserId, m.TicketId, m.CreatedAt })
            .ToListAsync();
        var replyCountByUser = messagesInRange.GroupBy(m => m.AuthorUserId).ToDictionary(g => g.Key, g => g.Count());
        var lastMessageAtByUser = messagesInRange.GroupBy(m => m.AuthorUserId).ToDictionary(g => g.Key, g => g.Max(m => m.CreatedAt));

        // TicketActivityEvents: status changes and last activity by ActorUserId in range
        var eventsInRange = await _context.TicketActivityEvents
            .AsNoTracking()
            .Where(e => techIds.Contains(e.ActorUserId) && e.CreatedAt >= fromUtc && e.CreatedAt <= toUtc)
            .Select(e => new { e.ActorUserId, e.TicketId, e.CreatedAt, e.EventType })
            .ToListAsync();
        var statusChangeCountByUser = eventsInRange
            .Where(e => e.EventType == "StatusChanged" || e.EventType == "Revision")
            .GroupBy(e => e.ActorUserId)
            .ToDictionary(g => g.Key, g => g.Count());
        var lastActivityAtByUserFromEvents = eventsInRange.GroupBy(e => e.ActorUserId).ToDictionary(g => g.Key, g => g.Max(e => e.CreatedAt));

        // TicketTechnicianAssignments: collaborator counts (distinct tickets per user)
        // EF Core cannot translate string.Equals(..., StringComparison). Use ToUpper() == "COLLABORATOR" (translatable in SQLite/SQL Server).
        var assignments = await _context.TicketTechnicianAssignments
            .AsNoTracking()
            .Where(a => a.IsActive && a.Role != null && a.Role.ToUpper() == "COLLABORATOR"
                && techIds.Contains(a.TechnicianUserId))
            .Select(a => new { a.TicketId, a.TechnicianUserId })
            .ToListAsync();
        var collaboratedTicketIdsByUser = assignments
            .GroupBy(a => a.TechnicianUserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TicketId).ToHashSet());

        var allCollaboratedTicketIds = collaboratedTicketIdsByUser.Values.SelectMany(s => s).Distinct().ToList();
        var collaboratedTicketStatuses = allCollaboratedTicketIds.Count > 0
            ? await _context.Tickets.AsNoTracking()
                .Where(t => allCollaboratedTicketIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Status)
            : new Dictionary<Guid, TicketStatus>();

        var ticketsById = await _context.Tickets
            .AsNoTracking()
            .Where(t => allTicketsForCounts.Select(x => x.Id).Concat(allCollaboratedTicketIds).Distinct().Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Title);

        var users = new List<TechnicianWorkReportUserDto>();
        foreach (var u in technicianUsers)
        {
            var isSupervisor = u.Role == UserRole.Supervisor
                || (techByUserId.TryGetValue(u.Id, out var tech) && tech.IsSupervisor);

            var ownedTickets = allTicketsForCounts.Where(t => t.AssignedToUserId == u.Id).ToList();
            var ticketsOwned = ownedTickets.Count;
            var collaboratedSet = collaboratedTicketIdsByUser.GetValueOrDefault(u.Id, new HashSet<Guid>());
            var ticketsCollaborated = collaboratedSet.Count;
            var ticketsTotalInvolved = ticketsOwned + ticketsCollaborated;

            var repliesCount = replyCountByUser.GetValueOrDefault(u.Id, 0);
            var statusChangesCount = statusChangeCountByUser.GetValueOrDefault(u.Id, 0);

            DateTime? lastActivityAt = null;
            var hasMsg = lastMessageAtByUser.TryGetValue(u.Id, out var lastMsgAt);
            var hasEvt = lastActivityAtByUserFromEvents.TryGetValue(u.Id, out var lastEvtAt);
            if (hasMsg && hasEvt) lastActivityAt = lastMsgAt > lastEvtAt ? lastMsgAt : lastEvtAt;
            else if (hasMsg) lastActivityAt = lastMsgAt;
            else if (hasEvt) lastActivityAt = lastEvtAt;

            var ownedStatuses = ownedTickets.Select(t => t.Status).ToList();
            var collaboratedStatuses = collaboratedSet.Select(id => collaboratedTicketStatuses.GetValueOrDefault(id)).ToList();
            var allStatuses = ownedStatuses.Concat(collaboratedStatuses).ToList();
            var openCount = allStatuses.Count(s => s == TicketStatus.Submitted || s == TicketStatus.SeenRead || s == TicketStatus.Open || s == TicketStatus.Redo);
            var inProgressCount = allStatuses.Count(s => s == TicketStatus.InProgress);
            var resolvedCount = allStatuses.Count(s => s == TicketStatus.Solved);

            var userEventsInRange = eventsInRange.Where(e => e.ActorUserId == u.Id).ToList();
            var userMsgTicketDates = messagesInRange.Where(m => m.AuthorUserId == u.Id)
                .GroupBy(m => m.TicketId)
                .ToDictionary(g => g.Key, g => g.Max(m => m.CreatedAt));
            var userEvtTicketDates = userEventsInRange.GroupBy(e => e.TicketId).ToDictionary(g => g.Key, g => g.Max(e => e.CreatedAt));
            var ticketLastAt = new Dictionary<Guid, DateTime>();
            foreach (var tid in userMsgTicketDates.Keys.Concat(userEvtTicketDates.Keys).Distinct())
            {
                var a = userMsgTicketDates.GetValueOrDefault(tid);
                var b = userEvtTicketDates.GetValueOrDefault(tid);
                ticketLastAt[tid] = (a >= b ? a : b);
            }
            var topTickets = ticketLastAt
                .OrderByDescending(x => x.Value)
                .Take(10)
                .Select(x => new TechnicianWorkReportTicketSummaryDto
                {
                    TicketId = x.Key,
                    Title = ticketsById.GetValueOrDefault(x.Key, ""),
                    LastActionAt = x.Value,
                    ActionsCount = userEventsInRange.Count(e => e.TicketId == x.Key) + messagesInRange.Count(m => m.AuthorUserId == u.Id && m.TicketId == x.Key)
                })
                .ToList();

            users.Add(new TechnicianWorkReportUserDto
            {
                UserId = u.Id,
                Name = u.FullName ?? "",
                Email = u.Email ?? "",
                Role = isSupervisor ? "Supervisor" : "Technician",
                IsSupervisor = isSupervisor,
                TicketsOwned = ticketsOwned,
                TicketsCollaborated = ticketsCollaborated,
                TicketsTotalInvolved = ticketsTotalInvolved,
                OpenCount = openCount,
                InProgressCount = inProgressCount,
                ResolvedCount = resolvedCount,
                RepliesCount = repliesCount,
                StatusChangesCount = statusChangesCount,
                AttachmentsCount = 0,
                GrantsCount = userEventsInRange.Count(e => e.EventType == "AccessGranted"),
                RevokesCount = userEventsInRange.Count(e => e.EventType == "AccessRevoked"),
                LastActivityAt = lastActivityAt,
                TopTickets = topTickets
            });
        }

        return new TechnicianWorkReportDto
        {
            From = fromUtc,
            To = toUtc,
            Users = users.OrderByDescending(u => u.LastActivityAt ?? DateTime.MinValue).ToList()
        };
    }

    /// <summary>
    /// Generate Technician Performance report as Excel (.xlsx). Uses same dataset as UI (GetTechnicianWorkReportAsync).
    /// One row per technician so export row count always matches UI table.
    /// </summary>
    public async Task<byte[]> GenerateTechnicianWorkReportExcelAsync(DateTime from, DateTime to, Guid? userId = null)
    {
        var report = await GetTechnicianWorkReportAsync(from, to, userId);
        _logger.LogInformation("Generating technician work Excel report with {Count} users, from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}", report.Users.Count, report.From, report.To);
        LogReportRowCountInDevelopment("TechnicianWorkReportExcel", from, to, report.Users.Count);

        if (report.Users.Count == 0)
        {
            _logger.LogWarning(
                "Technician work export has 0 rows. If UI showed data, check that export uses same filters. From={From:yyyy-MM-dd}, To={To:yyyy-MM-dd}, UserId={UserId}",
                from, to, userId);
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("گزارش عملکرد تکنسین‌ها");
        ws.RightToLeft = true;

        var fromStr = PersianFormat.ToPersianDate(report.From);
        var toStr = PersianFormat.ToPersianDate(report.To);
        ws.Cell(1, 1).Value = $"بازه: از {fromStr} تا {toStr}";
        ws.Row(1).Style.Font.Bold = true;

        var headers = new[] { "نام", "ایمیل", "نقش", "مسئول", "همکار", "کل درگیر", "پاسخ", "تغییر وضعیت", "باز", "در حال انجام", "حل‌شده", "آخرین فعالیت" };
        for (var c = 1; c <= headers.Length; c++)
            ws.Cell(2, c).Value = headers[c - 1];
        ws.Row(2).Style.Font.Bold = true;

        var row = 3;
        foreach (var u in report.Users)
        {
            ws.Cell(row, 1).Value = u.Name;
            ws.Cell(row, 2).Value = u.Email;
            ws.Cell(row, 3).Value = u.IsSupervisor ? "سرپرست" : "تکنسین";
            ws.Cell(row, 4).Value = u.TicketsOwned;
            ws.Cell(row, 5).Value = u.TicketsCollaborated;
            ws.Cell(row, 6).Value = u.TicketsTotalInvolved;
            ws.Cell(row, 7).Value = u.RepliesCount;
            ws.Cell(row, 8).Value = u.StatusChangesCount;
            ws.Cell(row, 9).Value = u.OpenCount;
            ws.Cell(row, 10).Value = u.InProgressCount;
            ws.Cell(row, 11).Value = u.ResolvedCount;
            ws.Cell(row, 12).Value = u.LastActivityAt.HasValue ? PersianFormat.ToPersianDateTime(u.LastActivityAt.Value) : "";
            row++;
        }

        var dataRowsWritten = row - 3;
        if (report.Users.Count != dataRowsWritten)
        {
            _logger.LogError(
                "Export row count mismatch: report had {ReportCount} users but Excel wrote {ExportRows} data rows. From={From:yyyy-MM-dd}, To={To:yyyy-MM-dd}, UserId={UserId}",
                report.Users.Count, dataRowsWritten, from, to, userId);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream, new SaveOptions { ValidatePackage = false });
        stream.Position = 0;
        return stream.ToArray();
    }

    public async Task<TechnicianWorkReportDetailDto?> GetTechnicianWorkReportDetailAsync(Guid userId, DateTime from, DateTime to)
    {
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        var events = await _context.TicketActivityEvents
            .AsNoTracking()
            .Where(e => e.ActorUserId == userId && e.CreatedAt >= fromUtc && e.CreatedAt <= toUtc)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        if (events.Count == 0)
        {
            return new TechnicianWorkReportDetailDto
            {
                UserId = userId,
                UserName = user.FullName ?? user.Email ?? userId.ToString(),
                From = fromUtc,
                To = toUtc,
                ByTicket = new List<TechnicianWorkReportTicketActivityDto>()
            };
        }

        var ticketIds = events.Select(e => e.TicketId).Distinct().ToList();
        var tickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => ticketIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        var byTicket = events
            .GroupBy(e => e.TicketId)
            .Select(g => new TechnicianWorkReportTicketActivityDto
            {
                TicketId = g.Key,
                Title = tickets.TryGetValue(g.Key, out var t) ? t.Title : "",
                Actions = g.OrderByDescending(e => e.CreatedAt).Select(e => new TechnicianWorkReportActivityItemDto
                {
                    EventId = e.Id,
                    EventType = e.EventType,
                    ActorRole = e.ActorRole ?? "",
                    OldStatus = e.OldStatus,
                    NewStatus = e.NewStatus,
                    CreatedAt = e.CreatedAt
                }).ToList()
            })
            .OrderByDescending(x => x.Actions.FirstOrDefault()?.CreatedAt ?? DateTime.MinValue)
            .ToList();

        return new TechnicianWorkReportDetailDto
        {
            UserId = userId,
            UserName = user.FullName ?? user.Email ?? userId.ToString(),
            From = fromUtc,
            To = toUtc,
            ByTicket = byTicket
        };
    }
}















