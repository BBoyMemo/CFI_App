using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The rota, in two levels. A shift is drawn up once as a template - name, hours, the days
/// it runs, the day it starts - and then put into the pool, which is the set of shifts
/// actually being worked. People go on the pool, never on the template, which is why a
/// template can be deleted afterwards without disturbing anybody.
///
/// Within the pool the rota is a standing pattern: somebody is put on a shift from a date and
/// stays there until somebody moves them. Short term cover sits on top as a dated exception.
///
/// Every screen asks the same question through <see cref="IShiftResolver"/>: what is this
/// person on, on this day. That is also what makes the board double as the history browser -
/// ask it about a date three weeks ago and it answers with the rota as it stood then.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shifts")]
[Authorize]
public sealed class ShiftsController(
    CfiAppDbContext context,
    RosterService rosterService,
    IShiftResolver resolver,
    IManagerScopeReader scopeReader,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// The planner board for one date: the pool with the people on each shift, and everyone
    /// in the manager's scope who is not on any of them. One call, because the two lists are
    /// two halves of the same answer and fetching them separately makes them disagree.
    /// </summary>
    [HttpGet("roster")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(RosterBoardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RosterBoardDto>> Board(
        [FromQuery] DateOnly? on = null, CancellationToken cancellationToken = default)
    {
        var date = on ?? SiteCalendar.Today(clock);

        var people = await PeopleInScopeAsync(cancellationToken);
        var resolved = await resolver.ResolveAsync([.. people.Keys], date, date, cancellationToken);

        var pool = await context.ActiveShifts.AsNoTracking()
            .Where(x => x.StartsOn <= date && x.EndsOn >= date)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var covers = await context.ShiftOverrides.AsNoTracking()
            .Where(x => people.Keys.Contains(x.UserId) && x.FromDate <= date && x.ToDate >= date)
            .Select(x => new { x.Id, x.UserId, x.FromDate, x.ToDate })
            .ToListAsync(cancellationToken);

        var cards = pool.Select(shift => new RosterShiftCardDto(
            shift.Id, shift.Name, shift.StartTime, shift.EndTime,
            shift.Weekdays.ToDays(), shift.StartsOn, shift.DisplayOrder, shift.SourceShiftTypeId,
            [.. resolved
                .Where(x => x.ActiveShiftId == shift.Id)
                .OrderBy(x => people[x.UserId])
                .Select(x =>
                {
                    var cover = x.Source == ShiftSource.Cover
                        ? covers.FirstOrDefault(c => c.UserId == x.UserId)
                        : null;

                    return new RosterPersonDto(
                        x.UserId, people[x.UserId], x.Source,
                        cover?.FromDate, cover?.ToDate, cover?.Id, x.Note);
                })]))
            .ToList();

        var unassigned = resolved
            .Where(x => x.ActiveShiftId is null)
            .OrderBy(x => people[x.UserId])
            .Select(x => new UnassignedPersonDto(x.UserId, people[x.UserId], x.Source))
            .ToList();

        return Ok(new RosterBoardDto(date, cards, unassigned));
    }

    // ---------------------------------------------------------------- the pool

    [HttpPost("pool")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddToPool(AddShiftToPoolRequest request, CancellationToken cancellationToken)
    {
        var shift = await rosterService.AddToPoolAsync(request.ShiftTypeId, cancellationToken);
        return CreatedAtAction(nameof(Board), new { on = shift.StartsOn }, null);
    }

    [HttpDelete("pool/{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromPool(int id, CancellationToken cancellationToken) =>
        await rosterService.RemoveFromPoolAsync(id, cancellationToken) ? NoContent() : NotFound();

    // ---------------------------------------------------------------- the rota

    [HttpPost("roster")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetRoster(SetRosterRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(request.UserId, cancellationToken)) return OutsideScope();

        await rosterService.SetAsync(request, cancellationToken);
        return Ok();
    }

    [HttpPost("roster/{userId:int}/end")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EndRoster(
        int userId, EndRosterRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(userId, cancellationToken)) return OutsideScope();

        await rosterService.EndAsync(userId, request, cancellationToken);
        return Ok();
    }

    [HttpPost("cover")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCover(CreateCoverRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(request.UserId, cancellationToken)) return OutsideScope();

        var cover = await rosterService.AddCoverAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Board), new { on = cover.FromDate }, null);
    }

    [HttpDelete("cover/{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCover(int id, CancellationToken cancellationToken)
    {
        var owner = await context.ShiftOverrides.AsNoTracking()
            .Where(x => x.Id == id).Select(x => (int?)x.UserId).FirstOrDefaultAsync(cancellationToken);

        if (owner is null) return NotFound();
        if (!await IsInCallerScopeAsync(owner.Value, cancellationToken)) return OutsideScope();

        await rosterService.RemoveCoverAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// What changed and when, newest first. Rota rows and cover are both changes to
    /// somebody's week, so they are one list rather than two.
    /// </summary>
    [HttpGet("changes")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ShiftChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ShiftChangeDto>>> Changes(
        [FromQuery] int? userId = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (userId is not null && !await IsInCallerScopeAsync(userId.Value, cancellationToken))
        {
            return OutsideScope();
        }

        var people = await PeopleInScopeAsync(cancellationToken);
        var ids = userId is null ? people.Keys.ToArray() : [userId.Value];
        var limit = take is < 1 or > 200 ? 50 : take;

        var rosterChanges = await context.ShiftRosterEntries.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new ShiftChangeDto(
                x.UserId, x.User!.FullName, x.ActiveShift!.Name,
                x.EffectiveFrom, x.EffectiveTo == DateOnly.MaxValue ? null : x.EffectiveTo,
                ShiftSource.Roster, x.CreatedAt,
                context.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                null))
            .ToListAsync(cancellationToken);

        var coverChanges = await context.ShiftOverrides.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new ShiftChangeDto(
                x.UserId, x.User!.FullName,
                x.ActiveShift != null ? x.ActiveShift.Name : null,
                x.FromDate, x.ToDate,
                x.ActiveShiftId == null ? ShiftSource.Off : ShiftSource.Cover, x.CreatedAt,
                context.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                x.Note))
            .ToListAsync(cancellationToken);

        var changes = rosterChanges.Concat(coverChanges)
            .OrderByDescending(x => x.ChangedAt)
            .Take(limit)
            .ToList();

        return Ok(changes);
    }

    /// <summary>The signed-in person's own week, already reconciled - cover and leave included.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.ShiftViewOwn)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ResolvedShiftDayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ResolvedShiftDayDto>>> Mine(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        var rangeStart = from ?? SiteCalendar.Today(clock);
        var rangeEnd = to ?? rangeStart.AddDays(6);

        if (rangeEnd < rangeStart) rangeEnd = rangeStart;

        // A whole year of days is not a screen anybody reads; it is a way to make the server work.
        if (rangeEnd > rangeStart.AddDays(62)) rangeEnd = rangeStart.AddDays(62);

        var days = await resolver.ResolveAsync([userId], rangeStart, rangeEnd, cancellationToken);
        return Ok(days);
    }

    /// <summary>Everyone the caller is allowed to plan for, by id, with their name.</summary>
    private async Task<Dictionary<int, string>> PeopleInScopeAsync(CancellationToken cancellationToken)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        return await context.Users.AsNoTracking()
            .Where(x => x.Status == UserStatus.Active)
            .Where(x =>
                (x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
                x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
    }

    private async Task<bool> IsInCallerScopeAsync(int targetUserId, CancellationToken cancellationToken)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        return await context.Users.AsNoTracking().AnyAsync(x =>
            x.Id == targetUserId &&
            ((x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
             x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId))),
            cancellationToken);
    }

    private ObjectResult OutsideScope() => Problem(
        title: "This person is outside the unit you plan for",
        statusCode: StatusCodes.Status403Forbidden);
}
