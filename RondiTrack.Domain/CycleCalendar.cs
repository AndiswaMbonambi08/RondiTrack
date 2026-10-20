// Pure date maths for stokvel cycles. No clock, no DateTime.Now: callers pass the instant in,
// so the same code works in the job, the seeder and the tests.
// Cycles are calendar dates in the stokvel's own time zone (Africa/Johannesburg).
using System.Globalization;

namespace RondiTrack.Domain;

public static class CycleCalendar
{
    // "Which day is it?" depends on whose calendar you mean: this answers it for the given zone.
    public static DateOnly Today(DateTimeOffset now, TimeZoneInfo zone)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    public static string Label(DateOnly end) => end.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // The period (start..end, both inclusive) that contains `date`, for cycles ending on `endDay` each month.
    public static (DateOnly Start, DateOnly End) PeriodContaining(DateOnly date, int endDay)
    {
        var end = MonthEnd(date, 0, endDay);
        if (date > end) end = MonthEnd(date, 1, endDay);
        var previousEnd = MonthEnd(end, -1, endDay);
        return (previousEnd.AddDays(1), end);
    }

    // The period that starts the day after `previousEnd`.
    public static (DateOnly Start, DateOnly End) NextAfter(DateOnly previousEnd, int endDay)
    {
        var end = MonthEnd(previousEnd, 0, endDay);
        if (end <= previousEnd) end = MonthEnd(previousEnd, 1, endDay);
        return (previousEnd.AddDays(1), end);
    }

    private static DateOnly MonthEnd(DateOnly anyDayInMonth, int monthOffset, int endDay)
    {
        var first = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1).AddMonths(monthOffset);
        return new DateOnly(first.Year, first.Month, Math.Min(endDay, DateTime.DaysInMonth(first.Year, first.Month)));
    }
}