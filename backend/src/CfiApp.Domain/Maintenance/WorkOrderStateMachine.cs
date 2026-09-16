namespace CfiApp.Domain.Maintenance;

/// <summary>
/// The only place that decides whether a work order may move from one status to another.
///
/// Keeping this in one table rather than spreading if-statements across controllers means
/// a new endpoint cannot invent a transition by accident, and the whole lifecycle can be
/// read - and tested - in one screen.
/// </summary>
public static class WorkOrderStateMachine
{
    private static readonly IReadOnlyDictionary<WorkOrderStatus, WorkOrderStatus[]> Allowed =
        new Dictionary<WorkOrderStatus, WorkOrderStatus[]>
        {
            // In the pool: a engineer takes it, a manager assigns it, or it is turned down.
            [WorkOrderStatus.New] =
            [
                WorkOrderStatus.Accepted,
                WorkOrderStatus.Rejected
            ],

            // Accepted work can be handed to someone else, so it may go back to the pool.
            [WorkOrderStatus.Accepted] =
            [
                WorkOrderStatus.InProgress,
                WorkOrderStatus.WaitingParts,
                WorkOrderStatus.AwaitingQa,
                WorkOrderStatus.Completed,
                WorkOrderStatus.New,
                WorkOrderStatus.Rejected
            ],

            [WorkOrderStatus.InProgress] =
            [
                WorkOrderStatus.WaitingParts,
                WorkOrderStatus.AwaitingQa,
                WorkOrderStatus.Completed,
                WorkOrderStatus.New,
                WorkOrderStatus.Rejected
            ],

            // Waiting for a part counts as closed for the reporter, but the part can arrive
            // and the job resume.
            [WorkOrderStatus.WaitingParts] =
            [
                WorkOrderStatus.InProgress,
                WorkOrderStatus.AwaitingQa,
                WorkOrderStatus.Completed,
                WorkOrderStatus.Rejected
            ],

            // QA decides: pass completes it, fail sends it back to the engineer.
            [WorkOrderStatus.AwaitingQa] =
            [
                WorkOrderStatus.Completed,
                WorkOrderStatus.QaFailed
            ],

            // The engineer fixes and resubmits the same closure form.
            [WorkOrderStatus.QaFailed] =
            [
                WorkOrderStatus.InProgress,
                WorkOrderStatus.AwaitingQa
            ],

            // Terminal. A completed job is reopened by raising a new one, so the original
            // record stays exactly as it was signed off.
            [WorkOrderStatus.Completed] = [],
            [WorkOrderStatus.Rejected] = []
        };

    public static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to) =>
        from != to && Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyCollection<WorkOrderStatus> NextStatuses(WorkOrderStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool IsTerminal(WorkOrderStatus status) =>
        status is WorkOrderStatus.Completed or WorkOrderStatus.Rejected;

    /// <summary>
    /// Thrown as a 409 by the API: the caller is authenticated and allowed, the job is
    /// simply not in a state where this makes sense - usually because someone else got
    /// there first.
    /// </summary>
    public static void EnsureCanTransition(WorkOrderStatus from, WorkOrderStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidWorkOrderTransitionException(from, to);
        }
    }
}

public sealed class InvalidWorkOrderTransitionException(WorkOrderStatus from, WorkOrderStatus to)
    : InvalidOperationException($"A work order cannot move from {from} to {to}.")
{
    public WorkOrderStatus From { get; } = from;
    public WorkOrderStatus To { get; } = to;
}
