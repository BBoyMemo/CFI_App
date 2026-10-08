using DailyTasks.Api.Domain;

namespace DailyTasks.Api.Contracts;

public record UserRef(Guid Id, string Name);

public record UserDto(Guid Id, string Name, Role Role, DateTimeOffset CreatedAt);

public record LoginRequest(string? Name, string? Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public record CreateUserRequest(string? Name, string? Password, Role? Role);

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
    bool Completed,
    DateTimeOffset? CompletedAt,
    UserRef? CompletedBy,
    string? CompletionComment,
    bool HasPhoto,
    // Photos attached when the task was planned, oldest first: GET /api/tasks/{id}/photos/{photoId}.
    IReadOnlyList<Guid> PhotoIds,
    // Photos the engineer attached when completing, same URL form. (HasPhoto = the older single photo.)
    IReadOnlyList<Guid> CompletionPhotoIds,
    // { language: { field: text } } for "title", "description", "comment"; missing = show the original.
    IReadOnlyDictionary<string, Dictionary<string, string>> Translations);

public record OrderDto(
    Guid Id,
    string Description,
    bool HasPhoto,
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
    public const int MaxTaskPhotos = 5;

    // Multipart bodies carry at most one photo plus a little text...
    public const long UploadRequestBytes = 12 * 1024 * 1024;

    // ...except completing a task, which may carry up to MaxTaskPhotos photos of 10 MB each.
    public const long MultiPhotoRequestBytes = 55 * 1024 * 1024;
}
