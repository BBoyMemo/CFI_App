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

// Where a task stands. Open = nothing reported yet; InProgress = work reported but not finished
// (it carries over day to day); Completed = finished (it is in History).
public enum WorkStatus
{
    Open,
    InProgress,
    Completed,
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

    public WorkStatus Status { get; set; }

    // The latest time the task was (re)completed and by whom. Once set the task belongs to History,
    // even if someone later takes it back into progress.
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public User? CompletedBy { get; set; }

    // Single comment / photo of the time before update cards. Only read by TaskUpdateBackfill, which
    // turns them into the task's first card and clears them.
    public string? CompletionComment { get; set; }
    public string? CompletionPhotoKey { get; set; }

    public List<DailyTaskAssignee> Assignees { get; set; } = [];

    // Planning photos (UpdateId = null) and photos attached to update cards.
    public List<TaskPhoto> Photos { get; set; } = [];

    // What people reported on the task, oldest first: progress, completion, later follow-ups.
    public List<TaskUpdate> Updates { get; set; } = [];

    public bool IsCompleted => Status == WorkStatus.Completed;
    public bool EverCompleted => CompletedAt is not null;
}

// One card under a task: who reported what, when, with which photos, and where it left the task.
public class TaskUpdate
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public DailyTask Task { get; set; } = null!;

    // InProgress or Completed: the status the task had right after this card.
    public WorkStatus Outcome { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string? Comment { get; set; }
}

public enum TaskPhotoKind
{
    // Attached by the manager while planning.
    Plan,
    // Attached to an update card (the stored name predates progress cards).
    Completion,
}

public class TaskPhoto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public TaskPhotoKind Kind { get; set; }
    public DailyTask Task { get; set; } = null!;
    public Guid? UpdateId { get; set; }
    public TaskUpdate? Update { get; set; }
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
