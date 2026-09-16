using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Attendance;
using CfiApp.Application.Auth;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Attendance feeds payroll, so these tests focus on the cases that would otherwise cost
/// someone real money or real trust: clocking in twice in a row, a GPS fix that is not
/// actually on site, a phone with a wrong clock, and a wrong record that has to be
/// corrected without erasing what was first recorded.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AttendanceTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private const double SiteLat = 53.3654;
    private const double SiteLon = -2.7350;
    private static int _counter;

    private static string NewEmail() =>
        $"att{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

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

            // Mirrors approval, where the department follows the role - without it a
            // manager in these tests would see nobody, which is not the case being tested.
            if (Permissions.RoleDepartments.TryGetValue(roleName, out var departmentName))
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

    private async Task ActivateGeofenceAsync(HttpClient adminClient)
    {
        var response = await adminClient.PostAsJsonAsync("/api/v1/admin/geofences", new UpsertGeofenceRequest(
            "Widnes Site", SiteLat, SiteLon, RadiusMeters: 300,
            ReentryToleranceMinutes: 10, RequiredAccuracyMeters: 50, MaxClockDriftMinutes: 15));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var geofence = (await response.Content.ReadFromJsonAsync<GeofenceDto>())!;

        var activate = await adminClient.PostAsync($"/api/v1/admin/geofences/{geofence.Id}/activate", null);
        activate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Manual_clock_in_and_out_alternate_correctly()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var now = DateTimeOffset.UtcNow;

        var clockIn = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, now, null, null, null, false, "device-1", Guid.NewGuid()));
        clockIn.StatusCode.ShouldBe(HttpStatusCode.Created);

        var clockOut = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.Out, ClockSource.Manual, now.AddHours(8), null, null, null, false, "device-1", Guid.NewGuid()));
        clockOut.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Clocking_in_twice_in_a_row_is_rejected()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var now = DateTimeOffset.UtcNow;

        var first = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, now, null, null, null, false, null, Guid.NewGuid()));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, now.AddMinutes(5), null, null, null, false, null, Guid.NewGuid()));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_very_first_event_for_someone_must_be_a_clock_in_not_a_clock_out()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.Out, ClockSource.Manual, DateTimeOffset.UtcNow, null, null, null, false, null, Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_same_offline_clock_event_replayed_twice_only_creates_one_record()
    {
        var (client, userId) = await SignedInAsAsync(Permissions.Roles.Operator);
        var clientId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, now, null, null, null, false, null, clientId));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var replay = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, now, null, null, null, false, null, clientId));
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        (await context.ClockEvents.CountAsync(x => x.UserId == userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Automatic_clock_in_without_an_active_geofence_is_refused()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.AutoGeofence, DateTimeOffset.UtcNow,
            SiteLat, SiteLon, 15, false, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Automatic_clock_in_inside_the_geofence_with_a_good_fix_succeeds()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        await ActivateGeofenceAsync(adminClient);

        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.AutoGeofence, DateTimeOffset.UtcNow,
            SiteLat, SiteLon, 15, false, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Automatic_clock_in_far_from_the_site_is_refused()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        await ActivateGeofenceAsync(adminClient);

        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        // Roughly 5km north - well outside a 300m radius.
        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.AutoGeofence, DateTimeOffset.UtcNow,
            SiteLat + 0.045, SiteLon, 15, false, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Automatic_clock_in_with_a_poor_gps_fix_is_refused_even_on_site()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        await ActivateGeofenceAsync(adminClient);

        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.AutoGeofence, DateTimeOffset.UtcNow,
            SiteLat, SiteLon, AccuracyMeters: 500, false, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Automatic_clock_in_flagged_as_a_mock_location_is_refused()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        await ActivateGeofenceAsync(adminClient);

        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.AutoGeofence, DateTimeOffset.UtcNow,
            SiteLat, SiteLon, 15, IsMockLocation: true, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_large_gap_between_device_time_and_server_time_is_flagged_but_still_recorded()
    {
        var (client, userId) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, DateTimeOffset.UtcNow.AddHours(-3),
            null, null, null, false, "device-1", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = (await response.Content.ReadFromJsonAsync<ClockEventDto>())!;
        dto.IsSuspect.ShouldBeTrue();
        dto.SuspectReason.ShouldNotBeNullOrWhiteSpace();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        (await context.ClockEvents.CountAsync(x => x.UserId == userId)).ShouldBe(1, "flagged, not rejected");
    }

    [Fact]
    public async Task An_operator_cannot_see_someone_elses_attendance_history()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await workerClient.GetAsync("/api/v1/attendance/team");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_sees_clock_events_only_for_people_in_their_scope()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (managerClient, managerId) = await SignedInAsAsync(Permissions.Roles.ProductionManager);
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Operator);

        // Genuinely outside a Production Manager's reach: an engineer belongs to
        // maintenance, so no scope row of theirs can pull him in.
        var (outsiderClient, outsiderId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var departmentId = (await context.Departments.FirstAsync(x => x.Name == "Production")).Id;
            var worker = await context.Users.FirstAsync(x => x.Id == workerId);
            worker.DepartmentId = departmentId;
            await context.SaveChangesAsync();
        }

        var scopeResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/manager-scopes",
            new CfiApp.Application.Admin.AssignManagerScopeRequest(managerId,
                (await adminClient.GetFromJsonAsync<PagedResult<CfiApp.Application.Admin.DepartmentDto>>(
                    "/api/v1/admin/departments"))!.Items.First(x => x.Name == "Production").Id,
                null));
        scopeResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        await workerClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, DateTimeOffset.UtcNow, null, null, null, false, null, Guid.NewGuid()));
        await outsiderClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, DateTimeOffset.UtcNow, null, null, null, false, null, Guid.NewGuid()));

        var team = await managerClient.GetFromJsonAsync<List<TeamMemberHoursDto>>("/api/v1/attendance/team");

        team.ShouldNotBeNull();
        team.ShouldContain(x => x.UserId == workerId);
        team.ShouldNotContain(x => x.UserId == outsiderId,
            "an engineer belongs to maintenance, not to this manager's production team");
    }

    [Fact]
    public async Task A_manager_can_correct_a_wrong_clock_event_without_erasing_the_original()
    {
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var wrongTime = DateTimeOffset.UtcNow.AddHours(-10);
        var create = await workerClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, wrongTime, null, null, null, false, null, Guid.NewGuid()));
        var created = (await create.Content.ReadFromJsonAsync<ClockEventDto>())!;

        var correction = await managerClient.PostAsJsonAsync($"/api/v1/attendance/{created.Id}/correction",
            new ClockCorrectionRequest("Forgot to clock in at the actual start of the shift.", DateTimeOffset.UtcNow));
        correction.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mine = await workerClient.GetFromJsonAsync<PagedResult<ClockEventDto>>("/api/v1/attendance/mine");
        // PostgreSQL's timestamptz holds microsecond precision, one tick coarser than
        // .NET's DateTimeOffset - the tolerance accounts for that round trip, not for any
        // uncertainty about whether the row was actually left alone.
        mine!.Items.Single().OccurredAtUtc.ShouldBe(wrongTime, TimeSpan.FromMilliseconds(1));

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var storedCorrection = await context.ClockCorrections.SingleAsync(x => x.ClockEventId == created.Id);
        storedCorrection.Reason.ShouldContain("Forgot to clock in");
        _ = workerId;
    }

    [Fact]
    public async Task An_operator_cannot_issue_a_clock_correction()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var create = await workerClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, DateTimeOffset.UtcNow, null, null, null, false, null, Guid.NewGuid()));
        var created = (await create.Content.ReadFromJsonAsync<ClockEventDto>())!;

        var response = await workerClient.PostAsJsonAsync($"/api/v1/attendance/{created.Id}/correction",
            new ClockCorrectionRequest("I want to change this myself.", DateTimeOffset.UtcNow));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------- overtime

    [Fact]
    public async Task An_engineer_declares_overtime_and_a_manager_approves_it()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var create = await engineerClient.PostAsJsonAsync("/api/v1/overtime", new CreateOvertimeRequest(
            DateOnly.FromDateTime(DateTime.UtcNow), 90, "Stayed to finish an urgent repair."));
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var declared = (await create.Content.ReadFromJsonAsync<OvertimeDto>())!;
        declared.Status.ShouldBe(nameof(ApprovalStatus.Pending));

        var decide = await managerClient.PostAsJsonAsync(
            $"/api/v1/overtime/{declared.Id}/decide", new DecideRequest(true, "Confirmed against the clock record."));
        decide.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mine = await engineerClient.GetFromJsonAsync<PagedResult<OvertimeDto>>("/api/v1/overtime/mine");
        mine!.Items.Single(x => x.Id == declared.Id).Status.ShouldBe(nameof(ApprovalStatus.Approved));
    }

    [Fact]
    public async Task An_engineer_cannot_approve_their_own_overtime()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var create = await engineerClient.PostAsJsonAsync("/api/v1/overtime",
            new CreateOvertimeRequest(DateOnly.FromDateTime(DateTime.UtcNow), 30, null));
        var declared = (await create.Content.ReadFromJsonAsync<OvertimeDto>())!;

        (await engineerClient.GetAsync("/api/v1/overtime")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var response = await engineerClient.PostAsJsonAsync(
            $"/api/v1/overtime/{declared.Id}/decide", new DecideRequest(true, null));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------- holiday

    [Fact]
    public async Task A_holiday_request_computes_working_days_excluding_weekends()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var start = new DateOnly(2026, 9, 7); // Monday
        var end = new DateOnly(2026, 9, 13); // Sunday

        var response = await workerClient.PostAsJsonAsync("/api/v1/holiday", new CreateHolidayRequest(start, end));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = (await response.Content.ReadFromJsonAsync<HolidayRequestDto>())!;
        dto.WorkingDays.ShouldBe(5);
        dto.Status.ShouldBe(nameof(ApprovalStatus.Pending));
    }

    [Fact]
    public async Task A_public_holiday_reduces_the_working_day_count()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var addHoliday = await adminClient.PostAsJsonAsync("/api/v1/admin/public-holidays",
            new CreatePublicHolidayRequest(new DateOnly(2026, 12, 25), "Christmas Day", null));
        addHoliday.StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await workerClient.PostAsJsonAsync("/api/v1/holiday", new CreateHolidayRequest(
            new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 25)));

        var dto = (await response.Content.ReadFromJsonAsync<HolidayRequestDto>())!;
        dto.WorkingDays.ShouldBe(1, "Christmas Day itself does not count as a working day taken");
    }

    [Fact]
    public async Task A_manager_approves_a_holiday_request_from_their_own_scoped_team()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var create = await workerClient.PostAsJsonAsync("/api/v1/holiday", new CreateHolidayRequest(
            new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9)));
        var holiday = (await create.Content.ReadFromJsonAsync<HolidayRequestDto>())!;

        var decide = await managerClient.PostAsJsonAsync(
            $"/api/v1/holiday/{holiday.Id}/decide", new DecideRequest(true, null));
        decide.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mine = await workerClient.GetFromJsonAsync<PagedResult<HolidayRequestDto>>("/api/v1/holiday/mine");
        mine!.Items.Single(x => x.Id == holiday.Id).Status.ShouldBe(nameof(ApprovalStatus.Approved));
    }

    [Fact]
    public async Task End_date_before_start_date_is_rejected()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await workerClient.PostAsJsonAsync("/api/v1/holiday", new CreateHolidayRequest(
            new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 5)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- admin

    [Fact]
    public async Task Activating_a_new_geofence_deactivates_the_previous_one()
    {
        var (adminClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        await ActivateGeofenceAsync(adminClient);

        var second = await adminClient.PostAsJsonAsync("/api/v1/admin/geofences", new UpsertGeofenceRequest(
            "Second Site", SiteLat + 1, SiteLon, 200, 10, 50, 15));
        var secondDto = (await second.Content.ReadFromJsonAsync<GeofenceDto>())!;
        await adminClient.PostAsync($"/api/v1/admin/geofences/{secondDto.Id}/activate", null);

        var all = await adminClient.GetFromJsonAsync<List<GeofenceDto>>("/api/v1/admin/geofences");
        all!.Count(x => x.IsActive).ShouldBe(1);
        all!.Single(x => x.IsActive).Name.ShouldBe("Second Site");
    }

    [Fact]
    public async Task An_engineer_cannot_manage_the_geofence()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var response = await engineerClient.PostAsJsonAsync("/api/v1/admin/geofences",
            new UpsertGeofenceRequest("Not allowed", SiteLat, SiteLon, 200, 10, 50, 15));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// "Who's In" answers a question a manager asks by walking the floor: who is here now.
    /// It is decided by each person's own most recent punch, so somebody who forgot to
    /// clock out still reads as in - which is the state that needs correcting, not hiding.
    /// </summary>
    [Fact]
    public async Task Who_is_in_lists_the_people_whose_last_punch_was_an_arrival()
    {
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (workerClient, workerId) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var before = await managerClient.GetFromJsonAsync<List<OnSiteDto>>("/api/v1/attendance/who-is-in");
        before!.ShouldNotContain(x => x.UserId == workerId);

        var arrived = DateTimeOffset.UtcNow.AddHours(-2);
        (await workerClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.In, ClockSource.Manual, arrived, null, null, null, false, "device-1", Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var onSite = await managerClient.GetFromJsonAsync<List<OnSiteDto>>("/api/v1/attendance/who-is-in");
        onSite.ShouldNotBeNull();
        onSite.ShouldContain(x => x.UserId == workerId);
        var row = onSite.Single(x => x.UserId == workerId);
        row.SinceUtc.ShouldBe(arrived, TimeSpan.FromSeconds(1));
        // Counted by the server: two hours ago means two hours, whatever a phone thinks.
        row.MinutesOnSite.ShouldBeInRange(118, 122);

        (await workerClient.PostAsJsonAsync("/api/v1/attendance/clock", new ClockRequest(
            ClockType.Out, ClockSource.Manual, DateTimeOffset.UtcNow, null, null, null, false, "device-1", Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var afterLeaving = await managerClient.GetFromJsonAsync<List<OnSiteDto>>("/api/v1/attendance/who-is-in");
        afterLeaving!.ShouldNotContain(x => x.UserId == workerId);
    }

    [Fact]
    public async Task Who_is_in_is_not_something_a_worker_can_read()
    {
        var (workerClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        (await workerClient.GetAsync("/api/v1/attendance/who-is-in"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
