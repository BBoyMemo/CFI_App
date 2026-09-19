using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Messaging;
using CfiApp.Domain.Scheduling;
using CfiApp.Domain.Work;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Attendance feeds payroll and messaging reaches real people, so the rules that stop bad
/// rows are enforced by the schema and checked here.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OperationsRecordTests(CfiAppApiFactory factory)
{
    private static int _counter;

    private async Task<int> NewUserIdAsync()
    {
        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var user = new User
        {
            FullName = "Shift Worker",
            Email = $"worker{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-a-real-hash",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Status = UserStatus.Active
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedAsync()
    {
        await using var scope = factory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    [Fact]
    public async Task The_three_default_shifts_are_seeded_and_night_crosses_midnight()
    {
        await SeedAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        // By name, not by an exact list: other tests in this collection legitimately add
        // their own shift types (that is real Phase 7 functionality), so the table can
        // hold more than these three by the time this runs.
        var shifts = await context.ShiftTypes.OrderBy(x => x.DisplayOrder).ToListAsync();
        var shiftNames = shifts.Select(x => x.Name).ToHashSet();

        shiftNames.ShouldContain("Morning");
        shiftNames.ShouldContain("Afternoon");
        shiftNames.ShouldContain("Night");

        var night = shifts.Single(x => x.Name == "Night");
        night.StartTime.ShouldBe(new TimeOnly(22, 0));
        night.EndTime.ShouldBe(new TimeOnly(6, 0));
        (night.EndTime < night.StartTime).ShouldBeTrue("the night shift runs past midnight");
    }

    [Fact]
    public async Task The_database_itself_refuses_putting_someone_on_two_shifts_at_once()
    {
        await SeedAsync();
        var userId = await NewUserIdAsync();

        int morningId, nightId;
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

            var morning = new ActiveShift
            {
                Name = $"Morning {Guid.NewGuid():N}",
                StartTime = new TimeOnly(6, 0),
                EndTime = new TimeOnly(14, 0),
                Weekdays = Weekdays.WorkingWeek,
                StartsOn = new DateOnly(2026, 1, 1)
            };

            var night = new ActiveShift
            {
                Name = $"Night {Guid.NewGuid():N}",
                StartTime = new TimeOnly(22, 0),
                EndTime = new TimeOnly(6, 0),
                Weekdays = Weekdays.WorkingWeek,
                StartsOn = new DateOnly(2026, 1, 1)
            };

            context.ActiveShifts.AddRange(morning, night);
            await context.SaveChangesAsync();
            morningId = morning.Id;
            nightId = night.Id;

            context.ShiftRosterEntries.Add(new ShiftRosterEntry
            {
                UserId = userId,
                ActiveShiftId = morningId,
                EffectiveFrom = new DateOnly(2026, 9, 7)
            });
            await context.SaveChangesAsync();
        }

        // Nobody works two shifts at the same time. The service closes the old row before
        // opening a new one; this is the guard for when something bypasses it.
        await using var clashScope = factory.CreateScope();
        var clashContext = clashScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        clashContext.ShiftRosterEntries.Add(new ShiftRosterEntry
        {
            UserId = userId,
            ActiveShiftId = nightId,
            EffectiveFrom = new DateOnly(2026, 9, 14)
        });

        await Should.ThrowAsync<DbUpdateException>(() => clashContext.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_offline_clock_event_sent_twice_is_stored_once()
    {
        var userId = await NewUserIdAsync();
        var clientId = Guid.NewGuid();

        ClockEvent Build() => new()
        {
            UserId = userId,
            Type = ClockType.In,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            Source = ClockSource.Manual,
            ClientId = clientId
        };

        await using (var firstScope = factory.CreateScope())
        {
            var context = firstScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            context.ClockEvents.Add(Build());
            await context.SaveChangesAsync();
        }

        await using var secondScope = factory.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        secondContext.ClockEvents.Add(Build());

        await Should.ThrowAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task A_clock_event_carries_half_a_coordinate_never()
    {
        var userId = await NewUserIdAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        context.ClockEvents.Add(new ClockEvent
        {
            UserId = userId,
            Type = ClockType.In,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            Source = ClockSource.AutoGeofence,
            ClientId = Guid.NewGuid(),
            Latitude = 53.3654,
            Longitude = null
        });

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_wrong_clock_record_is_corrected_by_adding_to_it_not_by_editing_it()
    {
        var userId = await NewUserIdAsync();
        int correctionId;

        await using (var setupScope = factory.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

            var clockEvent = new ClockEvent
            {
                UserId = userId,
                Type = ClockType.Out,
                OccurredAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                ReceivedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                Source = ClockSource.Manual,
                ClientId = Guid.NewGuid()
            };
            context.ClockEvents.Add(clockEvent);
            await context.SaveChangesAsync();

            var correction = new ClockCorrection
            {
                ClockEventId = clockEvent.Id,
                CorrectedByUserId = userId,
                Reason = "Forgot to clock out at the end of the shift",
                NewOccurredAtUtc = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };
            context.ClockCorrections.Add(correction);
            await context.SaveChangesAsync();
            correctionId = correction.Id;
        }

        await using var scope = factory.CreateScope();
        var editContext = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var stored = await editContext.ClockCorrections.SingleAsync(x => x.Id == correctionId);
        stored.Reason = "Changed my mind";

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => editContext.SaveChangesAsync());
        exception.Message.ShouldContain("append-only");
    }

    [Fact]
    public async Task Holiday_cannot_end_before_it_starts()
    {
        var userId = await NewUserIdAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        context.HolidayRequests.Add(new HolidayRequest
        {
            UserId = userId,
            StartDate = new DateOnly(2026, 9, 10),
            EndDate = new DateOnly(2026, 9, 8),
            WorkingDays = 3,
            RequestedAt = DateTimeOffset.UtcNow
        });

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_message_addressed_to_nobody_is_rejected()
    {
        var senderId = await NewUserIdAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        var message = new Message { SenderUserId = senderId, Body = "Tomorrow we start at 07:00." };
        message.Recipients.Add(new MessageRecipient { UserId = null, DepartmentId = null });
        context.Messages.Add(message);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_message_cannot_target_a_person_and_a_department_at_once()
    {
        await SeedAsync();
        var senderId = await NewUserIdAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        var departmentId = (await context.Departments.FirstAsync(x => x.Name == "Production")).Id;

        var message = new Message { SenderUserId = senderId, Body = "Ambiguous target." };
        message.Recipients.Add(new MessageRecipient { UserId = senderId, DepartmentId = departmentId });
        context.Messages.Add(message);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_part_request_for_zero_items_is_rejected()
    {
        var userId = await NewUserIdAsync();

        await using var scope = factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

        context.PartOrderRequests.Add(new PartOrderRequest
        {
            PartName = "Drive belt",
            Quantity = 0,
            RequestedByUserId = userId,
            RequestedAt = DateTimeOffset.UtcNow
        });

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task One_person_completes_a_task_once()
    {
        var userId = await NewUserIdAsync();
        int taskId;

        await using (var setupScope = factory.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

            var task = new MaintenanceTask
            {
                Title = "Grease the line 1 conveyor bearings",
                Kind = TaskKind.Daily,
                ScheduledDate = new DateOnly(2026, 9, 4),
                Priority = TaskPriority.Preventive
            };
            task.Assignments.Add(new TaskAssignment { UserId = userId });
            task.Completions.Add(new TaskCompletion
            {
                UserId = userId,
                Note = "Done, no issues found.",
                CompletedAt = DateTimeOffset.UtcNow
            });

            context.MaintenanceTasks.Add(task);
            await context.SaveChangesAsync();
            taskId = task.Id;
        }

        await using var scope = factory.CreateScope();
        var duplicateContext = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        duplicateContext.TaskCompletions.Add(new TaskCompletion
        {
            MaintenanceTaskId = taskId,
            UserId = userId,
            Note = "Done again?",
            CompletedAt = DateTimeOffset.UtcNow
        });

        await Should.ThrowAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }
}
