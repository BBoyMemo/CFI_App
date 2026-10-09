using DailyTasks.Api.Auth;
using DailyTasks.Api.Common;
using DailyTasks.Api.Contracts;
using DailyTasks.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Data;

public class DatabaseOptions
{
    public const string Section = "Database";

    public bool MigrateOnStartup { get; set; }
}

// There is no self-registration, so an empty database needs one Manager to start from.
// Name and password come from configuration and are used only while the users table is empty.
public class BootstrapOptions
{
    public const string Section = "Bootstrap";

    public string? ManagerName { get; set; }
    public string? ManagerPassword { get; set; }
}

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        if (config.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>()?.MigrateOnStartup == true)
            await db.Database.MigrateAsync();

        var cards = await TaskUpdateBackfill.RunAsync(db);
        if (cards > 0) logger.LogInformation("Turned {Count} earlier completions into update cards.", cards);

        if (await db.Users.AnyAsync()) return;

        var bootstrap = config.GetSection(BootstrapOptions.Section).Get<BootstrapOptions>();
        var name = bootstrap?.ManagerName?.Trim();
        var password = bootstrap?.ManagerPassword;
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(password) || password.Length < Limits.PasswordMin)
        {
            logger.LogWarning(
                "No users exist and Bootstrap:ManagerName / Bootstrap:ManagerPassword (min {Min} chars) are not set. " +
                "Nobody can log in until they are.", Limits.PasswordMin);
            return;
        }

        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = User.Normalize(name),
            PasswordHash = PasswordHasher.Hash(password),
            Role = Role.Manager,
            CreatedAt = sp.GetRequiredService<AppClock>().UtcNow,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Created the first Manager account '{Name}'.", name);
    }
}
