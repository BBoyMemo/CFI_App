using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// The seeder runs on every deployment, so "runs twice" and "does not undo an admin
/// edit" are the two behaviours that actually matter.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DatabaseSeederTests(CfiAppApiFactory factory)
{
    private static readonly string[] ExpectedUnitCodes = ["UNIT1", "UNIT2", "UNIT3", "YARD"];
    private static readonly string[] ExpectedDepartments = ["Production", "Maintenance", "FLT", "QA"];

    private async Task RunSeedAsync()
    {
        await using var scope = factory.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    [Fact]
    public async Task Seeds_the_four_site_units_and_the_four_departments()
    {
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var codes = await context.Units.Select(x => x.Code).ToListAsync();
        foreach (var expected in ExpectedUnitCodes)
        {
            codes.ShouldContain(expected);
        }

        var departments = await context.Departments.Select(x => x.Name).ToListAsync();
        foreach (var expected in ExpectedDepartments)
        {
            departments.ShouldContain(expected);
        }
    }

    [Fact]
    public async Task Running_the_seed_twice_does_not_duplicate_anything()
    {
        await RunSeedAsync();
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        foreach (var code in ExpectedUnitCodes)
        {
            (await context.Units.CountAsync(x => x.Code == code))
                .ShouldBe(1, $"unit {code} must exist exactly once");
        }

        foreach (var name in ExpectedDepartments)
        {
            (await context.Departments.CountAsync(x => x.Name == name))
                .ShouldBe(1, $"department {name} must exist exactly once");
        }
    }

    /// <summary>
    /// The shape of the site, not a row count: a room inside a unit, a line inside a room,
    /// a machine on a line, and a part inside a machine. Each of the four checks here is a
    /// case that only works if the level above it was resolved correctly.
    /// </summary>
    [Fact]
    public async Task Seeds_the_site_four_levels_deep()
    {
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var filling = await context.Areas
            .SingleAsync(x => x.Code == "FILLING" && x.Unit!.Code == "UNIT1");

        var lines = await context.Lines.Where(x => x.AreaId == filling.Id).ToListAsync();
        // Four lines plus the inkjet printer, which stands at the same level as them and
        // will have machines of its own once the site adds them.
        lines.Select(x => x.Name).ShouldBe(
            ["Line 1 (2kg)", "Line 2", "Line 3", "Line 4 (Box Line)", "Inkjet Printer"],
            ignoreOrder: true);

        // Line 2 and Line 3 each have their own Seamer. If the seeder keyed machines on
        // name alone, one of these would be missing and a fault report could not say which
        // seamer stopped.
        foreach (var lineName in new[] { "Line 2", "Line 3" })
        {
            var line = lines.Single(x => x.Name == lineName);

            (await context.Equipment.CountAsync(x => x.LineId == line.Id && x.Name == "Seamer"))
                .ShouldBe(1, $"{lineName} has a seamer of its own");
        }

        // A part hangs off its machine rather than off the room.
        var blender2 = await context.Equipment
            .SingleAsync(x => x.Name == "Blender 2" && x.Area!.Code == "BLENDING");

        var parts = await context.Equipment
            .Where(x => x.ParentEquipmentId == blender2.Id)
            .Select(x => x.Name)
            .ToListAsync();

        parts.ShouldBe(["FIBC1", "FIBC2"], ignoreOrder: true);

        // The yard is outside, so its machines stand in the unit with no room at all.
        var gate1 = await context.Equipment
            .SingleAsync(x => x.Name == "Gate 1" && x.Unit!.Code == "YARD");

        gate1.AreaId.ShouldBeNull();
    }

    /// <summary>
    /// Both warehouses have a pallet wrapper. They are two different machines in two
    /// different units, and the seeder has to keep them apart.
    /// </summary>
    [Fact]
    public async Task Two_machines_with_the_same_name_in_different_places_both_survive()
    {
        await RunSeedAsync();
        await RunSeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var wrappers = await context.Equipment
            .Where(x => x.Name == "Pallet Wrapper")
            .Select(x => x.Unit!.Code)
            .ToListAsync();

        wrappers.ShouldBe(["UNIT1", "UNIT2"], ignoreOrder: true);
    }

    [Fact]
    public async Task Seed_does_not_overwrite_a_name_the_site_has_changed()
    {
        await RunSeedAsync();

        await using (var editScope = factory.CreateScope())
        {
            var context = editScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var yard = await context.Units.SingleAsync(x => x.Code == "YARD");
            yard.Name = "Outside Yard";
            yard.IsActive = false;
            await context.SaveChangesAsync();
        }

        await RunSeedAsync();

        await using var verifyScope = factory.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stored = await verifyContext.Units.SingleAsync(x => x.Code == "YARD");

        stored.Name.ShouldBe("Outside Yard");
        stored.IsActive.ShouldBeFalse();
    }
}
