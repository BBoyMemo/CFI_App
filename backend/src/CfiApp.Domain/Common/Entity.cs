namespace CfiApp.Domain.Common;

public abstract class Entity
{
    public int Id { get; set; }
}

/// <summary>
/// Records who created or last changed a row. Filled automatically by
/// AuditableEntityInterceptor - never set by hand in a service, because a forgotten
/// assignment is exactly the gap a BRC auditor finds.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    int? CreatedByUserId { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Reference data is switched off rather than deleted: a work order closed three years
/// ago must still be able to name the machine it was raised against.
/// </summary>
public interface IDeactivatable
{
    bool IsActive { get; set; }
}

/// <summary>
/// Marks a table that is only ever inserted into. The DbContext refuses updates and
/// deletes on these at SaveChanges time, so an audit trail cannot be rewritten by a
/// future bug or by a service that was not thinking about it.
/// </summary>
public interface IAppendOnly;
