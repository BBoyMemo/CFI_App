using System.Net;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using Shouldly;

namespace CfiApp.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class SystemEndpointTests(CfiAppApiFactory factory)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    [Fact]
    public async Task Info_endpoint_is_served_under_the_versioned_route()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var info = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();
        info.ShouldNotBeNull();
        info.Application.ShouldBe("CFI App API");
        info.ServerTimeUtc.Offset.ShouldBe(TimeSpan.Zero, "attendance depends on the server never returning local time");
    }

    [Fact]
    public async Task Unversioned_route_is_not_served()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/system/info");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/info");

        response.Headers.TryGetValues(CorrelationHeader, out var values).ShouldBeTrue();
        values!.Single().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Client_supplied_correlation_id_is_echoed_back()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/info");
        request.Headers.Add(CorrelationHeader, "mobile-abc-123");

        var response = await client.SendAsync(request);

        response.Headers.GetValues(CorrelationHeader).Single().ShouldBe("mobile-abc-123");
    }

    [Fact]
    public async Task Malformed_correlation_id_is_replaced_rather_than_written_to_the_log()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/info");
        request.Headers.TryAddWithoutValidation(CorrelationHeader, "bad id with spaces");

        var response = await client.SendAsync(request);

        response.Headers.GetValues(CorrelationHeader).Single().ShouldNotBe("bad id with spaces");
    }
}
