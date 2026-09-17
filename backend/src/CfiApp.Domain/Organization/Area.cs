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

    /// <summary>
    /// Whether a person can be posted here to work - separate from <see cref="IsActive"/>,
    /// which is about whether the room exists at all. The Boiler House and the P Tanks
    /// Room are real, active places a fault can still be reported against; nobody is
    /// stationed in them, so they should not appear as a choice on the "works in" form.
    /// </summary>
    public bool IsWorkArea { get; set; } = true;

    public ICollection<Equipment> Equipment { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
