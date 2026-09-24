using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Recitations.Models;

// ── What the reader's app receives, inside the catalogue ──

/// <summary>
/// A published reciter with at least one published recording. A reciter with
/// none would be a page with nothing to play, so the catalogue leaves him out.
/// </summary>
public class ReciterOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>Resolved into the requested language, falling back to Arabic and then to the key.</summary>
    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }

    public List<RecitationOutput> Recitations { get; set; } = [];
}

public class RecitationOutput
{
    public int Id { get; set; }

    /// <summary>«حفص عن عاصم - مرتل». Carries the riwaya.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The folder; surah <c>n</c> is <c>ServerUrl + n:000 + ".mp3"</c>.</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>The surahs this recording has, in order.</summary>
    public List<int> Surahs { get; set; } = [];

    /// <summary>
    /// Where the ayah timing for one surah is read from, with the surah number
    /// to be appended. Null when the recording has none — the app then says
    /// «لا يوجد توقيت لهذه التلاوة» and hides the verse tracker.
    /// </summary>
    public string? TimingUrl { get; set; }

    /// <summary>The publisher, credited beside the player.</summary>
    public string SourceName { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public int SortOrder { get; set; }
}

// ── The console ──

public class ReciterListInput : PageInput
{
    /// <summary>Null: all. Otherwise only published, or only drafts.</summary>
    public bool? IsPublished { get; set; }
}

/// <summary>
/// What an editor changes about a reciter. The key and the publisher's ids are
/// not here: they are how a re-sync finds the row, and an edit that changed
/// them would make the next sync write a duplicate.
/// </summary>
public class ReciterInput
{
    [StringLength(ContentRules.MaxUrlLength)]
    [Url]
    public string? ImageUrl { get; set; }

    public bool IsFeatured { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>At least the source language. <c>Title</c> is the name.</summary>
    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class RecitationInput
{
    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>At least the source language. <c>Title</c> is the name, riwaya included.</summary>
    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class AdminReciterOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int? ExternalId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }

    /// <summary>The Arabic name, for a list row. The full set is in <see cref="Translations"/>.</summary>
    public string Name { get; set; } = string.Empty;

    public List<string> TranslatedLanguages { get; set; } = [];
    public List<TranslationInput> Translations { get; set; } = [];
    public List<AdminRecitationOutput> Recitations { get; set; } = [];
}

public class AdminRecitationOutput
{
    public int Id { get; set; }
    public int? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public int SurahCount { get; set; }
    public bool HasTiming { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }

    public List<TranslationInput> Translations { get; set; } = [];
}

// ── The sync ──

/// <summary>
/// Which of the publisher's reciters to write. Same contract as the takhrij
/// sync: the check proposes, the editor chooses. Null or empty means every
/// reciter the check listed — safe only because nothing a sync writes is
/// published.
/// </summary>
public class RecitationSyncInput
{
    public List<int>? ExternalIds { get; set; }
}

/// <summary>What a sync found, or did. One shape for both, so the report reads the same before and after.</summary>
public class RecitationSyncOutput
{
    public bool Applied { get; set; }

    /// <summary>The publisher, as credited.</summary>
    public string Source { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public int PublisherReciters { get; set; }
    public int PublisherRecordings { get; set; }

    /// <summary>Recordings the publisher has ayah timing for.</summary>
    public int TimedRecordings { get; set; }

    public int New { get; set; }
    public int Changed { get; set; }
    public int Unchanged { get; set; }

    /// <summary>
    /// Recordings the console holds that the publisher no longer lists. Reported,
    /// never unpublished by the sync: a folder that vanished for an afternoon is
    /// not a reason to take a reciter off every phone.
    /// </summary>
    public int Gone { get; set; }

    /// <summary>
    /// Reciters an editor deleted that the publisher still lists. Skipped: a
    /// deletion is a decision, and a sync that restores what somebody removed is
    /// a bug that takes a week to notice.
    /// </summary>
    public int SkippedDeleted { get; set; }

    /// <summary>How many reciters were actually written.</summary>
    public int Written { get; set; }

    /// <summary>Nothing a sync writes is published, and the report says so.</summary>
    public int Published => 0;

    public List<RecitationSyncRow> Rows { get; set; } = [];
}

public class RecitationSyncRow
{
    public int ExternalId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }

    /// <summary>The console's row, when there is one.</summary>
    public int? ReciterId { get; set; }

    public bool IsPublished { get; set; }

    public int Recordings { get; set; }
    public int TimedRecordings { get; set; }

    /// <summary>
    /// For a changed row, what changed, as codes the console translates:
    /// <c>recording-new</c>, <c>recording-gone</c>, <c>server</c>, <c>surahs</c>,
    /// <c>timing-added</c>, <c>timing-removed</c>, <c>name-en</c>.
    /// </summary>
    public List<string> Changes { get; set; } = [];

    public bool Chosen { get; set; }

    public RecitationSyncStatus Status { get; set; }
}

public enum RecitationSyncStatus
{
    /// <summary>The console does not have him yet. Written as a draft.</summary>
    New = 0,

    /// <summary>The console has him, and the publisher's details moved.</summary>
    Changed = 1,

    /// <summary>Nothing to do.</summary>
    Unchanged = 2,

    /// <summary>The console has him; the publisher no longer lists him. Reported only.</summary>
    Gone = 3,
}
