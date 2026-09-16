using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CfiApp.Application.Auth;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// Bad input has to come back as a readable, consistent error - not a 500, and not a
/// field-by-field guess for whoever is building the screen.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ValidationTests(CfiAppApiFactory factory)
{
    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        return errors.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.EnumerateArray().Select(x => x.GetString()!).ToArray());
    }

    [Fact]
    public async Task A_short_password_is_refused_with_a_message_a_person_can_act_on()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", "short@example.test", null, "abc123", "en"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var errors = await ErrorsAsync(response);
        errors.ShouldContainKey(nameof(RegisterRequest.Password));
        errors[nameof(RegisterRequest.Password)].ShouldContain(x => x.Contains("at least 10"));
    }

    [Fact]
    public async Task An_obvious_password_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", "obvious@example.test", null, "password123", "en"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey(nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task A_password_containing_the_email_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", "jsmithy@example.test", null, "jsmithy-2026-cfi", "en"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey(nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task An_invalid_email_is_refused()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", "not-an-email", null, "Widnes-Shift-2026!", "en"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey(nameof(RegisterRequest.Email));
    }

    [Fact]
    public async Task An_unsupported_language_is_refused_at_registration()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test Worker", "lang@example.test", null, "Widnes-Shift-2026!", "de"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response)).ShouldContainKey(nameof(RegisterRequest.PreferredLanguage));
    }

    [Fact]
    public async Task Several_bad_fields_are_reported_together_not_one_at_a_time()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("", "also-not-an-email", null, "x", "fr"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var errors = await ErrorsAsync(response);
        errors.Count.ShouldBeGreaterThanOrEqualTo(3, "the form should not be fixed one field per round trip");
    }

    [Fact]
    public async Task Every_error_carries_the_correlation_id_for_support()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("", "bad", null, "x", "en"));

        response.Headers.TryGetValues("X-Correlation-Id", out var values).ShouldBeTrue();
        values!.Single().ShouldNotBeNullOrWhiteSpace();
    }
}
