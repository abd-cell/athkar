namespace Athkar.Areas.Services.Content.Models;

/// <summary>What an import did, or — in preview — what it would do.</summary>
public class AdhkarImportOutput
{
    public bool Applied { get; set; }

    /// <summary>Where the text came from, so the report says so rather than implying this server authored it.</summary>
    public string Source { get; set; } = string.Empty;

    public int ChaptersChecked { get; set; }
    public int ChaptersAdded { get; set; }
    public int AdhkarAdded { get; set; }

    /// <summary>
    /// Adhkar already present — matched on folded Arabic, so a difference in
    /// diacritics or brackets does not import a second copy of the same words.
    /// </summary>
    public int AdhkarAlreadyPresent { get; set; }

    /// <summary>
    /// Chapters this import deliberately left alone because an editor owns them:
    /// the key exists and the chapter already holds adhkar.
    /// </summary>
    public int ChaptersSkipped { get; set; }

    /// <summary>The new content version, when one was issued. Null on a preview or a no-op.</summary>
    public int? ContentVersion { get; set; }

    /// <summary>
    /// Every dhikr from this source arrives unpublished, because the source
    /// carries no takhrij. The count is surfaced so an admin sees the size of
    /// the review queue they have just created rather than discovering it.
    /// </summary>
    public int DraftsAwaitingSource { get; set; }

    public List<AdhkarImportChapter> Chapters { get; set; } = [];
}

/// <summary>One باب's worth of difference.</summary>
public class AdhkarImportChapter
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>The book's own chapter number.</summary>
    public int SourceId { get; set; }

    /// <summary>Null for a chapter that would be created.</summary>
    public int? CategoryId { get; set; }

    public AdhkarImportAction Action { get; set; }

    /// <summary>How many adhkar this باب would add.</summary>
    public int Adding { get; set; }

    /// <summary>How many of its adhkar are already in the database.</summary>
    public int Present { get; set; }
}

public enum AdhkarImportAction
{
    /// <summary>The chapter and all its adhkar are already there.</summary>
    Unchanged = 0,

    /// <summary>The chapter does not exist and would be created, with its adhkar.</summary>
    Added = 1,

    /// <summary>The chapter exists and would gain adhkar it does not have.</summary>
    Extended = 2,

    /// <summary>Left alone — an editor has taken this chapter over.</summary>
    Skipped = 3,
}
