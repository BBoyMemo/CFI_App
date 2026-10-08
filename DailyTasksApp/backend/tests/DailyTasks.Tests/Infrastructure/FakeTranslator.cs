using DailyTasks.Api.Localization;

namespace DailyTasks.Tests.Infrastructure;

// Stands in for DeepL. A text starting with "[xx] " is treated as written in language xx,
// anything else as English. The "translation" is the text prefixed with "<target> ".
public class FakeTranslator : ITranslator
{
    public bool IsEnabled { get; set; } = true;
    public Exception? FailWith { get; set; }
    public List<(string Language, IReadOnlyList<string> Texts)> Calls { get; } = [];

    public Task<IReadOnlyList<TranslatedText>> TranslateAsync(
        IReadOnlyList<string> texts, string language, CancellationToken ct)
    {
        if (FailWith is not null) throw FailWith;
        lock (Calls) Calls.Add((language, texts));
        IReadOnlyList<TranslatedText> result = texts
            .Select(t => new TranslatedText($"<{language}> {t}", SourceOf(t)))
            .ToList();
        return Task.FromResult(result);
    }

    private static string SourceOf(string text) =>
        text.Length > 5 && text[0] == '[' && text[3] == ']' ? text[1..3] : "en";
}
