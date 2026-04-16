using System.Globalization;
using System.Text;

namespace Ticketing.Backend.Application.Common;

/// <summary>
/// Utility for formatting dates, times, and numbers in Persian (fa-IR) with Persian calendar and digits.
/// All report outputs must use Persian calendar dates and Persian digits (۰۱۲۳۴۵۶۷۸۹).
/// </summary>
public static class PersianFormat
{
    private static readonly PersianCalendar PersianCalendar = new();
    private static readonly CultureInfo PersianCulture = new("fa-IR");

    /// <summary>
    /// Format DateTime to Persian calendar with format: ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲
    /// </summary>
    public static string ToPersianDateTime(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue)
            return "";

        var year = PersianCalendar.GetYear(dateTime);
        var month = PersianCalendar.GetMonth(dateTime);
        var day = PersianCalendar.GetDayOfMonth(dateTime);
        var hour = dateTime.Hour;
        var minute = dateTime.Minute;

        var gregorianString = $"{year:0000}/{month:00}/{day:00} {hour:00}:{minute:00}";
        return ToPersianDigits(gregorianString);
    }

    /// <summary>
    /// Format DateTime to Persian date only: ۱۴۰۳/۱۱/۱۲
    /// </summary>
    public static string ToPersianDate(DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue)
            return "";

        var year = PersianCalendar.GetYear(dateTime);
        var month = PersianCalendar.GetMonth(dateTime);
        var day = PersianCalendar.GetDayOfMonth(dateTime);

        var gregorianString = $"{year:0000}/{month:00}/{day:00}";
        return ToPersianDigits(gregorianString);
    }

    /// <summary>
    /// Format nullable DateTime to Persian calendar, returns empty string if null
    /// </summary>
    public static string ToPersianDateTime(DateTime? dateTime)
    {
        return dateTime.HasValue ? ToPersianDateTime(dateTime.Value) : "";
    }

    /// <summary>
    /// Format nullable DateTime to Persian date, returns empty string if null
    /// </summary>
    public static string ToPersianDate(DateTime? dateTime)
    {
        return dateTime.HasValue ? ToPersianDate(dateTime.Value) : "";
    }

    /// <summary>
    /// Convert English digits (0-9) to Persian digits (۰-۹)
    /// </summary>
    public static string ToPersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            result.Append(ch switch
            {
                '0' => '۰',
                '1' => '۱',
                '2' => '۲',
                '3' => '۳',
                '4' => '۴',
                '5' => '۵',
                '6' => '۶',
                '7' => '۷',
                '8' => '۸',
                '9' => '۹',
                _ => ch
            });
        }
        return result.ToString();
    }

    /// <summary>
    /// Convert integer to Persian digits string
    /// </summary>
    public static string ToPersianDigits(int number)
    {
        return ToPersianDigits(number.ToString());
    }

    /// <summary>
    /// Null-safe string wrapper, returns empty string for null
    /// </summary>
    public static string SafePersian(string? value)
    {
        return value ?? "";
    }

    /// <summary>
    /// Create UTF-8 encoding with BOM for CSV export
    /// Ensures Persian text displays correctly in Excel
    /// </summary>
    public static Encoding GetCsvEncoding()
    {
        return new UTF8Encoding(true); // true = include BOM
    }

    /// <summary>
    /// Convert CSV string to bytes with UTF-8 BOM prepended
    /// This ensures Excel and other tools properly detect Persian text encoding
    /// </summary>
    public static byte[] GetCsvBytes(string csvContent)
    {
        var encoding = new UTF8Encoding(true); // UTF-8 with BOM
        var preamble = encoding.GetPreamble(); // BOM bytes: EF BB BF
        var contentBytes = encoding.GetBytes(csvContent);
        
        // Combine BOM + content
        var result = new byte[preamble.Length + contentBytes.Length];
        Array.Copy(preamble, 0, result, 0, preamble.Length);
        Array.Copy(contentBytes, 0, result, preamble.Length, contentBytes.Length);
        
        return result;
    }

    /// <summary>
    /// Get Persian status display name from enum
    /// Matches actual TicketStatus enum: Submitted, SeenRead, Open, InProgress, Solved, Redo
    /// </summary>
    public static string GetPersianStatus(Domain.Enums.TicketStatus status)
    {
        return status switch
        {
            Domain.Enums.TicketStatus.Submitted => "ارسال شده",
            Domain.Enums.TicketStatus.SeenRead => "مشاهده شده",
            Domain.Enums.TicketStatus.Open => "باز",
            Domain.Enums.TicketStatus.InProgress => "در حال انجام",
            Domain.Enums.TicketStatus.Solved => "حل شده",
            Domain.Enums.TicketStatus.Redo => "بازنگری",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// Get Persian priority display name
    /// Matches actual TicketPriority enum: Low, Medium, High, Critical
    /// </summary>
    public static string GetPersianPriority(Domain.Enums.TicketPriority priority)
    {
        return priority switch
        {
            Domain.Enums.TicketPriority.Low => "کم",
            Domain.Enums.TicketPriority.Medium => "متوسط",
            Domain.Enums.TicketPriority.High => "زیاد",
            Domain.Enums.TicketPriority.Critical => "بحرانی",
            _ => priority.ToString()
        };
    }

    /// <summary>
    /// Escape CSV value properly (handles commas, quotes, newlines)
    /// </summary>
    public static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Always wrap Persian text in quotes to avoid CSV parsing issues
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r') || ContainsPersianChars(value))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// Check if string contains Persian characters
    /// </summary>
    private static bool ContainsPersianChars(string text)
    {
        return text.Any(ch => ch >= '\u0600' && ch <= '\u06FF');
    }

    /// <summary>
    /// Create Persian-safe filename (replaces unsafe characters)
    /// </summary>
    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new StringBuilder();
        
        foreach (var ch in name)
        {
            if (invalid.Contains(ch))
                safe.Append('_');
            else
                safe.Append(ch);
        }
        
        return safe.ToString();
    }
}
