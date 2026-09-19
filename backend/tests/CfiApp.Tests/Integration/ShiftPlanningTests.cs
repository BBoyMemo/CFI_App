using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Application.Messaging;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// The rota, exercised the way the planner uses it. Two levels are being locked in here: a
/// shift is drawn up as a template and then copied into the pool, and people go on the pool -
/// which is what lets a template be deleted without disturbing anybody.
///
/// On top of that, the rules that make the rota trustworthy rather than merely convenient: a
/// shift planned once repeats until somebody changes it, a change is dated and never rewrites
/// the past, and cover sits on top without touching the pattern underneath.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ShiftPlanningTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"shift{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// A rota change may not start in the past, so the tests plan from tomorrow. The site
    /// runs on Europe/London days, which is what the server compares against.
    /// </summary>
    private static DateOnly Tomorrow =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime).AddDays(1);

    private static DateOnly NextOccurrenceOf(DayOfWeek weekday, DateOnly notBefore)
    {
        var date = notBefore;
        while (date.DayOfWeek != weekday) date = date.AddDays(1);
        return date;
    }

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

    /// <summary>Draws up a shift of its own, so tests sharing a database do not collide.</summary>
    private static async Task<ShiftTypeDto> NewShiftTypeAsync(
        HttpClient client,
        DateOnly startsOn,
        params DayOfWeek[] weekdays)
    {
        var created = await client.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest(
                $"Shift {Guid.NewGuid():N}", new TimeOnly(6, 0), new TimeOnly(14, 0),
                weekdays.Length == 0 ? [DayOfWeek.Monday] : weekdays, startsOn, 5));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<ShiftTypeDto>())!;
    }

    /// <summary>Draws a shift up and puts it in the pool, which is where people can go on it.</summary>
    private static async Task<int> PooledShiftAsync(
        HttpClient client,
        DateOnly startsOn,
        params DayOfWeek[] weekdays)
    {
        var template = await NewShiftTypeAsync(client, startsOn, weekdays);

        (await client.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var board = await BoardAsync(client, startsOn);
        return board.Shifts.Single(x => x.SourceShiftTypeId == template.Id).ActiveShiftId;
    }

    private static async Task<RosterBoardDto> BoardAsync(HttpClient client, DateOnly on) =>
        (await client.GetFromJsonAsync<RosterBoardDto>($"/api/v1/shifts/roster?on={on:yyyy-MM-dd}"))!;

    private static RosterShiftCardDto Card(RosterBoardDto board, int activeShiftId) =>
        board.Shifts.Single(x => x.ActiveShiftId == activeShiftId);

    private static Task<HttpResponseMessage> SetRosterAsync(
        HttpClient client, int userId, int activeShiftId, DateOnly from) =>
        client.PostAsJsonAsync("/api/v1/shifts/roster", new SetRosterRequest(userId, activeShiftId, from));

    // ---------------------------------------------------------------- the pool

    [Fact]
    public async Task A_shift_only_takes_people_once_it_is_in_the_pool()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var startsOn = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var template = await NewShiftTypeAsync(managerClient, startsOn, DayOfWeek.Monday);

        // Drawn up but not in the pool: it is not on the board and nobody can be put on it.
        var before = await BoardAsync(managerClient, startsOn);
        before.Shifts.ShouldNotContain(x => x.SourceShiftTypeId == template.Id);

        (await SetRosterAsync(managerClient, workerId, template.Id, startsOn))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var after = await BoardAsync(managerClient, startsOn);
        after.Shifts.ShouldContain(x => x.SourceShiftTypeId == template.Id);
    }

    [Fact]
    public async Task Deleting_the_shift_it_was_copied_from_leaves_the_one_in_the_pool_running()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var template = await NewShiftTypeAsync(managerClient, monday, DayOfWeek.Monday);

        await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id));
        var shiftId = Card(await BoardAsync(managerClient, monday),
            (await BoardAsync(managerClient, monday)).Shifts.Single(x => x.SourceShiftTypeId == template.Id).ActiveShiftId)
            .ActiveShiftId;

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        // The template is a sketch. Deleting it must not move anybody.
        (await managerClient.DeleteAsync($"/api/v1/admin/shift-types/{template.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Card(await BoardAsync(managerClient, monday), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task The_same_shift_cannot_be_put_in_the_pool_twice()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var template = await NewShiftTypeAsync(managerClient, Tomorrow, DayOfWeek.Monday);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Taking_a_shift_out_of_the_pool_keeps_the_weeks_that_were_worked_on_it()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        (await managerClient.DeleteAsync($"/api/v1/shifts/pool/{shiftId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Gone from next week...
        (await BoardAsync(managerClient, monday.AddDays(7)))
            .Shifts.ShouldNotContain(x => x.ActiveShiftId == shiftId);

        // ...but the day it was actually worked still reads back.
        Card(await BoardAsync(managerClient, monday), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);
    }

    // ---------------------------------------------------------------- the standing rota

    [Fact]
    public async Task Putting_someone_on_a_shift_from_a_date_keeps_them_there_every_following_week()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var firstMonday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, firstMonday, DayOfWeek.Monday);

        (await SetRosterAsync(managerClient, workerId, shiftId, firstMonday))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // The point of the whole redesign: planned once, still true weeks later without
        // anybody touching it again.
        foreach (var week in new[] { 0, 1, 5 })
        {
            Card(await BoardAsync(managerClient, firstMonday.AddDays(7 * week)), shiftId)
                .People.ShouldContain(x => x.UserId == workerId);
        }
    }

    [Fact]
    public async Task A_shift_only_runs_on_the_days_of_the_week_it_was_given()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        Card(await BoardAsync(managerClient, monday), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);

        // Wednesday is not one of the shift's days, so nobody is on it - the days belong to
        // the shift, not to each person on it.
        var wednesday = await BoardAsync(managerClient, monday.AddDays(2));
        Card(wednesday, shiftId).People.ShouldBeEmpty();
        wednesday.Unassigned.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task Moving_someone_from_a_date_leaves_the_days_before_it_alone()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var firstMonday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var laterMonday = firstMonday.AddDays(14);

        var morningId = await PooledShiftAsync(managerClient, firstMonday, DayOfWeek.Monday);
        var nightId = await PooledShiftAsync(managerClient, firstMonday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, morningId, firstMonday);
        await SetRosterAsync(managerClient, workerId, nightId, laterMonday);

        // Ask about a Monday before the change and the board answers with what was true then.
        Card(await BoardAsync(managerClient, firstMonday), morningId)
            .People.ShouldContain(x => x.UserId == workerId);

        Card(await BoardAsync(managerClient, laterMonday), nightId)
            .People.ShouldContain(x => x.UserId == workerId);

        Card(await BoardAsync(managerClient, laterMonday), morningId)
            .People.ShouldNotContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task A_rota_change_dated_in_the_past_is_refused()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var shiftId = await PooledShiftAsync(managerClient, Tomorrow.AddDays(-30), DayOfWeek.Monday);

        // Rewriting a week that has already been worked and already been notified is not a
        // planning action, it is a falsification.
        (await SetRosterAsync(managerClient, workerId, shiftId, Tomorrow.AddDays(-10)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Taking_someone_off_the_rota_ends_their_shifts_and_keeps_the_history()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var firstMonday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var leavingMonday = firstMonday.AddDays(14);
        var shiftId = await PooledShiftAsync(managerClient, firstMonday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, firstMonday);

        (await managerClient.PostAsJsonAsync($"/api/v1/shifts/roster/{workerId}/end",
            new EndRosterRequest(leavingMonday))).StatusCode.ShouldBe(HttpStatusCode.OK);

        Card(await BoardAsync(managerClient, leavingMonday), shiftId)
            .People.ShouldNotContain(x => x.UserId == workerId);

        // The weeks they did work are still on the record - nothing was deleted.
        Card(await BoardAsync(managerClient, firstMonday), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task Putting_someone_where_they_already_are_changes_nothing()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);
        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var rows = await context.ShiftRosterEntries.Where(x => x.UserId == workerId).ToListAsync();

        // A re-drop on the same card is not a change, so it must not leave a closed row
        // behind and clutter the history with something that never happened.
        rows.Count.ShouldBe(1);
        rows.Single().EffectiveTo.ShouldBe(DateOnly.MaxValue);
    }

    // ---------------------------------------------------------------- cover

    [Fact]
    public async Task Cover_puts_someone_else_on_the_shift_only_for_the_dates_given()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, standInId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var from = Tomorrow;
        var to = from.AddDays(1);
        var shiftId = await PooledShiftAsync(managerClient, from, DayOfWeek.Monday);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(standInId, shiftId, from, to, "Covering for Ahmet")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var standIn = Card(await BoardAsync(managerClient, from), shiftId)
            .People.Single(x => x.UserId == standInId);

        standIn.Source.ShouldBe(ShiftSource.Cover);
        standIn.CoverTo.ShouldBe(to);

        // The day after it ends, the cover is simply gone - nothing had to be undone.
        Card(await BoardAsync(managerClient, to.AddDays(1)), shiftId)
            .People.ShouldNotContain(x => x.UserId == standInId);
    }

    [Fact]
    public async Task Marking_someone_off_removes_them_from_the_board_without_touching_their_rota()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, null, monday, monday, "Off sick")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        Card(await BoardAsync(managerClient, monday), shiftId)
            .People.ShouldNotContain(x => x.UserId == workerId);

        // Their normal pattern is untouched: next Monday they are back on it without anybody
        // having to put them there again.
        Card(await BoardAsync(managerClient, monday.AddDays(7)), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task Cover_that_lands_on_approved_leave_is_refused()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var from = Tomorrow;
        var shiftId = await PooledShiftAsync(managerClient, from, DayOfWeek.Monday);

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            context.HolidayRequests.Add(new HolidayRequest
            {
                UserId = workerId,
                StartDate = from,
                EndDate = from.AddDays(4),
                WorkingDays = 3,
                Status = ApprovalStatus.Approved,
                RequestedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Booked leave is not silently overwritten: the manager has to decide which of the
        // two is wrong, because the server cannot know.
        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, shiftId, from, from.AddDays(1), null)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_covers_for_the_same_person_over_the_same_days_are_refused()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var from = Tomorrow;
        var shiftId = await PooledShiftAsync(managerClient, from, DayOfWeek.Monday);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, shiftId, from, from.AddDays(3), null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, shiftId, from.AddDays(2), from.AddDays(5), null)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------- permissions and scope

    [Fact]
    public async Task A_manager_cannot_change_the_rota_of_someone_outside_their_scope()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, outsiderId) = await SignedInAsAsync(Permissions.Roles.Operator);

        var shiftId = await PooledShiftAsync(managerClient, Tomorrow, DayOfWeek.Monday);

        (await SetRosterAsync(managerClient, outsiderId, shiftId, Tomorrow))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_operator_cannot_open_the_planner_board()
    {
        var (operatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        (await operatorClient.GetAsync("/api/v1/shifts/roster")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await operatorClient.GetAsync("/api/v1/shifts/changes")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_shift_that_runs_on_no_day_of_the_week_cannot_be_drawn_up()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var response = await managerClient.PostAsJsonAsync("/api/v1/admin/shift-types",
            new UpsertShiftTypeRequest(
                $"Nowhere {Guid.NewGuid():N}", new TimeOnly(6, 0), new TimeOnly(14, 0),
                [], new DateOnly(2026, 1, 1), 5));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- the worker's own week

    [Fact]
    public async Task A_worker_sees_a_week_of_their_own_shifts_including_cover()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var dayShift = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday);
        var nightShift = await PooledShiftAsync(managerClient, monday, DayOfWeek.Tuesday);

        await SetRosterAsync(managerClient, workerId, dayShift, monday);

        await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, nightShift, monday.AddDays(1), monday.AddDays(1), "Covering nights"));

        var week = (await workerClient.GetFromJsonAsync<List<ResolvedShiftDayDto>>(
            $"/api/v1/shifts/mine?from={monday:yyyy-MM-dd}&to={monday.AddDays(6):yyyy-MM-dd}"))!;

        week.Count.ShouldBe(7, "a week is seven days, including the ones they are not in");

        var mondayEntry = week.Single(x => x.Date == monday);
        mondayEntry.ActiveShiftId.ShouldBe(dayShift);
        mondayEntry.Source.ShouldBe(ShiftSource.Roster);

        // Tuesday was overridden, and the worker is told which it is rather than just seeing
        // a different name and wondering whether the rota changed permanently.
        var tuesdayEntry = week.Single(x => x.Date == monday.AddDays(1));
        tuesdayEntry.ActiveShiftId.ShouldBe(nightShift);
        tuesdayEntry.Source.ShouldBe(ShiftSource.Cover);

        week.Single(x => x.Date == monday.AddDays(2)).Source.ShouldBe(ShiftSource.None);
    }

    [Fact]
    public async Task A_worker_is_notified_when_their_shift_changes()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        var notifications = await workerClient
            .GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications");

        // Being moved to nights is exactly the thing somebody must not find out by turning
        // up at six in the morning.
        notifications!.Items.ShouldContain(x => x.Type == "shift.rosterChanged");
    }

    [Fact]
    public async Task The_history_shows_who_moved_whom_and_when()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        var changes = (await managerClient.GetFromJsonAsync<List<ShiftChangeDto>>(
            $"/api/v1/shifts/changes?userId={workerId}"))!;

        var entry = changes.ShouldHaveSingleItem();
        entry.FromDate.ShouldBe(monday);
        entry.ChangedByName.ShouldNotBeNullOrWhiteSpace("the audit answer is who did it, not just what changed");
    }
}
