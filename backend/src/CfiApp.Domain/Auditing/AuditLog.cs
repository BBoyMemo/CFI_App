using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;

namespace CfiApp.Domain.Auditing;

/// <summary>
/// Who changed what data, and when.
///
/// This is not the same thing as WorkOrderEvent: that one tells the story of a job in
/// business terms, this one records data changes across the whole system - a user being
/// approved, a unit being switched off, a role grant being edited. An auditor asking
/// "who disabled this account in March" is answered from here.
///
/// Append-only, enforced by the DbContext.
/// </summary>
public sealed class AuditLog : Entity, IAppendOnly
{
    /// <summary>Null for changes made by a background job rather than by a person.</summary>
    public int? ActorUserId { get; set; }
    public User? Actor { get; set; }

    /// <summary>Created, Updated, Deleted, Approved, Disabled, and so on.</summary>
    public required string Action { get; set; }

    public required string EntityType { get; set; }
    public int? EntityId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Ties the change to the request that caused it.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Changed fields as JSON. Stored as text rather than a PostgreSQL json column so the
    /// schema still works if the database moves to SQL Server.
    /// </summary>
    public string? ChangesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
