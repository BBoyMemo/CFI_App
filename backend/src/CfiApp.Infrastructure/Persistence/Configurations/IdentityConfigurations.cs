using CfiApp.Domain.Identity;
using CfiApp.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CfiApp.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PreferredLanguage).HasMaxLength(5).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.Property(x => x.Status).HasConversion<int>();

        // Email is the login identifier, so uniqueness is a database guarantee rather
        // than a check that a race condition can slip past.
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Occupation)
            .WithMany()
            .HasForeignKey(x => x.OccupationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserAreaConfiguration : IEntityTypeConfiguration<UserArea>
{
    public void Configure(EntityTypeBuilder<UserArea> builder)
    {
        builder.HasKey(x => new { x.UserId, x.AreaId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.Areas)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AreaId);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.HasOne(x => x.Role)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ManagerScopeConfiguration : IEntityTypeConfiguration<ManagerScope>
{
    public void Configure(EntityTypeBuilder<ManagerScope> builder)
    {
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserId);

        // A scope row that names neither a department nor a unit would silently widen or
        // narrow every query built on it, so the database refuses to store one.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ManagerScope_TargetIsExclusive",
            """("DepartmentId" IS NOT NULL AND "UnitId" IS NULL) OR ("DepartmentId" IS NULL AND "UnitId" IS NOT NULL)"""));
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DeviceId).HasMaxLength(128);
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.RevokedReason).HasMaxLength(120);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAt });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReplacedByToken)
            .WithMany()
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserSignatureConfiguration : IEntityTypeConfiguration<UserSignature>
{
    public void Configure(EntityTypeBuilder<UserSignature> builder)
    {
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one signature is current per user; superseded rows keep ReplacedAt set and
        // stay for the audit trail.
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("\"ReplacedAt\" IS NULL");
    }
}

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.Property(x => x.StorageKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(256);

        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => x.Sha256);
    }
}
