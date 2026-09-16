using CfiApp.Domain.Organization;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// These behaviours are the foundation the BRC audit trail rests on, so they are tested
/// at the database level rather than trusted to code review.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuditAndConcurrencyTests(CfiAppApiFactory factory)
{
    private static int _nameCounter;

    private static string UniqueName(string prefix, int maxLength = 40)
    {
        var value = $"{prefix}-{Interlocked.Increment(ref _nameCounter)}-{Guid.NewGuid():N}";
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task<Unit> AddUnitAsync(int? actingUserId)
    {
        factory.CurrentUser.UserId = actingUserId;

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var unit = new Unit { Name = UniqueName("Unit"), Code = UniqueName("A", 20) };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        return unit;
    }

    [Fact]
    public async Task Insert_stamps_created_columns_without_the_service_setting_them()
    {
        var unit = await AddUnitAsync(actingUserId: 42);

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stored = await context.Units.SingleAsync(x => x.Id == unit.Id);

        stored.CreatedAt.ShouldNotBe(default);
        stored.CreatedAt.Offset.ShouldBe(TimeSpan.Zero, "timestamps are stored in UTC");
        stored.CreatedByUserId.ShouldBe(42);
        stored.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Update_stamps_updated_columns_and_cannot_rewrite_who_created_the_row()
    {
        var unit = await AddUnitAsync(actingUserId: 42);

        factory.CurrentUser.UserId = 99;

        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var loaded = await context.Units.SingleAsync(x => x.Id == unit.Id);

            loaded.Name = UniqueName("Renamed");

            // A caller trying to rewrite history must not succeed.
            loaded.CreatedByUserId = 1234;
            loaded.CreatedAt = DateTimeOffset.UnixEpoch;

            await context.SaveChangesAsync();
        }

        await using var verifyScope = factory.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stored = await verifyContext.Units.SingleAsync(x => x.Id == unit.Id);

        stored.CreatedByUserId.ShouldBe(42);
        stored.CreatedAt.ShouldNotBe(DateTimeOffset.UnixEpoch);
        stored.UpdatedByUserId.ShouldBe(99);
        stored.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Two_people_editing_the_same_row_produces_a_conflict_not_a_silent_overwrite()
    {
        var unit = await AddUnitAsync(actingUserId: 1);

        await using var firstScope = factory.CreateScope();
        await using var secondScope = factory.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var firstCopy = await firstContext.Units.SingleAsync(x => x.Id == unit.Id);
        var secondCopy = await secondContext.Units.SingleAsync(x => x.Id == unit.Id);

        firstCopy.Name = UniqueName("First");
        await firstContext.SaveChangesAsync();

        secondCopy.Name = UniqueName("Second");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Reference_data_cannot_be_deleted_while_something_still_points_at_it()
    {
        var unit = await AddUnitAsync(actingUserId: 1);

        await using (var seedScope = factory.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            seedContext.Areas.Add(new Area { UnitId = unit.Id, Name = UniqueName("Area") });
            await seedContext.SaveChangesAsync();
        }

        // A fresh context has not loaded the area, so this reaches the database and proves
        // the foreign key itself refuses the delete - not just the change tracker.
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var storedUnit = await context.Units.SingleAsync(x => x.Id == unit.Id);
        context.Units.Remove(storedUnit);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
