using CfiApp.Domain.Common;
using CfiApp.Domain.Organization;

namespace CfiApp.Domain.Identity;

/// <summary>
/// A job role. One per user: a Maintenance Manager covers everything an Engineer does by
/// holding the Engineer permissions as well, not by wearing two roles - that keeps
/// "why can this person do that" answerable with a single lookup.
/// </summary>
public sealed class Role : Entity, IAuditable
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>System roles are created by the seeder and cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// A single capability, named module.action. Authorization is always checked against one
/// of these, never against a role name in a controller.
/// </summary>
public sealed class Permission : Entity
{
    public required string Key { get; set; }
    public required string Description { get; set; }
}

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

/// <summary>
/// What a manager is responsible for. Every list query is narrowed by this - a
/// Production Manager must not be able to read another department data by guessing an id,
/// so it is enforced in the query, not by hiding buttons.
/// Exactly one of DepartmentId or UnitId is set.
/// </summary>
public sealed class ManagerScope : Entity, IAuditable
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
