using CfiApp.Domain.Scheduling;

namespace CfiApp.Application.Scheduling;

/// <summary>Turns the stored flags into something a JSON client can loop over, and back.</summary>
public static class WeekdaysExtensions
{
    private static readonly (DayOfWeek Day, Weekdays Flag)[] Map =
    [
        (DayOfWeek.Monday, Weekdays.Monday),
        (DayOfWeek.Tuesday, Weekdays.Tuesday),
        (DayOfWeek.Wednesday, Weekdays.Wednesday),
        (DayOfWeek.Thursday, Weekdays.Thursday),
        (DayOfWeek.Friday, Weekdays.Friday),
        (DayOfWeek.Saturday, Weekdays.Saturday),
        (DayOfWeek.Sunday, Weekdays.Sunday)
    ];

    public static IReadOnlyCollection<DayOfWeek> ToDays(this Weekdays weekdays) =>
        [.. Map.Where(x => weekdays.HasFlag(x.Flag)).Select(x => x.Day)];

    public static Weekdays ToFlags(this IEnumerable<DayOfWeek>? days) =>
        days is null
            ? Weekdays.None
            : days.Aggregate(Weekdays.None, (all, day) =>
                all | Map.First(x => x.Day == day).Flag);

    public static bool Runs(this Weekdays weekdays, DateOnly date) =>
        weekdays.HasFlag(Map.First(x => x.Day == date.DayOfWeek).Flag);
}

// ---------------------------------------------------------------- the planner

/// <summary>Copy a drawn-up shift into the pool, so it starts running with its crew.</summary>
public sealed record AddShiftToPoolRequest(int ShiftTypeId);

/// <summary>
/// Put somebody on a shift's crew. They stay on any other shift that never shares a day with
/// this one. If one does share a day, the request is refused unless MoveFromClashing says the
/// manager has seen the warning and wants them moved - off those shifts, from the date.
///
/// The date only matters when a running shift is involved, because only then does the change
/// go into history; on shifts that are not running it is ignored.
/// </summary>
public sealed record PlaceOnCrewRequest(
    int UserId,
    int ShiftTypeId,
    DateOnly? EffectiveFrom,
    bool MoveFromClashing = false);

/// <summary>Take somebody off one shift's crew. Same rule for the date as above.</summary>
public sealed record RemoveFromCrewRequest(int ShiftTypeId, DateOnly? EffectiveFrom);

/// <summary>
/// Cover, or time off, for a run of days on a running shift. A null shift means "not in" -
/// the same shape records both the person who is away and the person standing in for them.
/// </summary>
public sealed record CreateCoverRequest(
    int UserId,
    int? ActiveShiftId,
    DateOnly FromDate,
    DateOnly ToDate,
    string? Note);

/// <summary>Where a person's shift on a given day came from. Drives what the screen says.</summary>
public enum ShiftSource
{
    None = 0,
    Roster = 1,
    Cover = 2,
    Off = 3,
    Holiday = 4,
    PublicHoliday = 5
}

/// <summary>
/// A name on a shift card. FromDate and ToDate are only set when they matter: cover always
/// has both, a rota entry has FromDate when the move has not started yet and ToDate when it
/// is due to end - otherwise the person is simply on that shift.
/// </summary>
public sealed record RosterPersonDto(
    int UserId,
    string FullName,
    ShiftSource Source,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int? CoverId,
    string? Note);

/// <summary>
/// A shift in the shift column, with its crew. When it is in the pool, RunningShiftId says
/// which pool entry and the crew shown is the dated rota of that entry; when it is not, the
/// crew is the undated draft.
/// </summary>
public sealed record PlannerShiftDto(
    int ShiftTypeId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyCollection<DayOfWeek> Weekdays,
    DateOnly StartsOn,
    int DisplayOrder,
    int? RunningShiftId,
    IReadOnlyCollection<RosterPersonDto> People);

/// <summary>A shift in the pool: the compact card, no names.</summary>
public sealed record PoolShiftDto(
    int ActiveShiftId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyCollection<DayOfWeek> Weekdays,
    DateOnly StartsOn,
    int? SourceShiftTypeId,
    int PeopleCount);

/// <summary>Someone in scope who was not working on a given date.</summary>
public sealed record UnassignedPersonDto(int UserId, string FullName, ShiftSource Source);

/// <summary>
/// Everybody the manager plans for - always all of them, because dragging a name onto a shift
/// assigns it rather than moving it out of here. ShiftNames says which shifts they are already
/// on, so that is visible before anybody is dragged anywhere.
/// </summary>
public sealed record TeamMemberDto(
    int UserId,
    string FullName,
    ShiftSource Source,
    IReadOnlyCollection<string> ShiftNames);

public sealed record PlannerDto(
    DateOnly Today,
    IReadOnlyCollection<PlannerShiftDto> Shifts,
    IReadOnlyCollection<PoolShiftDto> Pool,
    IReadOnlyCollection<TeamMemberDto> Team);

// ---------------------------------------------------------------- one day, resolved

/// <summary>One person, one day, after the rota, cover and leave have been reconciled.</summary>
public sealed record ResolvedShiftDayDto(
    int UserId,
    DateOnly Date,
    int? ActiveShiftId,
    string? ShiftName,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    ShiftSource Source,
    string? Note);

/// <summary>A running shift on a given date, and who was on it that day.</summary>
public sealed record RosterShiftCardDto(
    int ActiveShiftId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyCollection<DayOfWeek> Weekdays,
    DateOnly StartsOn,
    int DisplayOrder,
    int? SourceShiftTypeId,
    IReadOnlyCollection<RosterPersonDto> People);

/// <summary>Everything that was running on one date.</summary>
public sealed record RosterBoardDto(
    DateOnly On,
    IReadOnlyCollection<RosterShiftCardDto> Shifts,
    IReadOnlyCollection<UnassignedPersonDto> Unassigned);

// ---------------------------------------------------------------- shift history

/// <summary>
/// One spell of a shift in the pool. History is only ever about the pool: a shift that was
/// drawn up but never put in has nothing to show here.
/// </summary>
public sealed record HistoryShiftDto(
    int ActiveShiftId,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyCollection<DayOfWeek> Weekdays,
    DateOnly StartsOn,
    DateOnly? EndsOn,
    IReadOnlyCollection<string> People);

public sealed record HistoryDayDto(DateOnly Date, ShiftSource Source, string? Note);

public sealed record HistoryPersonWeekDto(int UserId, string FullName, IReadOnlyCollection<HistoryDayDto> Days);

public enum ShiftChangeKind
{
    Joined = 0,
    Left = 1,
    Cover = 2,
    Off = 3
}

/// <summary>One crew change on a running shift: who, what, from when, and who did it.</summary>
public sealed record ShiftChangeDto(
    int UserId,
    string FullName,
    ShiftChangeKind Kind,
    DateOnly FromDate,
    DateOnly? ToDate,
    DateTimeOffset ChangedAt,
    string? ChangedByName,
    string? Note);

/// <summary>One pool shift in detail: a week of who worked which day, and every change made.</summary>
public sealed record HistoryShiftDetailDto(
    HistoryShiftDto Shift,
    DateOnly WeekStart,
    IReadOnlyCollection<DateOnly> Days,
    IReadOnlyCollection<HistoryPersonWeekDto> People,
    IReadOnlyCollection<ShiftChangeDto> Changes);
