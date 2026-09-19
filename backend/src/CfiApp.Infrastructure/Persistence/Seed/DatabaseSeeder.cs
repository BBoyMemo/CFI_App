using CfiApp.Domain.Identity;
using CfiApp.Domain.Organization;
using CfiApp.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CfiApp.Infrastructure.Persistence.Seed;

/// <summary>
/// Puts the minimum reference data in place so the application is usable on first run.
///
/// Two rules make this safe to run on every deployment:
/// it only inserts rows that are missing, and it never overwrites an existing one - if a
/// manager renames "Unit 1" or switches it off, the next deployment leaves that alone.
/// Everything beyond this is entered through the admin panel.
/// </summary>
public sealed class DatabaseSeeder(CfiAppDbContext context, ILogger<DatabaseSeeder> logger)
{
    /// <summary>
    /// The top level of the site. Rooms and machines inside them are entered from the
    /// admin panel, because only the site knows what is actually in each unit today.
    /// </summary>
    private static readonly (string Code, string Name, int Order)[] Units =
    [
        ("UNIT1", "Unit 1", 1),
        ("UNIT2", "Unit 2", 2),
        ("UNIT3", "Unit 3", 3),
        ("YARD", "Yard", 4)
    ];

    private static readonly string[] Departments = ["Production", "Maintenance", "FLT", "QA"];

    /// <summary>
    /// The three shifts the site runs today, as templates to draw from. Hours and days are
    /// editable, more can be added, and any of them can be deleted - nothing is working to
    /// these directly, only to the copies put into the pool.
    /// </summary>
    private static readonly (string Name, TimeOnly Start, TimeOnly End, int Order)[] Shifts =
    [
        ("Morning", new TimeOnly(6, 0), new TimeOnly(14, 0), 1),
        ("Afternoon", new TimeOnly(14, 0), new TimeOnly(22, 0), 2),
        // Crosses midnight, which is why EndTime is earlier than StartTime.
        ("Night", new TimeOnly(22, 0), new TimeOnly(6, 0), 3)
    ];


    /// <summary>
    /// The rooms inside each unit, as the site listed them. Only the Yard has none - it is
    /// outside, and its gates and DAF plant stand in the unit itself. Names are editable
    /// from the admin panel and the seeder never overwrites them.
    /// </summary>
    /// <summary>
    /// IsWorkArea is false for rooms nobody is stationed in - a utility or plant room
    /// rather than a place with people on shift. The room still exists and still takes
    /// equipment, so a fault in the Boiler House is still reportable; it just never shows
    /// up on the "works in" approval form. Office is off for the same reason for now, even
    /// though that may change once the site has office-based staff to assign there.
    /// </summary>
    private static readonly (string UnitCode, string Code, string Name, int Order, bool IsWorkArea)[] Areas =
    [
        ("UNIT1", "FILLING", "Filling Room", 1, true),
        ("UNIT1", "PTANKS", "P Tanks Room", 2, false),
        ("UNIT1", "PLANT", "Plant Room", 3, true),
        ("UNIT1", "MELTING", "Melting Room", 4, true),
        ("UNIT1", "PACKING", "Packing", 5, true),
        ("UNIT1", "WAREHOUSE", "Warehouse", 6, true),
        ("UNIT1", "BOILER", "Boiler House", 7, false),
        ("UNIT1", "OFFICE", "Office", 8, false),

        ("UNIT2", "BLENDING", "Blending Room", 1, true),
        ("UNIT2", "WAREHOUSE", "Warehouse", 2, true),

        ("UNIT3", "WAREHOUSE", "Warehouse", 1, true),
        ("UNIT3", "WORKSHOP", "Workshop", 2, true)
    ];

    /// <summary>
    /// Production lines. Only the Filling Room runs them; everywhere else a machine stands
    /// in the room itself. The line matters because Line 2 and Line 3 each have a Seamer
    /// and a conveyor, and a fault report has to say which one broke.
    /// </summary>
    private static readonly (string UnitCode, string AreaCode, string Name, int Order)[] Lines =
    [
        ("UNIT1", "FILLING", "Line 1 (2kg)", 1),
        ("UNIT1", "FILLING", "Line 2", 2),
        ("UNIT1", "FILLING", "Line 3", 3),
        ("UNIT1", "FILLING", "Line 4 (Box Line)", 4),
        // No machines under it yet - the site adds those from the admin panel. It sits here
        // rather than as a machine in the room because it is a station of its own, the same
        // level as the four lines beside it.
        ("UNIT1", "FILLING", "Inkjet Printer", 5)
    ];

    /// <summary>
    /// One machine a fault can be reported against.
    /// <para>
    /// AreaCode is null for something standing in the unit itself rather than in a room -
    /// the yard gates, for instance. LineName is set only in the Filling Room. ParentName
    /// makes this a part of a bigger machine: FIBC1 and FIBC2 belong to Blender 2, and a
    /// fault raised against one still reads as belonging to Blender 2.
    /// </para>
    /// </summary>
    private sealed record SeedMachine(
        string UnitCode,
        string? AreaCode,
        string? LineName,
        string? ParentName,
        string Name,
        string Icon,
        int Order);

    /// <summary>
    /// The machine list as the factory gave it. A starting point, not a fixed list:
    /// everything here is editable from the admin panel, new machines are added there, and
    /// "Other" on the report form still accepts anything that is not on it yet.
    /// </summary>
    private static readonly SeedMachine[] Machines =
    [
        new("UNIT1", "FILLING", "Line 2", null, "Filler", "filler", 1),
        new("UNIT1", "FILLING", "Line 2", null, "Seamer", "seamer", 2),
        new("UNIT1", "FILLING", "Line 2", null, "Conveyor", "conveyor", 3),
        new("UNIT1", "FILLING", "Line 3", null, "AAK", "filler", 1),
        new("UNIT1", "FILLING", "Line 3", null, "Seamer", "seamer", 2),
        new("UNIT1", "FILLING", "Line 3", null, "Conveyor", "conveyor", 3),
        new("UNIT1", "FILLING", "Line 4 (Box Line)", null, "Printer", "printer", 1),

        new("UNIT1", "PTANKS", null, null, "P Tank 1", "tank", 1),
        new("UNIT1", "PTANKS", null, null, "P Tank 2", "tank", 2),
        new("UNIT1", "PTANKS", null, null, "P Tank 3", "tank", 3),
        new("UNIT1", "PTANKS", null, null, "P Tank 4", "tank", 4),

        new("UNIT1", "PLANT", null, null, "Separator 7", "separator", 1),
        new("UNIT1", "PLANT", null, null, "Separator 6", "separator", 2),
        new("UNIT1", "PLANT", null, null, "Separator 5", "separator", 3),
        new("UNIT1", "PLANT", null, null, "Separator 4", "separator", 4),
        new("UNIT1", "PLANT", null, null, "Separator 3", "separator", 5),
        new("UNIT1", "PLANT", null, null, "Separator 2", "separator", 6),
        new("UNIT1", "PLANT", null, null, "Separator 1", "separator", 7),
        new("UNIT1", "PLANT", null, null, "Homogeniser 1", "homogeniser", 8),
        new("UNIT1", "PLANT", null, null, "Homogeniser 2", "homogeniser", 9),
        new("UNIT1", "PLANT", null, null, "Pasteurizer 1", "pasteurizer", 10),
        new("UNIT1", "PLANT", null, null, "Pasteurizer 2", "pasteurizer", 11),
        new("UNIT1", "PLANT", null, null, "H1 Tank", "tank", 12),
        new("UNIT1", "PLANT", null, null, "H2 Tank", "tank", 13),
        new("UNIT1", "PLANT", null, null, "Product Feed Tank 1", "tank", 14),
        new("UNIT1", "PLANT", null, null, "Product Feed Tank 2", "tank", 15),

        new("UNIT1", "MELTING", null, null, "Melter 1", "melter", 1),
        new("UNIT1", "MELTING", null, null, "Melter 2", "melter", 2),

        new("UNIT1", "PACKING", null, null, "Big Shrink Wrap Tunnel", "tunnel", 1),
        new("UNIT1", "PACKING", null, null, "Big Shrink Wrapper", "wrapper", 2),
        new("UNIT1", "PACKING", null, null, "Small Shrink Wrap Tunnel", "tunnel", 3),
        new("UNIT1", "PACKING", null, null, "Small Shrink Wrapper", "wrapper", 4),

        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 1", "tank", 1),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 2", "tank", 2),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 3", "tank", 3),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 4", "tank", 4),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 5", "tank", 5),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 6", "tank", 6),
        new("UNIT1", "WAREHOUSE", null, null, "RM Tank 7", "tank", 7),
        new("UNIT1", "WAREHOUSE", null, null, "P Tank 5", "tank", 8),
        new("UNIT1", "WAREHOUSE", null, null, "P Tank 6", "tank", 9),
        new("UNIT1", "WAREHOUSE", null, null, "P Tank 7", "tank", 10),
        new("UNIT1", "WAREHOUSE", null, null, "Intake Pump", "pump", 11),
        new("UNIT1", "WAREHOUSE", null, null, "Diaphragm Pump RM6 / RM7", "pump", 12),
        new("UNIT1", "WAREHOUSE", null, null, "Pallet Wrapper", "wrapper", 13),

        new("UNIT1", "BOILER", null, null, "Air Compressor", "compressor", 1),
        new("UNIT1", "BOILER", null, null, "Hot Water Circulation Pump", "pump", 2),
        new("UNIT1", "BOILER", null, null, "Steam Boiler", "boiler", 3),
        new("UNIT1", "BOILER", null, null, "Hot Water Boiler", "boiler", 4),

        new("UNIT1", "OFFICE", null, null, "CCTV Room", "cctv", 1),

        new("UNIT2", "BLENDING", null, null, "Sack Filler", "filler", 1),
        new("UNIT2", "BLENDING", null, null, "Band Sealer", "sealer", 2),
        new("UNIT2", "BLENDING", null, null, "Blender 1", "blender", 3),
        new("UNIT2", "BLENDING", null, null, "Blender 2", "blender", 4),
        new("UNIT2", "BLENDING", null, "Blender 2", "FIBC1", "hopper", 1),
        new("UNIT2", "BLENDING", null, "Blender 2", "FIBC2", "hopper", 2),
        new("UNIT2", "BLENDING", null, null, "Blending Metal Detector", "detector", 5),
        new("UNIT2", "BLENDING", null, null, "Stitcher", "stitcher", 6),

        new("UNIT2", "WAREHOUSE", null, null, "Pallet Wrapper", "wrapper", 1),

        new("YARD", null, null, null, "Gate 1", "gate", 1),
        new("YARD", null, null, null, "Gate 2", "gate", 2),
        new("YARD", null, null, null, "Gate 3", "gate", 3),
        new("YARD", null, null, null, "DAF Plant", "daf", 4)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var added = await SeedUnitsAsync(cancellationToken)
                  + await SeedDepartmentsAsync(cancellationToken)
                  + await SeedShiftTypesAsync(cancellationToken)
                  + await SeedPermissionsAsync(cancellationToken);

        // Roles need the permission rows to exist first, so this save is not optional.
        if (added > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Saves its own work, because role grants are written after their permissions exist.
        added += await SeedRolesAsync(cancellationToken);

        // Each step reads ids the step before it wrote - rooms need their unit, lines need
        // their room, machines need both, and a machine's parts need the machine - so every
        // one of them saves before the next runs.
        added += await SeedAreasAsync(cancellationToken);
        added += await SeedLinesAsync(cancellationToken);
        added += await SeedMachinesAsync(cancellationToken);

        if (added == 0)
        {
            logger.LogInformation("Seed check complete: reference data already present");
            return;
        }

        logger.LogInformation("Seed complete: {Count} reference row(s) added", added);
    }

    private async Task<int> SeedUnitsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await context.Units
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var missing = Units
            .Where(unit => !existingCodes.Contains(unit.Code))
            .Select(unit => new Unit
            {
                Code = unit.Code,
                Name = unit.Name,
                DisplayOrder = unit.Order
            })
            .ToList();

        context.Units.AddRange(missing);
        return missing.Count;
    }

    /// <summary>
    /// Permission keys are defined in code because controllers reference them. The
    /// database copy exists so the admin panel can show and grant them.
    /// </summary>
    private async Task<int> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await context.Permissions
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);

        var missing = Permissions.Catalog
            .Where(entry => !existingKeys.Contains(entry.Key))
            .Select(entry => new Permission { Key = entry.Key, Description = entry.Value })
            .ToList();

        context.Permissions.AddRange(missing);
        return missing.Count;
    }

    /// <summary>
    /// Creates the five system roles and grants them the permissions declared in
    /// <see cref="Permissions.RoleGrants"/>. A permission added to the code since the last
    /// deployment is granted on the next run; nothing is ever revoked here, so an
    /// administrator can still tighten a role by hand without the seeder undoing it.
    /// </summary>
    private async Task<int> SeedRolesAsync(CancellationToken cancellationToken)
    {
        var permissionIdsByKey = await context.Permissions
            .ToDictionaryAsync(x => x.Key, x => x.Id, cancellationToken);

        var systemRoleNames = Permissions.RoleGrants.Keys.ToArray();

        var existingRoles = await context.Roles
            .Include(x => x.Permissions)
            .Where(x => systemRoleNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        var changes = 0;

        foreach (var (roleName, grantedKeys) in Permissions.RoleGrants)
        {
            var role = existingRoles.SingleOrDefault(x => x.Name == roleName);

            if (role is null)
            {
                role = new Role { Name = roleName, IsSystem = true };
                context.Roles.Add(role);
                changes++;
            }

            foreach (var key in grantedKeys.Distinct())
            {
                var permissionId = permissionIdsByKey[key];

                if (role.Permissions.Any(x => x.PermissionId == permissionId)) continue;

                role.Permissions.Add(new RolePermission { PermissionId = permissionId });
                changes++;
            }
        }

        if (changes > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return changes;
    }

    private async Task<int> SeedShiftTypesAsync(CancellationToken cancellationToken)
    {
        var existingNames = await context.ShiftTypes
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var missing = Shifts
            .Where(shift => !existingNames.Contains(shift.Name))
            .Select(shift => new ShiftType
            {
                Name = shift.Name,
                StartTime = shift.Start,
                EndTime = shift.End,
                // A starting point, not a decision: the site edits the days and the start
                // date on the planner before putting a shift into the pool.
                Weekdays = Weekdays.WorkingWeek,
                StartsOn = new DateOnly(2026, 1, 1),
                DisplayOrder = shift.Order
            })
            .ToList();

        context.ShiftTypes.AddRange(missing);
        return missing.Count;
    }

    private async Task<int> SeedDepartmentsAsync(CancellationToken cancellationToken)
    {
        var existingNames = await context.Departments
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var missing = Departments
            .Where(name => !existingNames.Contains(name))
            .Select(name => new Department { Name = name })
            .ToList();

        context.Departments.AddRange(missing);
        return missing.Count;
    }

    /// <summary>
    /// Areas are matched on unit code plus area code, so renaming "Melt Room (F7)" in the
    /// admin panel does not make the next deployment insert a duplicate.
    /// </summary>
    private async Task<int> SeedAreasAsync(CancellationToken cancellationToken)
    {
        var unitIdsByCode = await context.Units
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);

        var existing = await context.Areas
            .Select(x => new { x.UnitId, x.Code })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Where(x => x.Code != null)
            .Select(x => (x.UnitId, Code: x.Code!))
            .ToHashSet();

        var missing = new List<Area>();

        foreach (var (unitCode, code, name, order, isWorkArea) in Areas)
        {
            if (!unitIdsByCode.TryGetValue(unitCode, out var unitId)) continue;
            if (existingKeys.Contains((unitId, code))) continue;

            missing.Add(new Area
            {
                UnitId = unitId, Code = code, Name = name, DisplayOrder = order, IsWorkArea = isWorkArea
            });
        }

        if (missing.Count == 0) return 0;

        context.Areas.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    /// <summary>
    /// Lines are matched on their room plus name, so a line renamed on site is left alone.
    /// </summary>
    private async Task<int> SeedLinesAsync(CancellationToken cancellationToken)
    {
        var unitIdsByCode = await UnitIdsByCodeAsync(cancellationToken);
        var areaIdsByKey = await AreaIdsByKeyAsync(cancellationToken);

        var existingKeys = (await context.Lines
                .Select(x => new { x.AreaId, x.Name })
                .ToListAsync(cancellationToken))
            .Select(x => (x.AreaId, x.Name))
            .ToHashSet();

        var missing = new List<Line>();

        foreach (var (unitCode, areaCode, name, order) in Lines)
        {
            if (!unitIdsByCode.TryGetValue(unitCode, out var unitId)) continue;
            if (!areaIdsByKey.TryGetValue((unitId, areaCode), out var areaId)) continue;
            if (existingKeys.Contains(((int?)areaId, name))) continue;

            missing.Add(new Line { UnitId = unitId, AreaId = areaId, Name = name, DisplayOrder = order });
        }

        if (missing.Count == 0) return 0;

        context.Lines.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }

    /// <summary>
    /// Machines are matched on everything that places them - unit, room, line and parent
    /// machine - plus the name. That full key is what lets Line 2 and Line 3 each keep
    /// their own Seamer, and both warehouses their own Pallet Wrapper.
    ///
    /// Whole machines go in first and their parts second, because a part needs the id of
    /// the machine it hangs off.
    /// </summary>
    private async Task<int> SeedMachinesAsync(CancellationToken cancellationToken)
    {
        var unitIdsByCode = await UnitIdsByCodeAsync(cancellationToken);
        var areaIdsByKey = await AreaIdsByKeyAsync(cancellationToken);

        var lineIdsByKey = (await context.Lines
                .Where(x => x.AreaId != null)
                .Select(x => new { x.Id, x.AreaId, x.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => (AreaId: x.AreaId!.Value, x.Name), x => x.Id);

        var added = 0;

        foreach (var partsPass in new[] { false, true })
        {
            var placed = await context.Equipment
                .Select(x => new { x.Id, x.UnitId, x.AreaId, x.LineId, x.ParentEquipmentId, x.Name })
                .ToListAsync(cancellationToken);

            var existingKeys = placed
                .Select(x => (x.UnitId, x.AreaId, x.LineId, x.ParentEquipmentId, x.Name))
                .ToHashSet();

            // A part names its parent by machine name, so the lookup has to be keyed on
            // the whole place as well: "Seamer" alone is ambiguous in the filling room,
            // where Line 2 and Line 3 each have one.
            var parentIdsByKey = new Dictionary<(int, int?, int?, string), int>();
            foreach (var candidate in placed.Where(x => x.ParentEquipmentId is null))
            {
                parentIdsByKey[(candidate.UnitId, candidate.AreaId, candidate.LineId, candidate.Name)] =
                    candidate.Id;
            }

            var missing = new List<Equipment>();

            foreach (var machine in Machines.Where(x => (x.ParentName is not null) == partsPass))
            {
                if (!unitIdsByCode.TryGetValue(machine.UnitCode, out var unitId)) continue;

                int? areaId = null;
                if (machine.AreaCode is not null)
                {
                    if (!areaIdsByKey.TryGetValue((unitId, machine.AreaCode), out var room)) continue;
                    areaId = room;
                }

                int? lineId = null;
                if (machine.LineName is not null)
                {
                    if (areaId is null) continue;
                    if (!lineIdsByKey.TryGetValue((areaId.Value, machine.LineName), out var line)) continue;
                    lineId = line;
                }

                // A part stands exactly where its machine does, so the parent is looked up
                // in the same unit, room and line.
                int? parentId = null;
                if (machine.ParentName is not null)
                {
                    if (!parentIdsByKey.TryGetValue((unitId, areaId, lineId, machine.ParentName), out var parent))
                    {
                        continue;
                    }

                    parentId = parent;
                }

                if (existingKeys.Contains((unitId, areaId, lineId, parentId, machine.Name))) continue;

                missing.Add(new Equipment
                {
                    UnitId = unitId,
                    AreaId = areaId,
                    LineId = lineId,
                    ParentEquipmentId = parentId,
                    Name = machine.Name,
                    IconKey = machine.Icon,
                    DisplayOrder = machine.Order
                });
            }

            if (missing.Count == 0) continue;

            context.Equipment.AddRange(missing);
            await context.SaveChangesAsync(cancellationToken);
            added += missing.Count;
        }

        return added;
    }

    private async Task<Dictionary<string, int>> UnitIdsByCodeAsync(CancellationToken cancellationToken) =>
        await context.Units.ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);

    private async Task<Dictionary<(int UnitId, string Code), int>> AreaIdsByKeyAsync(
        CancellationToken cancellationToken) =>
        (await context.Areas
            .Where(x => x.Code != null)
            .Select(x => new { x.Id, x.UnitId, x.Code })
            .ToListAsync(cancellationToken))
        .ToDictionary(x => (x.UnitId, Code: x.Code!), x => x.Id);
}
