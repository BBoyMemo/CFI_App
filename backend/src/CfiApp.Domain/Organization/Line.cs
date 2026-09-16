using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// A production line. Operators report a breakdown by picking a line first, then the
/// part on it, which is why equipment can hang off a line as well as off an area.
/// </summary>
public sealed class Line : Entity, IAuditable, IDeactivatable
{
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Equipment> Equipment { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
