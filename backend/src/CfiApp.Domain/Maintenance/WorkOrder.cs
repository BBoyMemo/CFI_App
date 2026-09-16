using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Media;
using CfiApp.Domain.Organization;

namespace CfiApp.Domain.Maintenance;

/// <summary>
/// What kind of work this is. Reactive is a breakdown somebody reported; the other three
/// are planned and only a manager raises them.
/// </summary>
public enum JobType
{
    /// <summary>Something broke. The report-a-breakdown flow always produces this.</summary>
    Reactive = 0,
    Task = 1,
    /// <summary>Planned Preventive Maintenance - the monthly schedule.</summary>
    Ppm = 2,
    Project = 3
}

public enum WorkOrderStatus
{
    /// <summary>In the pool. Nobody has taken it.</summary>
    New = 0,
    /// <summary>A engineer has taken it on.</summary>
    Accepted = 1,
    InProgress = 2,
    /// <summary>Parked with a stated reason: the part has been ordered.</summary>
    WaitingParts = 3,
    /// <summary>Closure submitted and intrusive, so QA has to swab before it can close.</summary>
    AwaitingQa = 4,
    /// <summary>QA failed it. Back with the engineer, who edits the same closure form.</summary>
    QaFailed = 5,
    Completed = 6,
    /// <summary>Turned down with a reason - not a fault, a duplicate, or not ours.</summary>
    Rejected = 7
}

/// <summary>
/// A reported breakdown. This is the record a BRC auditor asks for, so every state change
/// is written to WorkOrderEvent and the closure form is versioned rather than edited.
/// </summary>
public sealed class WorkOrder : Entity, IAuditable
{
    /// <summary>Human readable reference, e.g. WO-1044. Quoted on the floor and in emails.</summary>
    public required string Number { get; set; }

    /// <summary>
    /// Reactive unless a manager says otherwise. Stored on the job rather than inferred
    /// from who raised it, because the same form covers all four kinds of work.
    /// </summary>
    public JobType JobType { get; set; } = JobType.Reactive;

    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public int? LineId { get; set; }
    public Line? Line { get; set; }

    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    /// <summary>
    /// Used when the broken thing is not on the list yet. The engineer flow deliberately
    /// allows free text so an unexpected fault is never blocked by missing reference data.
    /// </summary>
    public string? EquipmentFreeText { get; set; }

    /// <summary>
    /// The four name-and-position boxes at the top of the Factory Equipment Fault
    /// Reporting Log. Positions are stored as text rather than looked up later: the form
    /// records what somebody was on the day, and an occupation changed next year must not
    /// rewrite a report an auditor already saw.
    /// </summary>
    public string? ReportedByPosition { get; set; }

    public string? ReportedToName { get; set; }
    public string? ReportedToPosition { get; set; }

    public int ReportedByUserId { get; set; }
    public User? ReportedBy { get; set; }
    public DateTimeOffset ReportedAt { get; set; }

    public Priority Priority { get; set; }
    public required string Description { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.New;

    public int? AssignedEngineerId { get; set; }
    public User? AssignedEngineer { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }

    /// <summary>
    /// Time on the job, measured from claim to close. Never typed in by hand - the site
    /// asked for this explicitly, and a typed number is a number nobody trusts at audit.
    /// </summary>
    public int? LabourMinutes { get; set; }

    /// <summary>Id generated on the device, so an offline report sent twice lands once.</summary>
    public Guid? ClientId { get; set; }

    public ICollection<WorkOrderPhoto> Photos { get; set; } = [];
    public ICollection<WorkOrderEvent> Events { get; set; } = [];
    public ICollection<WorkOrderClosure> Closures { get; set; } = [];
    public ICollection<QaCheck> QaChecks { get; set; } = [];
    public ICollection<SignOff> SignOffs { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public enum PhotoCategory
{
    /// <summary>Taken by the reporter when raising the breakdown.</summary>
    Report = 0,
    /// <summary>Evidence for the tools and parts accounted question.</summary>
    ToolsAndParts = 1,
    /// <summary>Taken by the engineer when closing.</summary>
    Closing = 2
}

public sealed class WorkOrderPhoto : Entity, IAuditable
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }

    public PhotoCategory Category { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
