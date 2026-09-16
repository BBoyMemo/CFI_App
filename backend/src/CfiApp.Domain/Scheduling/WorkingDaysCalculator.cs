namespace CfiApp.Domain.Scheduling;

/// <summary>
/// Working days in a date range - weekends and named public holidays excluded. A pure
/// function on purpose: it is what a holiday request's day count is built from, and that
/// number has to be right independent of whatever the public holiday list happens to
/// contain in the database on a given day.
/// </summary>
public static class WorkingDaysCalculator
{
    public static int Count(DateOnly start, DateOnly end, IReadOnlySet<DateOnly> publicHolidays)
    {
        if (end < start) return 0;

        var count = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            if (!isWeekend && !publicHolidays.Contains(date))
            {
                count++;
            }
        }

        return count;
    }
}
