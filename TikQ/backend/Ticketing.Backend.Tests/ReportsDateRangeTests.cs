using System;
using Ticketing.Backend.Application.Common;
using Xunit;

namespace Ticketing.Backend.Tests;

/// <summary>
/// Unit tests for ReportsDateRange: ensures from/to parsing is consistent and invalid ranges return 400.
/// </summary>
public class ReportsDateRangeTests
{
    [Fact]
    public void Parse_from_before_to_returns_inclusive_utc_range()
    {
        var (startUtc, endUtc) = ReportsDateRange.Parse("2025-01-10", "2025-01-20");

        Assert.Equal(2025, startUtc.Year);
        Assert.Equal(1, startUtc.Month);
        Assert.Equal(10, startUtc.Day);
        Assert.Equal(0, startUtc.Hour);
        Assert.Equal(0, startUtc.Minute);
        Assert.Equal(DateTimeKind.Utc, startUtc.Kind);

        Assert.Equal(2025, endUtc.Year);
        Assert.Equal(1, endUtc.Month);
        Assert.Equal(20, endUtc.Day);
        Assert.Equal(23, endUtc.Hour);
        Assert.Equal(59, endUtc.Minute);
        Assert.Equal(DateTimeKind.Utc, endUtc.Kind);
    }

    [Fact]
    public void Parse_from_equals_to_valid_single_day()
    {
        var (startUtc, endUtc) = ReportsDateRange.Parse("2025-02-01", "2025-02-01");

        Assert.Equal(2025, startUtc.Year);
        Assert.Equal(2, startUtc.Month);
        Assert.Equal(1, startUtc.Day);
        Assert.Equal(0, startUtc.Hour);

        Assert.Equal(2025, endUtc.Year);
        Assert.Equal(2, endUtc.Month);
        Assert.Equal(1, endUtc.Day);
        Assert.True(endUtc.TimeOfDay >= TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)));
    }

    [Fact]
    public void Parse_from_after_to_throws_with_persian_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => ReportsDateRange.Parse("2025-02-10", "2025-02-01"));
        Assert.Contains("تاریخ", ex.Message);
    }

    [Fact]
    public void Parse_invalid_from_throws()
    {
        Assert.Throws<ArgumentException>(() => ReportsDateRange.Parse("not-a-date", "2025-02-01"));
    }

    [Fact]
    public void Parse_invalid_to_throws()
    {
        Assert.Throws<ArgumentException>(() => ReportsDateRange.Parse("2025-02-01", "invalid"));
    }

    [Fact]
    public void Parse_jalali_dates_accepted_and_converted_to_gregorian()
    {
        // 1404/11/21 and 1404/11/25 (Jalali) => Gregorian Feb 2026
        var (startUtc, endUtc) = ReportsDateRange.Parse("1404-11-21", "1404-11-25");
        Assert.Equal(2026, startUtc.Year);
        Assert.Equal(2, startUtc.Month);
        Assert.Equal(10, startUtc.Day);
        Assert.Equal(2026, endUtc.Year);
        Assert.Equal(2, endUtc.Month);
        Assert.True(endUtc.Day >= 14 && endUtc.Day <= 15); // 1404/11/25 ≈ 2026-02-14
        Assert.Equal(DateTimeKind.Utc, startUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, endUtc.Kind);
        Assert.True(startUtc <= endUtc);
    }

    [Fact]
    public void Parse_jalali_with_slash_format_accepted()
    {
        var (startUtc, endUtc) = ReportsDateRange.Parse("1404/10/01", "1404/10/15");
        Assert.Equal(DateTimeKind.Utc, startUtc.Kind);
        Assert.True(startUtc.Year >= 2025 && startUtc.Year <= 2026);
        Assert.True(endUtc >= startUtc);
    }

    [Fact]
    public void ParseForTechnicianWork_both_missing_returns_default_range()
    {
        var (startUtc, endUtc) = ReportsDateRange.ParseForTechnicianWork(null, null);
        var diff = endUtc - startUtc;
        Assert.True(diff.TotalDays >= 29 && diff.TotalDays <= 31);
    }

    [Fact]
    public void ParseForTechnicianWork_both_provided_uses_parsed_range()
    {
        var (startUtc, endUtc) = ReportsDateRange.ParseForTechnicianWork("2025-01-01", "2025-01-31");
        Assert.Equal(2025, startUtc.Year);
        Assert.Equal(1, startUtc.Month);
        Assert.Equal(1, startUtc.Day);
        Assert.Equal(2025, endUtc.Year);
        Assert.Equal(1, endUtc.Month);
        Assert.Equal(31, endUtc.Day);
    }

    [Fact]
    public void ParseForTechnicianWork_from_after_to_throws()
    {
        Assert.Throws<ArgumentException>(() => ReportsDateRange.ParseForTechnicianWork("2025-02-10", "2025-02-01"));
    }

    [Fact]
    public void ParsePreset_1m_returns_range()
    {
        var (startUtc, endUtc) = ReportsDateRange.ParsePreset("1m");
        Assert.True(startUtc < endUtc);
        Assert.Equal(DateTimeKind.Utc, startUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, endUtc.Kind);
    }

    [Fact]
    public void ParsePreset_invalid_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ReportsDateRange.ParsePreset("invalid"));
        Assert.Contains("1w", ex.Message);
    }

    [Fact]
    public void ParseForBasicAnalytic_custom_from_to_takes_precedence()
    {
        var (startUtc, endUtc) = ReportsDateRange.ParseForBasicAnalytic("1m", "2025-01-05", "2025-01-15");
        Assert.Equal(5, startUtc.Day);
        Assert.Equal(15, endUtc.Day);
    }

    [Fact]
    public void ParseForBasicAnalytic_no_custom_uses_preset()
    {
        var (startUtc, endUtc) = ReportsDateRange.ParseForBasicAnalytic("1m", null, null);
        Assert.True((DateTime.UtcNow - startUtc).TotalDays >= 28 && (DateTime.UtcNow - startUtc).TotalDays <= 32);
    }
}
