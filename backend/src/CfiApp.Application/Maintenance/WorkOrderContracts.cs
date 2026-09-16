using CfiApp.Domain.Maintenance;

namespace CfiApp.Application.Maintenance;

public sealed record CreateWorkOrderRequest(
    int UnitId,
    int? AreaId,
    int? LineId,
    int? EquipmentId,
    string? EquipmentFreeText,
    Priority Priority,
    string Description,
    IReadOnlyCollection<int> PhotoAssetIds,
    string? ReportedToName = null,
    string? ReportedToPosition = null);

public sealed record AssignWorkOrderRequest(int EngineerUserId);

public enum EngineerNotificationKind
{
    Busy = 0,
    OnMyWay = 1
}

public sealed record NotifyReporterRequest(EngineerNotificationKind Kind);

public sealed record CloseWorkOrderRequest(
    string RootCause,
    string CorrectiveAction,
    bool AbleToRepair,
    string? UnableToRepairReason,
    bool ContractorRequired,
    string? ContractorUsed,
    int DowntimeMinutes,
    bool ToolsAndPartsAccounted,
    string? MissingItemsNote,
    bool PostDeodorisationIntervention,
    string? PartsRequired,
    decimal? PartsPrice,
    decimal? LabourCostPerHour,
    string? PoNumber,
    IReadOnlyCollection<int> PhotoAssetIds);

public sealed record QaResultRequest(QaResult Result, string? Note);

public sealed record RejectWorkOrderRequest(string Reason);

public sealed record SignOffRequest(bool AreaCleanAndTidy, bool ReleasedBackIntoService);

public sealed record WorkOrderPhotoDto(int Id, int MediaAssetId, PhotoCategory Category, DateTimeOffset CreatedAt);

public sealed record WorkOrderEventDto(
    int Id, WorkOrderEventType Type, string? ActorName, DateTimeOffset OccurredAt, string? Summary);

public sealed record WorkOrderClosureDto(
    int Version,
    string RootCause,
    string CorrectiveAction,
    bool AbleToRepair,
    string? UnableToRepairReason,
    bool ContractorRequired,
    string? ContractorUsed,
    int DowntimeMinutes,
    bool ToolsAndPartsAccounted,
    string? MissingItemsNote,
    bool PostDeodorisationIntervention,
    string SubmittedByName,
    DateTimeOffset SubmittedAt);

public sealed record QaCheckDto(
    int Attempt, QaResult Result, DateTimeOffset RequestedAt, DateTimeOffset? ResultedAt,
    string? ResultByName, string? Note);

public sealed record SignOffDto(
    SignOffKind Kind, string SignedByName, DateTimeOffset SignedAt,
    bool AreaCleanAndTidy, bool ReleasedBackIntoService, bool HasSignatureImage);

public sealed record WorkOrderSummaryDto(
    int Id,
    string Number,
    JobType JobType,
    string UnitName,
    string? AreaName,
    string? LineName,
    string? EquipmentName,
    /// <summary>Set when the fault is on a part of a machine - "Blender 2" for FIBC1.</summary>
    string? EquipmentParentName,
    string? EquipmentFreeText,
    Priority Priority,
    WorkOrderStatus Status,
    string ReportedByName,
    DateTimeOffset ReportedAt,
    string? AssignedEngineerName,
    DateTimeOffset? ClaimedAt,
    int PhotoCount);

public sealed record WorkOrderDetailDto(
    int Id,
    string Number,
    JobType JobType,
    string UnitName,
    string? AreaName,
    string? LineName,
    string? EquipmentName,
    string? EquipmentParentName,
    string? EquipmentFreeText,
    Priority Priority,
    string Description,
    WorkOrderStatus Status,
    int ReportedByUserId,
    string ReportedByName,
    string? ReportedByPosition,
    string? ReportedToName,
    string? ReportedToPosition,
    DateTimeOffset ReportedAt,
    int? AssignedEngineerId,
    string? AssignedEngineerName,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset? ClosedAt,
    int? LabourMinutes,
    IReadOnlyCollection<WorkOrderPhotoDto> Photos,
    IReadOnlyCollection<WorkOrderEventDto> Events,
    IReadOnlyCollection<WorkOrderClosureDto> Closures,
    IReadOnlyCollection<QaCheckDto> QaChecks,
    IReadOnlyCollection<SignOffDto> SignOffs);
