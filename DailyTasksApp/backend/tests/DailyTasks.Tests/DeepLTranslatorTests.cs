using System.Net;
using System.Text;
using System.Text.Json;
using DailyTasks.Api.Localization;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DailyTasks.Tests;

// The real DeepL client against a recorded-style HTTP handler: no network involved.
public class DeepLTranslatorTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Monitor(TranslationOptions value) : IOptionsMonitor<TranslationOptions>
    {
        public TranslationOptions CurrentValue => value;
        public TranslationOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<TranslationOptions, string?> listener) => null;
    }

    private static (DeepLTranslator Translator, Handler Handler) Create(string? key, HttpStatusCode status, string body)
    {
        var handler = new Handler(status, body);
        return (new DeepLTranslator(new HttpClient(handler), new Monitor(new TranslationOptions { DeepLAuthKey = key })), handler);
    }

    [Fact]
    public async Task Free_key_goes_to_the_free_endpoint_with_the_auth_header_and_british_english()
    {
        var (translator, handler) = Create("abc-123:fx", HttpStatusCode.OK,
            """{"translations":[{"detected_source_language":"PL","text":"Replace the filter"}]}""");

        var result = await translator.TranslateAsync(["Wymień filtr"], "en", CancellationToken.None);

        result.ShouldHaveSingleItem().ShouldBe(new TranslatedText("Replace the filter", "pl"));
        handler.Request!.RequestUri!.ToString().ShouldBe("https://api-free.deepl.com/v2/translate");
        handler.Request.Headers.GetValues("Authorization").Single().ShouldBe("DeepL-Auth-Key abc-123:fx");
        using var json = JsonDocument.Parse(handler.RequestBody!);
        json.RootElement.GetProperty("target_lang").GetString().ShouldBe("EN-GB");
        json.RootElement.GetProperty("text")[0].GetString().ShouldBe("Wymień filtr");
    }

    [Fact]
    public async Task Paid_key_goes_to_the_pro_endpoint()
    {
        var (translator, handler) = Create("paid-key", HttpStatusCode.OK,
            """{"translations":[{"detected_source_language":"EN","text":"Wymień filtr"}]}""");
        await translator.TranslateAsync(["Replace the filter"], "pl", CancellationToken.None);
        handler.Request!.RequestUri!.ToString().ShouldBe("https://api.deepl.com/v2/translate");
    }

    [Theory]
    [InlineData(456, typeof(TranslationQuotaExceededException))]
    [InlineData(403, typeof(TranslationAuthException))]
    [InlineData(429, typeof(TranslationUnavailableException))]
    [InlineData(503, typeof(TranslationUnavailableException))]
    public async Task Error_answers_map_to_what_the_processor_acts_on(int status, Type expected)
    {
        var (translator, _) = Create("k:fx", (HttpStatusCode)status, "{}");
        var error = await Should.ThrowAsync<Exception>(() => translator.TranslateAsync(["x"], "es", CancellationToken.None));
        error.ShouldBeOfType(expected);
    }

    [Fact]
    public void No_key_means_disabled()
    {
        Create(null, HttpStatusCode.OK, "{}").Translator.IsEnabled.ShouldBeFalse();
        Create("  ", HttpStatusCode.OK, "{}").Translator.IsEnabled.ShouldBeFalse();
        Create("k:fx", HttpStatusCode.OK, "{}").Translator.IsEnabled.ShouldBeTrue();
    }
}
