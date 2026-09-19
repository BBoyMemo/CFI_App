using CfiApp.Infrastructure.Messaging;
using CfiApp.Infrastructure.Files;
using CfiApp.Infrastructure.Auth;
using CfiApp.Application.Abstractions;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Scheduling;
using CfiApp.Application.Scheduling;
using CfiApp.Infrastructure.Persistence.Interceptors;
using CfiApp.Infrastructure.Persistence.Seed;
using CfiApp.Infrastructure.Security;
using CfiApp.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CfiApp.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.Configure<LocalFileStorageOptions>(configuration.GetSection(LocalFileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IPushNotificationSender, NoOpPushNotificationSender>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<IManagerScopeReader, ManagerScopeReader>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IShiftResolver, ShiftResolver>();
        services.AddScoped<RosterService>();

        services.AddDbContext<CfiAppDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(CfiAppDbContext).Assembly.FullName);

                // A factory network is not always reliable; transient faults are retried
                // instead of surfacing as a failed work order submission.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            options.AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>());
        });

        return services;
    }
}
