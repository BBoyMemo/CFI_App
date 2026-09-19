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
/// Reference data CRUD: readable by anyone signed in (every role needs these lists for
/// dropdowns), writable only by the Maintenance Manager, and nothing is ever hard deleted.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminCrudTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"admin{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<HttpClient> SignedInAsAsync(string roleName)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test User", email, null, Password, "en"));

        await using (var scope = factory.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var user = await context.Users.SingleAsync(x => x.Email == email);
            var role = await context.Roles.SingleAsync(x => x.Name == roleName);
            user.RoleId = role.Id;
            user.Status = UserStatus.Active;
            await context.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task Any_signed_in_role_can_read_the_unit_list()
    {
        var operatorClient = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await operatorClient.GetAsync("/api/v1/admin/units");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<UnitDto>>();
        page!.Items.ShouldContain(x => x.Name == "Unit 1");
    }

    [Fact]
    public async Task An_operator_cannot_create_an_unit()
    {
        var operatorClient = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/admin/units",
            new UpsertUnitRequest("Test Unit", $"TA{_counter}", 9));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_maintenance_manager_can_create_and_deactivate_an_unit()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var code = $"TA{Interlocked.Increment(ref _counter)}";

        var create = await managerClient.PostAsJsonAsync("/api/v1/admin/units",
            new UpsertUnitRequest("Test Unit", code, 9));

        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await create.Content.ReadFromJsonAsync<UnitDto>())!;
        created.IsActive.ShouldBeTrue();

        var deactivate = await managerClient.PostAsync($"/api/v1/admin/units/{created.Id}/deactivate", null);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await managerClient.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units?pageSize=200");
        var stillThere = list!.Items.Single(x => x.Id == created.Id);
        stillThere.IsActive.ShouldBeFalse("switched off, not deleted - a work order may already reference it");
    }

    [Fact]
    public async Task Creating_two_units_with_the_same_code_is_rejected_as_a_conflict()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var code = $"DUP{Interlocked.Increment(ref _counter)}";

        var first = await managerClient.PostAsJsonAsync("/api/v1/admin/units",
            new UpsertUnitRequest("First", code, 1));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await managerClient.PostAsJsonAsync("/api/v1/admin/units",
            new UpsertUnitRequest("Second", code, 2));

        // Enforced by the database unique index and surfaced through the global handler.
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_area_cannot_be_created_under_an_unit_that_does_not_exist()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var response = await managerClient.PostAsJsonAsync("/api/v1/admin/areas",
            new UpsertAreaRequest(999_999, "Ghost Room", null, 1, true));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_area_created_under_a_real_unit_is_returned_with_its_unit_name()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var units = await managerClient.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units");
        var unit1 = units!.Items.Single(x => x.Name == "Unit 1");

        var response = await managerClient.PostAsJsonAsync("/api/v1/admin/areas",
            new UpsertAreaRequest(unit1.Id, "Chiller Room", "F4", 5, true));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<AreaDto>())!;
        created.UnitName.ShouldBe("Unit 1");
    }

    [Fact]
    public async Task Equipment_can_be_filtered_by_line_for_the_operator_report_flow()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        // By code, not by name: another test in this suite deliberately renames "Yard" to
        // prove the seeder never overwrites a site edit, and that rename is shared state
        // across the whole collection. The Unit code is never touched by any test.
        var units = await managerClient.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units?pageSize=200");
        var yard = units!.Items.Single(x => x.Code == "YARD");

        var line = (await (await managerClient.PostAsJsonAsync("/api/v1/admin/lines",
            new UpsertLineRequest(yard.Id, null, "Line 9", 1))).Content.ReadFromJsonAsync<LineDto>())!;

        var equipment = (await (await managerClient.PostAsJsonAsync("/api/v1/admin/equipment",
            new UpsertEquipmentRequest(yard.Id, null, line.Id, null, "Compactor Motor", "motor", 1)))
            .Content.ReadFromJsonAsync<EquipmentDto>())!;

        var filtered = await managerClient
            .GetFromJsonAsync<PagedResult<EquipmentDto>>($"/api/v1/admin/equipment?lineId={line.Id}");

        filtered!.Items.ShouldContain(x => x.Id == equipment.Id);
    }

    /// <summary>
    /// The machine list reads down the site, not across it. Every room numbers its own
    /// machines from 1, so ordering on DisplayOrder alone used to interleave all of them
    /// into one run - "AAK, Filler, Inkjet Printer, P Tank 1, Separator 7" - which made the
    /// admin panel unusable at 62 machines.
    ///
    /// Within a room the order is the one the site reads its own list in: the machines on
    /// each line, line by line, then whatever stands in the room itself.
    /// </summary>
    [Fact]
    public async Task The_machine_list_is_ordered_room_by_room_and_line_by_line()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var units = await managerClient.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units?pageSize=200");
        var unit1 = units!.Items.Single(x => x.Code == "UNIT1");

        var rooms = await managerClient.GetFromJsonAsync<PagedResult<AreaDto>>(
            $"/api/v1/admin/areas?unitId={unit1.Id}&pageSize=200");
        var filling = rooms!.Items.Single(x => x.Code == "FILLING");

        var machines = await managerClient.GetFromJsonAsync<PagedResult<EquipmentDto>>(
            $"/api/v1/admin/equipment?unitId={unit1.Id}&pageSize=200");

        var all = machines!.Items.ToList();

        // Rooms come out in one block each, never mixed together.
        var roomRuns = all
            .Select(x => x.AreaId)
            .Aggregate(new List<int?>(), (runs, areaId) =>
            {
                if (runs.Count == 0 || runs[^1] != areaId) runs.Add(areaId);
                return runs;
            });

        roomRuns.Distinct().Count().ShouldBe(roomRuns.Count, "a room's machines should be one unbroken run");

        // Inside any room: everything on a line first, then whatever stands in the room
        // itself. Asserted over the whole list rather than one named machine, so it still
        // holds when the site moves something onto a line or off one.
        foreach (var room in all.GroupBy(x => x.AreaId))
        {
            var seenRoomLevel = false;
            foreach (var machine in room)
            {
                if (machine.LineId is null) seenRoomLevel = true;
                else
                {
                    seenRoomLevel.ShouldBeFalse(
                        $"{machine.Name} is on a line, so it cannot come after a machine that is not");
                }
            }
        }

        // And each line's machines stay together, in the line's own order.
        var inFilling = all.Where(x => x.AreaId == filling.Id).ToList();
        var lineRuns = inFilling
            .Where(x => x.LineId is not null)
            .Select(x => x.LineId!.Value)
            .Aggregate(new List<int>(), (runs, lineId) =>
            {
                if (runs.Count == 0 || runs[^1] != lineId) runs.Add(lineId);
                return runs;
            });

        lineRuns.Distinct().Count().ShouldBe(lineRuns.Count, "a line's machines should be one unbroken run");
        lineRuns.ShouldBe(lineRuns.OrderBy(x => x).ToList());
    }

    /// <summary>
    /// A part of a machine - Blender 2's FIBC1, and anything the site adds later from this
    /// panel. The three refusals are the ones that would otherwise put a part somewhere its
    /// machine is not, which makes the fault log name two places for one repair.
    /// </summary>
    [Fact]
    public async Task A_machine_part_must_stand_where_its_machine_stands()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var units = await managerClient.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units?pageSize=200");
        var unit3 = units!.Items.Single(x => x.Code == "UNIT3");
        var yard = units.Items.Single(x => x.Code == "YARD");

        var machine = (await (await managerClient.PostAsJsonAsync("/api/v1/admin/equipment",
            new UpsertEquipmentRequest(unit3.Id, null, null, null, $"Blender {_counter}", "blender", 1)))
            .Content.ReadFromJsonAsync<EquipmentDto>())!;

        // In the same place: accepted, and it comes back naming its machine.
        var part = await managerClient.PostAsJsonAsync("/api/v1/admin/equipment",
            new UpsertEquipmentRequest(unit3.Id, null, null, machine.Id, $"FIBC {_counter}", "hopper", 1));

        part.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = (await part.Content.ReadFromJsonAsync<EquipmentDto>())!;
        created.ParentEquipmentId.ShouldBe(machine.Id);
        created.ParentEquipmentName.ShouldBe(machine.Name);

        // Somewhere else: refused.
        var elsewhere = await managerClient.PostAsJsonAsync("/api/v1/admin/equipment",
            new UpsertEquipmentRequest(yard.Id, null, null, machine.Id, $"Stray {_counter}", null, 1));

        elsewhere.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // One level deep: a part cannot have parts of its own.
        var grandchild = await managerClient.PostAsJsonAsync("/api/v1/admin/equipment",
            new UpsertEquipmentRequest(unit3.Id, null, null, created.Id, $"Valve {_counter}", null, 1));

        grandchild.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // And nothing can be a part of itself.
        var itself = await managerClient.PutAsJsonAsync($"/api/v1/admin/equipment/{machine.Id}",
            new UpsertEquipmentRequest(unit3.Id, null, null, machine.Id, machine.Name, "blender", 1));

        itself.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_shift_type_may_cross_midnight_but_start_and_end_must_differ()
    {
        var managerClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var night = await managerClient.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest($"Night {_counter}", new TimeOnly(22, 0), new TimeOnly(6, 0),
                [DayOfWeek.Sunday, DayOfWeek.Monday], new DateOnly(2026, 1, 1), 9));
        night.StatusCode.ShouldBe(HttpStatusCode.Created);

        var invalid = await managerClient.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest("Zero Length", new TimeOnly(9, 0), new TimeOnly(9, 0),
                [DayOfWeek.Monday], new DateOnly(2026, 1, 1), 9));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Production_managers_can_plan_shifts_but_not_administer_the_site()
    {
        var productionManagerClient = await SignedInAsAsync(Permissions.Roles.ProductionManager);

        var shiftAttempt = await productionManagerClient.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest($"Swing {_counter}", new TimeOnly(10, 0), new TimeOnly(18, 0),
                [DayOfWeek.Monday], new DateOnly(2026, 1, 1), 9));
        shiftAttempt.StatusCode.ShouldBe(HttpStatusCode.Created);

        var unitAttempt = await productionManagerClient.PostAsJsonAsync("/api/v1/admin/units",
            new UpsertUnitRequest("Should Fail", $"SF{_counter}", 1));
        unitAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_approver_is_only_offered_the_roles_they_are_allowed_to_hand_out()
    {
        // Maintenance runs the engineers and, as the site's only admin, lets in QA and new
        // managers - but not operators, who belong to production.
        var maintenanceClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var maintenanceRoles = await maintenanceClient.GetFromJsonAsync<List<RoleDto>>("/api/v1/admin/roles");

        // No QA in this list on purpose: the admin lets the QA Manager in, and the QA
        // Manager decides who does QA work.
        maintenanceRoles!.Select(x => x.Name).ShouldBe(
        [
            Permissions.Roles.Engineer, Permissions.Roles.MaintenanceManager,
            Permissions.Roles.QaManager, Permissions.Roles.ProductionManager
        ],
            ignoreOrder: true);

        var qaManagerClient = await SignedInAsAsync(Permissions.Roles.QaManager);

        var qaManagerRoles = await qaManagerClient.GetFromJsonAsync<List<RoleDto>>("/api/v1/admin/roles");

        qaManagerRoles!.Select(x => x.Name).ShouldBe([Permissions.Roles.Qa]);

        var productionClient = await SignedInAsAsync(Permissions.Roles.ProductionManager);

        var productionRoles = await productionClient.GetFromJsonAsync<List<RoleDto>>("/api/v1/admin/roles");

        productionRoles!.Select(x => x.Name).ShouldBe(
        [
            Permissions.Roles.Operator, Permissions.Roles.Supervisor, Permissions.Roles.FltDriver
        ],
            ignoreOrder: true);

        var operatorClient = await SignedInAsAsync(Permissions.Roles.Operator);
        (await operatorClient.GetAsync("/api/v1/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_cannot_approve_someone_into_a_role_that_is_not_theirs_to_give()
    {
        var newStarterEmail = $"starter{Guid.NewGuid():N}@cfi.test";

        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("New Starter", newStarterEmail, null, Password, "en")))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        int starterId, operatorRoleId;

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            starterId = (await context.Users.SingleAsync(x => x.Email == newStarterEmail)).Id;
            operatorRoleId = (await context.Roles.SingleAsync(x => x.Name == Permissions.Roles.Operator)).Id;
        }

        // Maintenance holds user.approve, so this is a 403 about the role, not a 401.
        var maintenanceClient = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var refused = await maintenanceClient.PostAsJsonAsync($"/api/v1/users/{starterId}/approve",
            new ApproveUserRequest(operatorRoleId, null, []));

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The account is untouched: still waiting, not half approved.
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var starter = await context.Users.SingleAsync(x => x.Id == starterId);
            starter.Status.ShouldBe(UserStatus.PendingApproval);
            starter.RoleId.ShouldBeNull();
        }

        // Production, who the operator actually reports to, can.
        var productionClient = await SignedInAsAsync(Permissions.Roles.ProductionManager);

        var accepted = await productionClient.PostAsJsonAsync($"/api/v1/users/{starterId}/approve",
            new ApproveUserRequest(operatorRoleId, null, []));

        accepted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
