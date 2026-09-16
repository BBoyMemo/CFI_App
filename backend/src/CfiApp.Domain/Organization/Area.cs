using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// A room or section inside a unit - Chiller, Filling Room (F11), Melt Room (F7),
/// Boiler Room (F6), Blending (F13/F14), and so on. Workers are assigned at this level,
/// not to individual machines.
/// </summary>
public sealed class Area : Entity, IAuditable, IDeactivatable
{
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    public required string Name { get; set; }
    public string? Code { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Equipment> Equipment { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
