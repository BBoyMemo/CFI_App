using CfiApp.Application.Abstractions;
using CfiApp.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CfiApp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit columns and enforces the append-only rule in one place.
///
/// Doing this in an interceptor rather than in each service is deliberate: BRC
/// traceability must not depend on every future service remembering to set CreatedAt,
/// and an audit trail that a service is able to overwrite is not an audit trail.
/// </summary>
public sealed class AuditableEntityInterceptor(IClock clock, ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAppendOnly &&
                entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} is append-only and cannot be " +
                    $"{entry.State.ToString().ToLowerInvariant()}. Add a new record instead.");
            }

            if (entry.Entity is not IAuditable auditable) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedByUserId ??= userId;
                    break;

                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    auditable.UpdatedByUserId = userId;

                    // A later save must not be able to rewrite who created the row.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedByUserId)).IsModified = false;
                    break;
            }
        }
    }
}
