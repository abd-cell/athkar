using System.Text.Json;
using System.Text.Json.Serialization;

namespace Athkar.Areas.Services.Content;

/// <summary>
/// حصن المسلم's own takhrij, as <c>tools/adhkar/pull_takhrij.py</c> read it out
/// of the book's footnotes.
///
/// The text the import brings in comes from an API that carries no attribution
/// at all, which is why every imported dhikr is a draft. This is the other half:
/// the same book's footnotes, which do name a source. Two properties of this
/// file decide how the sync may use it:
///
/// - **It is keyed by folded Arabic**, the same folding
///   <see cref="Athkar.Shareds.Text.ArabicText.Normalize"/> writes into
///   <c>Dhikr.SearchText</c>, so a row is matched by what it says rather than by
///   an id that two digitisations of one book would never agree on.
/// - **It is deliberately incomplete.** Seven أبواب carry more footnotes than
///   adhkar — a dhikr there has two notes — and once the counts diverge nothing
///   says which note belongs to which dhikr. The tool drops those chapters
///   instead of guessing, so a row the sync cannot attribute stays a draft and
///   an editor does it by hand. That is the intended outcome, not a shortfall.
/// </summary>
public static class TakhrijCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<Catalog> Loaded = new(Load);

    /// <summary>Empty when the data file is absent — callers treat that as "nothing to do".</summary>
    public static IReadOnlyList<TakhrijEntry> Entries => Loaded.Value.Entries;

    /// <summary>Which edition the footnotes came from, carried into the audit trail.</summary>
    public static string Source => Loaded.Value.Source;

    private static Catalog Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DataAccess", "Seeders", "Data", "takhrij.json");
        if (!File.Exists(path)) return new Catalog();

        try
        {
            return JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), JsonOptions) ?? new Catalog();
        }
        catch (JsonException)
        {
            // Callers log. A malformed file must not take the process down at startup.
            return new Catalog();
        }
    }

    public sealed class Catalog
    {
        [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("entries")] public List<TakhrijEntry> Entries { get; set; } = [];
    }

    public sealed class TakhrijEntry
    {
        /// <summary>The dhikr's folded Arabic — what this is matched on.</summary>
        [JsonPropertyName("fold")] public string Fold { get; set; } = string.Empty;

        /// <summary>The باب it sits in, so a report can name where an entry came from.</summary>
        [JsonPropertyName("chapter")] public string Chapter { get; set; } = string.Empty;

        [JsonPropertyName("book")] public string Book { get; set; } = string.Empty;

        /// <summary>
        /// The locator exactly as the book writes it — usually volume/page,
        /// sometimes a hadith number. Null when the footnote names a book and no
        /// place in it, which is not enough to publish on.
        /// </summary>
        [JsonPropertyName("reference")] public string? Reference { get; set; }

        /// <summary>
        /// Only what the footnote states outright: «متفق عليه», a named grader,
        /// or a Qur'anic citation. A note that does not grade leaves this null
        /// rather than have the sync issue a verdict of its own.
        /// </summary>
        [JsonPropertyName("grade")] public int? Grade { get; set; }

        [JsonPropertyName("gradedBy")] public string? GradedBy { get; set; }

        /// <summary>The footnote verbatim, for the audit trail and the report.</summary>
        [JsonPropertyName("note")] public string Note { get; set; } = string.Empty;
    }
}
