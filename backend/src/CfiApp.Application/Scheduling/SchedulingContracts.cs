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

// ---------------------------------------------------------------- the pool

/// <summary>Copy a drawn-up shift into the pool, so people can be put on it.</summary>
public sealed record AddShiftToPoolRequest(int ShiftTypeId);

/// <summary>A shift that is actually running, and who is on it.</summary>
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

// ---------------------------------------------------------------- the rota

/// <summary>Put somebody on a shift from a date. Which days that means comes from the shift.</summary>
public sealed record SetRosterRequest(int UserId, int ActiveShiftId, DateOnly EffectiveFrom);

/// <summary>Take somebody off the rota from a date. The row is closed, never deleted.</summary>
public sealed record EndRosterRequest(DateOnly EffectiveFrom);

/// <summary>
/// Cover, or time off, for a run of days. A null shift means "not in" - the same shape
/// records both the person who is away and the person standing in for them.
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

/// <summary>A name on a shift card.</summary>
public sealed record RosterPersonDto(
    int UserId,
    string FullName,
    ShiftSource Source,
    DateOnly? CoverFrom,
    DateOnly? CoverTo,
    int? CoverId,
    string? Note);

/// <summary>Someone in the manager's scope who is not on any shift on the browsed date.</summary>
public sealed record UnassignedPersonDto(int UserId, string FullName, ShiftSource Source);

/// <summary>The whole planner board for one date.</summary>
public sealed record RosterBoardDto(
    DateOnly On,
    IReadOnlyCollection<RosterShiftCardDto> Shifts,
    IReadOnlyCollection<UnassignedPersonDto> Unassigned);

/// <summary>One line in the history: what changed, when, and who did it.</summary>
public sealed record ShiftChangeDto(
    int UserId,
    string FullName,
    string? ShiftName,
    DateOnly FromDate,
    DateOnly? ToDate,
    ShiftSource Source,
    DateTimeOffset ChangedAt,
    string? ChangedByName,
    string? Note);
