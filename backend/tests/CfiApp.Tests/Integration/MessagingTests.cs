using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Application.Messaging;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Messaging;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Messages are addressed to a person or a department, stay in the app whether or not the
/// push arrives, and department membership is fixed at send time - someone who joins that
/// department afterwards must not see it appear retroactively.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MessagingTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"msg{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, int UserId)> SignedInAsAsync(string roleName, string? departmentName = null)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test User", email, null, Password, "en"));

        int userId;

        await using (var scope = factory.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var user = await context.Users.SingleAsync(x => x.Email == email);
            var role = await context.Roles.SingleAsync(x => x.Name == roleName);
            user.RoleId = role.Id;
            user.Status = UserStatus.Active;

            if (departmentName is not null)
            {
                user.DepartmentId = (await context.Departments.FirstAsync(x => x.Name == departmentName)).Id;
            }

            await context.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return (client, userId);
    }

    [Fact]
    public async Task A_message_addressed_to_one_person_reaches_only_that_persons_inbox()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (recipientClient, recipientId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (bystanderClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Tomorrow we start at 07:00.", MessagePriority.Normal, null, [recipientId], []));
        send.StatusCode.ShouldBe(HttpStatusCode.Created);

        var recipientInbox = await recipientClient.GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        recipientInbox!.Items.ShouldContain(x => x.Body.Contains("07:00"));

        var bystanderInbox = await bystanderClient.GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        bystanderInbox!.Items.ShouldNotContain(x => x.Body.Contains("07:00"));
    }

    [Fact]
    public async Task A_message_addressed_to_a_department_reaches_everyone_in_it_at_send_time()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (productionWorkerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator, "Production");
        var (maintenanceWorkerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer, "Maintenance");

        var departments = await managerClient.GetFromJsonAsync<PagedResult<DepartmentDto>>("/api/v1/admin/departments");
        var productionId = departments!.Items.First(x => x.Name == "Production").Id;

        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Production department safety briefing at 09:00.", MessagePriority.High, null, [], [productionId]));
        send.StatusCode.ShouldBe(HttpStatusCode.Created);

        var productionInbox = await productionWorkerClient
            .GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        productionInbox!.Items.ShouldContain(x => x.Body.Contains("safety briefing"));

        var maintenanceInbox = await maintenanceWorkerClient
            .GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        maintenanceInbox!.Items.ShouldNotContain(x => x.Body.Contains("safety briefing"));
    }

    [Fact]
    public async Task Someone_who_joins_the_department_after_the_message_was_sent_does_not_see_it()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (lateJoinerClient, lateJoinerId) = await SignedInAsAsync(Permissions.Roles.Operator, departmentName: null);

        var departments = await managerClient.GetFromJsonAsync<PagedResult<DepartmentDto>>("/api/v1/admin/departments");
        var productionId = departments!.Items.First(x => x.Name == "Production").Id;

        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Sent before this person joined Production.", MessagePriority.Normal, null, [], [productionId]));
        send.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Joins the department only now, after the message already went out.
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var user = await context.Users.FirstAsync(x => x.Id == lateJoinerId);
            user.DepartmentId = productionId;
            await context.SaveChangesAsync();
        }

        var inbox = await lateJoinerClient.GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");

        inbox!.Items.ShouldNotContain(x => x.Body.Contains("joined Production"),
            "department membership is resolved once, at send time");
    }

    [Fact]
    public async Task Opening_a_message_marks_it_read_and_the_sender_can_see_the_read_receipt()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (recipientClient, recipientId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Please confirm you have seen this.", MessagePriority.Normal, null, [recipientId], []));
        var sent = (await send.Content.ReadFromJsonAsync<MessageSummaryDto>())!;

        var beforeReading = await recipientClient
            .GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        beforeReading!.Items.Single(x => x.Id == sent.Id).IsRead.ShouldBeFalse();

        var openResponse = await recipientClient.GetAsync($"/api/v1/messages/{sent.Id}");
        openResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterReading = await recipientClient
            .GetFromJsonAsync<PagedResult<MessageSummaryDto>>("/api/v1/messages/inbox");
        afterReading!.Items.Single(x => x.Id == sent.Id).IsRead.ShouldBeTrue();

        var receipts = await managerClient
            .GetFromJsonAsync<List<ReadReceiptDto>>($"/api/v1/messages/{sent.Id}/read-receipts");
        receipts!.ShouldContain(x => x.UserId == recipientId && x.ReadAt != null);
    }

    [Fact]
    public async Task Someone_the_message_was_not_addressed_to_cannot_open_it()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (recipientClient, recipientId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (unrelatedClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        _ = recipientClient;

        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Private note.", MessagePriority.Normal, null, [recipientId], []));
        var sent = (await send.Content.ReadFromJsonAsync<MessageSummaryDto>())!;

        var response = await unrelatedClient.GetAsync($"/api/v1/messages/{sent.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_operator_cannot_send_a_message()
    {
        var (operatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (_, targetId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Should not be allowed.", MessagePriority.Normal, null, [targetId], []));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_message_addressed_to_nobody_is_rejected_before_it_reaches_the_database()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var response = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Addressed to nobody.", MessagePriority.Normal, null, [], []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registering_a_device_token_lets_the_system_write_notification_history_without_error()
    {
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var register = await workerClient.PutAsJsonAsync("/api/v1/me/device-tokens",
            new RegisterDeviceTokenRequest($"test-token-{_counter}", DevicePlatform.Android));
        register.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A push attempt is logged even though no real provider is configured yet (Phase 8
        // ships the mechanism; Firebase credentials are a later, external dependency).
        var send = await managerClient.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest(
            "Push delivery smoke test.", MessagePriority.Normal, null, [workerId], []));
        send.StatusCode.ShouldBe(HttpStatusCode.Created);

        var revoke = await workerClient.PostAsJsonAsync("/api/v1/me/device-tokens/revoke", $"test-token-{_counter}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
