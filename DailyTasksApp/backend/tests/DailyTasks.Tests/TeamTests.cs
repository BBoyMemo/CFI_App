using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyTasks.Tests.Infrastructure;
using Shouldly;

namespace DailyTasks.Tests;

// Editing and deactivating team members, password resets and renaming oneself.
public class TeamTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string UserPassword = "UserPass123";
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    private string Today() => DateOnly
        .FromDateTime(TimeZoneInfo.ConvertTime(api.Clock.Now, London).DateTime).ToString("yyyy-MM-dd");

    private async Task<HttpResponseMessage> CreateTaskAsync(params Guid[] assignees)
    {
        var manager = await api.ManagerAsync();
        return await manager.PostAsJsonAsync("/api/tasks", new
        {
            title = "Check boiler pressure", date = Today(), priority = "Low", shift = "Morning", assigneeIds = assignees,
        });
    }

    private static async Task<JsonElement> UserAsync(HttpClient manager, Guid id) =>
        (await (await manager.GetAsync("/api/users")).JsonAsync())
        .EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == id);

    private async Task<HttpResponseMessage> LoginAsync(string name, string password) =>
        await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { name, password });

    [Fact]
    public async Task Manager_gives_a_new_password_and_the_user_must_change_it_before_anything_else()
    {
        var engineer = await api.NewUserAsync("Engineer", "Harriet");
        var manager = await api.ManagerAsync();
        await api.Anonymous().PostAsJsonAsync("/api/auth/forgot-password", new { name = engineer.Name });

        var edited = await manager.PutAsJsonAsync($"/api/users/{engineer.Id}",
            new { name = engineer.Name, role = "Engineer", password = "TempPass123" });
        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await edited.JsonAsync()).GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        (await LoginAsync(engineer.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var login = await LoginAsync(engineer.Name, "TempPass123");
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await login.JsonAsync();
        session.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        var client = api.Anonymous();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
        var blocked = await client.GetAsync($"/api/tasks?date={Today()}");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.JsonAsync()).GetProperty("code").GetString().ShouldBe("auth.mustChangePassword");
        (await client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var changed = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "TempPass123", newPassword = "MyOwnPass456" });
        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/tasks?date={Today()}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UserAsync(manager, engineer.Id)).GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Edit_renames_and_changes_role_without_touching_the_password()
    {
        var engineer = await api.NewUserAsync("Engineer", "Rupert");
        var manager = await api.ManagerAsync();
        var newName = $"Rupert Hale {Guid.NewGuid().ToString("N")[..6]}";

        var edited = await manager.PutAsJsonAsync($"/api/users/{engineer.Id}", new { name = newName, role = "Manager", password = "" });
        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await edited.JsonAsync();
        body.GetProperty("role").GetString().ShouldBe("Manager");
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();

        // The old session carries the old role, so it ends; the new name logs in with the old password.
        (await engineer.Client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(newName, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_is_validated_and_never_applies_to_oneself()
    {
        var a = await api.NewUserAsync("Engineer", "Imogen");
        var b = await api.NewUserAsync("Engineer", "Jasper");
        var manager = await api.ManagerAsync();
        var me = (await (await manager.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("id").GetGuid();

        var self = await manager.PutAsJsonAsync($"/api/users/{me}", new { name = "Someone", role = "Engineer" });
        self.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await self.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.self");

        var taken = await manager.PutAsJsonAsync($"/api/users/{a.Id}", new { name = b.Name.ToUpperInvariant(), role = "Engineer" });
        taken.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await taken.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.nameTaken");

        var shortPassword = await manager.PutAsJsonAsync($"/api/users/{a.Id}", new { name = a.Name, role = "Engineer", password = "short" });
        shortPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Without a "Forgot password?" request a manager cannot set a password.
        var unasked = await manager.PutAsJsonAsync($"/api/users/{a.Id}", new { name = a.Name, role = "Engineer", password = "LongEnough1" });
        unasked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await unasked.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.noResetRequest");
        (await LoginAsync(a.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await a.Client.PutAsJsonAsync($"/api/users/{b.Id}", new { name = b.Name, role = "Engineer" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_but_stays_in_history()
    {
        var engineer = await api.NewUserAsync("Engineer", "Wilfred");
        var manager = await api.ManagerAsync();
        var taskId = (await (await CreateTaskAsync(engineer.Id)).JsonAsync()).GetProperty("id").GetGuid();

        // An open task must go to someone else first.
        var blocked = await manager.PostAsync($"/api/users/{engineer.Id}/deactivate", null);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.hasOpenTasks");

        (await engineer.Client.PostAsync($"/api/tasks/{taskId}/updates", TestData.Card("Completed", "Done")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var deactivated = await manager.PostAsync($"/api/users/{engineer.Id}/deactivate", null);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deactivated.JsonAsync()).GetProperty("active").GetBoolean().ShouldBeFalse();

        // The open session ends at once, and logging in again fails like a wrong password.
        (await engineer.Client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var login = await LoginAsync(engineer.Name, UserPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.JsonAsync()).GetProperty("code").GetString().ShouldBe("invalidCredentials");

        // Still listed (as deactivated) and still named on the work they did; no longer assignable.
        (await UserAsync(manager, engineer.Id)).GetProperty("active").GetBoolean().ShouldBeFalse();
        var task = await (await manager.GetAsync($"/api/tasks/{taskId}")).JsonAsync();
        task.GetProperty("completedBy").GetProperty("name").GetString().ShouldBe(engineer.Name);
        var assign = await CreateTaskAsync(engineer.Id);
        assign.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await assign.JsonAsync()).GetProperty("errors").GetProperty("assigneeIds")[0].GetString().ShouldBe("unknownUser");

        // A deactivated user cannot be edited.
        (await manager.PutAsJsonAsync($"/api/users/{engineer.Id}", new { name = engineer.Name, role = "Engineer" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_deactivated_user_can_be_activated_again_and_logs_in_with_the_old_password()
    {
        var engineer = await api.NewUserAsync("Engineer", "Edmund");
        var manager = await api.ManagerAsync();
        await manager.PostAsync($"/api/users/{engineer.Id}/deactivate", null);
        (await LoginAsync(engineer.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await engineer.Client.PostAsync($"/api/users/{engineer.Id}/activate", null))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // their session ended with the deactivation
        var activated = await manager.PostAsync($"/api/users/{engineer.Id}/activate", null);
        activated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await activated.JsonAsync()).GetProperty("active").GetBoolean().ShouldBeTrue();

        (await LoginAsync(engineer.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CreateTaskAsync(engineer.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var other = await api.NewUserAsync("Engineer", "Mildred");
        (await other.Client.PostAsync($"/api/users/{engineer.Id}/activate", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Managers_deactivate_other_managers_but_never_themselves()
    {
        var otherManager = await api.NewUserAsync("Manager", "Cecily");
        var engineer = await api.NewUserAsync("Engineer", "Barnaby");
        var manager = await api.ManagerAsync();
        var me = (await (await manager.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("id").GetGuid();

        var self = await manager.PostAsync($"/api/users/{me}/deactivate", null);
        self.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await self.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.self");

        (await engineer.Client.PostAsync($"/api/users/{otherManager.Id}/deactivate", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await manager.PostAsync($"/api/users/{otherManager.Id}/deactivate", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forgot_password_flags_the_user_for_managers_without_revealing_names()
    {
        var engineer = await api.NewUserAsync("Engineer", "Dorothy");
        var manager = await api.ManagerAsync();
        var anonymous = api.Anonymous();

        (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { name = engineer.Name.ToLowerInvariant() }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { name = "Nobody At All" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UserAsync(manager, engineer.Id)).GetProperty("resetRequested").GetBoolean().ShouldBeTrue();

        // Asking does not change anything for the user: the old password still works.
        (await LoginAsync(engineer.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var reset = await manager.PutAsJsonAsync($"/api/users/{engineer.Id}",
            new { name = engineer.Name, role = "Engineer", password = "ResetPass789" });
        var body = await reset.JsonAsync();
        body.GetProperty("resetRequested").GetBoolean().ShouldBeFalse();
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Users_rename_themselves_with_their_password()
    {
        var engineer = await api.NewUserAsync("Engineer", "Florence");
        var other = await api.NewUserAsync("Engineer", "Gilbert");
        var newName = $"Florence Ward {Guid.NewGuid().ToString("N")[..6]}";

        var wrong = await engineer.Client.PostAsJsonAsync("/api/auth/change-name", new { currentPassword = "nope-nope", newName });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.JsonAsync()).GetProperty("errors").GetProperty("currentPassword")[0].GetString().ShouldBe("wrongPassword");

        var taken = await engineer.Client.PostAsJsonAsync("/api/auth/change-name", new { currentPassword = UserPassword, newName = other.Name });
        taken.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var renamed = await engineer.Client.PostAsJsonAsync("/api/auth/change-name", new { currentPassword = UserPassword, newName });
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await renamed.JsonAsync();
        session.GetProperty("user").GetProperty("name").GetString().ShouldBe(newName);
        session.GetProperty("token").GetString().ShouldNotBeNullOrEmpty();

        (await LoginAsync(engineer.Name, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(newName, UserPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
