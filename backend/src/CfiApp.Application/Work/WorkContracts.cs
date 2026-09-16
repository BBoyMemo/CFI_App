using CfiApp.Domain.Work;

namespace CfiApp.Application.Work;

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    TaskKind Kind,
    DateOnly ScheduledDate,
    TaskPriority? Priority,
    IReadOnlyCollection<int> AssignedUserIds);

public sealed record ReassignTaskRequest(IReadOnlyCollection<int> AssignedUserIds);

public sealed record CompleteTaskRequest(string Note, int? PhotoAssetId);

public sealed record TaskAssigneeDto(int UserId, string FullName);

public sealed record TaskCompletionDto(int UserId, string FullName, string Note, DateTimeOffset CompletedAt, bool HasPhoto);

public sealed record TaskSummaryDto(
    int Id,
    string Title,
    TaskKind Kind,
    DateOnly ScheduledDate,
    TaskPriority? Priority,
    IReadOnlyCollection<TaskAssigneeDto> Assignees,
    int CompletionCount);

public sealed record TaskDetailDto(
    int Id,
    string Title,
    string? Description,
    TaskKind Kind,
    DateOnly ScheduledDate,
    TaskPriority? Priority,
    IReadOnlyCollection<TaskAssigneeDto> Assignees,
    IReadOnlyCollection<TaskCompletionDto> Completions);

public sealed record CreatePartOrderRequest(string PartName, int Quantity, bool IsUrgent);

public sealed record PartOrderRequestDto(
    int Id,
    string PartName,
    int Quantity,
    bool IsUrgent,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    string Status,
    DateTimeOffset? OrderedAt);
