using CfiApp.Domain.Attendance;
using CfiApp.Domain.Auditing;
using CfiApp.Domain.Messaging;
using CfiApp.Domain.Scheduling;
using CfiApp.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CfiApp.Infrastructure.Persistence.Configurations;

// ---------------------------------------------------------------- tasks and orders

public sealed class MaintenanceTaskConfiguration : IEntityTypeConfiguration<MaintenanceTask>
{
    public void Configure(EntityTypeBuilder<MaintenanceTask> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.Priority).HasConversion<int>();

        builder.HasIndex(x => new { x.ScheduledDate, x.Kind });
        builder.HasIndex(x => x.DeletedAt);
    }
}

public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.HasKey(x => new { x.MaintenanceTaskId, x.UserId });

        builder.HasOne(x => x.MaintenanceTask).WithMany(x => x.Assignments)
            .HasForeignKey(x => x.MaintenanceTaskId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // "Which tasks are mine" is the engineer home screen query.
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class TaskCompletionConfiguration : IEntityTypeConfiguration<TaskCompletion>
{
    public void Configure(EntityTypeBuilder<TaskCompletion> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(2000).IsRequired();

        builder.HasOne(x => x.MaintenanceTask).WithMany(x => x.Completions)
            .HasForeignKey(x => x.MaintenanceTaskId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // One completion per person per task.
        builder.HasIndex(x => new { x.MaintenanceTaskId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.CompletedAt);
    }
}

public sealed class TaskCompletionPhotoConfiguration : IEntityTypeConfiguration<TaskCompletionPhoto>
{
    public void Configure(EntityTypeBuilder<TaskCompletionPhoto> builder)
    {
        builder.HasOne(x => x.TaskCompletion).WithMany(x => x.Photos)
            .HasForeignKey(x => x.TaskCompletionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MediaAsset).WithMany()
            .HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PartOrderRequestConfiguration : IEntityTypeConfiguration<PartOrderRequest>
{
    public void Configure(EntityTypeBuilder<PartOrderRequest> builder)
    {
        builder.Property(x => x.PartName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasOne(x => x.RequestedBy).WithMany()
            .HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Newest first, which is how both the engineer and the manager list is ordered.
        builder.HasIndex(x => new { x.Status, x.RequestedAt });
        builder.HasIndex(x => new { x.RequestedByUserId, x.RequestedAt });

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_PartOrderRequest_Quantity", "\"Quantity\" > 0"));
    }
}

// ---------------------------------------------------------------- attendance

public sealed class ClockEventConfiguration : IEntityTypeConfiguration<ClockEvent>
{
    public void Configure(EntityTypeBuilder<ClockEvent> builder)
    {
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.DeviceId).HasMaxLength(128);
        builder.Property(x => x.SuspectReason).HasMaxLength(200);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // A replayed offline queue must not double up somebody hours.
        builder.HasIndex(x => x.ClientId).IsUnique();

        // The monthly hours screen and the manager review screen.
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        builder.HasIndex(x => x.IsSuspect);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ClockEvent_Coordinates",
            """("Latitude" IS NULL AND "Longitude" IS NULL) OR ("Latitude" IS NOT NULL AND "Longitude" IS NOT NULL)"""));
    }
}

public sealed class ClockCorrectionConfiguration : IEntityTypeConfiguration<ClockCorrection>
{
    public void Configure(EntityTypeBuilder<ClockCorrection> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.ClockEvent).WithMany()
            .HasForeignKey(x => x.ClockEventId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CorrectedBy).WithMany()
            .HasForeignKey(x => x.CorrectedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClockEventId);
    }
}

public sealed class OvertimeDeclarationConfiguration : IEntityTypeConfiguration<OvertimeDeclaration>
{
    public void Configure(EntityTypeBuilder<OvertimeDeclaration> builder)
    {
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.DecisionNote).HasMaxLength(500);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.Date });
        builder.HasIndex(x => x.Status);

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_OvertimeDeclaration_Minutes", "\"Minutes\" > 0"));
    }
}

public sealed class GeofenceSettingConfiguration : IEntityTypeConfiguration<GeofenceSetting>
{
    public void Configure(EntityTypeBuilder<GeofenceSetting> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_GeofenceSetting_Radius", "\"RadiusMeters\" > 0"));
    }
}

// ---------------------------------------------------------------- scheduling

public sealed class ShiftTypeConfiguration : IEntityTypeConfiguration<ShiftType>
{
    public void Configure(EntityTypeBuilder<ShiftType> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ShiftType).WithMany()
            .HasForeignKey(x => x.ShiftTypeId).OnDelete(DeleteBehavior.Restrict);

        // Dropping the same person on the same shift twice is a no-op, not a duplicate.
        builder.HasIndex(x => new { x.UserId, x.Date, x.ShiftTypeId }).IsUnique();

        // The planner board reads a week at a time.
        builder.HasIndex(x => new { x.Date, x.ShiftTypeId });
    }
}

public sealed class HolidayRequestConfiguration : IEntityTypeConfiguration<HolidayRequest>
{
    public void Configure(EntityTypeBuilder<HolidayRequest> builder)
    {
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.DecisionNote).HasMaxLength(500);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.StartDate });
        builder.HasIndex(x => new { x.Status, x.StartDate });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_HolidayRequest_DateOrder", "\"EndDate\" >= \"StartDate\"");

            table.HasCheckConstraint(
                "CK_HolidayRequest_WorkingDays", "\"WorkingDays\" >= 0");
        });
    }
}

public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Region).HasMaxLength(60).IsRequired();

        builder.HasIndex(x => new { x.Date, x.Region }).IsUnique();
    }
}

// ---------------------------------------------------------------- messaging

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Priority).HasConversion<int>();

        builder.HasOne(x => x.Sender).WithMany()
            .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CreatedAt);
    }
}

public sealed class MessageRecipientConfiguration : IEntityTypeConfiguration<MessageRecipient>
{
    public void Configure(EntityTypeBuilder<MessageRecipient> builder)
    {
        builder.HasOne(x => x.Message).WithMany(x => x.Recipients)
            .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.DepartmentId);

        // A recipient row addressed to nobody would silently drop the message.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_MessageRecipient_TargetIsExclusive",
            """("UserId" IS NOT NULL AND "DepartmentId" IS NULL) OR ("UserId" IS NULL AND "DepartmentId" IS NOT NULL)"""));
    }
}

public sealed class MessageReadConfiguration : IEntityTypeConfiguration<MessageRead>
{
    public void Configure(EntityTypeBuilder<MessageRead> builder)
    {
        builder.HasKey(x => new { x.MessageId, x.UserId });

        builder.HasOne(x => x.Message).WithMany()
            .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Platform).HasConversion<int>();

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });
    }
}

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.Property(x => x.Type).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Delivery).HasConversion<int>();
        builder.Property(x => x.FailureReason).HasMaxLength(500);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Message).WithMany()
            .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.SentAt });
    }
}

// ---------------------------------------------------------------- auditing

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);

        builder.HasOne(x => x.Actor).WithMany()
            .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        // The two questions an auditor asks: what happened to this record, and what did
        // this person do.
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
    }
}
