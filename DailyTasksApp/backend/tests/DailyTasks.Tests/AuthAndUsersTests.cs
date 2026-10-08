using System.Net;
using System.Net.Http.Json;
using DailyTasks.Tests.Infrastructure;
using Shouldly;

namespace DailyTasks.Tests;

public class AuthAndUsersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Bootstrap_manager_can_log_in_with_any_name_casing()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/login",
            new { name = "  test MANAGER ", password = ApiFactory.ManagerPassword });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body.GetProperty("user").GetProperty("role").GetString().ShouldBe("Manager");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_name_both_give_401_invalidCredentials()
    {
        var client = api.Anonymous();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { name = ApiFactory.ManagerName, password = "nope-nope" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { name = "Nobody Here", password = "nope-nope" });

        foreach (var r in new[] { wrong, unknown })
        {
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await r.JsonAsync()).GetProperty("code").GetString().ShouldBe("invalidCredentials");
        }
    }

    [Fact]
    public async Task Requests_without_a_token_get_401()
    {
        var client = api.Anonymous();
        (await client.GetAsync("/api/tasks")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/orders")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Engineer_cannot_list_or_create_users()
    {
        var (_, _, engineer) = await api.NewUserAsync("Engineer");

        (await engineer.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await engineer.PostAsJsonAsync("/api/users", new { name = "Harry Wilson", password = "Password123", role = "Engineer" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_adds_users_and_sees_them_in_the_list()
    {
        var (id, name, _) = await api.NewUserAsync("Engineer", "Amelia");
        var manager = await api.ManagerAsync();

        var list = await (await manager.GetAsync("/api/users")).JsonAsync();
        list.EnumerateArray().ShouldContain(u => u.GetProperty("id").GetGuid() == id && u.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_regardless_of_casing()
    {
        var (_, name, _) = await api.NewUserAsync("Engineer", "George");
        var manager = await api.ManagerAsync();

        var response = await manager.PostAsJsonAsync("/api/users",
            new { name = name.ToUpperInvariant(), password = "Password123", role = "Manager" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("code").GetString().ShouldBe("user.nameTaken");
    }

    [Fact]
    public async Task User_changes_own_password_with_the_current_one()
    {
        var (_, name, engineer) = await api.NewUserAsync("Engineer", "Mia");

        var wrong = await engineer.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "not-my-password", newPassword = "BrandNew123" });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.JsonAsync()).GetProperty("errors").GetProperty("currentPassword")[0].GetString().ShouldBe("wrongPassword");

        var tooShort = await engineer.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "UserPass123", newPassword = "short" });
        (await tooShort.JsonAsync()).GetProperty("errors").GetProperty("newPassword")[0].GetString().ShouldBe("tooShort");

        var ok = await engineer.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "UserPass123", newPassword = "BrandNew123" });
        ok.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var anon = api.Anonymous();
        (await anon.PostAsJsonAsync("/api/auth/login", new { name, password = "UserPass123" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anon.PostAsJsonAsync("/api/auth/login", new { name, password = "BrandNew123" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Change_password_requires_login()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "x", newPassword = "BrandNew123" });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_user_validates_name_password_and_role()
    {
        var manager = await api.ManagerAsync();

        var response = await manager.PostAsJsonAsync("/api/users", new { name = "A", password = "short" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        errors.GetProperty("name")[0].GetString().ShouldBe("tooShort");
        errors.GetProperty("password")[0].GetString().ShouldBe("tooShort");
        errors.GetProperty("role")[0].GetString().ShouldBe("required");
    }
}
