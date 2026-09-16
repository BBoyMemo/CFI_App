using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;

namespace CfiApp.Domain.Attendance;

public enum ClockType
{
    In = 0,
    Out = 1
}

public enum ClockSource
{
    /// <summary>Recorded automatically when the device entered or left the site geofence.</summary>
    AutoGeofence = 0,
    /// <summary>Pressed by the person, because GPS or the network let them down.</summary>
    Manual = 1
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// One clock in or clock out. This module feeds payroll, so it records not just what the
/// device said but when the server heard it, and flags anything that does not add up
/// rather than quietly trusting a phone clock.
/// </summary>
public sealed class ClockEvent : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public ClockType Type { get; set; }

    /// <summary>When the device says it happened. Not trusted on its own.</summary>
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>When the server received it. Always server time.</summary>
    public DateTimeOffset ReceivedAtUtc { get; set; }

    public ClockSource Source { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>GPS accuracy in metres. A poor fix must not trigger an automatic record.</summary>
    public double? AccuracyMeters { get; set; }

    /// <summary>Android reports when a location came from a mock provider.</summary>
    public bool IsMockLocation { get; set; }

    public string? DeviceId { get; set; }

    /// <summary>Generated on the device so a replayed offline queue lands once.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Set when device time and server time disagree beyond the allowed drift.</summary>
    public bool IsSuspect { get; set; }
    public string? SuspectReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// A correction to a clock event. The original row is never edited - a correction is a
/// new record that points at it, so payroll can always show what was first recorded and
/// who changed it afterwards.
/// </summary>
public sealed class ClockCorrection : Entity, IAppendOnly
{
    public int ClockEventId { get; set; }
    public ClockEvent? ClockEvent { get; set; }

    public int CorrectedByUserId { get; set; }
    public User? CorrectedBy { get; set; }

    public required string Reason { get; set; }
    public DateTimeOffset NewOccurredAtUtc { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Overtime the person declares themselves. It can be cross checked against the real
/// clock events, which is why the site was comfortable with self declaration.
/// </summary>
public sealed class OvertimeDeclaration : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public string? Note { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public int? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// The site boundary used for automatic clocking, and the tolerances around it.
/// Kept in the database rather than in configuration so it can be tuned from the admin
/// panel once the real coordinates and a sensible radius are known.
/// </summary>
public sealed class GeofenceSetting : Entity, IAuditable
{
    public required string Name { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }

    /// <summary>
    /// A short signal drop is not a walk off site: leaving and reappearing inside this
    /// window counts as the same visit rather than a clock out and a new clock in.
    /// </summary>
    public int ReentryToleranceMinutes { get; set; } = 10;

    /// <summary>A fix worse than this never triggers an automatic clock event.</summary>
    public int RequiredAccuracyMeters { get; set; } = 100;

    /// <summary>Device and server time may differ by this much before the record is flagged.</summary>
    public int MaxClockDriftMinutes { get; set; } = 15;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
