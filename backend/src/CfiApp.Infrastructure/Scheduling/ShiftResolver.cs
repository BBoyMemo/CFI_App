using CfiApp.Application.Scheduling;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Scheduling;

/// <inheritdoc />
public sealed class ShiftResolver(CfiAppDbContext context) : IShiftResolver
{
    public async Task<IReadOnlyList<ResolvedShiftDayDto>> ResolveAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || to < from) return [];

        // Four queries whatever the size of the range: everything else is done in memory.
        // A board of forty people over a week is forty times seven answers, and asking the
        // database once per person per day would be two hundred and eighty round trips.
        var ids = userIds.Distinct().ToArray();

        var roster = await context.ShiftRosterEntries.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.EffectiveFrom <= to && x.EffectiveTo >= from)
            .Select(x => new RosterRow(
                x.UserId, x.ActiveShiftId, x.ActiveShift!.Name, x.ActiveShift.StartTime,
                x.ActiveShift.EndTime, x.ActiveShift.Weekdays, x.ActiveShift.StartsOn,
                x.ActiveShift.EndsOn, x.EffectiveFrom, x.EffectiveTo))
            .ToListAsync(cancellationToken);

        var overrides = await context.ShiftOverrides.AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.FromDate <= to && x.ToDate >= from)
            .Select(x => new OverrideRow(
                x.Id, x.UserId, x.ActiveShiftId,
                x.ActiveShift != null ? x.ActiveShift.Name : null,
                x.ActiveShift != null ? x.ActiveShift.StartTime : null,
                x.ActiveShift != null ? x.ActiveShift.EndTime : null,
                x.FromDate, x.ToDate, x.Note))
            .ToListAsync(cancellationToken);

        var leave = await context.HolidayRequests.AsNoTracking()
            .Where(x => ids.Contains(x.UserId)
                && x.Status == ApprovalStatus.Approved
                && x.StartDate <= to && x.EndDate >= from)
            .Select(x => new LeaveRow(x.UserId, x.StartDate, x.EndDate))
            .ToListAsync(cancellationToken);

        var publicHolidays = (await context.PublicHolidays.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to)
            .Select(x => x.Date)
            .ToListAsync(cancellationToken)).ToHashSet();

        var results = new List<ResolvedShiftDayDto>();

        foreach (var userId in ids)
        {
            var userRoster = roster.Where(x => x.UserId == userId).ToArray();
            var userOverrides = overrides.Where(x => x.UserId == userId).ToArray();
            var userLeave = leave.Where(x => x.UserId == userId).ToArray();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                results.Add(ResolveDay(userId, date, userRoster, userOverrides, userLeave, publicHolidays));
            }
        }

        return results;
    }

    private static ResolvedShiftDayDto ResolveDay(
        int userId,
        DateOnly date,
        IReadOnlyCollection<RosterRow> roster,
        IReadOnlyCollection<OverrideRow> overrides,
        IReadOnlyCollection<LeaveRow> leave,
        IReadOnlySet<DateOnly> publicHolidays)
    {
        // Cover first: it is the most recent, most deliberate statement about this day, and
        // it is the one thing a manager types in precisely to contradict everything else.
        var cover = overrides.FirstOrDefault(x => x.FromDate <= date && x.ToDate >= date);

        if (cover is not null)
        {
            return cover.ActiveShiftId is null
                ? new ResolvedShiftDayDto(userId, date, null, null, null, null, ShiftSource.Off, cover.Note)
                : new ResolvedShiftDayDto(
                    userId, date, cover.ActiveShiftId, cover.ShiftName,
                    cover.StartTime, cover.EndTime, ShiftSource.Cover, cover.Note);
        }

        if (leave.Any(x => x.StartDate <= date && x.EndDate >= date))
        {
            return new ResolvedShiftDayDto(userId, date, null, null, null, null, ShiftSource.Holiday, null);
        }

        if (publicHolidays.Contains(date))
        {
            return new ResolvedShiftDayDto(userId, date, null, null, null, null, ShiftSource.PublicHoliday, null);
        }

        // The rota says which shift; the shift itself says which days of the week it runs,
        // and from when. Both have to agree before somebody is in.
        var standing = roster.FirstOrDefault(x =>
            x.EffectiveFrom <= date && x.EffectiveTo >= date
            && x.ShiftStartsOn <= date && x.ShiftEndsOn >= date
            && x.Weekdays.Runs(date));

        return standing is null
            ? new ResolvedShiftDayDto(userId, date, null, null, null, null, ShiftSource.None, null)
            : new ResolvedShiftDayDto(
                userId, date, standing.ActiveShiftId, standing.ShiftName,
                standing.StartTime, standing.EndTime, ShiftSource.Roster, null);
    }

    private sealed record RosterRow(
        int UserId, int ActiveShiftId, string ShiftName, TimeOnly StartTime, TimeOnly EndTime,
        Weekdays Weekdays, DateOnly ShiftStartsOn, DateOnly ShiftEndsOn,
        DateOnly EffectiveFrom, DateOnly EffectiveTo);

    private sealed record OverrideRow(
        int Id, int UserId, int? ActiveShiftId, string? ShiftName, TimeOnly? StartTime, TimeOnly? EndTime,
        DateOnly FromDate, DateOnly ToDate, string? Note);

    private sealed record LeaveRow(int UserId, DateOnly StartDate, DateOnly EndDate);
}
