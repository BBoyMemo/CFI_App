namespace CfiApp.Application.Scheduling;

/// <summary>
/// Works out what somebody is actually on, on a given day, from the things that can decide
/// it. The planner board, the person's own week and the history browser all ask this one
/// question, so the precedence rule is written down once rather than three times:
///
/// cover beats booked leave, booked leave beats the standing rota, and anything left over
/// is a day off.
/// </summary>
public interface IShiftResolver
{
    /// <summary>
    /// Every day in the range for every person asked about, including the days they are not
    /// working - a blank Tuesday is an answer the board has to draw.
    /// </summary>
    Task<IReadOnlyList<ResolvedShiftDayDto>> ResolveAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
