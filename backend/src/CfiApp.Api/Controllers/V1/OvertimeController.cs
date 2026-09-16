using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Attendance;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Self-declared overtime, checkable against the real clock events already on file - which
/// is exactly why the site was comfortable letting people declare it themselves rather than
/// requiring a manager to log every minute up front.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/overtime")]
[Authorize]
public sealed class OvertimeController(
    CfiAppDbContext context,
    IClock clock,
    IManagerScopeReader scopeReader,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.OvertimeDeclare)]
    [ProducesResponseType(typeof(OvertimeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OvertimeDto>> Create(CreateOvertimeRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var declaration = new OvertimeDeclaration
        {
            UserId = userId,
            Date = request.Date,
            Minutes = request.Minutes,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        context.OvertimeDeclarations.Add(declaration);
        await context.SaveChangesAsync(cancellationToken);

        var name = await context.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstAsync(cancellationToken);
        return CreatedAtAction(nameof(Mine), new { }, ToDto(declaration, name));
    }

    [HttpGet("mine")]
    [Authorize(Policy = Permissions.OvertimeDeclare)]
    [ProducesResponseType(typeof(PagedResult<OvertimeDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<OvertimeDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        return ListAsync(BaseQuery().Where(x => x.UserId == userId), page, pageSize, cancellationToken);
    }

    /// <summary>Pending declarations for the manager's own team, scoped the same way /api/v1/team is.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.OvertimeApprove)]
    [ProducesResponseType(typeof(PagedResult<OvertimeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OvertimeDto>>> List(
        [FromQuery] ApprovalStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);
        var query = BaseQuery();

        query = query.Where(x =>
            (x.User!.DepartmentId != null && visibility.DepartmentIds.Contains(x.User.DepartmentId.Value)) ||
            x.User.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)));

        if (status is not null) query = query.Where(x => x.Status == status);

        return await ListAsync(query, page, pageSize, cancellationToken);
    }

    [HttpPost("{id:int}/decide")]
    [Authorize(Policy = Permissions.OvertimeApprove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decide(int id, DecideRequest request, CancellationToken cancellationToken)
    {
        var declaration = await context.OvertimeDeclarations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (declaration is null) return NotFound();

        declaration.Status = request.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        declaration.DecidedByUserId = currentUser.UserId;
        declaration.DecidedAt = clock.UtcNow;
        declaration.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<OvertimeDeclaration> BaseQuery() => context.OvertimeDeclarations.AsNoTracking();

    private static OvertimeDto ToDto(OvertimeDeclaration x, string requestedByName) => new(
        x.Id, x.Date, x.Minutes, x.Note, x.Status.ToString(), requestedByName, x.CreatedAt, x.DecisionNote);

    private async Task<ActionResult<PagedResult<OvertimeDto>>> ListAsync(
        IQueryable<OvertimeDeclaration> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        query = query.OrderByDescending(x => x.Date);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new OvertimeDto(
                x.Id, x.Date, x.Minutes, x.Note, x.Status.ToString(), x.User!.FullName, x.CreatedAt, x.DecisionNote))
            .ToListAsync(cancellationToken);

        return new PagedResult<OvertimeDto>(items, total, page, pageSize);
    }
}
