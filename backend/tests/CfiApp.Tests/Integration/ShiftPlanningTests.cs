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

    private sealed record Pooled(int TemplateId, int ShiftId);

    /// <summary>Draws a shift up and puts it in the pool, which is what makes it run.</summary>
    private static async Task<Pooled> PooledAsync(
        HttpClient client,
        DateOnly startsOn,
        params DayOfWeek[] weekdays)
    {
        var template = await NewShiftTypeAsync(client, startsOn, weekdays);

        (await client.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var planner = await PlannerAsync(client);
        return new Pooled(template.Id, planner.Pool.Single(x => x.SourceShiftTypeId == template.Id).ActiveShiftId);
    }

    private static async Task<int> PooledShiftAsync(
        HttpClient client,
        DateOnly startsOn,
        params DayOfWeek[] weekdays) =>
        (await PooledAsync(client, startsOn, weekdays)).ShiftId;

    private static async Task<RosterBoardDto> BoardAsync(HttpClient client, DateOnly on) =>
        (await client.GetFromJsonAsync<RosterBoardDto>($"/api/v1/shifts/roster?on={on:yyyy-MM-dd}"))!;

    private static async Task<PlannerDto> PlannerAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PlannerDto>("/api/v1/shifts/planner"))!;

    private static async Task<HistoryShiftDetailDto> HistoryDetailAsync(HttpClient client, int shiftId, DateOnly week) =>
        (await client.GetFromJsonAsync<HistoryShiftDetailDto>(
            $"/api/v1/shifts/history/{shiftId}?week={week:yyyy-MM-dd}"))!;

    private static readonly DayOfWeek[] EveryDay =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private static RosterShiftCardDto Card(RosterBoardDto board, int activeShiftId) =>
        board.Shifts.Single(x => x.ActiveShiftId == activeShiftId);

    /// <summary>
    /// Puts somebody on the crew of the shift a pool entry was drawn up from - the same drag the
    /// planner makes in the shift column. Kept in terms of the pool entry because that is what
    /// the board and the history answer in.
    /// </summary>
    private async Task<HttpResponseMessage> SetRosterAsync(
        HttpClient client, int userId, int activeShiftId, DateOnly from)
    {
        int templateId;

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            templateId = (await context.ActiveShifts.SingleAsync(x => x.Id == activeShiftId)).SourceShiftTypeId!.Value;
        }

        // "Set the rota" in these tests means "put them here", so a clash is confirmed.
        return await PlaceAsync(client, userId, templateId, from, moveFromClashing: true);
    }

    private static Task<HttpResponseMessage> PlaceAsync(
        HttpClient client, int userId, int shiftTypeId, DateOnly? from = null, bool moveFromClashing = false) =>
        client.PostAsJsonAsync("/api/v1/shifts/crew", new PlaceOnCrewRequest(userId, shiftTypeId, from, moveFromClashing));

    private async Task<int> TemplateOfAsync(int activeShiftId)
    {
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        return (await context.ActiveShifts.SingleAsync(x => x.Id == activeShiftId)).SourceShiftTypeId!.Value;
    }

    private static Task<HttpResponseMessage> RemoveAsync(
        HttpClient client, int userId, int shiftTypeId, DateOnly? from) =>
        client.PostAsJsonAsync($"/api/v1/shifts/crew/{userId}/remove", new RemoveFromCrewRequest(shiftTypeId, from));

    /// <summary>Gives a test worker a name of their own, so a search can find exactly them.</summary>
    private async Task<string> RenameAsync(int userId, string firstName)
    {
        var name = $"{firstName} {Guid.NewGuid():N}"[..20];

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        (await context.Users.SingleAsync(x => x.Id == userId)).FullName = name;
        await context.SaveChangesAsync();

        return name;
    }

    // ---------------------------------------------------------------- the pool

    [Fact]
    public async Task A_crew_put_together_on_a_shift_outside_the_pool_leaves_no_history()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var startsOn = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var template = await NewShiftTypeAsync(managerClient, startsOn, DayOfWeek.Monday);

        // Drafting a crew is allowed and changes nothing on record: the shift is not running.
        (await PlaceAsync(managerClient, workerId, template.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var planner = await PlannerAsync(managerClient);
        var drafted = planner.Shifts.Single(x => x.ShiftTypeId == template.Id);
        drafted.RunningShiftId.ShouldBeNull();
        drafted.People.ShouldContain(x => x.UserId == workerId);
        // Still in the team - dragging assigns, it does not move - and the team says where.
        planner.Team.Single(x => x.UserId == workerId).ShiftNames.ShouldContain(template.Name);

        (await BoardAsync(managerClient, startsOn)).Shifts.ShouldNotContain(x => x.SourceShiftTypeId == template.Id);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        (await context.ShiftRosterEntries.AnyAsync(x => x.UserId == workerId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Putting_a_shift_in_the_pool_starts_its_history_with_the_crew_it_has()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var startsOn = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var template = await NewShiftTypeAsync(managerClient, startsOn, DayOfWeek.Monday);

        await PlaceAsync(managerClient, workerId, template.Id);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(template.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var shiftId = (await PlannerAsync(managerClient)).Pool.Single(x => x.SourceShiftTypeId == template.Id).ActiveShiftId;

        // From its first day, the crew it went in with is who works it - and that is on record.
        Card(await BoardAsync(managerClient, startsOn), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);

        (await HistoryDetailAsync(managerClient, shiftId, startsOn))
            .Changes.ShouldContain(x => x.UserId == workerId && x.Kind == ShiftChangeKind.Joined && x.FromDate == startsOn);
    }

    [Fact]
    public async Task Taking_a_shift_out_of_the_pool_keeps_its_crew_for_next_time()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var pooled = await PooledAsync(managerClient, Tomorrow, EveryDay);
        await PlaceAsync(managerClient, workerId, pooled.TemplateId, Tomorrow);

        (await managerClient.DeleteAsync($"/api/v1/shifts/pool/{pooled.ShiftId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Out of the pool only: the shift and its crew are still there in the shift column.
        var planner = await PlannerAsync(managerClient);
        var shift = planner.Shifts.Single(x => x.ShiftTypeId == pooled.TemplateId);
        shift.RunningShiftId.ShouldBeNull();
        shift.People.ShouldContain(x => x.UserId == workerId);

        // Back into the pool, and the same crew is working it again.
        (await managerClient.PostAsJsonAsync("/api/v1/shifts/pool", new AddShiftToPoolRequest(pooled.TemplateId)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var again = (await PlannerAsync(managerClient)).Shifts.Single(x => x.ShiftTypeId == pooled.TemplateId);
        again.RunningShiftId.ShouldNotBeNull();
        again.People.ShouldContain(x => x.UserId == workerId && x.Source == ShiftSource.Roster);
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
    public async Task Taking_a_shift_out_of_the_pool_keeps_the_days_that_were_worked_on_it()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var today = Tomorrow.AddDays(-1);
        int shiftId;

        // Ten days of real history. The API refuses to backdate a rota on purpose, so the
        // past is written straight in - which is also exactly what ten days of using it leaves.
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

            var shift = new ActiveShift
            {
                Name = $"Worked {Guid.NewGuid():N}",
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(14, 0),
                Weekdays = Weekdays.EveryDay,
                StartsOn = today.AddDays(-10)
            };

            context.ActiveShifts.Add(shift);
            await context.SaveChangesAsync();
            shiftId = shift.Id;

            context.ShiftRosterEntries.Add(new ShiftRosterEntry
            {
                UserId = workerId,
                ActiveShiftId = shiftId,
                EffectiveFrom = today.AddDays(-10)
            });
            await context.SaveChangesAsync();
        }

        (await managerClient.DeleteAsync($"/api/v1/shifts/pool/{shiftId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Gone today - closing it "as of today" left it on screen and looked like the button
        // did nothing...
        (await PlannerAsync(managerClient)).Pool.ShouldNotContain(x => x.ActiveShiftId == shiftId);
        (await BoardAsync(managerClient, today)).Shifts.ShouldNotContain(x => x.ActiveShiftId == shiftId);

        // ...but yesterday, when it was actually worked, still reads back.
        Card(await BoardAsync(managerClient, today.AddDays(-1)), shiftId)
            .People.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task A_shift_that_never_ran_is_removed_from_the_pool_entirely()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var pooled = await PooledAsync(managerClient, Tomorrow, EveryDay);
        await PlaceAsync(managerClient, workerId, pooled.TemplateId, Tomorrow);

        (await managerClient.DeleteAsync($"/api/v1/shifts/pool/{pooled.ShiftId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PlannerAsync(managerClient)).Pool.ShouldNotContain(x => x.ActiveShiftId == pooled.ShiftId);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        (await context.ActiveShifts.AnyAsync(x => x.Id == pooled.ShiftId)).ShouldBeFalse("no day was ever worked on it");
        (await context.ShiftRosterEntries.AnyAsync(x => x.UserId == workerId)).ShouldBeFalse("nor was anybody on it");
    }

    // ---------------------------------------------------------------- the planner

    [Fact]
    public async Task A_shift_that_starts_later_is_on_the_planner_before_it_starts()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        // A night shift that begins on a Sunday is put in the pool during the week. Leaving it
        // off the planner until Sunday made it look as though putting it in had failed.
        var startsOn = Tomorrow.AddDays(5);
        var shiftId = await PooledShiftAsync(managerClient, startsOn, DayOfWeek.Sunday);

        var card = (await PlannerAsync(managerClient)).Pool.Single(x => x.ActiveShiftId == shiftId);
        card.StartsOn.ShouldBe(startsOn);
    }

    [Fact]
    public async Task The_planner_shows_who_is_on_a_shift_even_on_a_day_it_does_not_run()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        // A shift that only runs on a day other than tomorrow. On a "who works today" board the
        // worker would look free; on the planner they are on it, because they are.
        var notTomorrow = Tomorrow.AddDays(1).DayOfWeek;
        var shiftId = await PooledShiftAsync(managerClient, Tomorrow, notTomorrow);

        await SetRosterAsync(managerClient, workerId, shiftId, Tomorrow);

        var planner = await PlannerAsync(managerClient);

        var onIt = planner.Shifts.Single(x => x.RunningShiftId == shiftId).People.Single(x => x.UserId == workerId);
        onIt.FromDate.ShouldBe(Tomorrow, "the move has not started yet, so the card says when it does");
        planner.Team.Single(x => x.UserId == workerId).ShiftNames.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Moving_someone_who_already_has_a_move_queued_never_leaves_them_on_two_shifts()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var start = Tomorrow;
        var firstShift = await PooledShiftAsync(managerClient, start, EveryDay);
        var secondShift = await PooledShiftAsync(managerClient, start, EveryDay);

        await SetRosterAsync(managerClient, workerId, firstShift, start);

        // A move is queued for a fortnight's time, which gives the first row an end date...
        await SetRosterAsync(managerClient, workerId, secondShift, start.AddDays(14));

        // ...and then brought forward to next week. The row with an end date still covers the
        // days in between and has to be shortened too, not just the open-ended one.
        await SetRosterAsync(managerClient, workerId, secondShift, start.AddDays(7));

        var inBetween = start.AddDays(10);

        var board = await BoardAsync(managerClient, inBetween);
        board.Shifts.Count(x => x.People.Any(p => p.UserId == workerId)).ShouldBe(1);
        Card(board, secondShift).People.ShouldContain(x => x.UserId == workerId);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        (await context.ShiftRosterEntries.CountAsync(x =>
                x.UserId == workerId && x.EffectiveFrom <= inBetween && x.EffectiveTo >= inBetween))
            .ShouldBe(1);
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

        (await RemoveAsync(managerClient, workerId, await TemplateOfAsync(shiftId), leavingMonday))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

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
        var shiftId = await PooledShiftAsync(managerClient, from, EveryDay);

        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(standInId, shiftId, from, to, "Covering for Ahmet")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var standIn = Card(await BoardAsync(managerClient, from), shiftId)
            .People.Single(x => x.UserId == standInId);

        standIn.Source.ShouldBe(ShiftSource.Cover);
        standIn.ToDate.ShouldBe(to);

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
        (await operatorClient.GetAsync("/api/v1/shifts/planner")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await operatorClient.GetAsync("/api/v1/shifts/history")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
        var shiftId = await PooledShiftAsync(managerClient, monday, EveryDay);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        (await RemoveAsync(managerClient, workerId, await TemplateOfAsync(shiftId), monday.AddDays(3)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var changes = (await HistoryDetailAsync(managerClient, shiftId, monday)).Changes
            .Where(x => x.UserId == workerId)
            .ToList();

        changes.ShouldContain(x => x.Kind == ShiftChangeKind.Joined && x.FromDate == monday);
        changes.ShouldContain(x => x.Kind == ShiftChangeKind.Left && x.FromDate == monday.AddDays(3));
        changes.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.ChangedByName), "the audit answer is who did it");
    }

    [Fact]
    public async Task The_history_week_shows_who_worked_each_day()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (_, standInId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var shiftId = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday);

        await SetRosterAsync(managerClient, workerId, shiftId, monday);

        // Tuesday: off sick, and somebody else covers.
        await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, null, monday.AddDays(1), monday.AddDays(1), "Off sick"));
        await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(standInId, shiftId, monday.AddDays(1), monday.AddDays(1), "Covering"));

        var week = await HistoryDetailAsync(managerClient, shiftId, monday.AddDays(3));

        week.WeekStart.ShouldBe(monday, "any day in a week opens that whole week, Monday first");
        week.Days.Count.ShouldBe(7);

        var worker = week.People.Single(x => x.UserId == workerId).Days;
        worker.Single(x => x.Date == monday).Source.ShouldBe(ShiftSource.Roster);
        worker.Single(x => x.Date == monday.AddDays(1)).Source.ShouldBe(ShiftSource.Off);
        worker.Single(x => x.Date == monday.AddDays(2)).Source.ShouldBe(ShiftSource.None, "not one of its days");

        week.People.Single(x => x.UserId == standInId).Days
            .Single(x => x.Date == monday.AddDays(1)).Source.ShouldBe(ShiftSource.Cover);
    }

    [Fact]
    public async Task History_is_found_by_shift_name_by_a_name_on_it_and_by_date()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var workerName = await RenameAsync(workerId, "Oliver");

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var pooled = await PooledAsync(managerClient, monday, EveryDay);
        await PlaceAsync(managerClient, workerId, pooled.TemplateId, monday);

        var shiftName = (await PlannerAsync(managerClient)).Pool.Single(x => x.ActiveShiftId == pooled.ShiftId).Name;

        async Task<List<HistoryShiftDto>> SearchAsync(string query) =>
            (await managerClient.GetFromJsonAsync<List<HistoryShiftDto>>($"/api/v1/shifts/history?{query}"))!;

        (await SearchAsync($"search={Uri.EscapeDataString(shiftName)}"))
            .ShouldContain(x => x.ActiveShiftId == pooled.ShiftId);

        (await SearchAsync($"search={Uri.EscapeDataString(workerName)}"))
            .ShouldHaveSingleItem().ActiveShiftId.ShouldBe(pooled.ShiftId);

        (await SearchAsync($"on={monday:yyyy-MM-dd}"))
            .ShouldContain(x => x.ActiveShiftId == pooled.ShiftId);

        (await SearchAsync($"on={monday.AddDays(-1):yyyy-MM-dd}"))
            .ShouldNotContain(x => x.ActiveShiftId == pooled.ShiftId, "it had not started yet");
    }

    // ---------------------------------------------------------------- several shifts, clashes

    [Fact]
    public async Task Someone_can_be_on_two_shifts_that_never_share_a_day()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var weekdays = await PooledAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        var weekend = await PooledAsync(managerClient, monday, DayOfWeek.Saturday, DayOfWeek.Sunday);

        // No confirmation asked for, because nothing clashes.
        (await PlaceAsync(managerClient, workerId, weekdays.TemplateId, monday)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PlaceAsync(managerClient, workerId, weekend.TemplateId, monday)).StatusCode.ShouldBe(HttpStatusCode.OK);

        Card(await BoardAsync(managerClient, monday), weekdays.ShiftId)
            .People.ShouldContain(x => x.UserId == workerId);
        Card(await BoardAsync(managerClient, monday.AddDays(5)), weekend.ShiftId)
            .People.ShouldContain(x => x.UserId == workerId);

        var names = (await PlannerAsync(managerClient)).Team.Single(x => x.UserId == workerId).ShiftNames;
        names.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Joining_a_shift_that_shares_days_with_one_they_are_on_needs_confirming_and_then_moves_them()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var morning = await PooledAsync(managerClient, monday, EveryDay);
        var afternoon = await PooledAsync(managerClient, monday, EveryDay);

        await PlaceAsync(managerClient, workerId, morning.TemplateId, monday);

        // Without the confirmation: refused, and nothing changes.
        (await PlaceAsync(managerClient, workerId, afternoon.TemplateId, monday.AddDays(7)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        Card(await BoardAsync(managerClient, monday.AddDays(7)), morning.ShiftId)
            .People.ShouldContain(x => x.UserId == workerId);

        // Confirmed: on afternoons from the date, off mornings from the same date.
        (await PlaceAsync(managerClient, workerId, afternoon.TemplateId, monday.AddDays(7), moveFromClashing: true))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        Card(await BoardAsync(managerClient, monday), morning.ShiftId)
            .People.ShouldContain(x => x.UserId == workerId, "the week before the move is untouched");

        var afterMove = await BoardAsync(managerClient, monday.AddDays(7));
        Card(afterMove, afternoon.ShiftId).People.ShouldContain(x => x.UserId == workerId);
        Card(afterMove, morning.ShiftId).People.ShouldNotContain(x => x.UserId == workerId);

        var team = (await PlannerAsync(managerClient)).Team.Single(x => x.UserId == workerId);
        team.ShiftNames.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_same_person_cannot_be_put_on_the_same_shift_twice()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var template = await NewShiftTypeAsync(managerClient, Tomorrow, DayOfWeek.Monday);

        await PlaceAsync(managerClient, workerId, template.Id);
        (await PlaceAsync(managerClient, workerId, template.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PlannerAsync(managerClient)).Shifts.Single(x => x.ShiftTypeId == template.Id)
            .People.Count(x => x.UserId == workerId).ShouldBe(1);
    }

    [Fact]
    public async Task Cover_on_another_shift_hands_them_back_to_their_own_afterwards()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var start = Tomorrow;
        var own = await PooledShiftAsync(managerClient, start, EveryDay);
        var other = await PooledShiftAsync(managerClient, start, EveryDay);

        await SetRosterAsync(managerClient, workerId, own, start);

        // A move with an end date: cover. Their own shift is not touched.
        (await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, other, start, start.AddDays(1), "Two days on the other line")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        Card(await BoardAsync(managerClient, start), other).People.ShouldContain(x => x.UserId == workerId);
        Card(await BoardAsync(managerClient, start), own).People.ShouldNotContain(x => x.UserId == workerId);

        // The day after it ends they are back where they were, without anybody putting them there.
        Card(await BoardAsync(managerClient, start.AddDays(2)), own).People.ShouldContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task Cover_only_counts_on_the_days_the_covered_shift_runs()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var weekdays = await PooledShiftAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        var weekend = await PooledShiftAsync(managerClient, monday, DayOfWeek.Saturday, DayOfWeek.Sunday);

        await SetRosterAsync(managerClient, workerId, weekend, monday);

        // A week of cover on a Monday-to-Friday shift says nothing about the Saturday.
        await managerClient.PostAsJsonAsync("/api/v1/shifts/cover",
            new CreateCoverRequest(workerId, weekdays, monday, monday.AddDays(6), null));

        Card(await BoardAsync(managerClient, monday.AddDays(2)), weekdays)
            .People.ShouldContain(x => x.UserId == workerId);
        Card(await BoardAsync(managerClient, monday.AddDays(5)), weekend)
            .People.ShouldContain(x => x.UserId == workerId, "Saturday they are still on their weekend shift");
    }

    [Fact]
    public async Task Taking_someone_off_one_shift_leaves_them_on_the_other()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (_, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var monday = NextOccurrenceOf(DayOfWeek.Monday, Tomorrow);
        var weekdays = await PooledAsync(managerClient, monday, DayOfWeek.Monday, DayOfWeek.Tuesday);
        var weekend = await PooledAsync(managerClient, monday, DayOfWeek.Saturday, DayOfWeek.Sunday);

        await PlaceAsync(managerClient, workerId, weekdays.TemplateId, monday);
        await PlaceAsync(managerClient, workerId, weekend.TemplateId, monday);

        (await RemoveAsync(managerClient, workerId, weekend.TemplateId, monday))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        Card(await BoardAsync(managerClient, monday), weekdays.ShiftId)
            .People.ShouldContain(x => x.UserId == workerId);
        (await PlannerAsync(managerClient)).Team.Single(x => x.UserId == workerId)
            .ShiftNames.ShouldHaveSingleItem();
    }
}
