using System.Security.Claims;
using CfiApp.Application.Abstractions;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Lets a database level test decide who the acting user is, so audit columns can be
/// asserted without signing in over HTTP.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
}

/// <summary>
/// Inside a request the real signed in user always wins, so the auth tests exercise the
/// same code path production does. The test override only applies outside a request,
/// where there is no principal to read.
/// </summary>
internal sealed class RequestOrTestCurrentUser(
    TestCurrentUser fallback,
    IHttpContextAccessor accessor) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : fallback.UserId;
        }
    }
}

/// <summary>
/// Hosts the real API against a throwaway PostgreSQL container.
/// A real database is used rather than an in-memory provider, because the parts most
/// likely to break in production - migrations, indexes, concurrency tokens, provider
/// specific SQL - simply do not exist in an in-memory provider.
/// </summary>
public sealed class CfiAppApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Pinned to the same major version as the production database (PostgreSQL 16).
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("cfiAppDb_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public TestCurrentUser CurrentUser { get; } = new();

    public string ConnectionString => _database.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseEnvironment("Development");

        // The suite signs in far more often per minute than a human ever would. The
        // production limits stay as configured; RateLimitingTests proves they work by
        // building its own host with the real values.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICurrentUser>();
            services.AddScoped<ICurrentUser>(provider => new RequestOrTestCurrentUser(
                CurrentUser,
                provider.GetRequiredService<IHttpContextAccessor>()));
        });
    }

    /// <summary>Opens a context outside any request, the way a background job would.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Running the real migrations proves they apply to an empty database. A migration
        // that only ever ran against a developer machine is not a tested migration.
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CfiAppApiFactory>
{
    public const string Name = "CFI App API";
}
