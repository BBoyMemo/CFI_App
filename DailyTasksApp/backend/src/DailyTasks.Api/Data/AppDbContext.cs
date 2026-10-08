using DailyTasks.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<DailyTask> Tasks => Set<DailyTask>();
    public DbSet<DailyTaskAssignee> TaskAssignees => Set<DailyTaskAssignee>();
    public DbSet<TaskPhoto> TaskPhotos => Set<TaskPhoto>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<TranslationJob> TranslationJobs => Set<TranslationJob>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<DailyTask>(e =>
        {
            e.ToTable("tasks");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Shift).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CompletionComment).HasMaxLength(2000);
            e.Property(x => x.CompletionPhotoKey).HasMaxLength(200);
            e.Ignore(x => x.IsCompleted);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.CompletedAt, x.Date });
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CompletedBy).WithMany().HasForeignKey(x => x.CompletedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DailyTaskAssignee>(e =>
        {
            e.ToTable("task_assignees");
            e.HasKey(x => new { x.TaskId, x.UserId });
            e.HasOne(x => x.Task).WithMany(t => t.Assignees).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<TaskPhoto>(e =>
        {
            e.ToTable("task_photos");
            e.Property(x => x.PhotoKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TaskPhotoKind.Plan);
            e.HasOne(x => x.Task).WithMany(t => t.Photos).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.TaskId);
        });

        b.Entity<Translation>(e =>
        {
            e.ToTable("translations");
            e.Property(x => x.EntityType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Field).HasMaxLength(20).IsRequired();
            e.Property(x => x.Language).HasMaxLength(5).IsRequired();
            e.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.Field, x.Language }).IsUnique();
            e.HasIndex(x => x.EntityId);
        });

        b.Entity<TranslationJob>(e =>
        {
            e.ToTable("translation_jobs");
            e.Property(x => x.EntityType).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.EntityType, x.EntityId }).IsUnique();
            e.HasIndex(x => x.NextAttemptAt);
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.PhotoKey).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.OrderedBy).WithMany().HasForeignKey(x => x.OrderedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
