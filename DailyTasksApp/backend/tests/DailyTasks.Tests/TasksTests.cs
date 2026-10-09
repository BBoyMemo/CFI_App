using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DailyTasks.Tests.Infrastructure;
using Shouldly;

namespace DailyTasks.Tests;

public class TasksTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static string Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
        .ToString("yyyy-MM-dd");

    private static object NewTask(Guid[] assignees, string? date = null, string shift = "Morning") => new
    {
        title = "Replace filter on mixer 2",
        description = "Use the spare from store room",
        date = date ?? Today,
        priority = "High",
        shift,
        assigneeIds = assignees,
    };

    private async Task<JsonElement> CreateAsync(object request)
    {
        var manager = await api.ManagerAsync();
        var response = await manager.PostAsJsonAsync("/api/tasks", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.JsonAsync();
    }

    [Fact]
    public async Task Manager_creates_a_task_for_several_engineers()
    {
        var a = await api.NewUserAsync("Engineer", "Jack");
        var b = await api.NewUserAsync("Engineer", "Emily");

        var task = await CreateAsync(NewTask([a.Id, b.Id], shift: "Afternoon"));

        task.GetProperty("shift").GetString().ShouldBe("Afternoon");
        task.GetProperty("assignees").GetArrayLength().ShouldBe(2);
        task.GetProperty("completed").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Engineer_cannot_create_tasks()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var response = await engineer.Client.PostAsJsonAsync("/api/tasks", NewTask([engineer.Id]));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Task_must_be_assigned_to_existing_people_and_not_dated_in_the_past()
    {
        var manager = await api.ManagerAsync();

        var response = await manager.PostAsJsonAsync("/api/tasks", NewTask([Guid.NewGuid()], date: "2020-01-01"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        errors.GetProperty("assigneeIds")[0].GetString().ShouldBe("unknownUser");
        errors.GetProperty("date")[0].GetString().ShouldBe("inPast");
    }

    [Fact]
    public async Task Engineer_sees_only_own_tasks_manager_sees_all()
    {
        var mine = await api.NewUserAsync("Engineer", "Thomas");
        var other = await api.NewUserAsync("Engineer", "Lucy");
        var myTask = await CreateAsync(NewTask([mine.Id]));
        var otherTask = await CreateAsync(NewTask([other.Id]));

        var engineerList = await (await mine.Client.GetAsync($"/api/tasks?date={Today}")).JsonAsync();
        var ids = engineerList.EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        ids.ShouldContain(myTask.GetProperty("id").GetGuid());
        ids.ShouldNotContain(otherTask.GetProperty("id").GetGuid());

        var forbidden = await mine.Client.GetAsync($"/api/tasks/{otherTask.GetProperty("id").GetGuid()}");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var manager = await api.ManagerAsync();
        var managerList = await (await manager.GetAsync($"/api/tasks?date={Today}")).JsonAsync();
        managerList.EnumerateArray().Select(t => t.GetProperty("id").GetGuid())
            .ShouldContain(otherTask.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Assigned_engineer_completes_with_comment_and_several_photos()
    {
        var engineer = await api.NewUserAsync("Engineer", "William");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();

        using var form = TestData.Card("Completed", "Filter replaced, old one binned", photos: 3);
        var response = await engineer.Client.PostAsync($"/api/tasks/{id}/updates", form);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var task = await response.JsonAsync();
        task.GetProperty("completed").GetBoolean().ShouldBeTrue();
        task.GetProperty("completedBy").GetProperty("id").GetGuid().ShouldBe(engineer.Id);
        task.GetProperty("status").GetString().ShouldBe("Completed");
        var card = task.GetProperty("updates").EnumerateArray().Single();
        card.GetProperty("outcome").GetString().ShouldBe("Completed");
        card.GetProperty("author").GetProperty("id").GetGuid().ShouldBe(engineer.Id);
        card.GetProperty("comment").GetString().ShouldBe("Filter replaced, old one binned");
        var photoIds = card.GetProperty("photoIds").EnumerateArray().Select(p => p.GetGuid()).ToList();
        photoIds.Count.ShouldBe(3);
        task.GetProperty("photoIds").GetArrayLength().ShouldBe(0); // planning photos are separate

        var manager = await api.ManagerAsync();
        var photo = await manager.GetAsync($"/api/tasks/{id}/photos/{photoIds[2]}");
        photo.StatusCode.ShouldBe(HttpStatusCode.OK);
        photo.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await photo.Content.ReadAsByteArrayAsync()).ShouldBe(TestData.Jpeg);
    }

    [Fact]
    public async Task Completion_takes_at_most_five_photos_and_keeps_nothing_on_rejection()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent();
        for (var i = 0; i < 6; i++) form.Add(TestData.File(TestData.Jpeg), "photo", $"p{i}.jpg");

        var response = await engineer.Client.PostAsync($"/api/tasks/{id}/updates", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.JsonAsync()).GetProperty("errors").GetProperty("photo")[0].GetString().ShouldBe("photo.tooMany");
        var task = await (await engineer.Client.GetAsync($"/api/tasks/{id}")).JsonAsync();
        task.GetProperty("completed").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Completion_without_comment_or_photo_is_allowed()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();

        var response = await engineer.Client.PostAsync($"/api/tasks/{id}/updates", TestData.EmptyCompletion());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var card = (await response.JsonAsync()).GetProperty("updates").EnumerateArray().Single();
        card.GetProperty("photoIds").GetArrayLength().ShouldBe(0);
        card.TryGetProperty("comment", out var comment).ShouldBeTrue();
        comment.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Before_completion_only_assignees_and_managers_add_cards()
    {
        var assignee = await api.NewUserAsync("Engineer");
        var outsider = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([assignee.Id]))).GetProperty("id").GetGuid();

        (await outsider.Client.PostAsync($"/api/tasks/{id}/updates", TestData.Card("InProgress")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The manager can do whatever an engineer can, on any task.
        var manager = await api.ManagerAsync();
        (await manager.PostAsync($"/api/tasks/{id}/updates", TestData.Card("InProgress", "Started on it")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await outsider.Client.PostAsync($"/api/tasks/{id}/updates", TestData.Card("Completed")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden); // still in progress: still not theirs

        var done = await (await assignee.Client.PostAsync($"/api/tasks/{id}/updates", TestData.EmptyCompletion())).JsonAsync();
        done.GetProperty("status").GetString().ShouldBe("Completed");
        done.GetProperty("updates").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task A_card_needs_an_outcome()
    {
        var assignee = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([assignee.Id]))).GetProperty("id").GetGuid();
        using var form = new MultipartFormDataContent { { new StringContent("No outcome"), "comment" } };

        var response = await assignee.Client.PostAsync($"/api/tasks/{id}/updates", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.JsonAsync()).GetProperty("errors").GetProperty("outcome")[0].GetString().ShouldBe("required");
    }

    [Fact]
    public async Task Non_image_photo_is_rejected()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();

        using var form = TestData.Card("Completed");
        form.Add(TestData.File("not an image"u8.ToArray()), "photo", "fake.jpg");
        var response = await engineer.Client.PostAsync($"/api/tasks/{id}/updates", form);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.JsonAsync()).GetProperty("errors").GetProperty("photo")[0].GetString()
            .ShouldBe("photo.unsupportedFormat");
    }

    [Fact]
    public async Task Manager_moves_a_task_to_another_date_and_can_delete_it()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();
        var nextWeek = DateOnly.Parse(Today).AddDays(7).ToString("yyyy-MM-dd");
        var manager = await api.ManagerAsync();

        var moved = await manager.PutAsJsonAsync($"/api/tasks/{id}", NewTask([engineer.Id], date: nextWeek));
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await moved.JsonAsync();
        body.GetProperty("date").GetString().ShouldBe(nextWeek);
        body.GetProperty("originalDate").GetString().ShouldBe(nextWeek);

        (await manager.DeleteAsync($"/api/tasks/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.GetAsync($"/api/tasks/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_shows_every_completed_task_to_every_engineer_newest_first()
    {
        var harry = await api.NewUserAsync("Engineer", "Harry");
        var grace = await api.NewUserAsync("Engineer", "Grace");
        var first = (await CreateAsync(NewTask([harry.Id]))).GetProperty("id").GetGuid();
        var open = (await CreateAsync(NewTask([harry.Id]))).GetProperty("id").GetGuid();
        var gracesTask = (await CreateAsync(NewTask([grace.Id]))).GetProperty("id").GetGuid();
        await harry.Client.PostAsync($"/api/tasks/{first}/updates", TestData.EmptyCompletion());

        // The test clock stands still unless moved; make Grace finish visibly later.
        api.Clock.Now = api.Clock.Now.AddSeconds(30);
        using var withPhoto = TestData.Card("Completed", photos: 1);
        await grace.Client.PostAsync($"/api/tasks/{gracesTask}/updates", withPhoto);

        var history = await (await harry.Client.GetAsync("/api/tasks/history?pageSize=100")).JsonAsync();
        var ids = history.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        ids.IndexOf(gracesTask).ShouldBeLessThan(ids.IndexOf(first));
        ids.ShouldNotContain(open);
        history.GetProperty("items").EnumerateArray().ShouldAllBe(t => t.GetProperty("completed").GetBoolean());

        // A completed task of someone else, and its photo, are open to every engineer...
        var gracesPhoto = history.GetProperty("items").EnumerateArray()
            .Single(t => t.GetProperty("id").GetGuid() == gracesTask)
            .GetProperty("updates")[0].GetProperty("photoIds")[0].GetGuid();
        (await harry.Client.GetAsync($"/api/tasks/{gracesTask}/photos/{gracesPhoto}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        // ...while another engineer's open task stays private.
        (await grace.Client.GetAsync($"/api/tasks/{open}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task History_search_matches_title_or_description_and_completion_day()
    {
        var engineer = await api.NewUserAsync("Engineer", "Jacob");
        var marker = Guid.NewGuid().ToString("N")[..10];
        var manager = await api.ManagerAsync();

        async Task<Guid> CreateAndComplete(string title, string? description)
        {
            var r = await manager.PostAsJsonAsync("/api/tasks", new
            {
                title, description, date = Today, priority = "Low", shift = "Morning", assigneeIds = new[] { engineer.Id },
            });
            var id = (await r.JsonAsync()).GetProperty("id").GetGuid();
            await engineer.Client.PostAsync($"/api/tasks/{id}/updates", TestData.EmptyCompletion());
            return id;
        }

        var byTitle = await CreateAndComplete($"Pump {marker} seal", null);
        var byDescription = await CreateAndComplete("Inspect hopper", $"Old seal on PUMP {marker.ToUpperInvariant()}");
        var unrelated = await CreateAndComplete("Sweep floor", "Nothing to see");

        async Task<List<Guid>> Search(string query)
        {
            var body = await (await engineer.Client.GetAsync($"/api/tasks/history?pageSize=100&{query}")).JsonAsync();
            return body.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        }

        var text = await Search($"q=pump%20{marker}");
        text.ShouldBe([byTitle, byDescription], ignoreOrder: true);

        var completedDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            api.Clock.Now, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        (await Search($"date={completedDay:yyyy-MM-dd}")).ShouldContain(unrelated);
        (await Search($"date={completedDay.AddDays(-1):yyyy-MM-dd}")).ShouldNotContain(unrelated);
        (await Search($"q={marker}&date={completedDay:yyyy-MM-dd}")).ShouldBe([byTitle, byDescription], ignoreOrder: true);
    }

    [Fact]
    public async Task Completed_task_cannot_be_edited_and_engineer_cannot_delete()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();

        (await engineer.Client.DeleteAsync($"/api/tasks/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await engineer.Client.PostAsync($"/api/tasks/{id}/updates", TestData.EmptyCompletion());
        var manager = await api.ManagerAsync();
        var edit = await manager.PutAsJsonAsync($"/api/tasks/{id}", NewTask([engineer.Id]));
        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Completed work is kept: not even a manager can delete it.
        var delete = await manager.DeleteAsync($"/api/tasks/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await delete.JsonAsync()).GetProperty("code").GetString().ShouldBe("task.hasUpdates");
        (await manager.GetAsync($"/api/tasks/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static MultipartFormDataContent PhotoForm() => new() { { TestData.File(TestData.Jpeg), "photo", "plan.jpg" } };

    [Fact]
    public async Task Manager_attaches_up_to_five_photos_which_assignees_can_see()
    {
        var engineer = await api.NewUserAsync("Engineer", "Felix");
        var outsider = await api.NewUserAsync("Engineer", "Agnes");
        var manager = await api.ManagerAsync();
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();

        JsonElement task = default;
        for (var i = 0; i < 5; i++)
        {
            var r = await manager.PostAsync($"/api/tasks/{id}/photos", PhotoForm());
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
            task = await r.JsonAsync();
        }
        var photoIds = task.GetProperty("photoIds").EnumerateArray().Select(p => p.GetGuid()).ToList();
        photoIds.Count.ShouldBe(5);

        var sixth = await manager.PostAsync($"/api/tasks/{id}/photos", PhotoForm());
        sixth.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await sixth.JsonAsync()).GetProperty("errors").GetProperty("photo")[0].GetString().ShouldBe("photo.tooMany");

        var seen = await engineer.Client.GetAsync($"/api/tasks/{id}/photos/{photoIds[0]}");
        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await seen.Content.ReadAsByteArrayAsync()).ShouldBe(TestData.Jpeg);
        (await outsider.Client.GetAsync($"/api/tasks/{id}/photos/{photoIds[0]}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var removed = await (await manager.DeleteAsync($"/api/tasks/{id}/photos/{photoIds[0]}")).JsonAsync();
        removed.GetProperty("photoIds").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task Only_manager_attaches_photos_and_not_after_completion()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var manager = await api.ManagerAsync();
        var id = (await CreateAsync(NewTask([engineer.Id]))).GetProperty("id").GetGuid();
        var photoId = (await (await manager.PostAsync($"/api/tasks/{id}/photos", PhotoForm())).JsonAsync())
            .GetProperty("photoIds")[0].GetGuid();

        (await engineer.Client.PostAsync($"/api/tasks/{id}/photos", PhotoForm())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await engineer.Client.PostAsync($"/api/tasks/{id}/updates", TestData.EmptyCompletion());
        (await manager.PostAsync($"/api/tasks/{id}/photos", PhotoForm())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await manager.DeleteAsync($"/api/tasks/{id}/photos/{photoId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Once completed, everyone may look at it (History), photos included.
        var other = await api.NewUserAsync("Engineer");
        (await other.Client.GetAsync($"/api/tasks/{id}/photos/{photoId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task History_search_matches_people_and_completion_comment()
    {
        var finisher = await api.NewUserAsync("Engineer", "Henrietta");
        var helper = await api.NewUserAsync("Engineer", "Barnaby");
        var manager = await api.ManagerAsync();
        var r = await manager.PostAsJsonAsync("/api/tasks", new
        {
            title = "Swap gearbox oil", date = Today, priority = "Low", shift = "Morning",
            assigneeIds = new[] { finisher.Id, helper.Id },
        });
        var id = (await r.JsonAsync()).GetProperty("id").GetGuid();
        var marker = Guid.NewGuid().ToString("N")[..8];
        using var form = TestData.Card("Completed", $"Used drum {marker}");
        await finisher.Client.PostAsync($"/api/tasks/{id}/updates", form);

        async Task<List<Guid>> Search(string q)
        {
            var body = await (await helper.Client.GetAsync($"/api/tasks/history?pageSize=100&q={Uri.EscapeDataString(q)}")).JsonAsync();
            return body.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        }

        (await Search(finisher.Name.ToUpperInvariant())).ShouldContain(id); // who completed it
        (await Search(helper.Name)).ShouldContain(id);                      // an assignee
        (await Search(marker)).ShouldBe([id]);                              // the comment
    }
}
