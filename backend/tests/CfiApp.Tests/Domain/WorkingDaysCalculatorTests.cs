using CfiApp.Domain.Scheduling;
using Shouldly;

namespace CfiApp.Tests.Domain;

public sealed class WorkingDaysCalculatorTests
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void A_tuesday_to_friday_is_four_working_days()
    {
        var start = new DateOnly(2026, 9, 8); // Tuesday
        var end = new DateOnly(2026, 9, 11); // Friday

        WorkingDaysCalculator.Count(start, end, NoHolidays).ShouldBe(4);
    }

    [Fact]
    public void A_full_calendar_week_is_five_working_days()
    {
        var start = new DateOnly(2026, 9, 7); // Monday
        var end = new DateOnly(2026, 9, 13); // Sunday

        WorkingDaysCalculator.Count(start, end, NoHolidays).ShouldBe(5);
    }

    [Fact]
    public void A_single_weekend_day_counts_as_zero()
    {
        var saturday = new DateOnly(2026, 9, 12);

        WorkingDaysCalculator.Count(saturday, saturday, NoHolidays).ShouldBe(0);
    }

    [Fact]
    public void A_named_public_holiday_inside_the_range_does_not_count()
    {
        var start = new DateOnly(2026, 12, 24); // Thursday
        var end = new DateOnly(2026, 12, 28); // Monday
        var christmasAndBoxingDay = new HashSet<DateOnly> { new(2026, 12, 25), new(2026, 12, 28) };

        // Thu 24, Fri 25 (holiday), Sat 26 (weekend), Sun 27 (weekend), Mon 28 (holiday)
        // -> only the 24th counts.
        WorkingDaysCalculator.Count(start, end, christmasAndBoxingDay).ShouldBe(1);
    }

    [Fact]
    public void An_end_date_before_the_start_date_is_zero_working_days()
    {
        var start = new DateOnly(2026, 9, 10);
        var end = new DateOnly(2026, 9, 5);

        WorkingDaysCalculator.Count(start, end, NoHolidays).ShouldBe(0);
    }
}
