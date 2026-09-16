using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// The rota, exercised the way the web planner actually uses it: drop a worker on a
/// shift, read the board back, move them by deleting and re-creating. Visibility follows
/// the same ManagerScope rule as everywhere else - a manager cannot plan or see people
/// outside the unit they are responsible for.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ShiftPlanningTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"shift{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

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

            // Mirrors approval: the department comes from the role, so a test manager and
            // a test worker end up related the same way they would on the real site.
            var department = departmentName ?? Permissions.RoleDepartments.GetValueOrDefault(roleName);

            if (department is not null)
            {
                user.DepartmentId = (await context.Departments.FirstAsync(x => x.Name == department)).Id;
            }

            await context.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return (client, userId);
    }

    private static async Task<int> ShiftTypeIdAsync(HttpClient client, string name)
    {
        var shifts = await client.GetFromJsonAsync<PagedResult<ShiftTypeDto>>("/api/v1/admin/shift-types");
        return shifts!.Items.First(x => x.Name == name).Id;
    }

    [Fact]
    public async Task Dropping_a_worker_on_a_shift_puts_them_on_that_shift_for_that_day()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var morningId = await ShiftTypeIdAsync(managerClient, "Morning");
        var date = new DateOnly(2026, 9, 7);

        var response = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, date, morningId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var board = await managerClient
            .GetFromJsonAsync<List<ShiftAssignmentDto>>($"/api/v1/shifts/assignments?from={date:O}&to={date:O}");
        board!.ShouldContain(x => x.UserId == workerId && x.ShiftTypeName == "Morning");
    }

    [Fact]
    public async Task Dropping_the_same_person_on_the_same_shift_twice_is_a_conflict_not_a_duplicate()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var nightId = await ShiftTypeIdAsync(managerClient, "Night");
        var date = new DateOnly(2026, 9, 8);

        var first = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, date, nightId));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, date, nightId));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Moving_a_worker_is_deleting_the_old_slot_and_creating_a_new_one()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var morningId = await ShiftTypeIdAsync(managerClient, "Morning");
        var afternoonId = await ShiftTypeIdAsync(managerClient, "Afternoon");
        var date = new DateOnly(2026, 9, 9);

        var create = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, date, morningId));
        var assignment = (await create.Content.ReadFromJsonAsync<ShiftAssignmentDto>())!;

        var delete = await managerClient.DeleteAsync($"/api/v1/shifts/assignments/{assignment.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var move = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, date, afternoonId));
        move.StatusCode.ShouldBe(HttpStatusCode.Created);

        var board = await managerClient
            .GetFromJsonAsync<List<ShiftAssignmentDto>>($"/api/v1/shifts/assignments?from={date:O}&to={date:O}");
        board!.Count(x => x.UserId == workerId).ShouldBe(1);
        board!.Single(x => x.UserId == workerId).ShiftTypeName.ShouldBe("Afternoon");
    }

    [Fact]
    public async Task A_manager_cannot_plan_someone_outside_their_scope()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (productionManagerClient, productionManagerId) = await SignedInAsAsync(
            Permissions.Roles.ProductionManager, "Production");
        var (_, outsiderId) = await SignedInAsAsync(Permissions.Roles.Operator, "Maintenance");

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var productionDeptId = (await context.Departments.FirstAsync(x => x.Name == "Production")).Id;

        await adminClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new AssignManagerScopeRequest(productionManagerId, productionDeptId, null));

        var shiftId = await ShiftTypeIdAsync(productionManagerClient, "Morning");

        var response = await productionManagerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(outsiderId, new DateOnly(2026, 9, 10), shiftId));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_worker_sees_their_own_upcoming_shifts()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var morningId = await ShiftTypeIdAsync(managerClient, "Morning");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, today.AddDays(1), morningId));

        var mine = await workerClient.GetFromJsonAsync<List<ShiftAssignmentDto>>("/api/v1/shifts/mine");

        mine!.ShouldContain(x => x.UserId == workerId && x.ShiftTypeName == "Morning");
    }

    [Fact]
    public async Task An_operator_cannot_open_the_planner_board()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await workerClient.GetAsync(
            $"/api/v1/shifts/assignments?from={DateOnly.FromDateTime(DateTime.UtcNow):O}&to={DateOnly.FromDateTime(DateTime.UtcNow):O}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Assigning_to_a_deactivated_shift_type_is_rejected()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await managerClient.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest($"Retired Shift {_counter}", new TimeOnly(4, 0), new TimeOnly(12, 0), 9));
        var shiftType = (await create.Content.ReadFromJsonAsync<ShiftTypeDto>())!;
        await managerClient.PostAsync($"/api/v1/admin/shift-types/{shiftType.Id}/deactivate", null);

        var response = await managerClient.PostAsJsonAsync("/api/v1/shifts/assignments",
            new CreateShiftAssignmentRequest(workerId, new DateOnly(2026, 9, 11), shiftType.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
