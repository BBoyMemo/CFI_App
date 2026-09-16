using CfiApp.Domain.Attendance;
using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;

namespace CfiApp.Domain.Scheduling;

/// <summary>
/// A shift pattern. The site starts with three, can edit the hours and add more, and
/// switches unused ones off instead of deleting them - a deleted shift would orphan the
/// rota rows that reference it.
///
/// A night shift crosses midnight, so EndTime earlier than StartTime is normal and
/// deliberate.
/// </summary>
public sealed class ShiftType : Entity, IAuditable, IDeactivatable
{
    public required string Name { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>One person on one shift on one day. This is what drag and drop writes.</summary>
public sealed class ShiftAssignment : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateOnly Date { get; set; }

    public int ShiftTypeId { get; set; }
    public ShiftType? ShiftType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Booked leave. Submitting is the employee signature and approving is the manager
/// signature, so there is no separate signature field - that was a deliberate decision
/// when the paper form was digitised.
/// </summary>
public sealed class HolidayRequest : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Calculated excluding weekends and public holidays, stored as agreed.</summary>
    public int WorkingDays { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// UK bank holidays. Needed so a leave request over Easter does not silently charge the
/// employee days they were not going to work anyway.
/// </summary>
public sealed class PublicHoliday : Entity, IAuditable
{
    public DateOnly Date { get; set; }
    public required string Name { get; set; }
    public string Region { get; set; } = "England and Wales";

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
