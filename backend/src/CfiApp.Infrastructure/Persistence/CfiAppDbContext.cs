using CfiApp.Domain.Attendance;
using CfiApp.Domain.Auditing;
using CfiApp.Domain.Messaging;
using CfiApp.Domain.Scheduling;
using CfiApp.Domain.Work;
using CfiApp.Domain.Common;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Domain.Media;
using CfiApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Persistence;

/// <summary>
/// Application database context. Entity configurations live in
/// Persistence/Configurations and are discovered automatically, so this class stays a
/// list of tables rather than a wall of mapping code.
/// </summary>
public sealed class CfiAppDbContext(DbContextOptions<CfiAppDbContext> options) : DbContext(options)
{
    // ---- Organisation and site layout (Phase 1) ----
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Occupation> Occupations => Set<Occupation>();

    // ---- Identity and authorisation (Phase 1) ----
    public DbSet<User> Users => Set<User>();
    public DbSet<UserArea> UserAreas => Set<UserArea>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ManagerScope> ManagerScopes => Set<ManagerScope>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();

    // ---- Maintenance (Phase 1) ----
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderPhoto> WorkOrderPhotos => Set<WorkOrderPhoto>();
    public DbSet<WorkOrderEvent> WorkOrderEvents => Set<WorkOrderEvent>();
    public DbSet<WorkOrderClosure> WorkOrderClosures => Set<WorkOrderClosure>();
    public DbSet<WorkOrderCost> WorkOrderCosts => Set<WorkOrderCost>();
    public DbSet<QaCheck> QaChecks => Set<QaCheck>();
    public DbSet<SignOff> SignOffs => Set<SignOff>();

    // ---- Tasks and part orders (Phase 1) ----
    public DbSet<MaintenanceTask> MaintenanceTasks => Set<MaintenanceTask>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();
    public DbSet<TaskCompletionPhoto> TaskCompletionPhotos => Set<TaskCompletionPhoto>();
    public DbSet<PartOrderRequest> PartOrderRequests => Set<PartOrderRequest>();

    // ---- Attendance (Phase 1) ----
    public DbSet<ClockEvent> ClockEvents => Set<ClockEvent>();
    public DbSet<ClockCorrection> ClockCorrections => Set<ClockCorrection>();
    public DbSet<OvertimeDeclaration> OvertimeDeclarations => Set<OvertimeDeclaration>();
    public DbSet<GeofenceSetting> GeofenceSettings => Set<GeofenceSetting>();

    // ---- Scheduling (Phase 1) ----
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<HolidayRequest> HolidayRequests => Set<HolidayRequest>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();

    // ---- Messaging (Phase 1) ----
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
    public DbSet<MessageRead> MessageReads => Set<MessageRead>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    // ---- Auditing (Phase 1) ----
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ---- Files (Phase 1) ----
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CfiAppDbContext).Assembly);

        // Rows that more than one person can touch at the same time carry a concurrency
        // token, so a second writer is told about the conflict instead of overwriting it.
        // PostgreSQL exposes this as the system column xmin - no extra column is stored.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // The database may move from PostgreSQL to SQL Server later, so decimal precision
        // is stated explicitly instead of relying on provider defaults.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
