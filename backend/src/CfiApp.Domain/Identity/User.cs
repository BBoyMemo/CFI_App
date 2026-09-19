using CfiApp.Domain.Common;
using CfiApp.Domain.Organization;

namespace CfiApp.Domain.Identity;

public enum UserStatus
{
    /// <summary>Registered but not yet approved by a manager. Cannot log in.</summary>
    PendingApproval = 0,
    Active = 1,
    /// <summary>Left the company or suspended. All sessions end immediately.</summary>
    Disabled = 2,
    /// <summary>
    /// A registration a manager decided not to proceed with - not a real starter, a
    /// duplicate, or not theirs to approve. Distinct from Disabled: this account was
    /// never active in the first place.
    /// </summary>
    Rejected = 3
}

/// <summary>
/// A person with an account. Registration is self service, but the account is inert
/// until a manager approves it and assigns an occupation, a department and work units.
/// </summary>
public sealed class User : Entity, IAuditable
{
    public required string FullName { get; set; }

    /// <summary>Stored lower case so a unique index actually prevents duplicates.</summary>
    public required string Email { get; set; }

    public string? PhoneNumber { get; set; }

    public required string PasswordHash { get; set; }

    /// <summary>
    /// Changes whenever credentials or account status change. Every refresh token carries
    /// the stamp it was issued under, so disabling an account ends all its sessions at once.
    /// </summary>
    public required string SecurityStamp { get; set; }

    public UserStatus Status { get; set; } = UserStatus.PendingApproval;

    public int? RoleId { get; set; }
    public Role? Role { get; set; }

    public int? OccupationId { get; set; }
    public Occupation? Occupation { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>
    /// Interface language, stored on the account rather than on the device: every role has
    /// a language selector, and the choice must follow the person from web to phone.
    /// </summary>
    public string PreferredLanguage { get; set; } = "en";

    public DateTimeOffset? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public int? DisabledByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }

    public ICollection<UserArea> Areas { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Where a person works: the area, which is as deep as this goes. Machines are what a
/// breakdown names, not where somebody is posted, and the unit comes free because every
/// area belongs to one.
/// </summary>
public sealed class UserArea
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int AreaId { get; set; }
    public Area? Area { get; set; }
}
