using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// Top level of the site layout: Unit 1, Unit 2, Unit 3 and the Yard.
/// This is the "Unit" field on the official Factory Equipment Fault Reporting Log, which
/// is why the hierarchy starts here: a Unit contains Areas, an Area contains Equipment.
/// </summary>
public sealed class Unit : Entity, IAuditable, IDeactivatable
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Area> Areas { get; set; } = [];
    public ICollection<Line> Lines { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
