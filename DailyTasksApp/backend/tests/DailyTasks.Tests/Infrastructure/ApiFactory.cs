using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DailyTasks.Api.Localization;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DailyTasks.Tests.Infrastructure;

public class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}

// One Postgres container for the whole test run; every factory gets its own database in it.
public static class SharedPostgres
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Container = new(async () =>
    {
        var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await container.StartAsync();
        return container;
    });

    public static async Task<string> NewDatabaseAsync()
    {
        var container = await Container.Value;
        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = $"dt_test_{Guid.NewGuid():N}",
        };
        return builder.ConnectionString;
    }
}

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ManagerName = "Test Manager";
    public const string ManagerPassword = "ManagerPass1";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _storage = Path.Combine(Path.GetTempPath(), "dt_test_storage", Guid.NewGuid().ToString("N"));
    private string _connectionString = "";

    public TestClock Clock { get; } = new();
    public FakeTranslator Translator { get; } = new();

    // Runs what the background worker would: translates every job that is due.
    public async Task<int> ProcessTranslationsAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TranslationProcessor>().ProcessDueAsync(CancellationToken.None);
    }

    async Task IAsyncLifetime.InitializeAsync() => _connectionString = await SharedPostgres.NewDatabaseAsync();

    Task IAsyncLifetime.DisposeAsync()
    {
        if (Directory.Exists(_storage)) Directory.Delete(_storage, recursive: true);
        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (not ConfigureAppConfiguration) so Program sees these before Build().
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-0123456789");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Bootstrap:ManagerName", ManagerName);
        builder.UseSetting("Bootstrap:ManagerPassword", ManagerPassword);
        builder.UseSetting("Storage:RootPath", _storage);
        builder.UseSetting("RateLimit:LoginPerMinute", "1000");
        // The background worker is off: tests run the translation step themselves, deterministically.
        builder.UseSetting("Translation:RunWorker", "false");
        builder.UseSetting("Translation:PauseBetweenJobsMs", "0");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<ITranslator>();
            services.AddSingleton<ITranslator>(Translator);
        });
    }

    public HttpClient Anonymous() => CreateClient();

    public async Task<HttpClient> LoginAsync(string name, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { name, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    public Task<HttpClient> ManagerAsync() => LoginAsync(ManagerName, ManagerPassword);

    // Creates a user through the API as the bootstrap manager. Names are unique per call.
    public async Task<(Guid Id, string Name, HttpClient Client)> NewUserAsync(string role, string firstName = "Oliver")
    {
        var name = $"{firstName} {Guid.NewGuid().ToString("N")[..8]}";
        const string password = "UserPass123";
        var manager = await ManagerAsync();
        var response = await manager.PostAsJsonAsync("/api/users", new { name, password, role });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (body.GetProperty("id").GetGuid(), name, await LoginAsync(name, password));
    }
}

public static class TestData
{
    // Enough of a JPEG for signature sniffing: SOI marker + APP0 header bytes.
    public static byte[] Jpeg => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    // What the browser sends when the engineer completes without a comment or photo.
    public static MultipartFormDataContent EmptyCompletion() => new() { { new StringContent(""), "comment" } };

    public static ByteArrayContent File(byte[] bytes, string contentType = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiFactory.Json);
}
