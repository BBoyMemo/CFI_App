namespace DailyTasks.Api.Domain;

public enum Role
{
    Manager,
    Engineer,
}

public enum Shift
{
    Morning,
    Afternoon,
}

public enum Priority
{
    Low,
    Medium,
    High,
}

public enum OrderStatus
{
    New,
    Ordered,
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    // Upper-cased, trimmed copy of Name. Unique, so login by name is unambiguous.
    public string NormalizedName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

public class DailyTask
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    // The day the task is currently planned for. Moves forward on carry-over.
    public DateOnly Date { get; set; }

    // The day the task was first planned for. Differs from Date once carried over.
    public DateOnly OriginalDate { get; set; }
    public Priority Priority { get; set; }
    public Shift Shift { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public User? CompletedBy { get; set; }
    public string? CompletionComment { get; set; }
    public string? CompletionPhotoKey { get; set; }

    public List<DailyTaskAssignee> Assignees { get; set; } = [];

    // Planning photos (manager) and completion photos (engineer). Tasks completed before multiple
    // photos existed keep their single photo in CompletionPhotoKey.
    public List<TaskPhoto> Photos { get; set; } = [];

    public bool IsCompleted => CompletedAt is not null;
}

public enum TaskPhotoKind
{
    // Attached by the manager while planning.
    Plan,
    // Attached by the engineer when completing.
    Completion,
}

public class TaskPhoto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public TaskPhotoKind Kind { get; set; }
    public DailyTask Task { get; set; } = null!;
    public string PhotoKey { get; set; } = "";
    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DailyTaskAssignee
{
    public Guid TaskId { get; set; }
    public DailyTask Task { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public class Order
{
    public Guid Id { get; set; }
    public string Description { get; set; } = "";
    public string? PhotoKey { get; set; }
    public OrderStatus Status { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? OrderedAt { get; set; }
    public Guid? OrderedById { get; set; }
    public User? OrderedBy { get; set; }
}
