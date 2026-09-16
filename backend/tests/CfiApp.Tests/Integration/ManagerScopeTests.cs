using CfiApp.Api.Controllers.V1;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// This is the mechanism every future "manager sees only their own people" screen relies
/// on: not a UI filter, a query narrowed by ManagerScope rows. Getting this wrong means a
/// Production Manager could read another department's staff list by knowing an id existed
/// somewhere on the page - these tests exist to make that impossible.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ManagerScopeTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"scope{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, int UserId)> RegisterActiveAsync(
        string roleName, string? departmentName, string? unitCode)
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

            if (unitCode is not null)
            {
                // People are posted to an area, so working "in Unit 1" means being in one
                // of its areas - the first one is enough to prove the visibility rule.
                var area = await context.Areas.FirstAsync(x => x.Unit!.Code == unitCode);
                user.Areas.Add(new UserArea { AreaId = area.Id });
            }

            await context.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return (client, userId);
    }

    private async Task<string> EmailOfAsync(int userId)
    {
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        return (await context.Users.SingleAsync(x => x.Id == userId)).Email;
    }

    private async Task AssignScopeAsync(HttpClient adminClient, int managerUserId, int? departmentId, int? unitId)
    {
        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new AssignManagerScopeRequest(managerUserId, departmentId, unitId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_manager_with_no_department_and_no_scope_sees_nobody()
    {
        // Approval always sets a department, so this is a broken record rather than a
        // normal state - and a broken record must fail closed, not open.
        var (managerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, departmentName: null, unitCode: null);

        var response = await managerClient.GetAsync("/api/v1/team");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<TeamMemberDto>>();
        page!.Items.ShouldBeEmpty("a manager with nothing assigned is a configuration gap, not a reason to see everyone");
    }

    [Fact]
    public async Task A_manager_sees_their_own_department_without_needing_a_scope_row()
    {
        var (managerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, departmentName: "Production", unitCode: null);

        var (_, operatorId) = await RegisterActiveAsync(Permissions.Roles.Operator, "Production", null);

        var page = await managerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>(
            $"/api/v1/team?search={Uri.EscapeDataString(await EmailOfAsync(operatorId))}");

        page!.Items.ShouldContain(x => x.Id == operatorId);
    }

    [Fact]
    public async Task A_manager_scoped_to_a_department_sees_only_that_departments_people()
    {
        var (adminClient, _) = await RegisterActiveAsync(
            Permissions.Roles.MaintenanceManager, departmentName: null, unitCode: null);

        var (productionManagerClient, productionManagerId) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, departmentName: "Production", unitCode: null);

        var (productionWorkerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.Operator, departmentName: "Production", unitCode: null);

        var (maintenanceWorkerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.Engineer, departmentName: "Maintenance", unitCode: null);

        _ = productionWorkerClient;
        _ = maintenanceWorkerClient;

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var productionDepartmentId = (await context.Departments.FirstAsync(x => x.Name == "Production")).Id;

        await AssignScopeAsync(adminClient, productionManagerId, productionDepartmentId, null);

        var page = await productionManagerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>("/api/v1/team");

        page!.Items.ShouldContain(x => x.Department == "Production");
        page.Items.ShouldNotContain(x => x.Department == "Maintenance");
    }

    [Fact]
    public async Task A_manager_scoped_to_an_unit_sees_people_working_that_unit_regardless_of_department()
    {
        var (adminClient, _) = await RegisterActiveAsync(
            Permissions.Roles.MaintenanceManager, departmentName: null, unitCode: null);

        var (unitManagerClient, unitManagerId) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, departmentName: "Production", unitCode: null);

        // Unit 2 rather than the yard: people are posted to a room, and the yard is
        // outside - it has no rooms to post anybody to.
        var (blendingWorkerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.Operator, departmentName: "Maintenance", unitCode: "UNIT2");

        _ = blendingWorkerClient;

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var unit2Id = (await context.Units.FirstAsync(x => x.Code == "UNIT2")).Id;

        await AssignScopeAsync(adminClient, unitManagerId, null, unit2Id);

        var page = await unitManagerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>("/api/v1/team");

        page!.Items.ShouldContain(x => x.FullName == "Test User" && x.Department == "Maintenance");
    }

    [Fact]
    public async Task The_maintenance_manager_sees_maintenance_and_not_productions_operators()
    {
        var (maintenanceManagerClient, _) = await RegisterActiveAsync(
            Permissions.Roles.MaintenanceManager, departmentName: "Maintenance", unitCode: null);

        var (_, engineerId) = await RegisterActiveAsync(Permissions.Roles.Engineer, "Maintenance", null);
        var (_, operatorId) = await RegisterActiveAsync(Permissions.Roles.Operator, "Production", null);

        // Searched by email rather than read off page one: the suite shares a database, so
        // "is this person in the first hundred rows" is a different question from "can the
        // manager see this person".
        var engineers = await maintenanceManagerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>(
            $"/api/v1/team?search={Uri.EscapeDataString(await EmailOfAsync(engineerId))}");

        engineers!.Items.ShouldContain(x => x.Id == engineerId);

        // Being the site's admin is about approving accounts and editing the site layout,
        // not about reading production's hours.
        var operators = await maintenanceManagerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>(
            $"/api/v1/team?search={Uri.EscapeDataString(await EmailOfAsync(operatorId))}");

        operators!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_worker_without_the_team_visibility_permission_is_forbidden()
    {
        var (workerClient, _) = await RegisterActiveAsync(Permissions.Roles.Operator, "Production", null);

        var response = await workerClient.GetAsync("/api/v1/team");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_the_admin_can_assign_a_manager_scope()
    {
        var (managerClient, managerId) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, "Production", null);

        var response = await managerClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new AssignManagerScopeRequest(managerId, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_scope_request_naming_both_a_department_and_an_unit_is_rejected()
    {
        var (adminClient, _) = await RegisterActiveAsync(
            Permissions.Roles.MaintenanceManager, departmentName: null, unitCode: null);

        var (_, managerId) = await RegisterActiveAsync(Permissions.Roles.ProductionManager, "Production", null);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var departmentId = (await context.Departments.FirstAsync(x => x.Name == "Production")).Id;
        var unitId = (await context.Units.FirstAsync(x => x.Code == "UNIT1")).Id;

        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new AssignManagerScopeRequest(managerId, departmentId, unitId));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Removing_a_scope_takes_effect_immediately()
    {
        var (adminClient, _) = await RegisterActiveAsync(
            Permissions.Roles.MaintenanceManager, departmentName: null, unitCode: null);

        var (managerClient, managerId) = await RegisterActiveAsync(
            Permissions.Roles.ProductionManager, "Production", null);

        // Someone the manager can only reach through the extra scope, never through their
        // own department - otherwise removing the scope would change nothing.
        var (_, engineerId) = await RegisterActiveAsync(Permissions.Roles.Engineer, "Maintenance", null);
        var engineerSearch = $"/api/v1/team?search={Uri.EscapeDataString(await EmailOfAsync(engineerId))}";

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var maintenanceId = (await context.Departments.FirstAsync(x => x.Name == "Maintenance")).Id;

        var created = await adminClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new AssignManagerScopeRequest(managerId, maintenanceId, null));
        var scopeDto = (await created.Content.ReadFromJsonAsync<ManagerScopeDto>())!;

        var withScope = await managerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>(engineerSearch);
        withScope!.Items.ShouldContain(x => x.Id == engineerId);

        var remove = await adminClient.DeleteAsync($"/api/v1/admin/manager-scopes/{scopeDto.Id}");
        remove.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterRemoval = await managerClient.GetFromJsonAsync<PagedResult<TeamMemberDto>>(engineerSearch);
        afterRemoval!.Items.ShouldBeEmpty();
    }
}
