using System.Net;
using System.Net.Http.Json;
using CfiApp.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Brute forcing the sign in endpoint has to stop working. The shared factory raises the
/// limits so the rest of the suite can run, so this one builds its own host with a low
/// limit and checks the wall is actually there.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RateLimitingTests(CfiAppApiFactory factory)
{
    private sealed class StrictlyLimitedApi(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("Database:RunSeedOnStartup", "false");
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "3");
            builder.UseSetting("RateLimiting:Auth:WindowMinutes", "1");
        }
    }

    [Fact]
    public async Task Repeated_sign_in_attempts_are_throttled()
    {
        await using var api = new StrictlyLimitedApi(factory.ConnectionString);
        var client = api.CreateClient();

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest("nobody@example.test", "wrong-password", null));

            statuses.Add(response.StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests,
            "the sign in endpoint must stop answering after the configured number of attempts");
    }
}
