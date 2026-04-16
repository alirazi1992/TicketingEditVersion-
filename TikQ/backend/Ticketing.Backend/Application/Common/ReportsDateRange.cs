using System.Globalization;
using System.Text;

namespace Ticketing.Backend.Application.Common;

/// <summary>
/// Shared date range parsing for all report endpoints (basic, analytic, technician-work).
/// Accepts either Gregorian (YYYY-MM-DD / YYYY/MM/DD, year &gt;= 1900) or Jalali (e.g. 1404-11-21, year &lt; 1900).
/// All returned values are UTC with inclusive range: [startOfDay, endOfDay].
/// </summary>
public static class ReportsDateRange
{
    /// <summary>Year threshold: &gt;= this treated as Gregorian, &lt; this treated as Jalali.</summary>
    private const int GregorianYearThreshold = 1900;

    private static readonly PersianCalendar Persian = new();

    /// <summary>
    /// Parse from/to as date strings (Gregorian or Jalali), normalize to Gregorian UTC, return inclusive range.
    /// Accepts: YYYY-MM-DD, YYYY/MM/DD. Persian/Arabic digits (۰-۹) are normalized to 0-9.
    /// Rules: year &gt;= 1900 = Gregorian; year &lt; 1900 (e.g. 1404) = Jalali, converted via PersianCalendar.
    /// from = 00:00:00.000 UTC that day, to = 23:59:59.999 UTC that day (inclusive).
    /// </summary>
    /// <exception cref="ArgumentException">Invalid format, or from &gt; to. Message may be Persian for validation errors.</exception>
    public static (DateTime startUtc, DateTime endUtc) Parse(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from))
            throw new ArgumentException("'from' date is required. Use YYYY-MM-DD or Jalali YYYY-MM-DD.", nameof(from));
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("'to' date is required. Use YYYY-MM-DD or Jalali YYYY-MM-DD.", nameof(to));

        if (!TryParseOne(from!.Trim(), out var fromDate))
            throw new ArgumentException($"Invalid 'from' date format: {from}. Use YYYY-MM-DD or YYYY/MM/DD (Gregorian or Jalali).", nameof(from));
        if (!TryParseOne(to!.Trim(), out var toDate))
            throw new ArgumentException($"Invalid 'to' date format: {to}. Use YYYY-MM-DD or YYYY/MM/DD (Gregorian or Jalali).", nameof(to));

        var startUtc = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, DateTimeKind.Utc);
        // End of day inclusive: 23:59:59.9999999 (next day 00:00:00 minus 1 tick)
        var endUtc = new DateTime(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

        if (startUtc > endUtc)
            throw new ArgumentException("تاریخ شروع باید قبل از تاریخ پایان باشد.");

        return (startUtc, endUtc);
    }

    /// <summary>
    /// Normalize Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII 0-9.
    /// </summary>
    internal static string NormalizeDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c >= '\u06F0' && c <= '\u06F9') // Persian ۰-۹
                sb.Append((char)('0' + (c - '\u06F0')));
            else if (c >= '\u0660' && c <= '\u0669') // Arabic-Indic ٠-٩
                sb.Append((char)('0' + (c - '\u0660')));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Try parse one date string: YYYY-MM-DD or YYYY/MM/DD. Year &gt;= 1900 = Gregorian; &lt; 1900 = Jalali (converted to Gregorian).
    /// </summary>
    private static bool TryParseOne(string raw, out DateTime date)
    {
        date = default;
        var s = NormalizeDigits(raw).Replace('/', '-');
        var parts = s.Split('-');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d))
            return false;

        try
        {
            if (y >= GregorianYearThreshold)
            {
                date = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Unspecified);
                if (date.Year != y || date.Month != m || date.Day != d)
                    return false; // invalid calendar date
                return true;
            }
            // Jalali: convert to Gregorian via PersianCalendar
            date = Persian.ToDateTime(y, m, d, 0, 0, 0, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Get default range: last 30 days (start of day 30 days ago UTC, end of today UTC).
    /// </summary>
    public static (DateTime startUtc, DateTime endUtc) GetDefaultRange()
    {
        var nowUtc = DateTime.UtcNow;
        var startUtc = nowUtc.Date.AddDays(-30);
        var endUtc = nowUtc.Date.AddDays(1).AddTicks(-1);
        return (startUtc, endUtc);
    }

    /// <summary>
    /// Parse preset range (1w, 1m, 3m, 6m, 1y). Returns start of first day UTC and end of today UTC.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid range value.</exception>
    public static (DateTime startUtc, DateTime endUtc) ParsePreset(string? range)
    {
        var nowUtc = DateTime.UtcNow;
        var endOfTodayUtc = nowUtc.Date.AddDays(1).AddTicks(-1);

        var startUtc = (range?.Trim().ToLowerInvariant()) switch
        {
            "1w" => nowUtc.AddDays(-7).Date,
            "1m" => nowUtc.AddMonths(-1).Date,
            "3m" => nowUtc.AddMonths(-3).Date,
            "6m" => nowUtc.AddMonths(-6).Date,
            "1y" => nowUtc.AddYears(-1).Date,
            null or "" => nowUtc.AddMonths(-1).Date,
            _ => throw new ArgumentException($"Invalid range: {range}. Use 1w, 1m, 3m, 6m, or 1y.", nameof(range))
        };
        return (startUtc, endOfTodayUtc);
    }

    /// <summary>
    /// For basic and analytic reports: custom from/to takes precedence; otherwise use preset range.
    /// </summary>
    public static (DateTime startUtc, DateTime endUtc) ParseForBasicAnalytic(string? range, string? from, string? to)
    {
        if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
            return Parse(from, to);
        return ParsePreset(string.IsNullOrWhiteSpace(range) ? "1m" : range);
    }

    /// <summary>
    /// For technician-work report: when both from and to provided, parse and validate (throws if from &gt; to).
    /// When either missing, return default 30-day range.
    /// </summary>
    public static (DateTime startUtc, DateTime endUtc) ParseForTechnicianWork(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return GetDefaultRange();
        return Parse(from, to);
    }
}
