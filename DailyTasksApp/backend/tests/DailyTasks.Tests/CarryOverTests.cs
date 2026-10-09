using System.Net.Http.Json;
using DailyTasks.Tests.Infrastructure;
using Shouldly;

namespace DailyTasks.Tests;

// Own factory: these tests move the clock, which would disturb other test classes.
public class CarryOverTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private static DateOnly LondonDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

    [Fact]
    public async Task Unfinished_tasks_move_to_the_next_day_completed_ones_stay()
    {
        // The clock only moves forward from real time: tokens are checked against the real clock.
        var start = DateTimeOffset.UtcNow;
        api.Clock.Now = start;
        var day1 = LondonDate(start).ToString("yyyy-MM-dd");
        var day2 = LondonDate(start).AddDays(1).ToString("yyyy-MM-dd");

        var engineer = await api.NewUserAsync("Engineer", "Daniel");
        var manager = await api.ManagerAsync();

        async Task<Guid> Create(string title)
        {
            var r = await manager.PostAsJsonAsync("/api/tasks", new
            {
                title, date = day1, priority = "Medium", shift = "Morning", assigneeIds = new[] { engineer.Id },
            });
            return (await r.JsonAsync()).GetProperty("id").GetGuid();
        }

        var unfinished = await Create("Grease bearings");
        var finished = await Create("Check guards");
        await engineer.Client.PostAsync($"/api/tasks/{finished}/updates", TestData.EmptyCompletion());

        api.Clock.Now = start.AddDays(1);

        var today = await (await manager.GetAsync("/api/tasks")).JsonAsync();
        var carried = today.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == unfinished);
        carried.GetProperty("date").GetString().ShouldBe(day2);
        carried.GetProperty("originalDate").GetString().ShouldBe(day1);
        today.EnumerateArray().ShouldNotContain(t => t.GetProperty("id").GetGuid() == finished);

        var yesterday = await (await manager.GetAsync($"/api/tasks?date={day1}")).JsonAsync();
        yesterday.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ShouldBe([finished]);

        // The engineer sees the carried task on the new day too.
        var mine = await (await engineer.Client.GetAsync("/api/tasks")).JsonAsync();
        mine.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ShouldBe([unfinished]);
    }

    [Fact]
    public void Day_boundary_follows_London_time_not_UTC()
    {
        // 23:30 UTC on 6 October 2026 is 00:30 BST on the 7th.
        LondonDate(new DateTimeOffset(2026, 10, 6, 23, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 10, 7));
        // In winter (GMT) the two agree.
        LondonDate(new DateTimeOffset(2026, 12, 6, 23, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 12, 6));
    }
}
