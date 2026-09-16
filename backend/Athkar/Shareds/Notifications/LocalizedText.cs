namespace Athkar.Shareds.Notifications;

/// <summary>
/// A notification's wording, keyed by language code, with the fallback rule the
/// whole system uses in one place.
///
/// Notifications are resolved per recipient rather than per request: a device
/// picked Turkish in settings and the campaign may or may not have been
/// translated into it. Rather than let every call site invent its own fallback,
/// the rule lives here — asked language, then the platform default, then
/// whatever exists — and a message with no translations at all is a programming
/// error the caller should never have been able to construct.
/// </summary>
public sealed class LocalizedText
{
    private readonly Dictionary<string, (string Title, string Body)> byLanguage;
    private readonly string defaultLanguage;

    public LocalizedText(
        IReadOnlyDictionary<string, (string Title, string Body)> translations,
        string defaultLanguage = "ar")
    {
        byLanguage = translations.ToDictionary(
            kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        this.defaultLanguage = defaultLanguage.ToLowerInvariant();
    }

    public bool IsEmpty => byLanguage.Count == 0;

    /// <summary>The wording for one reader, or null when the message has none at all.</summary>
    public (string Title, string Body)? For(string? languageCode)
    {
        var code = (languageCode ?? string.Empty).ToLowerInvariant();

        if (byLanguage.TryGetValue(code, out var exact)) return exact;
        if (byLanguage.TryGetValue(defaultLanguage, out var fallback)) return fallback;

        // Ordered, so "whatever exists" is at least the same answer twice.
        return byLanguage.Count == 0
            ? null
            : byLanguage.OrderBy(kv => kv.Key, StringComparer.Ordinal).First().Value;
    }

    /// <summary>A single-language message, for system notifications with one wording.</summary>
    public static LocalizedText Single(string languageCode, string title, string body) =>
        new(new Dictionary<string, (string, string)> { [languageCode] = (title, body) }, languageCode);
}
