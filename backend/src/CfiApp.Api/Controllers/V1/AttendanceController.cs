using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Attendance;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Attendance;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/attendance")]
[Authorize]
public sealed class AttendanceController(
    CfiAppDbContext context,
    AttendanceService service,
    IManagerScopeReader scopeReader,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("clock")]
    [Authorize(Policy = Permissions.AttendanceClock)]
    [ProducesResponseType(typeof(ClockEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClockEventDto>> Clock(ClockRequest request, CancellationToken cancellationToken)
    {
        var clockEvent = await service.RecordAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Mine), new { }, ToDto(clockEvent));
    }

    /// <summary>The signed-in person's own clock history, most recent first.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.AttendanceViewOwn)]
    [ProducesResponseType(typeof(PagedResult<ClockEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClockEventDto>>> Mine(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        var query = context.ClockEvents.AsNoTracking().Where(x => x.UserId == userId);

        if (from is not null) query = query.Where(x => x.OccurredAtUtc >= from);
        if (to is not null) query = query.Where(x => x.OccurredAtUtc <= to);

        return await ListAsync(query, page, pageSize, cancellationToken);
    }

    /// <summary>
    /// Managers only, and only for the people ManagerScope actually puts in front of them
    /// - a manager with no scope assigned gets an empty team, the same rule /api/v1/team uses.
    /// </summary>
    [HttpGet("team")]
    [Authorize(Policy = Permissions.AttendanceViewTeam)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TeamMemberHoursDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TeamMemberHoursDto>>> Team(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        var usersQuery = context.Users.AsNoTracking().Where(x => x.Status == UserStatus.Active);

        usersQuery = usersQuery.Where(x =>
            (x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
            x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)));

        var userIds = await usersQuery.Select(x => x.Id).ToListAsync(cancellationToken);

        var eventsQuery = context.ClockEvents.AsNoTracking().Where(x => userIds.Contains(x.UserId));
        if (from is not null) eventsQuery = eventsQuery.Where(x => x.OccurredAtUtc >= from);
        if (to is not null) eventsQuery = eventsQuery.Where(x => x.OccurredAtUtc <= to);

        // Materialise first, then build the DTOs in memory - ToDto is plain C#, and EF
        // cannot translate a call to it into SQL.
        var events = await eventsQuery
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new { x.UserId, UserName = x.User!.FullName, Event = x })
            .ToListAsync(cancellationToken);

        var grouped = events
            .GroupBy(x => (x.UserId, x.UserName))
            .Select(g => new TeamMemberHoursDto(g.Key.UserId, g.Key.UserName, [.. g.Select(x => ToDto(x.Event))]))
            .OrderBy(x => x.FullName)
            .ToList();

        return Ok(grouped);
    }

    /// <summary>
    /// Who is on site right now - the Production Manager's "Who's In" screen.
    ///
    /// Being clocked in is decided per person by their own most recent event, not by
    /// counting rows: somebody who forgot to clock out yesterday reads as still in, which
    /// is exactly the state a manager needs to see and fix, rather than a number that
    /// quietly hides it.
    /// </summary>
    [HttpGet("who-is-in")]
    [Authorize(Policy = Permissions.AttendanceViewTeam)]
    [ProducesResponseType(typeof(IReadOnlyCollection<OnSiteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<OnSiteDto>>> WhoIsIn(CancellationToken cancellationToken)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        var people = await context.Users
            .AsNoTracking()
            .Where(x => x.Status == UserStatus.Active)
            .Where(x =>
                (x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
                x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)))
            .Select(x => new { x.Id, x.FullName })
            .ToListAsync(cancellationToken);

        var userIds = people.Select(x => x.Id).ToList();

        // One row per person: their own most recent punch, whenever it was.
        var latest = await context.ClockEvents
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => g.OrderByDescending(e => e.OccurredAtUtc).First())
            .ToListAsync(cancellationToken);

        var stillIn = latest
            .Where(x => x.Type == ClockType.In)
            .ToDictionary(x => x.UserId, x => x.OccurredAtUtc);

        var now = clock.UtcNow;

        var onSite = people
            .Where(x => stillIn.ContainsKey(x.Id))
            .Select(x => new OnSiteDto(
                x.Id, x.FullName, stillIn[x.Id],
                (int)Math.Max(0, (now - stillIn[x.Id]).TotalMinutes)))
            .OrderBy(x => x.FullName)
            .ToList();

        return Ok(onSite);
    }

    [HttpPost("{clockEventId:int}/correction")]
    [Authorize(Policy = Permissions.AttendanceCorrect)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Correct(
        int clockEventId, ClockCorrectionRequest request, CancellationToken cancellationToken)
    {
        await service.CorrectAsync(clockEventId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>The active geofence, without its precise coordinates - the mobile app only needs the tolerances to configure its own background check.</summary>
    [HttpGet("geofence")]
    [ProducesResponseType(typeof(GeofenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeofenceDto>> ActiveGeofence(CancellationToken cancellationToken)
    {
        var geofence = await context.GeofenceSettings.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new GeofenceDto(
                x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusMeters,
                x.ReentryToleranceMinutes, x.RequiredAccuracyMeters, x.MaxClockDriftMinutes, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return geofence is null ? NotFound() : Ok(geofence);
    }

    private static ClockEventDto ToDto(Domain.Attendance.ClockEvent x) => new(
        x.Id, x.Type, x.Source, x.OccurredAtUtc, x.ReceivedAtUtc, x.IsSuspect, x.SuspectReason);

    private static async Task<ActionResult<PagedResult<ClockEventDto>>> ListAsync(
        IQueryable<Domain.Attendance.ClockEvent> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        query = query.OrderByDescending(x => x.OccurredAtUtc);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new ClockEventDto(x.Id, x.Type, x.Source, x.OccurredAtUtc, x.ReceivedAtUtc, x.IsSuspect, x.SuspectReason))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClockEventDto>(items, total, page, pageSize);
    }
}
