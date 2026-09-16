using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// The أبواب and adhkar of حصن المسلم, as <c>tools/adhkar/pull.py</c> pulled them.
///
/// What the source gives is a book's structure and its Arabic. What it does not
/// give is a book, a hadith number or a grading — those fields are absent from
/// the API altogether. That absence is the single most important fact about this
/// file, and it decides everything the sync does with it: a dhikr from here is
/// written as an <b>unpublished draft</b>, because
/// <see cref="Athkar.Shareds.Models.ErrorCode.SourceRequired"/> exists precisely
/// to keep unattributed text off a reader's screen, and importing 267 of them
/// would be the largest possible way to go around it.
///
/// The أبواب themselves are published. A chapter is structure, not narration —
/// nothing is being asserted about the Prophet ﷺ by calling a chapter «أذكار
/// النوم».
/// </summary>
public static class AdhkarCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<Corpus> Loaded = new(Load);

    /// <summary>Empty when the data file is absent — every caller treats that as "nothing to do".</summary>
    public static IReadOnlyList<ImportedChapter> Chapters => Loaded.Value.Chapters;

    /// <summary>Where the text came from, carried into the audit trail and the admin's report.</summary>
    public static string Source => Loaded.Value.Source;

    private static Corpus Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DataAccess", "Seeders", "Data", "adhkar.json");
        if (!File.Exists(path)) return new Corpus();

        try
        {
            return JsonSerializer.Deserialize<Corpus>(File.ReadAllText(path), JsonOptions) ?? new Corpus();
        }
        catch (JsonException)
        {
            // Callers log. A malformed file must not take the process down at startup.
            return new Corpus();
        }
    }

    public sealed class Corpus
    {
        [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
        [JsonPropertyName("chapters")] public List<ImportedChapter> Chapters { get; set; } = [];
    }

    public sealed class ImportedChapter
    {
        /// <summary>
        /// The slug the chapter is filed under: an existing one for the three
        /// أبواب that mean exactly what a chapter here already means, and
        /// <c>hisn-&lt;id&gt;</c> for the rest. Stable, and the thing the sync
        /// matches on — a chapter an editor re-keys leaves the sync's reach.
        /// </summary>
        [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;

        /// <summary>The book's own chapter number, kept so a report can name it.</summary>
        [JsonPropertyName("source_id")] public int SourceId { get; set; }

        [JsonPropertyName("title_ar")] public string TitleArabic { get; set; } = string.Empty;

        /// <summary>Position in the book, which is the order these are read in.</summary>
        [JsonPropertyName("sort_order")] public int SortOrder { get; set; }

        [JsonPropertyName("adhkar")] public List<ImportedDhikr> Adhkar { get; set; } = [];
    }

    public sealed class ImportedDhikr
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

        /// <summary>How many times it is said. One for most.</summary>
        [JsonPropertyName("repeat")] public int Repeat { get; set; } = 1;
    }
}
