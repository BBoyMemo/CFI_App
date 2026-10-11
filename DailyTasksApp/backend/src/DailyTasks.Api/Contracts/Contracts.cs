using DailyTasks.Api.Domain;

namespace DailyTasks.Api.Contracts;

public record UserRef(Guid Id, string Name);

public record UserDto(
    Guid Id,
    string Name,
    Role Role,
    DateTimeOffset CreatedAt,
    bool Active,
    // The user asked for a password reset from the login screen (shown to managers).
    bool ResetRequested,
    // A manager set this user's password; they must choose their own before anything else.
    bool MustChangePassword);

public record LoginRequest(string? Name, string? Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public record CreateUserRequest(string? Name, string? Password, Role? Role);

// Password empty = unchanged. A new password means the user must change it at next login.
public record UpdateUserRequest(string? Name, Role? Role, string? Password);

public record ForgotPasswordRequest(string? Name);

public record ChangeNameRequest(string? CurrentPassword, string? NewName);

public record TaskRequest(
    string? Title,
    string? Description,
    DateOnly? Date,
    Priority? Priority,
    Shift? Shift,
    List<Guid>? AssigneeIds);

public record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateOnly Date,
    DateOnly OriginalDate,
    Priority Priority,
    Shift Shift,
    IReadOnlyList<UserRef> Assignees,
    DateTimeOffset CreatedAt,
    WorkStatus Status,
    bool Completed,
    // The latest completion (a task taken back into progress keeps it: it stays in History).
    DateTimeOffset? CompletedAt,
    UserRef? CompletedBy,
    // Photos attached when the task was planned, oldest first: GET /api/tasks/{id}/photos/{photoId}.
    IReadOnlyList<Guid> PhotoIds,
    // The cards under the task, oldest first.
    IReadOnlyList<TaskUpdateDto> Updates,
    // { language: { field: text } } for "title", "description"; missing = show the original.
    IReadOnlyDictionary<string, Dictionary<string, string>> Translations);

public record TaskUpdateDto(
    Guid Id,
    // Where the task stood right after this card: InProgress or Completed.
    WorkStatus Outcome,
    UserRef Author,
    DateTimeOffset CreatedAt,
    string? Comment,
    // Same URL form as the task's own photos.
    IReadOnlyList<Guid> PhotoIds,
    // { language: { "comment": text } }; missing = show the original.
    IReadOnlyDictionary<string, Dictionary<string, string>> Translations);

public record OrderDto(
    Guid Id,
    string Description,
    // Oldest first: GET /api/orders/{id}/photos/{photoId}.
    IReadOnlyList<Guid> PhotoIds,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    UserRef CreatedBy,
    DateTimeOffset? OrderedAt,
    UserRef? OrderedBy,
    // { language: { "description": text } }; missing = show the original.
    IReadOnlyDictionary<string, Dictionary<string, string>> Translations);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Limits
{
    public const int NameMin = 2;
    public const int NameMax = 100;
    public const int PasswordMin = 8;
    public const int PasswordMax = 128;
    public const int TitleMax = 200;
    public const int TextMax = 2000;
    public const int MaxPageSize = 100;
    // Photos on one task (planning), one card or one order.
    public const int MaxPhotos = 10;

    // Multipart bodies carry at most one photo plus a little text...
    public const long UploadRequestBytes = 12 * 1024 * 1024;

    // ...except a card or a new order, which may carry up to MaxPhotos photos of 10 MB each.
    public const long MultiPhotoRequestBytes = 105 * 1024 * 1024;
}
