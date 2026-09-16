using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Media;

namespace CfiApp.Domain.Maintenance;

/// <summary>
/// The closure form, mirroring the paper Factory Equipment Fault Reporting Log.
///
/// Versioned rather than edited: when QA fails a job the engineer fixes and resubmits,
/// and the earlier version stays readable. An auditor asking "what did it say before it
/// was corrected" has to get an answer.
/// </summary>
public sealed class WorkOrderClosure : Entity, IAuditable
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    /// <summary>1 for the first submission, incremented on every resubmission.</summary>
    public int Version { get; set; }

    public required string RootCause { get; set; }
    public required string CorrectiveAction { get; set; }

    public bool AbleToRepair { get; set; }
    /// <summary>Required when AbleToRepair is false.</summary>
    public string? UnableToRepairReason { get; set; }

    public bool ContractorRequired { get; set; }
    /// <summary>Free text name, required when ContractorRequired is true.</summary>
    public string? ContractorUsed { get; set; }

    public int DowntimeMinutes { get; set; }

    public bool ToolsAndPartsAccounted { get; set; }
    /// <summary>Required when ToolsAndPartsAccounted is false.</summary>
    public string? MissingItemsNote { get; set; }

    /// <summary>
    /// The hygiene gate. Yes sends the job to the QA swab test; the engineer decides,
    /// there is no fixed rule for when it applies.
    /// </summary>
    public bool PostDeodorisationIntervention { get; set; }

    public int SubmittedByUserId { get; set; }
    public User? SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// The cost section of the paper form. Labour hours are filled from the tracked labour
/// time; if someone overrides them, the audit columns record who did.
/// </summary>
public sealed class WorkOrderCost : Entity, IAuditable
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public string? PartsRequired { get; set; }
    public decimal? PartsPrice { get; set; }
    public decimal? LabourHours { get; set; }
    public decimal? LabourCostPerHour { get; set; }
    public string? PoNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public enum QaResult
{
    Pending = 0,
    Pass = 1,
    Fail = 2
}

/// <summary>
/// A QA swab test. One row per attempt, so a job that failed twice shows both attempts.
/// A failing result must carry a note - the engineer cannot be sent back empty handed.
/// </summary>
public sealed class QaCheck : Entity, IAuditable
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int Attempt { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    public QaResult Result { get; set; } = QaResult.Pending;
    public DateTimeOffset? ResultedAt { get; set; }
    public int? ResultByUserId { get; set; }
    public User? ResultBy { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public enum SignOffKind
{
    Production = 0,
    /// <summary>Required only when the work was intrusive.</summary>
    Qa = 1,
    /// <summary>
    /// The person who raised the fault, confirming the machine is actually working again.
    /// They are the one standing at it, and a repair nobody accepted is not a repair.
    /// </summary>
    Reporter = 2
}

/// <summary>
/// The signature block at the bottom of the paper form. The signature image is the one
/// stored on the account, attached here so the record shows what was actually signed with.
/// </summary>
public sealed class SignOff : Entity, IAuditable
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public SignOffKind Kind { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset SignedAt { get; set; }

    public int? SignatureAssetId { get; set; }
    public MediaAsset? SignatureAsset { get; set; }

    public bool AreaCleanAndTidy { get; set; }
    public bool ReleasedBackIntoService { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
