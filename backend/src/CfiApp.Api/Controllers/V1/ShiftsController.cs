using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The rota. A shift is drawn up with its crew in the shift column; putting it in the pool is
/// what makes it run, and only what runs is history. While a shift is in the pool, every crew
/// change made in the shift column is also written to its dated rota, so the history shows
/// who worked which day and who moved them.
///
/// The questions the screens ask - who worked this day, who is on that shift - all go
/// through <see cref="IShiftResolver"/>, so the precedence rule (cover, then leave, then the
/// rota) is written down once.
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
    // ---------------------------------------------------------------- the planner

    /// <summary>
    /// The planner in one call: the shift column with each shift's crew, the pool, and who
    /// is left over. A running shift shows its dated rota - including a move that has not
    /// started yet, or cover - because that is what people will actually work; one that is
    /// not running shows its draft crew.
    /// </summary>
    [HttpGet("planner")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(PlannerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlannerDto>> Planner(CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);

        var people = await PeopleInScopeAsync(cancellationToken);
        var ids = people.Keys.ToArray();

        var shiftTypes = await context.ShiftTypes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var pool = (await context.ActiveShifts.AsNoTracking()
                .Where(x => x.EndsOn >= today)
                .ToListAsync(cancellationToken))
            .OrderBy(x => x.StartsOn > today)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.StartTime)
            .ToList();

        var poolIds = pool.Select(x => x.Id).ToArray();

        var members = await context.ShiftTypeMembers.AsNoTracking()
            .Where(x => ids.Contains(x.UserId))
            .Select(x => new { x.UserId, x.ShiftTypeId })
            .ToListAsync(cancellationToken);

        var rota = await context.ShiftRosterEntries.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && poolIds.Contains(x.ActiveShiftId) && x.EffectiveTo >= today)
            .Select(x => new { x.UserId, x.ActiveShiftId, x.EffectiveFrom, x.EffectiveTo })
            .ToListAsync(cancellationToken);

        var covers = await context.ShiftOverrides.AsNoTracking()
            .Where(x => ids.Contains(x.UserId)
                && x.ActiveShiftId != null && poolIds.Contains(x.ActiveShiftId.Value)
                && x.ToDate >= today)
            .Select(x => new { x.Id, x.UserId, ActiveShiftId = x.ActiveShiftId!.Value, x.FromDate, x.ToDate, x.Note })
            .ToListAsync(cancellationToken);

        // Today only decides the small "on leave" mark in the team column.
        var sourceToday = (await resolver.ResolveAsync(ids, today, today, cancellationToken))
            .ToDictionary(x => x.UserId, x => x.Source);

        IReadOnlyCollection<RosterPersonDto> CrewOf(ShiftType shift, ActiveShift? running)
        {
            if (running is null)
            {
                return [.. members.Where(m => m.ShiftTypeId == shift.Id)
                    .Select(m => new RosterPersonDto(m.UserId, people[m.UserId], ShiftSource.None, null, null, null, null))
                    .OrderBy(p => p.FullName)];
            }

            return
            [
                .. rota.Where(r => r.ActiveShiftId == running.Id)
                    .Select(r => new RosterPersonDto(
                        r.UserId, people[r.UserId], ShiftSource.Roster,
                        r.EffectiveFrom > today ? r.EffectiveFrom : null,
                        r.EffectiveTo == DateOnly.MaxValue ? null : r.EffectiveTo,
                        null, null))
                    .OrderBy(p => p.FullName),
                .. covers.Where(c => c.ActiveShiftId == running.Id)
                    .Select(c => new RosterPersonDto(
                        c.UserId, people[c.UserId], ShiftSource.Cover, c.FromDate, c.ToDate, c.Id, c.Note))
                    .OrderBy(p => p.FullName)
            ];
        }

        var shifts = shiftTypes.Select(shift =>
        {
            var running = pool.FirstOrDefault(p => p.SourceShiftTypeId == shift.Id);

            return new PlannerShiftDto(
                shift.Id, shift.Name, shift.StartTime, shift.EndTime, shift.Weekdays.ToDays(),
                shift.StartsOn, shift.DisplayOrder, running?.Id, CrewOf(shift, running));
        }).ToList();

        var poolCards = pool.Select(p => new PoolShiftDto(
                p.Id, p.Name, p.StartTime, p.EndTime, p.Weekdays.ToDays(), p.StartsOn, p.SourceShiftTypeId,
                rota.Where(r => r.ActiveShiftId == p.Id).Select(r => r.UserId).Distinct().Count()))
            .ToList();

        // Everybody, always: dragging a name onto a shift assigns it, it does not take it out of
        // the team. Each name carries the shifts it is on - its crews, plus any running shift it
        // is still on whose drawn-up version has since been deleted - so a clash is visible
        // before anybody is dragged anywhere.
        var shiftOrder = shiftTypes.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var shiftNames = shiftTypes.ToDictionary(x => x.Id, x => x.Name);

        var namesByPerson = members
            .Where(m => shiftNames.ContainsKey(m.ShiftTypeId))
            .GroupBy(m => m.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => shiftOrder[m.ShiftTypeId]).Select(m => shiftNames[m.ShiftTypeId]).ToList());

        foreach (var orphan in rota.Where(r =>
                     pool.First(p => p.Id == r.ActiveShiftId).SourceShiftTypeId is not { } source
                     || !shiftNames.ContainsKey(source)))
        {
            var name = pool.First(p => p.Id == orphan.ActiveShiftId).Name;
            var names = namesByPerson.TryGetValue(orphan.UserId, out var list) ? list : namesByPerson[orphan.UserId] = [];
            if (!names.Contains(name)) names.Add(name);
        }

        var team = people
            .OrderBy(x => x.Value)
            .Select(x => new TeamMemberDto(
                x.Key, x.Value, sourceToday.GetValueOrDefault(x.Key),
                namesByPerson.TryGetValue(x.Key, out var names) ? names : []))
            .ToList();

        return Ok(new PlannerDto(today, shifts, poolCards, team));
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
        return CreatedAtAction(nameof(Board), new { on = shift.StartsOn }, new { activeShiftId = shift.Id });
    }

    [HttpDelete("pool/{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromPool(int id, CancellationToken cancellationToken) =>
        await rosterService.RemoveFromPoolAsync(id, cancellationToken) ? NoContent() : NotFound();

    // ---------------------------------------------------------------- the crew

    [HttpPost("crew")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PlaceOnCrew(PlaceOnCrewRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(request.UserId, cancellationToken)) return OutsideScope();

        await rosterService.PlaceOnCrewAsync(request, cancellationToken);
        return Ok();
    }

    [HttpPost("crew/{userId:int}/remove")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveFromCrew(
        int userId, RemoveFromCrewRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(userId, cancellationToken)) return OutsideScope();

        await rosterService.RemoveFromCrewAsync(userId, request, cancellationToken);
        return Ok();
    }

    // ---------------------------------------------------------------- cover

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

    // ---------------------------------------------------------------- one day

    /// <summary>
    /// Everything that was running on one date and who actually worked it. Because a change
    /// closes the old row rather than overwriting it, asking about last March answers with
    /// last March rather than with today.
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

    // ---------------------------------------------------------------- shift history

    /// <summary>
    /// Every shift that has been in the pool, newest first. A shift that was only ever drawn
    /// up has no history and is not here. One search box covers a shift's name and the names
    /// of anybody who was on it; the date narrows it to what was running that day.
    /// </summary>
    [HttpGet("history")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(IReadOnlyCollection<HistoryShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<HistoryShiftDto>>> History(
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? on = null,
        CancellationToken cancellationToken = default)
    {
        var people = await PeopleInScopeAsync(cancellationToken);
        var ids = people.Keys.ToArray();

        var query = context.ActiveShifts.AsNoTracking();
        if (on is not null) query = query.Where(x => x.StartsOn <= on && x.EndsOn >= on);

        var shifts = await query
            .OrderByDescending(x => x.StartsOn).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var shiftIds = shifts.Select(x => x.Id).ToArray();

        var everOn = await context.ShiftRosterEntries.AsNoTracking()
            .Where(x => shiftIds.Contains(x.ActiveShiftId) && ids.Contains(x.UserId))
            .Select(x => new { x.ActiveShiftId, x.UserId, x.EffectiveFrom, x.EffectiveTo })
            .ToListAsync(cancellationToken);

        var term = search?.Trim();

        var result = shifts
            .Select(shift =>
            {
                // On a chosen date, the people on it that day; otherwise everybody it has had.
                var rows = everOn.Where(r => r.ActiveShiftId == shift.Id);
                if (on is not null) rows = rows.Where(r => r.EffectiveFrom <= on && r.EffectiveTo >= on);

                return new HistoryShiftDto(
                    shift.Id, shift.Name, shift.StartTime, shift.EndTime, shift.Weekdays.ToDays(),
                    shift.StartsOn, shift.EndsOn == DateOnly.MaxValue ? null : shift.EndsOn,
                    [.. rows.Select(r => people[r.UserId]).Distinct().Order()]);
            })
            .Where(x => string.IsNullOrEmpty(term)
                || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.People.Any(p => p.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// One shift's history in detail: a week of who worked which day - cover, time off and
    /// leave included - and every crew change made while it was in the pool.
    /// </summary>
    [HttpGet("history/{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(HistoryShiftDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HistoryShiftDetailDto>> HistoryDetail(
        int id, [FromQuery] DateOnly? week = null, CancellationToken cancellationToken = default)
    {
        var shift = await context.ActiveShifts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (shift is null) return NotFound();

        var people = await PeopleInScopeAsync(cancellationToken);
        var ids = people.Keys.ToArray();

        // Default to this week, or to the shift's last week if it has already ended, so opening
        // a shift that stopped months ago does not land on an empty grid.
        var today = SiteCalendar.Today(clock);
        var anchor = week ?? (shift.EndsOn < today ? shift.EndsOn : today);
        var monday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
        var sunday = monday.AddDays(6);
        var days = Enumerable.Range(0, 7).Select(monday.AddDays).ToList();

        var rows = await context.ShiftRosterEntries.AsNoTracking()
            .Where(x => x.ActiveShiftId == id && ids.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId, x.EffectiveFrom, x.EffectiveTo, x.CreatedAt, x.UpdatedAt,
                CreatedBy = context.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault(),
                UpdatedBy = context.Users.Where(u => u.Id == x.UpdatedByUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var covers = await context.ShiftOverrides.AsNoTracking()
            .Where(x => x.ActiveShiftId == id && ids.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId, x.FromDate, x.ToDate, x.Note, x.CreatedAt,
                CreatedBy = context.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var crewIds = rows.Select(x => x.UserId).Distinct().ToArray();

        // Days off matter here only for people who were on this shift at the time.
        var offs = await context.ShiftOverrides.AsNoTracking()
            .Where(x => x.ActiveShiftId == null && crewIds.Contains(x.UserId))
            .Select(x => new
            {
                x.UserId, x.FromDate, x.ToDate, x.Note, x.CreatedAt,
                CreatedBy = context.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // The week grid: everybody whose rota or cover on this shift touches the week.
        var inWeek = rows.Where(r => r.EffectiveFrom <= sunday && r.EffectiveTo >= monday).Select(r => r.UserId)
            .Concat(covers.Where(c => c.FromDate <= sunday && c.ToDate >= monday).Select(c => c.UserId))
            .Distinct()
            .ToArray();

        var resolved = await resolver.ResolveAsync(inWeek, monday, sunday, cancellationToken);

        HistoryDayDto DayFor(int userId, DateOnly day)
        {
            var answer = resolved.First(r => r.UserId == userId && r.Date == day);

            if (answer.ActiveShiftId == id) return new HistoryDayDto(day, answer.Source, answer.Note);

            // Not on this shift that day. Say why only if they otherwise would have been: on
            // its rota, on one of its days, while it was running.
            var wouldHaveBeen = shift.StartsOn <= day && shift.EndsOn >= day && shift.Weekdays.Runs(day)
                && rows.Any(r => r.UserId == userId && r.EffectiveFrom <= day && r.EffectiveTo >= day);

            if (!wouldHaveBeen || answer.Source == ShiftSource.None) return new HistoryDayDto(day, ShiftSource.None, null);

            // Covering somewhere else counts as away from this one.
            return new HistoryDayDto(
                day, answer.Source == ShiftSource.Cover ? ShiftSource.Off : answer.Source, answer.Note);
        }

        var grid = inWeek
            .Select(userId => new HistoryPersonWeekDto(
                userId, people[userId], [.. days.Select(day => DayFor(userId, day))]))
            .OrderBy(x => x.FullName)
            .ToList();

        // Crew changes made while it was in the pool, cover and time off. The shift being taken
        // out of the pool is not a crew change, so a row ending exactly when the shift ended is
        // not reported as somebody leaving.
        var changes = rows
            .Select(r => new ShiftChangeDto(
                r.UserId, people[r.UserId], ShiftChangeKind.Joined, r.EffectiveFrom, null,
                r.CreatedAt, r.CreatedBy, null))
            .Concat(rows
                .Where(r => r.EffectiveTo != DateOnly.MaxValue && r.EffectiveTo != shift.EndsOn)
                .Select(r => new ShiftChangeDto(
                    r.UserId, people[r.UserId], ShiftChangeKind.Left, r.EffectiveTo.AddDays(1), null,
                    r.UpdatedAt ?? r.CreatedAt, r.UpdatedBy ?? r.CreatedBy, null)))
            .Concat(covers.Select(c => new ShiftChangeDto(
                c.UserId, people[c.UserId], ShiftChangeKind.Cover, c.FromDate, c.ToDate,
                c.CreatedAt, c.CreatedBy, c.Note)))
            .Concat(offs
                .Where(o => rows.Any(r => r.UserId == o.UserId && r.EffectiveFrom <= o.ToDate && r.EffectiveTo >= o.FromDate))
                .Select(o => new ShiftChangeDto(
                    o.UserId, people[o.UserId], ShiftChangeKind.Off, o.FromDate, o.ToDate,
                    o.CreatedAt, o.CreatedBy, o.Note)))
            .OrderByDescending(x => x.FromDate)
            .ThenByDescending(x => x.ChangedAt)
            .ToList();

        var summary = new HistoryShiftDto(
            shift.Id, shift.Name, shift.StartTime, shift.EndTime, shift.Weekdays.ToDays(),
            shift.StartsOn, shift.EndsOn == DateOnly.MaxValue ? null : shift.EndsOn,
            [.. rows.Where(r => r.EffectiveFrom <= sunday && r.EffectiveTo >= monday)
                .Select(r => people[r.UserId]).Distinct().Order()]);

        return Ok(new HistoryShiftDetailDto(summary, monday, days, grid, changes));
    }

    // ---------------------------------------------------------------- the worker's own week

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
