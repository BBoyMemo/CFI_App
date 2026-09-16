using CfiApp.Domain.Common;

namespace CfiApp.Domain.Organization;

/// <summary>
/// What a person does - Operator, Engineer, QA, FLT Driver, Packer. Separate from Role:
/// occupation describes the job, role decides what the account is allowed to do.
/// Managed from the admin panel so a new job title needs no code change.
/// </summary>
public sealed class Occupation : Entity, IAuditable, IDeactivatable
{
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
