namespace DailyTasks.Api.Domain;

// Which free-text field of which record a translation belongs to. Names are never translated.
public static class TranslatedEntity
{
    public const string Task = "Task";
    public const string TaskUpdate = "TaskUpdate";
    public const string Order = "Order";
}

public static class TranslatedField
{
    public const string Title = "title";
    public const string Description = "description";
    public const string Comment = "comment";
}

// One field of one record in one language. SourceHash is the hash of the original text it was
// made from: when the original is edited the old translation no longer matches and is not shown.
public class Translation
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Field { get; set; } = "";
    public string Language { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

// A record whose texts still need translating. Kept in the database so nothing is lost when the
// server restarts or the translation service is unreachable for a while.
public class TranslationJob
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
