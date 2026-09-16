using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Authorisation is only as good as the grant table behind it, so the seeded roles are
/// checked against the rules the site actually agreed - not just that rows exist.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PermissionSeedTests(CfiAppApiFactory factory)
{
    private async Task RunSeedAsync()
    {
        await using var scope = factory.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    private async Task<HashSet<string>> GrantedKeysAsync(string roleName)
    {
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var keys = await context.RolePermissions
            .Where(x => x.Role!.Name == roleName)
            .Select(x => x.Permission!.Key)
            .ToListAsync();

        return [.. keys];
    }

    [Fact]
    public async Task Every_permission_declared_in_code_exists_in_the_database()
    {
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stored = await context.Permissions.Select(x => x.Key).ToListAsync();

        foreach (var key in Permissions.Catalog.Keys)
        {
            stored.ShouldContain(key);
        }
    }

    [Fact]
    public async Task The_five_system_roles_are_created()
    {
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var roles = await context.Roles.Where(x => x.IsSystem).Select(x => x.Name).ToListAsync();

        roles.ShouldContain(Permissions.Roles.Operator);
        roles.ShouldContain(Permissions.Roles.Engineer);
        roles.ShouldContain(Permissions.Roles.MaintenanceManager);
        roles.ShouldContain(Permissions.Roles.Qa);
        roles.ShouldContain(Permissions.Roles.QaManager);
        roles.ShouldContain(Permissions.Roles.ProductionManager);
    }

    [Fact]
    public async Task Maintenance_manager_can_do_everything_an_engineer_can()
    {
        await RunSeedAsync();

        var engineer = await GrantedKeysAsync(Permissions.Roles.Engineer);
        var manager = await GrantedKeysAsync(Permissions.Roles.MaintenanceManager);

        engineer.ShouldNotBeEmpty();
        foreach (var key in engineer.Where(key => key != Permissions.HolidayRequest))
        {
            manager.ShouldContain(key, $"the manager is a superset of the engineer, missing {key}");
        }
    }

    [Fact]
    public async Task An_operator_cannot_claim_assign_or_close_work()
    {
        await RunSeedAsync();

        var granted = await GrantedKeysAsync(Permissions.Roles.Operator);

        granted.ShouldContain(Permissions.WorkOrderCreate);
        granted.ShouldContain(Permissions.WorkOrderViewOwn);
        granted.ShouldNotContain(Permissions.WorkOrderViewAll);
        granted.ShouldNotContain(Permissions.WorkOrderClaim);
        granted.ShouldNotContain(Permissions.WorkOrderAssign);
        granted.ShouldNotContain(Permissions.WorkOrderClose);
        granted.ShouldNotContain(Permissions.AdminManage);
    }

    [Fact]
    public async Task Only_the_maintenance_manager_administers_the_system_and_manages_tasks()
    {
        await RunSeedAsync();

        foreach (var role in Permissions.RoleGrants.Keys)
        {
            var granted = await GrantedKeysAsync(role);
            var shouldHold = role == Permissions.Roles.MaintenanceManager;

            granted.Contains(Permissions.AdminManage).ShouldBe(shouldHold, $"{role} / admin.manage");
            granted.Contains(Permissions.TaskManage).ShouldBe(shouldHold, $"{role} / task.manage");
            granted.Contains(Permissions.OrderManageAll).ShouldBe(shouldHold, $"{role} / order.manageAll");
        }
    }

    [Fact]
    public async Task Qa_signs_off_and_production_manager_does_not()
    {
        await RunSeedAsync();

        var qa = await GrantedKeysAsync(Permissions.Roles.Qa);
        var productionManager = await GrantedKeysAsync(Permissions.Roles.ProductionManager);

        qa.ShouldContain(Permissions.QaSignOff);
        qa.ShouldContain(Permissions.QaCheck);

        productionManager.ShouldContain(Permissions.ProductionSignOff);
        productionManager.ShouldNotContain(Permissions.QaSignOff);
    }

    /// <summary>
    /// The QA Manager swabs as well as running the team - the same shape as the Maintenance
    /// Manager over an engineer. What they must not pick up along the way is the admin
    /// panel: running QA is not running the site.
    /// </summary>
    [Fact]
    public async Task Qa_manager_can_do_everything_a_qa_can_plus_run_the_team()
    {
        await RunSeedAsync();

        var qa = await GrantedKeysAsync(Permissions.Roles.Qa);
        var qaManager = await GrantedKeysAsync(Permissions.Roles.QaManager);

        foreach (var key in qa.Where(x => x != Permissions.HolidayRequest))
        {
            qaManager.ShouldContain(key, $"a QA manager should be able to {key}");
        }

        qaManager.ShouldContain(Permissions.UserApprove);
        qaManager.ShouldContain(Permissions.AttendanceViewTeam);
        qaManager.ShouldContain(Permissions.HolidayApprove);

        qaManager.ShouldNotContain(Permissions.AdminManage);
        qaManager.ShouldNotContain(Permissions.WorkOrderClaim);

        // Managers approve leave, they do not book it here.
        qaManager.ShouldNotContain(Permissions.HolidayRequest);
    }

    [Fact]
    public async Task Managers_approve_leave_rather_than_booking_it_here()
    {
        await RunSeedAsync();

        var manager = await GrantedKeysAsync(Permissions.Roles.MaintenanceManager);

        manager.ShouldContain(Permissions.HolidayApprove);
        manager.ShouldNotContain(Permissions.HolidayRequest);
    }

    [Fact]
    public async Task Running_the_seed_twice_does_not_duplicate_grants()
    {
        await RunSeedAsync();
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var expected = Permissions.RoleGrants[Permissions.Roles.Engineer].Distinct().Count();
        var actual = await context.RolePermissions
            .CountAsync(x => x.Role!.Name == Permissions.Roles.Engineer);

        actual.ShouldBe(expected);
    }

    /// <summary>
    /// Nobody picks a department on the approval form - the role decides it. FLT drivers
    /// are the case worth pinning down: they move stock for both sides of the site, so
    /// they are their own department rather than production's.
    /// </summary>
    [Fact]
    public void Every_role_lands_in_a_department_and_FLT_drivers_get_their_own()
    {
        foreach (var role in Permissions.RoleGrants.Keys)
        {
            Permissions.RoleDepartments.ShouldContainKey(
                role, $"{role} would be approved into no department at all");
        }

        Permissions.RoleDepartments[Permissions.Roles.FltDriver]
            .ShouldBe(Permissions.Departments.Flt);

        // QA answers to the QA Manager, not to maintenance.
        Permissions.RoleDepartments[Permissions.Roles.Qa]
            .ShouldBe(Permissions.Departments.Qa);

        Permissions.RoleDepartments[Permissions.Roles.QaManager]
            .ShouldBe(Permissions.Departments.Qa);

        Permissions.RoleDepartments[Permissions.Roles.Supervisor]
            .ShouldBe(Permissions.Departments.Production);

        Permissions.RoleDepartments[Permissions.Roles.Engineer]
            .ShouldBe(Permissions.Departments.Maintenance);
    }
}
