using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The rota. One row per person per shift per day - what the web drag & drop planner
/// writes on every drop, and what the mobile screen for the person reads back.
///
/// Dropping the same person on the same shift twice is harmless (the unique index makes
/// it a no-op via a 409 the client already ignores); moving someone is a delete of the
/// old slot followed by a create of the new one, not a separate "move" endpoint - two
/// small calls are simpler to reason about than one that has to guess intent.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shifts")]
[Authorize]
public sealed class ShiftsController(
    CfiAppDbContext context,
    IManagerScopeReader scopeReader,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("assignments")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(ShiftAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShiftAssignmentDto>> Create(
        CreateShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        if (!await IsInCallerScopeAsync(request.UserId, cancellationToken))
        {
            return Problem(
                title: "This person is outside the unit you plan for",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var shiftType = await context.ShiftTypes
            .FirstOrDefaultAsync(x => x.Id == request.ShiftTypeId, cancellationToken);

        if (shiftType is null)
        {
            return Problem(title: "Unknown shift type", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!shiftType.IsActive)
        {
            return Problem(
                title: "This shift type is switched off",
                detail: "Reactivate it from the admin panel before planning against it.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var assignment = new ShiftAssignment
        {
            UserId = request.UserId,
            Date = request.Date,
            ShiftTypeId = request.ShiftTypeId
        };

        context.ShiftAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        var userName = await context.Users
            .Where(x => x.Id == request.UserId).Select(x => x.FullName).FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(Board), new { }, ToDto(assignment, userName, shiftType));
    }

    [HttpDelete("assignments/{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var assignment = await context.ShiftAssignments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (assignment is null) return NotFound();

        if (!await IsInCallerScopeAsync(assignment.UserId, cancellationToken))
        {
            return Problem(
                title: "This person is outside the unit you plan for",
                statusCode: StatusCodes.Status403Forbidden);
        }

        context.ShiftAssignments.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>The planner board: everyone in the manager's scope, for a date range.</summary>
    [HttpGet("assignments")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ShiftAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ShiftAssignmentDto>>> Board(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        var query = context.ShiftAssignments.AsNoTracking()
            .Where(x => x.Date >= from && x.Date <= to);

        query = query.Where(x =>
            (x.User!.DepartmentId != null && visibility.DepartmentIds.Contains(x.User.DepartmentId.Value)) ||
            x.User.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)));

        var assignments = await query
            .OrderBy(x => x.Date)
            .Select(x => new ShiftAssignmentDto(
                x.Id, x.UserId, x.User!.FullName, x.Date, x.ShiftTypeId, x.ShiftType!.Name,
                x.ShiftType.StartTime, x.ShiftType.EndTime))
            .ToListAsync(cancellationToken);

        return Ok(assignments);
    }

    /// <summary>What the signed-in person themselves is on the rota for - today and coming up.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.ShiftViewOwn)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ShiftAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ShiftAssignmentDto>>> Mine(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        var rangeStart = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rangeEnd = to ?? rangeStart.AddDays(14);

        var assignments = await context.ShiftAssignments.AsNoTracking()
            .Where(x => x.UserId == userId && x.Date >= rangeStart && x.Date <= rangeEnd)
            .OrderBy(x => x.Date)
            .Select(x => new ShiftAssignmentDto(
                x.Id, x.UserId, x.User!.FullName, x.Date, x.ShiftTypeId, x.ShiftType!.Name,
                x.ShiftType.StartTime, x.ShiftType.EndTime))
            .ToListAsync(cancellationToken);

        return Ok(assignments);
    }

    private async Task<bool> IsInCallerScopeAsync(int targetUserId, CancellationToken cancellationToken)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);

        return await context.Users.AsNoTracking().AnyAsync(x =>
            x.Id == targetUserId &&
            ((x.DepartmentId != null && visibility.DepartmentIds.Contains(x.DepartmentId.Value)) ||
             x.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId))),
            cancellationToken);
    }

    private static ShiftAssignmentDto ToDto(ShiftAssignment assignment, string userName, ShiftType shiftType) => new(
        assignment.Id, assignment.UserId, userName, assignment.Date, assignment.ShiftTypeId, shiftType.Name,
        shiftType.StartTime, shiftType.EndTime);
}
