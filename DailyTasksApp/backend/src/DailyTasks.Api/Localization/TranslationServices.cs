using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DailyTasks.Api.Common;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyTasks.Api.Localization;

public class TranslationOptions
{
    public const string Section = "Translation";

    // Empty = translation is off and no text ever leaves the server.
    // A DeepL API Free key ends in ":fx" and is sent to the free endpoint automatically.
    public string? DeepLAuthKey { get; set; }
    public bool RunWorker { get; set; } = true;
    public int PollSeconds { get; set; } = 5;

    // Pause between records, so a burst (e.g. translating existing data) stays under DeepL's rate limit.
    public int PauseBetweenJobsMs { get; set; } = 400;
}

// The UI languages. Every free-text field is kept in all of them.
public static class AppLanguages
{
    public static readonly string[] All = ["en", "pl", "bg", "fil"];

    public static string ToDeepLTarget(string language) => language switch
    {
        "en" => "EN-GB",
        // DeepL calls Filipino by its base language, Tagalog.
        "fil" => "TL",
        _ => language.ToUpperInvariant(),
    };

    // DeepL reports the source as e.g. "EN", "PL" or "TL"; we keep the app's language code.
    public static string FromDeepLSource(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        var two = code[..Math.Min(2, code.Length)].ToLowerInvariant();
        return two == "tl" ? "fil" : two;
    }
}

public record TranslatedText(string Text, string SourceLanguage);

public interface ITranslator
{
    bool IsEnabled { get; }

    // Translates every text into one app language; the source language is detected per text.
    Task<IReadOnlyList<TranslatedText>> TranslateAsync(IReadOnlyList<string> texts, string language, CancellationToken ct);
}

public class TranslationQuotaExceededException(string message) : Exception(message);
public class TranslationAuthException(string message) : Exception(message);
public class TranslationUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public class DeepLTranslator(HttpClient http, IOptionsMonitor<TranslationOptions> options) : ITranslator
{
    private const string FreeEndpoint = "https://api-free.deepl.com/v2/translate";
    private const string ProEndpoint = "https://api.deepl.com/v2/translate";

    private string? Key => options.CurrentValue.DeepLAuthKey?.Trim();

    public bool IsEnabled => !string.IsNullOrEmpty(Key);

    public async Task<IReadOnlyList<TranslatedText>> TranslateAsync(
        IReadOnlyList<string> texts, string language, CancellationToken ct)
    {
        var key = Key ?? throw new TranslationAuthException("No DeepL key configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, key.EndsWith(":fx") ? FreeEndpoint : ProEndpoint)
        {
            Content = JsonContent.Create(new DeepLRequest(texts, AppLanguages.ToDeepLTarget(language))),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"DeepL-Auth-Key {key}");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new TranslationUnavailableException("DeepL could not be reached.", e);
        }

        using (response)
        {
            switch ((int)response.StatusCode)
            {
                case 456:
                    throw new TranslationQuotaExceededException("The monthly DeepL character quota is used up.");
                case (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.Unauthorized:
                    throw new TranslationAuthException("DeepL rejected the key.");
            }
            if (!response.IsSuccessStatusCode)
                throw new TranslationUnavailableException($"DeepL answered {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<DeepLResponse>(ct)
                       ?? throw new TranslationUnavailableException("DeepL sent an empty answer.");
            if (body.Translations.Count != texts.Count)
                throw new TranslationUnavailableException("DeepL returned a different number of texts.");
            return body.Translations
                .Select(t => new TranslatedText(t.Text, AppLanguages.FromDeepLSource(t.DetectedSourceLanguage)))
                .ToList();
        }
    }

    private record DeepLRequest(
        [property: JsonPropertyName("text")] IReadOnlyList<string> Text,
        [property: JsonPropertyName("target_lang")] string TargetLang);

    private record DeepLResponse([property: JsonPropertyName("translations")] List<DeepLTranslation> Translations);

    private record DeepLTranslation(
        [property: JsonPropertyName("detected_source_language")] string? DetectedSourceLanguage,
        [property: JsonPropertyName("text")] string Text);
}

public static class TextHash
{
    public static string Of(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

// Adds a "translate this record" job to the context; the caller's SaveChanges commits it.
public class TranslationQueue(AppDbContext db, ITranslator translator, AppClock clock)
{
    public async Task AddAsync(string entityType, Guid entityId, CancellationToken ct)
    {
        if (!translator.IsEnabled) return;
        if (db.TranslationJobs.Local.Any(j => j.EntityType == entityType && j.EntityId == entityId)) return;

        var existing = await db.TranslationJobs
            .SingleOrDefaultAsync(j => j.EntityType == entityType && j.EntityId == entityId, ct);
        if (existing is not null)
        {
            // Already waiting (maybe backing off): the new text should go out promptly.
            existing.NextAttemptAt = clock.UtcNow;
            existing.Attempts = 0;
            return;
        }

        db.TranslationJobs.Add(new TranslationJob
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            NextAttemptAt = clock.UtcNow,
            CreatedAt = clock.UtcNow,
        });
    }

    // For a deleted record: its translations and any waiting job go too.
    public async Task RemoveAllForAsync(Guid entityId, CancellationToken ct)
    {
        await db.Translations.Where(t => t.EntityId == entityId).ExecuteDeleteAsync(ct);
        await db.TranslationJobs.Where(j => j.EntityId == entityId).ExecuteDeleteAsync(ct);
    }
}

// Translations of a set of records, filtered to the ones that still match the current text.
public class TranslationSet(ILookup<Guid, Translation> rows)
{
    public static async Task<TranslationSet> LoadAsync(AppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new TranslationSet(Array.Empty<Translation>().ToLookup(t => t.EntityId));
        var rows = await db.Translations.AsNoTracking().Where(t => ids.Contains(t.EntityId)).ToListAsync(ct);
        return new TranslationSet(rows.ToLookup(t => t.EntityId));
    }

    // { "pl": { "title": "...", "description": "..." }, ... } — only languages with something to show.
    public Dictionary<string, Dictionary<string, string>> For(Guid id, params (string Field, string? Text)[] fields)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (field, text) in fields)
        {
            if (string.IsNullOrEmpty(text)) continue;
            var hash = TextHash.Of(text);
            foreach (var row in rows[id].Where(r => r.Field == field && r.SourceHash == hash))
            {
                if (!result.TryGetValue(row.Language, out var perField))
                    result[row.Language] = perField = new Dictionary<string, string>();
                perField[field] = row.Text;
            }
        }
        return result;
    }
}

public class TranslationProcessor(
    AppDbContext db,
    ITranslator translator,
    AppClock clock,
    IOptionsMonitor<TranslationOptions> options,
    ILogger<TranslationProcessor> logger)
{
    private const int MaxAttempts = 12;

    // Handles the jobs that are due. Returns how many were finished.
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        if (!translator.IsEnabled) return 0;

        var now = clock.UtcNow;
        var jobs = await db.TranslationJobs
            .Where(j => j.NextAttemptAt <= now)
            .OrderBy(j => j.NextAttemptAt)
            .Take(10)
            .ToListAsync(ct);

        var finished = 0;
        var pause = options.CurrentValue.PauseBetweenJobsMs;
        foreach (var job in jobs)
        {
            if (finished > 0 && pause > 0) await Task.Delay(pause, ct);
            try
            {
                await TranslateAsync(job, ct);
                db.TranslationJobs.Remove(job);
                finished++;
            }
            catch (TranslationQuotaExceededException e)
            {
                // Nothing will get through until the quota resets; try again later, keep the rest waiting.
                logger.LogWarning("{Message} Translations wait and are retried later.", e.Message);
                await PostponeAllAsync(TimeSpan.FromHours(6), ct);
                break;
            }
            catch (TranslationAuthException e)
            {
                logger.LogError("{Message} Check Translation:DeepLAuthKey.", e.Message);
                await PostponeAllAsync(TimeSpan.FromHours(1), ct);
                break;
            }
            catch (TranslationUnavailableException e)
            {
                job.Attempts++;
                if (job.Attempts >= MaxAttempts)
                {
                    logger.LogError(e, "Giving up translating {Type} {Id} after {Attempts} attempts.",
                        job.EntityType, job.EntityId, job.Attempts);
                    db.TranslationJobs.Remove(job);
                }
                else
                {
                    var wait = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, job.Attempts), 360));
                    job.NextAttemptAt = clock.UtcNow + wait;
                    logger.LogWarning("{Message} Retrying {Type} {Id} in {Wait}.", e.Message, job.EntityType, job.EntityId, wait);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        return finished;
    }

    // Records written before translation was switched on, or before a language was added, lack
    // a translation in some language and have no job. Queues each of them once.
    public async Task<int> EnqueueMissingAsync(CancellationToken ct)
    {
        if (!translator.IsEnabled) return 0;
        var now = clock.UtcNow;
        var queued = new HashSet<Guid>(await db.TranslationJobs.Select(j => j.EntityId).ToListAsync(ct));
        var jobs = new List<TranslationJob>();

        void Add(string type, IEnumerable<Guid> ids)
        {
            foreach (var id in ids.Where(queued.Add)) jobs.Add(NewJob(type, id, now));
        }

        foreach (var language in AppLanguages.All)
        {
            Add(TranslatedEntity.Task, await db.Tasks.AsNoTracking()
                .Where(t => !db.Translations.Any(x => x.EntityId == t.Id && x.Language == language))
                .Select(t => t.Id).ToListAsync(ct));
            Add(TranslatedEntity.TaskUpdate, await db.TaskUpdates.AsNoTracking()
                .Where(u => u.Comment != null && u.Comment != "" &&
                            !db.Translations.Any(x => x.EntityId == u.Id && x.Language == language))
                .Select(u => u.Id).ToListAsync(ct));
            Add(TranslatedEntity.Order, await db.Orders.AsNoTracking()
                .Where(o => !db.Translations.Any(x => x.EntityId == o.Id && x.Language == language))
                .Select(o => o.Id).ToListAsync(ct));
        }

        db.TranslationJobs.AddRange(jobs);
        await db.SaveChangesAsync(ct);
        return jobs.Count;
    }

    private static TranslationJob NewJob(string type, Guid id, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = type,
        EntityId = id,
        NextAttemptAt = now,
        CreatedAt = now,
    };

    private async Task PostponeAllAsync(TimeSpan wait, CancellationToken ct)
    {
        var until = clock.UtcNow + wait;
        await db.TranslationJobs.Where(j => j.NextAttemptAt < until)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAt, until), ct);
    }

    private async Task TranslateAsync(TranslationJob job, CancellationToken ct)
    {
        var fields = await LoadFieldsAsync(job, ct);
        if (fields.Count == 0) return; // record deleted, or nothing to translate

        var existing = await db.Translations.Where(t => t.EntityId == job.EntityId).ToListAsync(ct);
        bool Done(string field, string text, string language) =>
            existing.Any(t => t.Field == field && t.Language == language && t.SourceHash == TextHash.Of(text));

        var todo = fields.Where(f => AppLanguages.All.Any(l => !Done(f.Field, f.Text, l))).ToList();
        if (todo.Count == 0) return;

        // English first: it also tells us each text's source language.
        var english = await translator.TranslateAsync(todo.Select(f => f.Text).ToList(), "en", ct);
        var source = new Dictionary<string, string>();
        for (var i = 0; i < todo.Count; i++)
        {
            var (field, text) = todo[i];
            source[field] = english[i].SourceLanguage;
            Store(job, field, text, "en", english[i].SourceLanguage == "en" ? text : english[i].Text, existing);
            // The original counts as its own language's version, so it is never sent out again.
            if (AppLanguages.All.Contains(english[i].SourceLanguage))
                Store(job, field, text, english[i].SourceLanguage, text, existing);
        }

        foreach (var language in AppLanguages.All.Where(l => l != "en"))
        {
            var needed = todo.Where(f => source[f.Field] != language && !Done(f.Field, f.Text, language)).ToList();
            if (needed.Count == 0) continue;
            var translated = await translator.TranslateAsync(needed.Select(f => f.Text).ToList(), language, ct);
            for (var i = 0; i < needed.Count; i++)
                Store(job, needed[i].Field, needed[i].Text, language, translated[i].Text, existing);
        }
    }

    private void Store(TranslationJob job, string field, string source, string language, string text, List<Translation> existing)
    {
        var hash = TextHash.Of(source);
        var row = existing.SingleOrDefault(t => t.Field == field && t.Language == language);
        if (row is null)
        {
            row = new Translation
            {
                Id = Guid.NewGuid(),
                EntityType = job.EntityType,
                EntityId = job.EntityId,
                Field = field,
                Language = language,
            };
            db.Translations.Add(row);
            existing.Add(row);
        }
        row.SourceHash = hash;
        row.Text = text.Length > 4000 ? text[..4000] : text;
        row.CreatedAt = clock.UtcNow;
    }

    private async Task<List<(string Field, string Text)>> LoadFieldsAsync(TranslationJob job, CancellationToken ct)
    {
        var fields = new List<(string Field, string? Text)>();
        if (job.EntityType == TranslatedEntity.Task)
        {
            var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == job.EntityId, ct);
            if (task is not null)
            {
                fields.Add((TranslatedField.Title, task.Title));
                fields.Add((TranslatedField.Description, task.Description));
            }
        }
        else if (job.EntityType == TranslatedEntity.TaskUpdate)
        {
            var update = await db.TaskUpdates.AsNoTracking().SingleOrDefaultAsync(u => u.Id == job.EntityId, ct);
            if (update is not null) fields.Add((TranslatedField.Comment, update.Comment));
        }
        else if (job.EntityType == TranslatedEntity.Order)
        {
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == job.EntityId, ct);
            if (order is not null) fields.Add((TranslatedField.Description, order.Description));
        }
        return fields.Where(f => !string.IsNullOrWhiteSpace(f.Text)).Select(f => (f.Field, f.Text!)).ToList();
    }
}

public class TranslationWorker(IServiceScopeFactory scopes, IOptionsMonitor<TranslationOptions> options, ILogger<TranslationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.RunWorker) return;
        var backfilled = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<TranslationProcessor>();
                // First pass after a key is available: pick up everything written before it.
                if (!backfilled && scope.ServiceProvider.GetRequiredService<ITranslator>().IsEnabled)
                {
                    var queued = await processor.EnqueueMissingAsync(stoppingToken);
                    if (queued > 0) logger.LogInformation("Queued {Count} existing records for translation.", queued);
                    backfilled = true;
                }
                // Keep going while there is work; otherwise check again shortly.
                while (await processor.ProcessDueAsync(stoppingToken) > 0) { }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(e, "Translation worker pass failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.PollSeconds)), stoppingToken)
                .ContinueWith(_ => { }, CancellationToken.None);
        }
    }
}
