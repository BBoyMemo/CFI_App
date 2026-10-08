using System.Net.Http.Json;
using System.Text.Json;
using DailyTasks.Api.Localization;
using DailyTasks.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DailyTasks.Tests;

// Own factory: these tests switch the fake translator off and make it fail.
public class TranslationTests(ApiFactory api) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static string Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime)
        .ToString("yyyy-MM-dd");

    public async Task InitializeAsync()
    {
        api.Translator.IsEnabled = true;
        api.Translator.FailWith = null;
        // Start every test with an empty queue.
        while (await api.ProcessTranslationsAsync() > 0) { }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Manager, (Guid Id, string Name, HttpClient Client) Engineer)> PeopleAsync() =>
        (await api.ManagerAsync(), await api.NewUserAsync("Engineer", "Ivy"));

    private static async Task<JsonElement> CreateTaskAsync(HttpClient manager, Guid engineer, string title, string? description)
    {
        var r = await manager.PostAsJsonAsync("/api/tasks", new
        {
            title, description, date = Today, priority = "Low", shift = "Morning", assigneeIds = new[] { engineer },
        });
        r.EnsureSuccessStatusCode();
        return await r.JsonAsync();
    }

    private static async Task<JsonElement> GetTaskAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/tasks/{id}")).JsonAsync();

    private static string? Text(JsonElement dto, string language, string field) =>
        dto.GetProperty("translations").TryGetProperty(language, out var lang) && lang.TryGetProperty(field, out var value)
            ? value.GetString()
            : null;

    [Fact]
    public async Task Task_texts_are_kept_in_every_language_after_processing()
    {
        var (manager, engineer) = await PeopleAsync();
        var created = await CreateTaskAsync(manager, engineer.Id, "Replace pump seal", "Spare seal on shelf B2");
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("translations").EnumerateObject().ShouldBeEmpty(); // not translated yet

        (await api.ProcessTranslationsAsync()).ShouldBe(1);

        var task = await GetTaskAsync(engineer.Client, id);
        task.GetProperty("title").GetString().ShouldBe("Replace pump seal"); // the original is untouched
        Text(task, "en", "title").ShouldBe("Replace pump seal");             // English source stays as written
        Text(task, "pl", "title").ShouldBe("<pl> Replace pump seal");
        Text(task, "bg", "description").ShouldBe("<bg> Spare seal on shelf B2");
        Text(task, "es", "description").ShouldBe("<es> Spare seal on shelf B2");
    }

    [Fact]
    public async Task Text_written_in_another_language_is_not_sent_back_into_it()
    {
        var (manager, engineer) = await PeopleAsync();
        api.Translator.Calls.Clear();
        var id = (await CreateTaskAsync(manager, engineer.Id, "[pl] Wymień filtr", null)).GetProperty("id").GetGuid();

        await api.ProcessTranslationsAsync();

        var task = await GetTaskAsync(manager, id);
        Text(task, "pl", "title").ShouldBe("[pl] Wymień filtr");
        Text(task, "en", "title").ShouldBe("<en> [pl] Wymień filtr");
        Text(task, "es", "title").ShouldBe("<es> [pl] Wymień filtr");
        api.Translator.Calls.ShouldNotContain(c => c.Language == "pl");
    }

    [Fact]
    public async Task Edited_text_hides_the_old_translation_until_it_is_translated_again()
    {
        var (manager, engineer) = await PeopleAsync();
        var id = (await CreateTaskAsync(manager, engineer.Id, "Grease chain", "Use EP2 grease")).GetProperty("id").GetGuid();
        await api.ProcessTranslationsAsync();

        var edit = await manager.PutAsJsonAsync($"/api/tasks/{id}", new
        {
            title = "Grease chain and sprockets", description = "Use EP2 grease", date = Today,
            priority = "Low", shift = "Morning", assigneeIds = new[] { engineer.Id },
        });
        var edited = await edit.JsonAsync();
        Text(edited, "pl", "title").ShouldBeNull();                             // stale: not shown
        Text(edited, "pl", "description").ShouldBe("<pl> Use EP2 grease");      // unchanged: still shown

        await api.ProcessTranslationsAsync();
        Text(await GetTaskAsync(manager, id), "pl", "title").ShouldBe("<pl> Grease chain and sprockets");
    }

    [Fact]
    public async Task Completion_comment_and_order_description_are_translated_and_searchable()
    {
        var (manager, engineer) = await PeopleAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var id = (await CreateTaskAsync(manager, engineer.Id, "Check guard", null)).GetProperty("id").GetGuid();
        using (var form = new MultipartFormDataContent { { new StringContent($"Guard bolt {marker} replaced"), "comment" } })
            await engineer.Client.PostAsync($"/api/tasks/{id}/complete", form);
        using (var form = new MultipartFormDataContent { { new StringContent($"Bolts M8 {marker}"), "description" } })
            await engineer.Client.PostAsync("/api/orders", form);

        while (await api.ProcessTranslationsAsync() > 0) { }

        Text(await GetTaskAsync(manager, id), "bg", "comment").ShouldBe($"<bg> Guard bolt {marker} replaced");

        // A search typed in the reader's language finds the record through its translation.
        var history = await (await engineer.Client.GetAsync($"/api/tasks/history?pageSize=100&q=%3Ces%3E%20guard%20bolt%20{marker}")).JsonAsync();
        history.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ShouldContain(id);

        var orders = await (await engineer.Client.GetAsync($"/api/orders?pageSize=100&q=%3Cpl%3E%20bolts%20m8%20{marker}")).JsonAsync();
        var order = orders.GetProperty("items").EnumerateArray().Single();
        Text(order, "pl", "description").ShouldBe($"<pl> Bolts M8 {marker}");
    }

    [Fact]
    public async Task Quota_exhaustion_postpones_the_work_and_nothing_breaks()
    {
        var (manager, engineer) = await PeopleAsync();
        api.Translator.FailWith = new TranslationQuotaExceededException("quota");
        var id = (await CreateTaskAsync(manager, engineer.Id, "Clean drains", null)).GetProperty("id").GetGuid();

        (await api.ProcessTranslationsAsync()).ShouldBe(0);
        (await GetTaskAsync(manager, id)).GetProperty("translations").EnumerateObject().ShouldBeEmpty();

        // Quota back next month: once the wait is over, the waiting job goes through.
        api.Translator.FailWith = null;
        (await api.ProcessTranslationsAsync()).ShouldBe(0); // still waiting
        api.Clock.Now = api.Clock.Now.AddHours(7);
        (await api.ProcessTranslationsAsync()).ShouldBe(1);
        Text(await GetTaskAsync(manager, id), "es", "title").ShouldBe("<es> Clean drains");
    }

    [Fact]
    public async Task Records_written_before_the_key_existed_are_picked_up_once_it_does()
    {
        var (manager, engineer) = await PeopleAsync();
        api.Translator.IsEnabled = false;
        var id = (await CreateTaskAsync(manager, engineer.Id, "Tighten guard bolts", null)).GetProperty("id").GetGuid();
        api.Translator.IsEnabled = true;

        using (var scope = api.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<TranslationProcessor>();
            (await processor.EnqueueMissingAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);
            (await processor.EnqueueMissingAsync(CancellationToken.None)).ShouldBe(0); // nothing queued twice
        }
        while (await api.ProcessTranslationsAsync() > 0) { }

        Text(await GetTaskAsync(manager, id), "pl", "title").ShouldBe("<pl> Tighten guard bolts");
    }

    [Fact]
    public async Task Without_a_key_nothing_is_queued_or_sent()
    {
        var (manager, engineer) = await PeopleAsync();
        api.Translator.IsEnabled = false;
        api.Translator.Calls.Clear();
        var id = (await CreateTaskAsync(manager, engineer.Id, "Inspect belts", null)).GetProperty("id").GetGuid();

        api.Translator.IsEnabled = true;
        (await api.ProcessTranslationsAsync()).ShouldBe(0);
        api.Translator.Calls.ShouldBeEmpty();
        (await GetTaskAsync(manager, id)).GetProperty("translations").EnumerateObject().ShouldBeEmpty();
    }
}
