using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// A machine or fixed asset. One table serves both report flows: the operator reaches it
/// through a line, the engineer through a unit and area.
/// </summary>
public sealed class Equipment : Entity, IAuditable, IDeactivatable
{
    public int UnitId { get; set; }
    public Unit? Unit { get; set; }

    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public int? LineId { get; set; }
    public Line? Line { get; set; }

    /// <summary>
    /// Part of a bigger machine rather than a machine of its own - Blender 2's FIBC1 and
    /// FIBC2, for example. A fault can be raised against the part, and it still reads as
    /// belonging to the machine it hangs off.
    /// </summary>
    public int? ParentEquipmentId { get; set; }
    public Equipment? ParentEquipment { get; set; }
    public ICollection<Equipment> Parts { get; set; } = [];

    public required string Name { get; set; }

    /// <summary>Key into the built-in industrial icon set (conveyor, motor, pump, tank, sieve).</summary>
    public string? IconKey { get; set; }

    /// <summary>Used when no icon fits: a real photograph taken by the manager.</summary>
    public int? PhotoAssetId { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
