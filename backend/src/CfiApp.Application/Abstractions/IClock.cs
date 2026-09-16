namespace CfiApp.Application.Abstractions;

/// <summary>
/// The only source of time in the application. Attendance, shifts and overtime all
/// depend on time behaving predictably, and a British Summer Time boundary has to be
/// testable - which it is not when code calls DateTimeOffset.UtcNow directly.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// The authenticated user for the current request, or null for anonymous and background
/// work. Populated from JWT claims in Phase 2.
/// </summary>
public interface ICurrentUser
{
    int? UserId { get; }
}
