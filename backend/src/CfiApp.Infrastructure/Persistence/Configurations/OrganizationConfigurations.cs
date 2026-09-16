using CfiApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CfiApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Reference tables for the physical site: Unit contains Areas, an Area contains
/// Equipment, and Lines run inside a unit. Deletes are restricted everywhere - a work
/// order raised years ago must still be able to name where it happened.
/// </summary>
public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder });
    }
}

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(20);

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.Areas)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UnitId, x.IsActive });
        builder.HasIndex(x => new { x.UnitId, x.Name }).IsUnique();
    }
}

public sealed class LineConfiguration : IEntityTypeConfiguration<Line>
{
    public void Configure(EntityTypeBuilder<Line> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UnitId, x.IsActive });
    }
}

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.IconKey).HasMaxLength(60);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area)
            .WithMany(x => x.Equipment)
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Line)
            .WithMany(x => x.Equipment)
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);

        // One level deep on purpose: a machine has parts, a part does not have parts of
        // its own. Anything deeper would be a spare-parts catalogue, not a place to report
        // a fault against.
        builder.HasOne(x => x.ParentEquipment)
            .WithMany(x => x.Parts)
            .HasForeignKey(x => x.ParentEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The two report flows: engineer picks by unit then room, operator picks by line.
        builder.HasIndex(x => new { x.UnitId, x.IsActive });
        builder.HasIndex(x => new { x.AreaId, x.IsActive });
        builder.HasIndex(x => new { x.LineId, x.IsActive });
        builder.HasIndex(x => new { x.ParentEquipmentId, x.IsActive });
    }
}

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class OccupationConfiguration : IEntityTypeConfiguration<Occupation>
{
    public void Configure(EntityTypeBuilder<Occupation> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
