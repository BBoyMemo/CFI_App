using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using DailyTasks.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DailyTasks.Tests;

// Progress / completion / follow-up cards. Own factory: some tests move the clock.
public class TaskUpdatesTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private string Day(int offset = 0) => DateOnly
        .FromDateTime(TimeZoneInfo.ConvertTime(api.Clock.Now, London).DateTime).AddDays(offset).ToString("yyyy-MM-dd");

    private async Task<Guid> CreateTaskAsync(params Guid[] assignees)
    {
        var manager = await api.ManagerAsync();
        var r = await manager.PostAsJsonAsync("/api/tasks", new
        {
            title = "Repair hopper gate", date = Day(), priority = "High", shift = "Morning", assigneeIds = assignees,
        });
        r.EnsureSuccessStatusCode();
        return (await r.JsonAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CardAsync(HttpClient client, Guid id, string outcome, string? comment = null, int photos = 0)
    {
        var r = await client.PostAsync($"/api/tasks/{id}/updates", TestData.Card(outcome, comment, photos));
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await r.JsonAsync();
    }

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string url)
    {
        var body = await (await client.GetAsync(url)).JsonAsync();
        var items = body.ValueKind == JsonValueKind.Array ? body : body.GetProperty("items");
        return items.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task In_progress_keeps_the_task_open_carries_it_over_and_keeps_it_out_of_history()
    {
        var engineer = await api.NewUserAsync("Engineer", "Rupert");
        var id = await CreateTaskAsync(engineer.Id);

        var task = await CardAsync(engineer.Client, id, "InProgress", "Gate stripped, waiting for seal", photos: 2);
        task.GetProperty("status").GetString().ShouldBe("InProgress");
        task.GetProperty("completed").GetBoolean().ShouldBeFalse();
        var card = task.GetProperty("updates").EnumerateArray().Single();
        card.GetProperty("outcome").GetString().ShouldBe("InProgress");
        card.GetProperty("photoIds").GetArrayLength().ShouldBe(2);

        (await IdsAsync(engineer.Client, "/api/tasks/history?pageSize=100")).ShouldNotContain(id);

        // Next day: still on the list, and the same person carries on and finishes it.
        api.Clock.Now = api.Clock.Now.AddDays(1);
        (await IdsAsync(engineer.Client, "/api/tasks")).ShouldContain(id);
        var done = await CardAsync(engineer.Client, id, "Completed", "New seal fitted");
        done.GetProperty("status").GetString().ShouldBe("Completed");
        done.GetProperty("updates").EnumerateArray().Select(u => u.GetProperty("outcome").GetString())
            .ShouldBe(["InProgress", "Completed"]);
        (await IdsAsync(engineer.Client, "/api/tasks/history?pageSize=100")).ShouldContain(id);
    }

    [Fact]
    public async Task After_completion_anyone_adds_a_follow_up_and_the_task_stays_completed()
    {
        var doer = await api.NewUserAsync("Engineer", "Tabitha");
        var colleague = await api.NewUserAsync("Engineer", "Ezra");
        var id = await CreateTaskAsync(doer.Id);
        await CardAsync(doer.Client, id, "Completed", "Done");

        var task = await CardAsync(colleague.Client, id, "Completed", "Found a loose bolt, tightened it", photos: 1);

        task.GetProperty("status").GetString().ShouldBe("Completed");
        var cards = task.GetProperty("updates").EnumerateArray().ToList();
        cards.Count.ShouldBe(2);
        cards[1].GetProperty("author").GetProperty("id").GetGuid().ShouldBe(colleague.Id);
        task.GetProperty("assignees").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(doer.Id);
    }

    [Fact]
    public async Task Taking_a_completed_task_back_makes_it_the_takers_task_for_today_and_keeps_it_in_history()
    {
        var doer = await api.NewUserAsync("Engineer", "Wilfred");
        var taker = await api.NewUserAsync("Engineer", "Imogen");
        var id = await CreateTaskAsync(doer.Id);
        await CardAsync(doer.Client, id, "Completed", "Done");

        api.Clock.Now = api.Clock.Now.AddDays(2);
        var task = await CardAsync(taker.Client, id, "InProgress", "Leaking again, working on it");

        task.GetProperty("status").GetString().ShouldBe("InProgress");
        task.GetProperty("date").GetString().ShouldBe(Day());
        task.GetProperty("assignees").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ShouldBe([taker.Id]);
        (await IdsAsync(taker.Client, "/api/tasks")).ShouldContain(id);
        (await IdsAsync(doer.Client, "/api/tasks")).ShouldNotContain(id);
        (await IdsAsync(doer.Client, "/api/tasks/history?pageSize=100")).ShouldContain(id); // still in History

        // While it is in progress again only the taker (or a manager) reports on it.
        (await doer.Client.PostAsync($"/api/tasks/{id}/updates", TestData.Card("Completed")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        api.Clock.Now = api.Clock.Now.AddDays(1);
        (await CardAsync(taker.Client, id, "Completed", "Fixed for good")).GetProperty("updates").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Manager_can_reassign_an_in_progress_task()
    {
        var first = await api.NewUserAsync("Engineer", "Florence");
        var second = await api.NewUserAsync("Engineer", "Percy");
        var id = await CreateTaskAsync(first.Id);
        await CardAsync(first.Client, id, "InProgress", "Half done");

        var manager = await api.ManagerAsync();
        var r = await manager.PutAsJsonAsync($"/api/tasks/{id}", new
        {
            title = "Repair hopper gate", date = Day(), priority = "High", shift = "Morning", assigneeIds = new[] { second.Id },
        });

        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await IdsAsync(second.Client, "/api/tasks")).ShouldContain(id);
        (await second.Client.PostAsync($"/api/tasks/{id}/updates", TestData.Card("Completed")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_task_with_any_card_cannot_be_deleted_but_an_untouched_one_can()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var manager = await api.ManagerAsync();
        var worked = await CreateTaskAsync(engineer.Id);
        var untouched = await CreateTaskAsync(engineer.Id);
        await CardAsync(engineer.Client, worked, "InProgress");

        var refused = await manager.DeleteAsync($"/api/tasks/{worked}");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("code").GetString().ShouldBe("task.hasUpdates");
        (await manager.DeleteAsync($"/api/tasks/{untouched}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Earlier_completions_become_the_tasks_first_card()
    {
        var engineer = await api.NewUserAsync("Engineer", "Mabel");
        var id = await CreateTaskAsync(engineer.Id);
        var completedAt = api.Clock.Now.AddHours(-3);

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // As the database looked before update cards: completion stored on the task itself.
            var task = await db.Tasks.SingleAsync(t => t.Id == id);
            task.CompletedAt = completedAt;
            task.CompletedById = engineer.Id;
            task.CompletionComment = "Old style completion";
            task.CompletionPhotoKey = "tasks/legacy-photo.jpg";
            db.TaskPhotos.Add(new TaskPhoto
            {
                Id = Guid.NewGuid(), TaskId = id, Kind = TaskPhotoKind.Completion, PhotoKey = "tasks/legacy-2.jpg",
                CreatedById = engineer.Id, CreatedAt = completedAt,
            });
            await db.SaveChangesAsync();

            (await TaskUpdateBackfill.RunAsync(db)).ShouldBeGreaterThanOrEqualTo(1);
            (await TaskUpdateBackfill.RunAsync(db)).ShouldBe(0); // nothing done twice
        }

        var manager = await api.ManagerAsync();
        var migrated = await (await manager.GetAsync($"/api/tasks/{id}")).JsonAsync();
        migrated.GetProperty("status").GetString().ShouldBe("Completed");
        var card = migrated.GetProperty("updates").EnumerateArray().Single();
        card.GetProperty("outcome").GetString().ShouldBe("Completed");
        card.GetProperty("comment").GetString().ShouldBe("Old style completion");
        card.GetProperty("author").GetProperty("id").GetGuid().ShouldBe(engineer.Id);
        card.GetProperty("photoIds").GetArrayLength().ShouldBe(2);
    }
}
