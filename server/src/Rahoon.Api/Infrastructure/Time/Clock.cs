using System.Globalization;

namespace Rahoon.Api.Infrastructure.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayRiyadh { get; }
}

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Riyadh = ResolveRiyadh();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayRiyadh => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, Riyadh).DateTime);

    internal static TimeZoneInfo ResolveRiyadh()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("AST", TimeSpan.FromHours(3), "AST", "AST");
    }
}

/// <summary>Business-day arithmetic for SLAs. Saudi weekend is Friday and Saturday.</summary>
public static class BusinessDays
{
    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    public static DateOnly Add(DateOnly start, int businessDays)
    {
        var d = start;
        var added = 0;
        while (added < businessDays)
        {
            d = d.AddDays(1);
            if (!IsWeekend(d)) added++;
        }
        return d;
    }

    public static int Between(DateOnly from, DateOnly to)
    {
        if (to <= from) return -CountForward(to, from);
        return CountForward(from, to);
    }

    private static int CountForward(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (!IsWeekend(d)) n++;
        return n;
    }
}

/// <summary>Hijri (Umm al-Qura) display helper — Gregorian stays the system of record.</summary>
public static class Hijri
{
    private static readonly UmAlQuraCalendar Cal = new();
    private static readonly string[] Months =
    [
        "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
        "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
    ];

    public static string Format(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        if (dt < Cal.MinSupportedDateTime || dt > Cal.MaxSupportedDateTime) return string.Empty;
        return $"{Cal.GetDayOfMonth(dt)} {Months[Cal.GetMonth(dt) - 1]} {Cal.GetYear(dt)}هـ";
    }
}
