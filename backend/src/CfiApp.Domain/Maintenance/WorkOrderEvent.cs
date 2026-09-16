using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;

namespace CfiApp.Domain.Maintenance;

public enum WorkOrderEventType
{
    Created = 0,
    Claimed = 1,
    Assigned = 2,
    Reassigned = 3,
    StatusChanged = 4,
    /// <summary>Engineer told the reporter "I am busy" or "on my way".</summary>
    ReporterNotified = 5,
    ClosureSubmitted = 6,
    QaRequested = 7,
    QaPassed = 8,
    QaFailed = 9,
    SignedOff = 10,
    Rejected = 11
}

/// <summary>
/// The story of a work order, one row per thing that happened.
///
/// Marked append-only: the DbContext refuses updates and deletes on it. This is what an
/// auditor is shown when they ask who did what and when, so it must be impossible for a
/// later bug - or a later feature - to quietly rewrite it.
/// </summary>
public sealed class WorkOrderEvent : Entity, IAppendOnly
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public WorkOrderEventType Type { get; set; }

    /// <summary>Null for events raised by the system rather than by a person.</summary>
    public int? ActorUserId { get; set; }
    public User? Actor { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Ties the event to the request that caused it, for log correlation.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Short human readable summary, already in English, safe to show in history.</summary>
    public string? Summary { get; set; }

    public WorkOrderStatus? FromStatus { get; set; }
    public WorkOrderStatus? ToStatus { get; set; }
}
