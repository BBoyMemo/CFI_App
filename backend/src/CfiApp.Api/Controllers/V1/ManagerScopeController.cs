using Asp.Versioning;
using CfiApp.Application.Admin;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Who a manager is responsible for. This is what every "team" and "my unit" query is
/// narrowed by - not something the UI hides, something the database query itself excludes.
/// Managed by the Maintenance Manager (admin.manage), since assigning responsibility is an
/// administrative decision, not something a manager grants themselves.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/manager-scopes")]
[Authorize(Policy = Permissions.AdminManage)]
public sealed class ManagerScopeController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet("{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ManagerScopeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ManagerScopeDto>>> ListForUser(
        int userId, CancellationToken cancellationToken)
    {
        var scopes = await context.ManagerScopes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new ManagerScopeDto(
                x.Id, x.UserId, x.User!.FullName,
                x.DepartmentId, x.Department != null ? x.Department.Name : null,
                x.UnitId, x.Unit != null ? x.Unit.Name : null))
            .ToListAsync(cancellationToken);

        return Ok(scopes);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ManagerScopeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ManagerScopeDto>> Assign(
        AssignManagerScopeRequest request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Problem(title: "Unknown user", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.DepartmentId is not null &&
            !await context.Departments.AnyAsync(x => x.Id == request.DepartmentId, cancellationToken))
        {
            return Problem(title: "Unknown department", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.UnitId is not null &&
            !await context.Units.AnyAsync(x => x.Id == request.UnitId, cancellationToken))
        {
            return Problem(title: "Unknown unit", statusCode: StatusCodes.Status400BadRequest);
        }

        var scope = new ManagerScope
        {
            UserId = request.UserId,
            DepartmentId = request.DepartmentId,
            UnitId = request.UnitId
        };

        context.ManagerScopes.Add(scope);
        await context.SaveChangesAsync(cancellationToken);

        var departmentName = request.DepartmentId is null
            ? null
            : await context.Departments.Where(x => x.Id == request.DepartmentId).Select(x => x.Name).FirstAsync(cancellationToken);

        var unitName = request.UnitId is null
            ? null
            : await context.Units.Where(x => x.Id == request.UnitId).Select(x => x.Name).FirstAsync(cancellationToken);

        var dto = new ManagerScopeDto(
            scope.Id, scope.UserId, user.FullName, scope.DepartmentId, departmentName, scope.UnitId, unitName);

        return CreatedAtAction(nameof(ListForUser), new { userId = scope.UserId }, dto);
    }

    /// <summary>
    /// Unassigning responsibility is a real deletion, unlike everything else in the admin
    /// API: the scope row itself carries no history worth keeping once it no longer applies.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var scope = await context.ManagerScopes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (scope is null) return NotFound();

        context.ManagerScopes.Remove(scope);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
