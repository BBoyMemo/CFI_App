using CfiApp.Application.Scheduling;
using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Admin;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Organization;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Departments, occupations and shift types: flat reference lists with no hierarchy.
/// Anyone signed in can read them - a form needs the list to populate a dropdown - but
/// only the Maintenance Manager (admin.manage) can change them. Nothing here is ever hard
/// deleted; a row is switched off instead, so history that points at it stays readable.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/departments")]
[Authorize]
public sealed class DepartmentsController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var query = context.Departments.AsNoTracking().OrderBy(x => x.Name);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new DepartmentDto(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<DepartmentDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DepartmentDto>> Create(
        UpsertDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = new Department { Name = request.Name.Trim() };
        context.Departments.Add(department);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new DepartmentDto(department.Id, department.Name, department.IsActive);
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, UpsertDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await context.Departments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (department is null) return NotFound();

        department.Name = request.Name.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, false, cancellationToken);

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var department = await context.Departments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (department is null) return NotFound();

        department.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/occupations")]
[Authorize]
public sealed class OccupationsController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OccupationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OccupationDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var query = context.Occupations.AsNoTracking().OrderBy(x => x.Name);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new OccupationDto(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<OccupationDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(OccupationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OccupationDto>> Create(
        UpsertOccupationRequest request, CancellationToken cancellationToken)
    {
        var occupation = new Occupation { Name = request.Name.Trim() };
        context.Occupations.Add(occupation);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new OccupationDto(occupation.Id, occupation.Name, occupation.IsActive);
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, UpsertOccupationRequest request, CancellationToken cancellationToken)
    {
        var occupation = await context.Occupations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (occupation is null) return NotFound();

        occupation.Name = request.Name.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, false, cancellationToken);

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var occupation = await context.Occupations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (occupation is null) return NotFound();

        occupation.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// The library of shifts a manager can draw from: name, hours, the days it runs and the day
/// it starts.
///
/// These are templates, not the rota. Putting one into the pool copies it, so deleting one
/// here takes nothing away from the people already working the copy - which is what makes
/// deleting safe, where the old design could only switch a shift off.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/shift-types")]
[Authorize]
public sealed class ShiftTypesController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ShiftTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ShiftTypeDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var query = context.ShiftTypes.AsNoTracking().OrderBy(x => x.DisplayOrder);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new
            {
                x.Id, x.Name, x.StartTime, x.EndTime, x.Weekdays, x.StartsOn, x.DisplayOrder, x.IsActive,
                InPool = context.ActiveShifts.Any(a => a.SourceShiftTypeId == x.Id && a.EndsOn == DateOnly.MaxValue)
            })
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(x => new ShiftTypeDto(
                x.Id, x.Name, x.StartTime, x.EndTime, x.Weekdays.ToDays(), x.StartsOn,
                x.DisplayOrder, x.IsActive, x.InPool))
            .ToList();

        return Ok(new PagedResult<ShiftTypeDto>(dtos, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(typeof(ShiftTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftTypeDto>> Create(
        UpsertShiftTypeRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await context.ShiftTypes.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Problem(
                title: "There is already a shift with that name",
                statusCode: StatusCodes.Status409Conflict);
        }

        var shiftType = new ShiftType
        {
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Weekdays = request.Weekdays.ToFlags(),
            StartsOn = request.StartsOn,
            DisplayOrder = request.DisplayOrder
        };

        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new ShiftTypeDto(
            shiftType.Id, shiftType.Name, shiftType.StartTime, shiftType.EndTime,
            shiftType.Weekdays.ToDays(), shiftType.StartsOn, shiftType.DisplayOrder,
            shiftType.IsActive, false);

        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, UpsertShiftTypeRequest request, CancellationToken cancellationToken)
    {
        var shiftType = await context.ShiftTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (shiftType is null) return NotFound();

        // Editing the template does not touch a copy already in the pool: people are working
        // to that copy, and a rota must not change because somebody tidied up a name.
        shiftType.Name = request.Name.Trim();
        shiftType.StartTime = request.StartTime;
        shiftType.EndTime = request.EndTime;
        shiftType.Weekdays = request.Weekdays.ToFlags();
        shiftType.StartsOn = request.StartsOn;
        shiftType.DisplayOrder = request.DisplayOrder;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes the template. Safe by construction: nothing points at it, because what people
    /// are working is the copy in the pool. Take that out of the pool separately if it should
    /// stop running.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var shiftType = await context.ShiftTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (shiftType is null) return NotFound();

        context.ShiftTypes.Remove(shiftType);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, false, cancellationToken);

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = Permissions.ShiftPlan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var shiftType = await context.ShiftTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (shiftType is null) return NotFound();

        shiftType.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Read-only role list, so the approval screen has something to populate its dropdown
/// with. Roles themselves are seeded system data (Phase 2) - there is no create/edit here
/// on purpose, changing what a role can do means changing Permissions.RoleGrants in code
/// and redeploying, not editing a database row by hand.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/roles")]
[Authorize(Policy = Permissions.UserApprove)]
public sealed class RolesController(CfiAppDbContext context, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Only the roles this approver is allowed to hand out, so the approval screen cannot
    /// offer a choice the server will then refuse. A Production Manager sees Operator; a
    /// Maintenance Manager sees engineers, QA and managers.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RoleDto>>> List(CancellationToken cancellationToken)
    {
        var grantable = await GrantableRoleNamesAsync(context, currentUser.UserId, cancellationToken);

        var roles = await context.Roles.AsNoTracking()
            .Where(x => grantable.Contains(x.Name))
            .OrderBy(x => x.Name)
            .Select(x => new RoleDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }

    /// <summary>
    /// Shared with the approval endpoint so the list the screen shows and the rule the
    /// server enforces can never drift apart.
    /// </summary>
    public static async Task<string[]> GrantableRoleNamesAsync(
        CfiAppDbContext context,
        int? approverUserId,
        CancellationToken cancellationToken)
    {
        var approverRole = await context.Users.AsNoTracking()
            .Where(x => x.Id == approverUserId)
            .Select(x => x.Role!.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (approverRole is null) return [];

        return Permissions.ApprovableRoles.TryGetValue(approverRole, out var grantable)
            ? grantable
            : [];
    }
}

public sealed record RoleDto(int Id, string Name);
