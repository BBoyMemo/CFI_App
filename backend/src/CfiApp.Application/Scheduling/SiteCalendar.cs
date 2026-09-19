using CfiApp.Application.Abstractions;

namespace CfiApp.Application.Scheduling;

/// <summary>
/// What day it is at the factory. Timestamps stay UTC everywhere else in this codebase, but
/// a rota is written in local days: at half past midnight in British Summer Time the UTC
/// date is still yesterday, and a manager planning "from today" would silently get a change
/// dated a day early.
/// </summary>
public static class SiteCalendar
{
    private static readonly TimeZoneInfo SiteZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static DateOnly Today(IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, SiteZone).DateTime);
}
