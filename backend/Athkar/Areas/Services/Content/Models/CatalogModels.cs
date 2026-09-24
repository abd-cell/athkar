using Athkar.Areas.Services.Radio.Models;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Content.Models;

/// <summary>
/// The whole published catalogue in one payload, already resolved into one
/// language.
///
/// One call and not a tree of them, because the app's contract with its reader
/// is that everything works offline: there is no lazy loading to fall back on,
/// so the sync either brings the corpus down or it has not happened. It is a few
/// hundred kilobytes of text, fetched when <see cref="Version"/> moves and not
/// otherwise.
/// </summary>
public class CatalogOutput
{
    /// <summary>The content version this snapshot is of. Store it; send it back next time.</summary>
    public int Version { get; set; }

    /// <summary>The language the text below was resolved in, after fallback.</summary>
    public string LanguageCode { get; set; } = "ar";

    /// <summary>
    /// True when the caller's version already matched, in which case
    /// <see cref="Categories"/> is empty and there is nothing to apply. The
    /// ordinary answer to a launch-time sync.
    /// </summary>
    public bool IsUpToDate { get; set; }

    public List<CategoryOutput> Categories { get; set; } = [];

    /// <summary>
    /// The live stations, ordered as the console ordered them. They ride the
    /// catalogue rather than an endpoint of their own: a station is published
    /// content, it changes when content changes, and this payload is already
    /// the thing the app caches and reads offline. The reader sees the list
    /// with no network; only pressing play needs one.
    /// </summary>
    public List<RadioStationOutput> Radios { get; set; } = [];

    /// <summary>
    /// The published reciters, each with his published recordings. Only the
    /// list rides here — the audio and its ayah timings are fetched by the
    /// phone from the publisher named on each recording, so the reader can
    /// browse reciters offline and needs a connection only to press play or to
    /// download.
    /// </summary>
    public List<ReciterOutput> Reciters { get; set; } = [];
}

public class CategoryOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public CategoryRhythm Rhythm { get; set; }
    public PrayerAnchor Anchor { get; set; }

    /// <summary>Which section of the index the app draws this chapter under.</summary>
    public CategorySection Section { get; set; }

    /// <summary>Populated in the catalogue; empty in the admin list, which pages its adhkar separately.</summary>
    public List<DhikrOutput> Adhkar { get; set; } = [];
}

public class DhikrOutput
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int SortOrder { get; set; }

    /// <summary>The vocalised Arabic. Always present, in every language's payload.</summary>
    public string ArabicText { get; set; } = string.Empty;

    public int RepeatCount { get; set; }

    /// <summary>
    /// The meaning in the requested language. Null when the reader asked for
    /// Arabic — the Arabic is not a translation of itself — and also null when
    /// nobody has translated this row yet.
    /// </summary>
    public string? Translation { get; set; }

    public string? Transliteration { get; set; }
    public string? Virtue { get; set; }

    // ── Attribution, the part this project will not ship without ──
    public string? SourceBook { get; set; }
    public string? SourceReference { get; set; }
    public HadithGrade? Grade { get; set; }
    public string? GradedBy { get; set; }
}

/// <summary>A search hit, carrying enough context to render a result row.</summary>
public class SearchHitOutput : DhikrOutput
{
    public string CategoryKey { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
}
