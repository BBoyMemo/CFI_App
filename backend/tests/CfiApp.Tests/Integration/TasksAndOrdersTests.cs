using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Auth;
using CfiApp.Application.Work;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Work;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Tasks and part orders follow the same permission and visibility desiсn as the
/// maintenance module, just with a smaller rule set - this is what proves the pattern
/// generalises rather than being a one-off for work orders.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TasksAndOrdersTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"task{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, int UserId)> SignedInAsAsync(string roleName)
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
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return (client, userId);
    }

    // ---------------------------------------------------------------- tasks

    [Fact]
    public async Task An_engineer_cannot_create_a_task_only_the_maintenance_manager_can()
    {
        var (engineerClient, engineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var response = await engineerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Grease conveyor bearings", null, TaskKind.Daily, DateOnly.FromDateTime(DateTime.UtcNow),
            null, [engineerId]));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_task_can_be_assigned_to_more_than_one_engineer()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, firstEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (_, secondEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var response = await managerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Check dust collector filters", "Weekly inspection", TaskKind.Weekend,
            DateOnly.FromDateTime(DateTime.UtcNow), TaskPriority.Preventive, [firstEngineerId, secondEngineerId]));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<TaskDetailDto>())!;
        created.Assignees.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_open_task_is_only_visible_to_the_people_it_is_assigned_to()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (assignedClient, assignedId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (unrelatedClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await managerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Inspect boiler room", null, TaskKind.Daily, DateOnly.FromDateTime(DateTime.UtcNow), null, [assignedId]));
        var task = (await create.Content.ReadFromJsonAsync<TaskDetailDto>())!;

        (await assignedClient.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await unrelatedClient.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var mine = await assignedClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks/mine");
        mine!.Items.ShouldContain(x => x.Id == task.Id);

        var unrelatedMine = await unrelatedClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks/mine");
        unrelatedMine!.Items.ShouldNotContain(x => x.Id == task.Id);
    }

    [Fact]
    public async Task Once_completed_a_task_becomes_visible_to_every_engineer_not_just_the_assignee()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (assignedClient, assignedId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (unrelatedClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await managerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Clean the melt room floor drains", null, TaskKind.Daily,
            DateOnly.FromDateTime(DateTime.UtcNow), TaskPriority.Reactive, [assignedId]));
        var task = (await create.Content.ReadFromJsonAsync<TaskDetailDto>())!;

        (await unrelatedClient.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var complete = await assignedClient.PostAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/complete", new CompleteTaskRequest("Cleared and rinsed.", null));
        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await unrelatedClient.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var completed = await unrelatedClient.GetFromJsonAsync<PagedResult<TaskSummaryDto>>("/api/v1/tasks/completed");
        completed!.Items.ShouldContain(x => x.Id == task.Id);
    }

    [Fact]
    public async Task Someone_not_assigned_cannot_complete_a_task()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (assignedClient, assignedId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (unrelatedClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        _ = assignedClient;

        var create = await managerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Lubricate line 2 rollers", null, TaskKind.Daily, DateOnly.FromDateTime(DateTime.UtcNow), null, [assignedId]));
        var task = (await create.Content.ReadFromJsonAsync<TaskDetailDto>())!;

        var response = await unrelatedClient.PostAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/complete", new CompleteTaskRequest("Done.", null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_manager_can_reassign_a_task_and_delete_it_without_losing_its_history()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, firstEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (secondEngineerClient, secondEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await managerClient.PostAsJsonAsync("/api/v1/tasks", new CreateTaskRequest(
            "Test the emergency stop on line 1", null, TaskKind.ManagerAssigned,
            DateOnly.FromDateTime(DateTime.UtcNow), null, [firstEngineerId]));
        var task = (await create.Content.ReadFromJsonAsync<TaskDetailDto>())!;

        var reassign = await managerClient.PutAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/assignees", new ReassignTaskRequest([secondEngineerId]));
        reassign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var completeAsNewAssignee = await secondEngineerClient.PostAsJsonAsync(
            $"/api/v1/tasks/{task.Id}/complete", new CompleteTaskRequest("Tested, works correctly.", null));
        completeAsNewAssignee.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var delete = await managerClient.DeleteAsync($"/api/v1/tasks/{task.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await managerClient.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stillThere = await context.MaintenanceTasks.IgnoreQueryFilters()
            .Include(x => x.Completions)
            .FirstAsync(x => x.Id == task.Id);
        stillThere.DeletedAt.ShouldNotBeNull("soft deleted, not gone - the completion record must survive");
        stillThere.Completions.ShouldHaveSingleItem();
    }

    // ---------------------------------------------------------------- orders

    [Fact]
    public async Task An_engineer_sees_only_their_own_order_requests()
    {
        var (firstEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (secondEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await firstEngineerClient.PostAsJsonAsync(
            "/api/v1/orders", new CreatePartOrderRequest("Drive belt", 2, false));
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (await create.Content.ReadFromJsonAsync<PartOrderRequestDto>())!;

        var ownList = await firstEngineerClient.GetFromJsonAsync<PagedResult<PartOrderRequestDto>>("/api/v1/orders/mine");
        ownList!.Items.ShouldContain(x => x.Id == order.Id);

        var otherList = await secondEngineerClient.GetFromJsonAsync<PagedResult<PartOrderRequestDto>>("/api/v1/orders/mine");
        otherList!.Items.ShouldNotContain(x => x.Id == order.Id);

        (await secondEngineerClient.GetAsync("/api/v1/orders")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_sees_every_request_and_can_mark_one_ordered()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var create = await engineerClient.PostAsJsonAsync(
            "/api/v1/orders", new CreatePartOrderRequest("Mechanical seal kit", 1, true));
        var order = (await create.Content.ReadFromJsonAsync<PartOrderRequestDto>())!;
        order.Status.ShouldBe(nameof(OrderStatus.Pending));

        var allOrders = await managerClient.GetFromJsonAsync<PagedResult<PartOrderRequestDto>>("/api/v1/orders");
        allOrders!.Items.ShouldContain(x => x.Id == order.Id);

        var markOrdered = await managerClient.PostAsync($"/api/v1/orders/{order.Id}/mark-ordered", null);
        markOrdered.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var pendingOnly = await managerClient
            .GetFromJsonAsync<PagedResult<PartOrderRequestDto>>($"/api/v1/orders?status={(int)OrderStatus.Pending}");
        pendingOnly!.Items.ShouldNotContain(x => x.Id == order.Id, "an ordered request drops off the pending list");
    }

    [Fact]
    public async Task An_engineer_can_withdraw_their_own_request_but_not_someone_elses()
    {
        var (firstEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (secondEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await firstEngineerClient.PostAsJsonAsync(
            "/api/v1/orders", new CreatePartOrderRequest("Gasket set", 3, false));
        var order = (await create.Content.ReadFromJsonAsync<PartOrderRequestDto>())!;

        var wrongOwner = await secondEngineerClient.DeleteAsync($"/api/v1/orders/{order.Id}");
        wrongOwner.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rightOwner = await firstEngineerClient.DeleteAsync($"/api/v1/orders/{order.Id}");
        rightOwner.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_manager_can_delete_any_engineers_request()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var create = await engineerClient.PostAsJsonAsync(
            "/api/v1/orders", new CreatePartOrderRequest("Filter cartridge", 5, false));
        var order = (await create.Content.ReadFromJsonAsync<PartOrderRequestDto>())!;

        var response = await managerClient.DeleteAsync($"/api/v1/orders/{order.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
