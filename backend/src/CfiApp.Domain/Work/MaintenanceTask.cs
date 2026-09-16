using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Media;

namespace CfiApp.Domain.Work;

public enum TaskKind
{
    Daily = 0,
    Weekend = 1,
    ManagerAssigned = 2
}

/// <summary>
/// Optional label the manager may set. Industry standard three, agreed with the site.
/// </summary>
public enum TaskPriority
{
    Reactive = 0,
    Corrective = 1,
    Preventive = 2
}

/// <summary>
/// A job of work that is not a breakdown. Only the Maintenance Manager creates these;
/// an engineer cannot set their own task, which is what the site asked for.
/// </summary>
public sealed class MaintenanceTask : Entity, IAuditable
{
    public required string Title { get; set; }
    public string? Description { get; set; }

    public TaskKind Kind { get; set; }
    public DateOnly ScheduledDate { get; set; }

    /// <summary>Null means the manager did not label it - the label is optional.</summary>
    public TaskPriority? Priority { get; set; }

    /// <summary>Removed from the lists but kept, so completed history stays readable.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
    public int? DeletedByUserId { get; set; }

    public ICollection<TaskAssignment> Assignments { get; set; } = [];
    public ICollection<TaskCompletion> Completions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>One task can go to several engineers.</summary>
public sealed class TaskAssignment
{
    public int MaintenanceTaskId { get; set; }
    public MaintenanceTask? MaintenanceTask { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
}

/// <summary>
/// Short note plus an optional photo. No notification is sent - the manager checks the
/// list, which is how the site wanted it.
/// </summary>
public sealed class TaskCompletion : Entity, IAuditable
{
    public int MaintenanceTaskId { get; set; }
    public MaintenanceTask? MaintenanceTask { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public required string Note { get; set; }
    public DateTimeOffset CompletedAt { get; set; }

    public ICollection<TaskCompletionPhoto> Photos { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public sealed class TaskCompletionPhoto : Entity
{
    public int TaskCompletionId { get; set; }
    public TaskCompletion? TaskCompletion { get; set; }

    public int MediaAssetId { get; set; }
    public MediaAsset? MediaAsset { get; set; }
}

public enum OrderStatus
{
    Pending = 0,
    Ordered = 1
}

/// <summary>
/// "This part has run out." The engineer sees their own requests, the manager sees them
/// all and marks them ordered. No notifications on either side.
/// </summary>
public sealed class PartOrderRequest : Entity, IAuditable
{
    public required string PartName { get; set; }
    public int Quantity { get; set; }
    public bool IsUrgent { get; set; }

    public int RequestedByUserId { get; set; }
    public User? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset? OrderedAt { get; set; }
    public int? OrderedByUserId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public int? DeletedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
