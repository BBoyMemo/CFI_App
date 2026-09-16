using CfiApp.Application.Abstractions;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Auth;

public sealed class ManagerScopeReader(CfiAppDbContext context) : IManagerScopeReader
{
    public async Task<ManagerVisibility> GetVisibilityAsync(int managerUserId, CancellationToken cancellationToken)
    {
        var manager = await context.Users
            .AsNoTracking()
            .Where(x => x.Id == managerUserId)
            .Select(x => new { x.DepartmentId })
            .FirstOrDefaultAsync(cancellationToken);

        var scopes = await context.ManagerScopes
            .AsNoTracking()
            .Where(x => x.UserId == managerUserId)
            .Select(x => new { x.DepartmentId, x.UnitId })
            .ToListAsync(cancellationToken);

        // A manager runs their own department by default - that is what being a manager
        // means, and it is why an ordinary manager does not need a scope row to see their
        // own people. Scope rows widen that: an extra department, or a unit whose workers
        // report to them regardless of which department they sit in.
        var departmentIds = scopes
            .Where(x => x.DepartmentId.HasValue)
            .Select(x => x.DepartmentId!.Value)
            .Concat(manager?.DepartmentId is { } own ? [own] : Array.Empty<int>())
            .Distinct()
            .ToArray();

        var unitIds = scopes
            .Where(x => x.UnitId.HasValue)
            .Select(x => x.UnitId!.Value)
            .Distinct()
            .ToArray();

        return new ManagerVisibility(departmentIds, unitIds);
    }
}
