namespace Athkar.Areas.Services.Content.Models;

/// <summary>
/// Which of the checked rows to actually write.
///
/// The check proposes and this chooses: an editor reads the footnote beside each
/// dhikr and picks the ones they accept. An empty list means the whole set — the
/// only caller that passes it is a deliberate "fill everything", never a click
/// that happened to land on nothing.
/// </summary>
public class TakhrijSyncInput
{
    /// <summary>
    /// Null or empty: every row the footnotes account for. Otherwise exactly
    /// these, and an id that is not among them is left as a draft.
    /// </summary>
    public List<int>? DhikrIds { get; set; }
}

/// <summary>
/// What filling the drafts' attribution from حصن المسلم's footnotes did, or
/// would do. The same shape answers both the check and the write, so an admin
/// reads the identical report before and after.
/// </summary>
public class TakhrijSyncOutput
{
    /// <summary>False for the check. Nothing was written.</summary>
    public bool Applied { get; set; }

    /// <summary>The edition the footnotes were read from.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Rows that had no book or no reference when this started.</summary>
    public int DraftsMissingSource { get; set; }

    /// <summary>Of those, the ones the book's footnotes account for.</summary>
    public int Matched { get; set; }

    /// <summary>
    /// Matched rows whose footnote names a book *and* a place in it — the only
    /// ones an editor can then publish, because that is what
    /// <c>SourceRequired</c> asks for.
    /// </summary>
    public int Publishable { get; set; }

    /// <summary>Matched rows whose footnote also states a grading.</summary>
    public int Graded { get; set; }

    /// <summary>
    /// How many rows were actually written. Equal to <see cref="Matched"/> only
    /// when the editor took everything the check offered.
    /// </summary>
    public int Filled { get; set; }

    /// <summary>
    /// Rows the footnotes do not account for. They stay drafts and an editor
    /// attributes them by hand — mostly the seven أبواب whose footnotes do not
    /// line up one-to-one, أذكار الصباح والمساء among them.
    /// </summary>
    public int Unmatched { get; set; }

    /// <summary>
    /// Rows already carrying an editor's own attribution, left untouched. A
    /// person who typed a takhrij outranks a file.
    /// </summary>
    public int AlreadyAttributed { get; set; }

    /// <summary>
    /// Rows where the book's footnote disagrees with the attribution already on
    /// the row. Reported and not changed: two sources differing is a question
    /// for an editor, not something to resolve by overwriting.
    /// </summary>
    public int Disagreements { get; set; }

    /// <summary>
    /// Nothing here publishes anything, and this says so in the report rather
    /// than only in the documentation. Publishing stays one row at a time.
    /// </summary>
    public int Published => 0;

    public List<TakhrijSyncRow> Rows { get; set; } = [];
}

/// <summary>One row the sync would fill, or one it could not.</summary>
public class TakhrijSyncRow
{
    public int DhikrId { get; set; }

    /// <summary>The first few words, so a report reads as a list of adhkar.</summary>
    public string Excerpt { get; set; } = string.Empty;

    public string CategoryKey { get; set; } = string.Empty;

    public string? Book { get; set; }
    public string? Reference { get; set; }
    public int? Grade { get; set; }
    public string? GradedBy { get; set; }

    /// <summary>The footnote verbatim — what the attribution above was read from.</summary>
    public string? Note { get; set; }

    /// <summary>Whether this row was among the ones the editor chose to write.</summary>
    public bool Chosen { get; set; }

    public TakhrijSyncStatus Status { get; set; }
}

public enum TakhrijSyncStatus
{
    /// <summary>The footnotes account for it: a book, and a place in the book.</summary>
    Filled = 0,

    /// <summary>A book but no locator. Written, and still short of publishable.</summary>
    BookOnly = 1,

    /// <summary>The footnotes do not account for it. Left alone.</summary>
    Unmatched = 2,

    /// <summary>An editor had already attributed it. Left alone.</summary>
    AlreadyAttributed = 3,

    /// <summary>The footnote names a different book than the row does. Left alone.</summary>
    Disagreement = 4,
}
