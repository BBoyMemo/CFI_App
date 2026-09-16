using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Admin;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// A manager's own people, and nobody else's.
///
/// The filter is their own department plus whatever their ManagerScope rows add, and it
/// is applied in the query itself rather than by trimming a full list afterwards. This
/// holds for every manager including the Maintenance Manager: maintenance sees engineers
/// and QA, production sees operators.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/team")]
[Authorize(Policy = Permissions.AttendanceViewTeam)]
public sealed class TeamController(
    CfiAppDbContext context,
    IManagerScopeReader scopeReader,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TeamMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TeamMemberDto>>> List(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        var query = context.Users
            .AsNoTracking()
            .Where(x => x.Status == UserStatus.Active);

        query = query.Where(x =>
            (x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
            x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)));

        if (!string.IsNullOrWhiteSpace(search))
        {
            // A manager with eighty people needs to find one of them, and paging past four
            // screens to reach a name is not finding it.
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.Email.ToLower().Contains(term));
        }

        query = query.OrderBy(x => x.FullName);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new TeamMemberDto(
                x.Id, x.FullName, x.Email, x.Role!.Name, x.Occupation != null ? x.Occupation.Name : null,
                x.Department != null ? x.Department.Name : null))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<TeamMemberDto>(items, total, page, pageSize));
    }
}
