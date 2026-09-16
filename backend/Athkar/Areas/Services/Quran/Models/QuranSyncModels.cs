namespace Athkar.Areas.Services.Quran.Models;

/// <summary>What a sync did, or — in preview — what it would do.</summary>
public class QuranSyncOutput
{
    /// <summary>False when nothing was written, whether because it was a preview or because nothing differed.</summary>
    public bool Applied { get; set; }

    /// <summary>The host the text was read from, so the report says where it came from.</summary>
    public string Source { get; set; } = string.Empty;

    public int Checked { get; set; }
    public int Added { get; set; }
    public int Changed { get; set; }

    /// <summary>
    /// Rows the sync declined to touch — an editor has changed the reference, so
    /// it is no longer the row the source was asked about. Reported rather than
    /// corrected: this system does not overrule a person about attribution.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>The new content version, when one was issued. Null on a preview or a no-op.</summary>
    public int? ContentVersion { get; set; }

    public List<QuranSyncChange> Changes { get; set; } = [];
}

/// <summary>One row's worth of difference, at the granularity an editor reviews.</summary>
public class QuranSyncChange
{
    /// <summary>Catalogue key: "ayat-al-kursi", "mulk".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>«البقرة: ٢٥٥» — the same locator the row carries.</summary>
    public string Reference { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Null for a row the sync would create.</summary>
    public int? DhikrId { get; set; }

    public QuranSyncAction Action { get; set; }

    /// <summary>Which fields differ: "arabic", "search", "en". Empty for an addition.</summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>
    /// The stored Arabic and the canonical Arabic, so a reviewer can see the
    /// difference rather than take the word "changed" on trust. Both null unless
    /// the Arabic itself differs — the diff is for reading, not for bulk.
    /// </summary>
    public string? StoredArabic { get; set; }
    public string? CanonicalArabic { get; set; }
}

public enum QuranSyncAction
{
    /// <summary>Stored text already matches the source.</summary>
    Unchanged = 0,

    /// <summary>The row does not exist and would be created.</summary>
    Added = 1,

    /// <summary>The row exists and its text differs from the source.</summary>
    Changed = 2,

    /// <summary>Left alone — an editor has moved it out of the sync's reach.</summary>
    Skipped = 3,
}
