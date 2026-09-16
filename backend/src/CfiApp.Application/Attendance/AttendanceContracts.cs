using CfiApp.Domain.Attendance;

namespace CfiApp.Application.Attendance;

public sealed record ClockRequest(
    ClockType Type,
    ClockSource Source,
    DateTimeOffset OccurredAtUtc,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    bool IsMockLocation,
    string? DeviceId,
    Guid ClientId);

public sealed record ClockEventDto(
    int Id,
    ClockType Type,
    ClockSource Source,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset ReceivedAtUtc,
    bool IsSuspect,
    string? SuspectReason);

public sealed record ClockCorrectionRequest(string Reason, DateTimeOffset NewOccurredAtUtc);

public sealed record TeamMemberHoursDto(int UserId, string FullName, IReadOnlyCollection<ClockEventDto> Events);

public sealed record CreateOvertimeRequest(DateOnly Date, int Minutes, string? Note);

public sealed record DecideRequest(bool Approve, string? Note);

public sealed record OvertimeDto(
    int Id, DateOnly Date, int Minutes, string? Note, string Status,
    string RequestedByName, DateTimeOffset RequestedAt, string? DecisionNote);

public sealed record CreateHolidayRequest(DateOnly StartDate, DateOnly EndDate);

public sealed record HolidayRequestDto(
    int Id, DateOnly StartDate, DateOnly EndDate, int WorkingDays, string Status,
    string RequestedByName, DateTimeOffset RequestedAt, string? DecisionNote);

public sealed record UpsertGeofenceRequest(
    string Name,
    double Latitude,
    double Longitude,
    int RadiusMeters,
    int ReentryToleranceMinutes,
    int RequiredAccuracyMeters,
    int MaxClockDriftMinutes);

public sealed record GeofenceDto(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    int RadiusMeters,
    int ReentryToleranceMinutes,
    int RequiredAccuracyMeters,
    int MaxClockDriftMinutes,
    bool IsActive);

/// <summary>
/// MinutesOnSite is worked out on the server, not from the browser's clock. This app
/// already treats a device clock as untrustworthy when it records attendance; it would be
/// odd to then measure someone's shift against it.
/// </summary>
public sealed record OnSiteDto(int UserId, string FullName, DateTimeOffset SinceUtc, int MinutesOnSite);
