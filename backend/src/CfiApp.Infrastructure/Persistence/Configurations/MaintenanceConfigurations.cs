using CfiApp.Domain.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CfiApp.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.Property(x => x.Number).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.EquipmentFreeText).HasMaxLength(200);

        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Priority).HasConversion<int>();

        builder.HasIndex(x => x.Number).IsUnique();

        // An offline client that sends the same report twice must not create two jobs.
        builder.HasIndex(x => x.ClientId).IsUnique().HasFilter("\"ClientId\" IS NOT NULL");

        // The pool and the history screens are the two hot queries in the whole app.
        builder.HasIndex(x => new { x.Status, x.ReportedAt });
        builder.HasIndex(x => new { x.AssignedEngineerId, x.Status });
        builder.HasIndex(x => new { x.UnitId, x.ReportedAt });
        builder.HasIndex(x => new { x.ReportedByUserId, x.ReportedAt });

        builder.HasOne(x => x.Unit).WithMany()
            .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area).WithMany()
            .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Line).WithMany()
            .HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment).WithMany()
            .HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReportedBy).WithMany()
            .HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedEngineer).WithMany()
            .HasForeignKey(x => x.AssignedEngineerId).OnDelete(DeleteBehavior.Restrict);

        // Either a known machine or a free text description, never neither: a report that
        // does not say what broke is useless at audit time.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_WorkOrder_EquipmentIdentified",
            """("EquipmentId" IS NOT NULL) OR ("EquipmentFreeText" IS NOT NULL)"""));
    }
}

public sealed class WorkOrderPhotoConfiguration : IEntityTypeConfiguration<WorkOrderPhoto>
{
    public void Configure(EntityTypeBuilder<WorkOrderPhoto> builder)
    {
        builder.Property(x => x.Category).HasConversion<int>();

        builder.HasOne(x => x.WorkOrder).WithMany(x => x.Photos)
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MediaAsset).WithMany()
            .HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkOrderId, x.Category });
    }
}

public sealed class WorkOrderEventConfiguration : IEntityTypeConfiguration<WorkOrderEvent>
{
    public void Configure(EntityTypeBuilder<WorkOrderEvent> builder)
    {
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.FromStatus).HasConversion<int>();
        builder.Property(x => x.ToStatus).HasConversion<int>();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.Summary).HasMaxLength(500);

        builder.HasOne(x => x.WorkOrder).WithMany(x => x.Events)
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Actor).WithMany()
            .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkOrderId, x.OccurredAt });
    }
}

public sealed class WorkOrderClosureConfiguration : IEntityTypeConfiguration<WorkOrderClosure>
{
    public void Configure(EntityTypeBuilder<WorkOrderClosure> builder)
    {
        builder.Property(x => x.RootCause).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CorrectiveAction).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.UnableToRepairReason).HasMaxLength(2000);
        builder.Property(x => x.ContractorUsed).HasMaxLength(200);
        builder.Property(x => x.MissingItemsNote).HasMaxLength(2000);

        builder.HasOne(x => x.WorkOrder).WithMany(x => x.Closures)
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubmittedBy).WithMany()
            .HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);

        // One row per version, and versions never collide.
        builder.HasIndex(x => new { x.WorkOrderId, x.Version }).IsUnique();

        // The form has to be internally consistent, whichever screen wrote it.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_WorkOrderClosure_UnableReason",
                """("AbleToRepair" = TRUE) OR ("UnableToRepairReason" IS NOT NULL)""");

            table.HasCheckConstraint(
                "CK_WorkOrderClosure_Contractor",
                """("ContractorRequired" = FALSE) OR ("ContractorUsed" IS NOT NULL)""");

            table.HasCheckConstraint(
                "CK_WorkOrderClosure_MissingItems",
                """("ToolsAndPartsAccounted" = TRUE) OR ("MissingItemsNote" IS NOT NULL)""");

            table.HasCheckConstraint(
                "CK_WorkOrderClosure_Downtime",
                "\"DowntimeMinutes\" >= 0");
        });
    }
}

public sealed class WorkOrderCostConfiguration : IEntityTypeConfiguration<WorkOrderCost>
{
    public void Configure(EntityTypeBuilder<WorkOrderCost> builder)
    {
        builder.Property(x => x.PartsRequired).HasMaxLength(2000);
        builder.Property(x => x.PoNumber).HasMaxLength(60);

        builder.HasOne(x => x.WorkOrder).WithMany()
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.WorkOrderId).IsUnique();
    }
}

public sealed class QaCheckConfiguration : IEntityTypeConfiguration<QaCheck>
{
    public void Configure(EntityTypeBuilder<QaCheck> builder)
    {
        builder.Property(x => x.Result).HasConversion<int>();
        builder.Property(x => x.Note).HasMaxLength(2000);

        builder.HasOne(x => x.WorkOrder).WithMany(x => x.QaChecks)
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResultBy).WithMany()
            .HasForeignKey(x => x.ResultByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WorkOrderId, x.Attempt }).IsUnique();
        builder.HasIndex(x => x.Result);

        // A failed swab must say why, otherwise the engineer has nothing to act on.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_QaCheck_FailNeedsNote",
            """("Result" <> 2) OR ("Note" IS NOT NULL)"""));
    }
}

public sealed class SignOffConfiguration : IEntityTypeConfiguration<SignOff>
{
    public void Configure(EntityTypeBuilder<SignOff> builder)
    {
        builder.Property(x => x.Kind).HasConversion<int>();

        builder.HasOne(x => x.WorkOrder).WithMany(x => x.SignOffs)
            .HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SignatureAsset).WithMany()
            .HasForeignKey(x => x.SignatureAssetId).OnDelete(DeleteBehavior.Restrict);

        // One production sign off and at most one QA sign off per job.
        builder.HasIndex(x => new { x.WorkOrderId, x.Kind }).IsUnique();
    }
}
