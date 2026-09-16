using System.Net;
using System.Text.Json;
using Shouldly;

namespace CfiApp.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(CfiAppApiFactory factory)
{
    [Fact]
    public async Task Liveness_reports_healthy_without_touching_the_database()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Readiness_reports_healthy_when_the_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");

        var checks = payload.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.ShouldContain(check => check.GetProperty("name").GetString() == "postgres");
    }

    [Fact]
    public async Task Readiness_never_leaks_connection_details_in_its_response()
    {
        var client = factory.CreateClient();

        var body = await client.GetStringAsync("/health/ready");

        body.ShouldNotContain("Password");
        body.ShouldNotContain("Host=");
    }
}
