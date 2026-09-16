using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Content;

/// <summary>
/// One remembrance, with the attribution that makes it publishable.
///
/// The Arabic is not a translation and does not live in the translations table:
/// it is the text itself, the thing being narrated, and every other language
/// hangs off it. A row cannot be published without a source — see
/// <see cref="Athkar.Shareds.Models.ErrorCode.SourceRequired"/> — because the
/// one promise this project makes that its competitors do not is that every
/// dhikr on screen can be traced to a book, a number and a grading.
/// </summary>
public class Dhikr : AuditableEntity
{
    public int CategoryId { get; set; }
    public AthkarCategory? Category { get; set; }

    public int SortOrder { get; set; }

    /// <summary>The vocalised Arabic, as it is to be read.</summary>
    public string ArabicText { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="ArabicText"/> folded by <c>ArabicText.Normalize</c> — no
    /// diacritics, one shape per letter family.
    ///
    /// A stored column and not a computed one: search has to be able to use an
    /// index, and a function applied to every row cannot. Written by the service
    /// on every save, never by hand.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>How many times it is said. One for most; 3, 7, 10, 33 or 100 for the rest.</summary>
    public int RepeatCount { get; set; } = 1;

    // ── Attribution ──

    /// <summary>The book: "صحيح البخاري", "سنن أبي داود".</summary>
    public string? SourceBook { get; set; }

    /// <summary>
    /// The locator within it, as text rather than a number: sources cite
    /// variously by hadith number, by volume and page, and Qur'anic ones by
    /// surah and ayah. A single int would fit only the first.
    /// </summary>
    public string? SourceReference { get; set; }

    public HadithGrade? Grade { get; set; }

    /// <summary>
    /// Who issued that grading — "الألباني", "شعيب الأرناؤوط". Recorded because
    /// a grade without a grader is an assertion, and this project does not make
    /// assertions about chains of narration on its own authority.
    /// </summary>
    public string? GradedBy { get; set; }

    /// <summary>
    /// An unpublished dhikr is invisible to the app. Publishing is gated on
    /// having a source, so this flag is also the review checkpoint.
    /// </summary>
    public bool IsPublished { get; set; }

    public ICollection<DhikrTranslation> Translations { get; set; } = [];
}
