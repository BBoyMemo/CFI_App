using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Auth;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// The whole onboarding and sign in path, exercised through HTTP the way the web and
/// mobile clients will use it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthFlowTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"worker{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", email, "07000 000000", Password, "en"));

    private async Task<int> ApproveAsync(string email, string roleName)
    {
        await using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var user = await context.Users.SingleAsync(x => x.Email == email);
        var role = await context.Roles.SingleAsync(x => x.Name == roleName);

        user.RoleId = role.Id;
        user.Status = UserStatus.Active;
        user.ApprovedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task RejectAsync(string email)
    {
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var user = await context.Users.SingleAsync(x => x.Email == email);

        user.Status = UserStatus.Rejected;
        user.RejectedAt = DateTimeOffset.UtcNow;
        user.RejectionReason = "Duplicate registration.";
        await context.SaveChangesAsync();
    }

    private async Task<AuthTokens> SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password, "test-device"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    private static void Authorise(HttpClient client, AuthTokens tokens) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

    [Fact]
    public async Task A_new_registration_cannot_sign_in_until_a_manager_approves_it()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var registration = await RegisterAsync(client, email);
        registration.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password, null));

        // Authenticated correctly but the account is not usable yet: 403, not 401.
        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await ApproveAsync(email, Permissions.Roles.Operator);

        var afterApproval = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password, null));

        afterApproval.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_rejected_registration_cannot_sign_in()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var registration = await RegisterAsync(client, email);
        registration.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await RejectAsync(email);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password, null));

        // Authenticated correctly but the account was never approved: 403, not 401 and
        // absolutely not 200 - a rejected registration handing out a valid session was
        // exactly the bug this test exists to catch.
        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_same_email_cannot_register_twice()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        (await RegisterAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await RegisterAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_account_give_the_same_answer()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, "not-the-password", null));

        var unknownAccount = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(NewEmail(), Password, null));

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownAccount.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var first = await wrongPassword.Content.ReadAsStringAsync();
        var second = await unknownAccount.Content.ReadAsStringAsync();
        first.ShouldContain("Email or password is incorrect");
        second.ShouldContain("Email or password is incorrect");
    }

    [Fact]
    public async Task An_access_token_carries_the_permissions_of_the_role()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Engineer);

        var tokens = await SignInAsync(client, email);
        Authorise(client, tokens);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");

        me.ShouldNotBeNull();
        me.Role.ShouldBe(Permissions.Roles.Engineer);
        me.Permissions.ShouldContain(Permissions.WorkOrderClaim);
        me.Permissions.ShouldNotContain(Permissions.AdminManage);
    }

    [Fact]
    public async Task Without_a_token_a_protected_endpoint_answers_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task With_a_token_but_without_the_permission_the_answer_is_403_not_401()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        Authorise(client, await SignInAsync(client, email));

        var response = await client.GetAsync("/api/v1/users/pending");

        // This distinction is the whole reason the old system logged people out at random.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_can_read_the_pending_list()
    {
        var client = factory.CreateClient();
        var managerEmail = NewEmail();
        await RegisterAsync(client, managerEmail);
        await ApproveAsync(managerEmail, Permissions.Roles.MaintenanceManager);

        Authorise(client, await SignInAsync(client, managerEmail));

        var response = await client.GetAsync("/api/v1/users/pending?page=1&pageSize=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refreshing_rotates_the_token_and_the_old_one_stops_working()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        var original = await SignInAsync(client, email);

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(original.RefreshToken, "test-device"));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AuthTokens>())!;
        rotated.RefreshToken.ShouldNotBe(original.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(original.RefreshToken, "test-device"));

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Replaying_an_old_refresh_token_kills_every_session_of_that_user()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        var original = await SignInAsync(client, email);

        var rotated = (await (await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(original.RefreshToken, null))).Content.ReadFromJsonAsync<AuthTokens>())!;

        // Someone replays the stolen, already rotated token.
        await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(original.RefreshToken, null));

        // The legitimate device is signed out too - that is the point: a stolen chain is
        // not left running just because the thief was noticed.
        var afterBreach = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(rotated.RefreshToken, null));

        afterBreach.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signing_out_invalidates_the_refresh_token()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        var tokens = await SignInAsync(client, email);

        (await client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshRequest(tokens.RefreshToken, null))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(tokens.RefreshToken, null));

        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_signs_other_devices_out()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        var phone = await SignInAsync(client, email);
        var desktop = await SignInAsync(client, email);

        Authorise(client, desktop);

        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest(Password, "New-Widnes-Password-2027!"));

        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var phoneRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(phone.RefreshToken, null));

        phoneRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_disabled_account_stops_working_immediately()
    {
        var client = factory.CreateClient();

        var managerEmail = NewEmail();
        await RegisterAsync(client, managerEmail);
        await ApproveAsync(managerEmail, Permissions.Roles.MaintenanceManager);

        var workerEmail = NewEmail();
        await RegisterAsync(client, workerEmail);
        var workerId = await ApproveAsync(workerEmail, Permissions.Roles.Operator);

        var workerTokens = await SignInAsync(client, workerEmail);

        Authorise(client, await SignInAsync(client, managerEmail));
        var disable = await client.PostAsync($"/api/v1/users/{workerId}/disable", null);
        disable.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(workerTokens.RefreshToken, null));

        refresh.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approval_assigns_the_role_department_and_work_units()
    {
        var client = factory.CreateClient();

        // A Production Manager, because the new starter below is an operator and operators
        // are approved by production, not by maintenance.
        var managerEmail = NewEmail();
        await RegisterAsync(client, managerEmail);
        await ApproveAsync(managerEmail, Permissions.Roles.ProductionManager);

        var newStarterEmail = NewEmail();
        await RegisterAsync(client, newStarterEmail);

        int newStarterId, roleId, areaId;

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            newStarterId = (await context.Users.SingleAsync(x => x.Email == newStarterEmail)).Id;
            roleId = (await context.Roles.SingleAsync(x => x.Name == Permissions.Roles.Operator)).Id;
            areaId = (await context.Areas.FirstAsync(x => x.Code == "FILLING")).Id;
        }

        Authorise(client, await SignInAsync(client, managerEmail));

        var approve = await client.PostAsJsonAsync($"/api/v1/users/{newStarterId}/approve",
            new ApproveUserRequest(roleId, null, [areaId]));

        approve.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Approving twice is a conflict, not a silent second approval.
        var again = await client.PostAsJsonAsync($"/api/v1/users/{newStarterId}/approve",
            new ApproveUserRequest(roleId, null, [areaId]));

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        client.DefaultRequestHeaders.Authorization = null;
        Authorise(client, await SignInAsync(client, newStarterEmail));

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        me.ShouldNotBeNull();
        me.Status.ShouldBe(nameof(UserStatus.Active));
        // Nobody chose Production on the form: it comes from being an operator.
        me.Department.ShouldBe("Production");
        me.AreaIds.ShouldContain(areaId);
    }

    [Fact]
    public async Task A_manager_can_reject_a_pending_registration()
    {
        var client = factory.CreateClient();

        var managerEmail = NewEmail();
        await RegisterAsync(client, managerEmail);
        await ApproveAsync(managerEmail, Permissions.Roles.MaintenanceManager);

        var newStarterEmail = NewEmail();
        await RegisterAsync(client, newStarterEmail);

        int newStarterId;
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            newStarterId = (await context.Users.SingleAsync(x => x.Email == newStarterEmail)).Id;
        }

        Authorise(client, await SignInAsync(client, managerEmail));

        var reject = await client.PostAsJsonAsync($"/api/v1/users/{newStarterId}/reject",
            new RejectPendingUserRequest("Duplicate registration."));

        reject.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Rejecting twice is a conflict, not a silent second rejection.
        var again = await client.PostAsJsonAsync($"/api/v1/users/{newStarterId}/reject",
            new RejectPendingUserRequest("again"));

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var pending = await client.GetFromJsonAsync<PagedResult<PendingUserResponse>>(
            "/api/v1/users/pending?page=1&pageSize=50");
        pending!.Items.ShouldNotContain(x => x.Id == newStarterId);

        // And the rejected starter, who never got tokens in the first place, still can't get any.
        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(newStarterEmail, Password, null));

        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Language_is_stored_on_the_account_so_it_follows_the_person()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        await ApproveAsync(email, Permissions.Roles.Operator);

        Authorise(client, await SignInAsync(client, email));

        var set = await client.PutAsJsonAsync("/api/v1/auth/me/language", "pl");
        set.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        me!.PreferredLanguage.ShouldBe("pl");

        var unsupported = await client.PutAsJsonAsync("/api/v1/auth/me/language", "de");
        unsupported.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
