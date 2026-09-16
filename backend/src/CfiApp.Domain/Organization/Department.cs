using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// An organisational department - the area a manager is responsible for and the target a
/// company message can be addressed to.
/// </summary>
public sealed class Department : Entity, IAuditable, IDeactivatable
{
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
