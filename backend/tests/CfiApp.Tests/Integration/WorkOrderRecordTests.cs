using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Domain.Organization;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Database level guarantees for the maintenance record. These are the rules a BRC
/// auditor relies on, so they are enforced by the schema rather than by whichever service
/// happens to write the row.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderRecordTests(CfiAppApiFactory factory)
{
    private static int _counter;

    private static string Unique(string prefix) =>
        $"{prefix}{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}"[..18];

    private async Task<(int UnitId, int UserId)> EnsureFixtureAsync()
    {
        await using var scope = factory.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();

        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var unit = await context.Units.FirstAsync(x => x.Code == "UNIT1");

        var user = new User
        {
            FullName = "Test Reporter",
            Email = $"{Unique("rep")}@example.test",
            PasswordHash = "not-a-real-hash",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return (unit.Id, user.Id);
    }

    private static WorkOrder NewWorkOrder(int unitId, int userId) => new()
    {
        Number = Unique("WO-"),
        UnitId = unitId,
        EquipmentFreeText = "Conveyor drive motor",
        ReportedByUserId = userId,
        ReportedAt = DateTimeOffset.UtcNow,
        Priority = Priority.High,
        Description = "Motor overheating and cutting out under load."
    };

    [Fact]
    public async Task A_report_must_name_a_machine_or_describe_it_in_words()
    {
        var (unitId, userId) = await EnsureFixtureAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var workOrder = NewWorkOrder(unitId, userId);
        workOrder.EquipmentId = null;
        workOrder.EquipmentFreeText = null;

        context.WorkOrders.Add(workOrder);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_offline_report_sent_twice_is_stored_once()
    {
        var (unitId, userId) = await EnsureFixtureAsync();
        var clientId = Guid.NewGuid();

        await using (var firstScope = factory.CreateScope())
        {
            var context = firstScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var first = NewWorkOrder(unitId, userId);
            first.ClientId = clientId;
            context.WorkOrders.Add(first);
            await context.SaveChangesAsync();
        }

        await using var secondScope = factory.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var duplicate = NewWorkOrder(unitId, userId);
        duplicate.ClientId = clientId;
        secondContext.WorkOrders.Add(duplicate);

        await Should.ThrowAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task The_event_trail_cannot_be_edited_or_deleted()
    {
        var (unitId, userId) = await EnsureFixtureAsync();

        int eventId;

        await using (var setupScope = factory.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var workOrder = NewWorkOrder(unitId, userId);

            workOrder.Events.Add(new WorkOrderEvent
            {
                Type = WorkOrderEventType.Created,
                ActorUserId = userId,
                OccurredAt = DateTimeOffset.UtcNow,
                Summary = "Breakdown reported",
                ToStatus = WorkOrderStatus.New
            });

            context.WorkOrders.Add(workOrder);
            await context.SaveChangesAsync();
            eventId = workOrder.Events.Single().Id;
        }

        await using (var editScope = factory.CreateScope())
        {
            var context = editScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var stored = await context.WorkOrderEvents.SingleAsync(x => x.Id == eventId);
            stored.Summary = "Something else entirely";

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
            exception.Message.ShouldContain("append-only");
        }

        await using var deleteScope = factory.CreateScope();
        var deleteContext = deleteScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var toDelete = await deleteContext.WorkOrderEvents.SingleAsync(x => x.Id == eventId);
        deleteContext.WorkOrderEvents.Remove(toDelete);

        await Should.ThrowAsync<InvalidOperationException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task A_failed_swab_test_cannot_be_recorded_without_a_reason()
    {
        var (unitId, userId) = await EnsureFixtureAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var workOrder = NewWorkOrder(unitId, userId);
        workOrder.QaChecks.Add(new QaCheck
        {
            Attempt = 1,
            RequestedAt = DateTimeOffset.UtcNow,
            Result = QaResult.Fail,
            ResultedAt = DateTimeOffset.UtcNow,
            Note = null
        });

        context.WorkOrders.Add(workOrder);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_closure_that_says_it_could_not_repair_must_say_why()
    {
        var (unitId, userId) = await EnsureFixtureAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var workOrder = NewWorkOrder(unitId, userId);
        workOrder.Closures.Add(new WorkOrderClosure
        {
            Version = 1,
            RootCause = "Bearing seized",
            CorrectiveAction = "Awaiting replacement bearing",
            AbleToRepair = false,
            UnableToRepairReason = null,
            ToolsAndPartsAccounted = true,
            DowntimeMinutes = 45,
            SubmittedByUserId = userId,
            SubmittedAt = DateTimeOffset.UtcNow
        });

        context.WorkOrders.Add(workOrder);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_corrected_closure_is_a_new_version_and_the_first_one_survives()
    {
        var (unitId, userId) = await EnsureFixtureAsync();
        int workOrderId;

        await using (var setupScope = factory.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var workOrder = NewWorkOrder(unitId, userId);

            workOrder.Closures.Add(new WorkOrderClosure
            {
                Version = 1,
                RootCause = "Seal failure",
                CorrectiveAction = "Replaced seal",
                AbleToRepair = true,
                ToolsAndPartsAccounted = true,
                DowntimeMinutes = 30,
                PostDeodorisationIntervention = true,
                SubmittedByUserId = userId,
                SubmittedAt = DateTimeOffset.UtcNow
            });

            context.WorkOrders.Add(workOrder);
            await context.SaveChangesAsync();
            workOrderId = workOrder.Id;
        }

        await using (var resubmitScope = factory.CreateScope())
        {
            var context = resubmitScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            context.WorkOrderClosures.Add(new WorkOrderClosure
            {
                WorkOrderId = workOrderId,
                Version = 2,
                RootCause = "Seal failure",
                CorrectiveAction = "Replaced seal and resanitised the unit",
                AbleToRepair = true,
                ToolsAndPartsAccounted = true,
                DowntimeMinutes = 55,
                PostDeodorisationIntervention = true,
                SubmittedByUserId = userId,
                SubmittedAt = DateTimeOffset.UtcNow
            });

            await context.SaveChangesAsync();
        }

        await using var verifyScope = factory.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var closures = await verifyContext.WorkOrderClosures
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderBy(x => x.Version)
            .ToListAsync();

        closures.Count.ShouldBe(2);
        closures[0].CorrectiveAction.ShouldBe("Replaced seal");
        closures[1].CorrectiveAction.ShouldBe("Replaced seal and resanitised the unit");
    }

    [Fact]
    public async Task A_job_cannot_carry_two_production_sign_offs()
    {
        var (unitId, userId) = await EnsureFixtureAsync();
        int workOrderId;

        await using (var setupScope = factory.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var workOrder = NewWorkOrder(unitId, userId);

            workOrder.SignOffs.Add(new SignOff
            {
                Kind = SignOffKind.Production,
                UserId = userId,
                SignedAt = DateTimeOffset.UtcNow,
                AreaCleanAndTidy = true,
                ReleasedBackIntoService = true
            });

            context.WorkOrders.Add(workOrder);
            await context.SaveChangesAsync();
            workOrderId = workOrder.Id;
        }

        await using var scope = factory.CreateScope();
        var duplicateContext = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        duplicateContext.SignOffs.Add(new SignOff
        {
            WorkOrderId = workOrderId,
            Kind = SignOffKind.Production,
            UserId = userId,
            SignedAt = DateTimeOffset.UtcNow,
            AreaCleanAndTidy = true,
            ReleasedBackIntoService = true
        });

        await Should.ThrowAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }
}
