namespace DailyTasks.Api.Common;

public class SiteOptions
{
    public const string Section = "Site";

    // The factory's local time zone decides when "today" ends and carry-over happens.
    public string TimeZone { get; set; } = "Europe/London";
}

public class AppClock(TimeProvider time, Microsoft.Extensions.Options.IOptions<SiteOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    // The UTC instants a local calendar day starts and ends at (23 or 25 hours on clock-change days).
    public (DateTimeOffset From, DateTimeOffset To) DayBounds(DateOnly day)
    {
        DateTimeOffset StartOf(DateOnly d)
        {
            var local = d.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
        }

        return (StartOf(day), StartOf(day.AddDays(1)));
    }
}
