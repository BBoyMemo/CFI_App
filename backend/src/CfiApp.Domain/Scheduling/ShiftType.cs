using CfiApp.Domain.Attendance;
using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;

namespace CfiApp.Domain.Scheduling;

/// <summary>
/// Which days of the week a shift runs. A flags enum stored as an int: the days belong to
/// the shift rather than to each person on it, so they are set once when the shift is drawn
/// up and never have to be split apart per person.
/// </summary>
[Flags]
public enum Weekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,

    WorkingWeek = Monday | Tuesday | Wednesday | Thursday | Friday,
    EveryDay = WorkingWeek | Saturday | Sunday
}

/// <summary>
/// A shift as it is drawn up: a name, the hours, the days it runs and the day it starts.
///
/// This is a template, not something anybody is working. Nothing points at it, which is why
/// it can simply be deleted - what is actually being worked is the copy of it in the pool,
/// and that copy is untouched when the template goes.
///
/// A night shift crosses midnight, so EndTime earlier than StartTime is normal and
/// deliberate.
/// </summary>
public sealed class ShiftType : Entity, IAuditable, IDeactivatable
{
    public required string Name { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public Weekdays Weekdays { get; set; } = Weekdays.WorkingWeek;

    /// <summary>The first day it would run from - a night shift that starts on a Sunday.</summary>
    public DateOnly StartsOn { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Whether it still appears in the library of shifts to draw from.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// A shift that is actually being worked - the pool. People go on these, not on templates.
///
/// It carries its own copy of the name, hours and days rather than reading them from the
/// template it came from. That is the whole point: the template is a starting sketch that
/// can be deleted or edited afterwards, and neither must change a rota people are already
/// working to. <see cref="SourceShiftTypeId"/> is kept only to say where it came from.
///
/// Taking a shift out of the pool sets <see cref="EndsOn"/> rather than deleting the row,
/// so the weeks that were worked on it still read back correctly.
/// </summary>
public sealed class ActiveShift : Entity, IAuditable
{
    public required string Name { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public Weekdays Weekdays { get; set; }

    public DateOnly StartsOn { get; set; }

    /// <summary>Still running while this is <see cref="DateOnly.MaxValue"/>.</summary>
    public DateOnly EndsOn { get; set; } = DateOnly.MaxValue;

    public int DisplayOrder { get; set; }

    public int? SourceShiftTypeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// One person on one shift in the pool, from a date. The same people work the same shift
/// week after week, so this is planned once and repeats until somebody changes it; which
/// days of the week that means comes from the shift itself.
///
/// A change is never an edit and never a delete. The row that was true until now is closed
/// by setting <see cref="EffectiveTo"/>, and a new one opens the next day, which is what
/// makes "who was on nights last month" answerable at audit time.
/// </summary>
public sealed class ShiftRosterEntry : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int ActiveShiftId { get; set; }
    public ActiveShift? ActiveShift { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>
    /// The last day this row applies to. A row that is still in force carries
    /// <see cref="DateOnly.MaxValue"/> rather than null: that is what lets "one person is on
    /// one shift" be a unique index the database enforces, instead of a rule the service has
    /// to remember. A filtered index would say it more plainly but is written in provider
    /// specific SQL, and this schema has to survive a move to SQL Server.
    /// </summary>
    public DateOnly EffectiveTo { get; set; } = DateOnly.MaxValue;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// A short lived exception to the standing rota: somebody covering a few days, or somebody
/// not in at all. It sits on top and never rewrites the rota, so when the cover ends the
/// normal pattern simply resumes.
///
/// <see cref="ActiveShiftId"/> null means "not working these days" - the same row shape
/// covers both halves of "Ahmet is off sick, Piotr is covering his nights".
/// </summary>
public sealed class ShiftOverride : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int? ActiveShiftId { get; set; }
    public ActiveShift? ActiveShift { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public string? Note { get; set; }

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
