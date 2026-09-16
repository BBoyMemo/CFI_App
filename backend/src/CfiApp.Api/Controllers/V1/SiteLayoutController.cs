using Asp.Versioning;
using CfiApp.Application.Admin;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Organization;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The site hierarchy: Unit contains Areas, Areas and Lines contain Equipment.
/// Read access is open to any signed in user - the breakdown report screens for every
/// role depend on these lists. Only admin.manage can change them, and nothing here is
/// ever hard deleted.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/units")]
[Authorize]
public sealed class UnitsController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UnitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UnitDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var query = context.Units.AsNoTracking().OrderBy(x => x.DisplayOrder);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new UnitDto(x.Id, x.Name, x.Code, x.DisplayOrder, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<UnitDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(UnitDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UnitDto>> Create(UpsertUnitRequest request, CancellationToken cancellationToken)
    {
        var unit = new Unit { Name = request.Name.Trim(), Code = request.Code.Trim(), DisplayOrder = request.DisplayOrder };
        context.Units.Add(unit);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new UnitDto(unit.Id, unit.Name, unit.Code, unit.DisplayOrder, unit.IsActive);
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpsertUnitRequest request, CancellationToken cancellationToken)
    {
        var unit = await context.Units.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (unit is null) return NotFound();

        unit.Name = request.Name.Trim();
        unit.Code = request.Code.Trim();
        unit.DisplayOrder = request.DisplayOrder;

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
        var unit = await context.Units.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (unit is null) return NotFound();

        unit.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/areas")]
[Authorize]
public sealed class AreasController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AreaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AreaDto>>> List(
        [FromQuery] int? unitId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = context.Areas.AsNoTracking().Include(x => x.Unit).AsQueryable();

        if (unitId is not null)
        {
            query = query.Where(x => x.UnitId == unitId);
        }

        query = query.OrderBy(x => x.UnitId).ThenBy(x => x.DisplayOrder);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new AreaDto(x.Id, x.UnitId, x.Unit!.Name, x.Name, x.Code, x.DisplayOrder, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AreaDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AreaDto>> Create(UpsertAreaRequest request, CancellationToken cancellationToken)
    {
        var unit = await context.Units.FirstOrDefaultAsync(x => x.Id == request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Problem(title: "Unknown unit", statusCode: StatusCodes.Status400BadRequest);
        }

        var area = new Area
        {
            UnitId = request.UnitId,
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            DisplayOrder = request.DisplayOrder
        };

        context.Areas.Add(area);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new AreaDto(area.Id, area.UnitId, unit.Name, area.Name, area.Code, area.DisplayOrder, area.IsActive);
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, UpsertAreaRequest request, CancellationToken cancellationToken)
    {
        var area = await context.Areas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (area is null) return NotFound();

        if (!await context.Units.AnyAsync(x => x.Id == request.UnitId, cancellationToken))
        {
            return Problem(title: "Unknown unit", statusCode: StatusCodes.Status400BadRequest);
        }

        area.UnitId = request.UnitId;
        area.Name = request.Name.Trim();
        area.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        area.DisplayOrder = request.DisplayOrder;

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
        var area = await context.Areas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (area is null) return NotFound();

        area.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/lines")]
[Authorize]
public sealed class LinesController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LineDto>>> List(
        [FromQuery] int? unitId = null,
        [FromQuery] int? areaId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = context.Lines.AsNoTracking().AsQueryable();

        if (unitId is not null)
        {
            query = query.Where(x => x.UnitId == unitId);
        }

        // The report form asks this room by room: most rooms run no lines at all, and the
        // form only shows the step when the answer is not empty.
        if (areaId is not null)
        {
            query = query.Where(x => x.AreaId == areaId);
        }

        // Grouped by room as well, so two rooms that both run lines never interleave.
        // Today only the filling room has any, but the site adds these from the panel.
        query = query
            .OrderBy(x => x.UnitId)
            .ThenBy(x => x.AreaId == null)
            .ThenBy(x => x.AreaId)
            .ThenBy(x => x.DisplayOrder);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new LineDto(x.Id, x.UnitId, x.AreaId, x.Name, x.DisplayOrder, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<LineDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(LineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LineDto>> Create(UpsertLineRequest request, CancellationToken cancellationToken)
    {
        var problem = await ValidateParentsAsync(request.UnitId, request.AreaId, cancellationToken);
        if (problem is not null) return problem;

        var line = new Line
        {
            UnitId = request.UnitId,
            AreaId = request.AreaId,
            Name = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder
        };

        context.Lines.Add(line);
        await context.SaveChangesAsync(cancellationToken);

        var dto = new LineDto(line.Id, line.UnitId, line.AreaId, line.Name, line.DisplayOrder, line.IsActive);
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, UpsertLineRequest request, CancellationToken cancellationToken)
    {
        var line = await context.Lines.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (line is null) return NotFound();

        var problem = await ValidateParentsAsync(request.UnitId, request.AreaId, cancellationToken);
        if (problem is not null) return problem;

        line.UnitId = request.UnitId;
        line.AreaId = request.AreaId;
        line.Name = request.Name.Trim();
        line.DisplayOrder = request.DisplayOrder;

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
        var line = await context.Lines.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (line is null) return NotFound();

        line.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ObjectResult?> ValidateParentsAsync(int unitId, int? areaId, CancellationToken cancellationToken)
    {
        if (!await context.Units.AnyAsync(x => x.Id == unitId, cancellationToken))
        {
            return Problem(title: "Unknown unit", statusCode: StatusCodes.Status400BadRequest);
        }

        if (areaId is not null && !await context.Areas.AnyAsync(x => x.Id == areaId, cancellationToken))
        {
            return Problem(title: "Unknown area", statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }
}

/// <summary>
/// A machine or fixed asset. The two report flows read this list two different ways -
/// engineers filter by unit/area, operators filter by line - which is why both filters
/// are supported here rather than forcing one shape on both screens.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/equipment")]
[Authorize]
public sealed class EquipmentController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EquipmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EquipmentDto>>> List(
        [FromQuery] int? unitId = null,
        [FromQuery] int? areaId = null,
        [FromQuery] int? lineId = null,
        [FromQuery] int? parentEquipmentId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var query = context.Equipment.AsNoTracking().AsQueryable();

        if (unitId is not null) query = query.Where(x => x.UnitId == unitId);
        if (areaId is not null) query = query.Where(x => x.AreaId == areaId);
        if (lineId is not null) query = query.Where(x => x.LineId == lineId);
        if (parentEquipmentId is not null) query = query.Where(x => x.ParentEquipmentId == parentEquipmentId);

        // Down the site, not across it. Ordering on DisplayOrder alone interleaved all 62
        // machines into one run - "AAK, Filler, Inkjet Printer, P Tank 1, Separator 7" -
        // because every room numbers its own machines from 1.
        //
        // The null checks are spelled out rather than left to the database: PostgreSQL sorts
        // NULLs last on an ascending column and SQL Server sorts them first, and this list
        // must not change shape if the project ever moves.
        query = query
            .OrderBy(x => x.UnitId)
            .ThenBy(x => x.AreaId == null)
            .ThenBy(x => x.AreaId)
            // Machines on a line first, then the ones standing in the room itself - the
            // order the site reads its own list in.
            .ThenBy(x => x.LineId == null)
            .ThenBy(x => x.LineId)
            // Whole machines, then the parts that hang off them.
            .ThenBy(x => x.ParentEquipmentId != null)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new EquipmentDto(
                x.Id, x.UnitId, x.AreaId, x.LineId, x.ParentEquipmentId,
                x.ParentEquipment != null ? x.ParentEquipment.Name : null,
                x.Name, x.IconKey, x.DisplayOrder, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<EquipmentDto>(items, total, page, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(EquipmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EquipmentDto>> Create(
        UpsertEquipmentRequest request, CancellationToken cancellationToken)
    {
        var problem = await ValidateParentsAsync(
            request.UnitId, request.AreaId, request.LineId, request.ParentEquipmentId, null, cancellationToken);
        if (problem is not null) return problem;

        var equipment = new Equipment
        {
            UnitId = request.UnitId,
            AreaId = request.AreaId,
            LineId = request.LineId,
            ParentEquipmentId = request.ParentEquipmentId,
            Name = request.Name.Trim(),
            IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? null : request.IconKey.Trim(),
            DisplayOrder = request.DisplayOrder
        };

        context.Equipment.Add(equipment);
        await context.SaveChangesAsync(cancellationToken);

        var parentName = equipment.ParentEquipmentId is null
            ? null
            : await context.Equipment
                .Where(x => x.Id == equipment.ParentEquipmentId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);

        var dto = new EquipmentDto(
            equipment.Id, equipment.UnitId, equipment.AreaId, equipment.LineId,
            equipment.ParentEquipmentId, parentName,
            equipment.Name, equipment.IconKey, equipment.DisplayOrder, equipment.IsActive);

        return CreatedAtAction(nameof(List), new { }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        int id, UpsertEquipmentRequest request, CancellationToken cancellationToken)
    {
        var equipment = await context.Equipment.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (equipment is null) return NotFound();

        var problem = await ValidateParentsAsync(
            request.UnitId, request.AreaId, request.LineId, request.ParentEquipmentId, id, cancellationToken);
        if (problem is not null) return problem;

        equipment.UnitId = request.UnitId;
        equipment.AreaId = request.AreaId;
        equipment.LineId = request.LineId;
        equipment.ParentEquipmentId = request.ParentEquipmentId;
        equipment.Name = request.Name.Trim();
        equipment.IconKey = string.IsNullOrWhiteSpace(request.IconKey) ? null : request.IconKey.Trim();
        equipment.DisplayOrder = request.DisplayOrder;

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
        var equipment = await context.Equipment.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (equipment is null) return NotFound();

        equipment.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ObjectResult?> ValidateParentsAsync(
        int unitId,
        int? areaId,
        int? lineId,
        int? parentEquipmentId,
        int? equipmentId,
        CancellationToken cancellationToken)
    {
        if (!await context.Units.AnyAsync(x => x.Id == unitId, cancellationToken))
        {
            return Problem(title: "Unknown unit", statusCode: StatusCodes.Status400BadRequest);
        }

        if (areaId is not null && !await context.Areas.AnyAsync(x => x.Id == areaId, cancellationToken))
        {
            return Problem(title: "Unknown area", statusCode: StatusCodes.Status400BadRequest);
        }

        if (lineId is not null && !await context.Lines.AnyAsync(x => x.Id == lineId, cancellationToken))
        {
            return Problem(title: "Unknown line", statusCode: StatusCodes.Status400BadRequest);
        }

        if (parentEquipmentId is not null)
        {
            if (parentEquipmentId == equipmentId)
            {
                return Problem(
                    title: "A machine cannot be a part of itself",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var parent = await context.Equipment
                .Where(x => x.Id == parentEquipmentId)
                .Select(x => new { x.UnitId, x.AreaId, x.LineId, x.ParentEquipmentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null)
            {
                return Problem(title: "Unknown parent machine", statusCode: StatusCodes.Status400BadRequest);
            }

            // One level deep. Parts of parts would be a spare-parts catalogue, and the
            // report form has nowhere to show them.
            if (parent.ParentEquipmentId is not null)
            {
                return Problem(
                    title: "That machine is already a part of another one",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // A part stands wherever its machine stands. Letting the two drift apart is
            // how a fault log ends up naming two different places for one repair.
            if (parent.UnitId != unitId || parent.AreaId != areaId || parent.LineId != lineId)
            {
                return Problem(
                    title: "A part must be in the same place as the machine it belongs to",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return null;
    }
}
