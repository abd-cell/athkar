using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Content.Models;

/// <summary>
/// One language's wording for something, as the CMS sends it.
///
/// Translations always arrive as a complete set and replace what was there: a
/// per-language PATCH would need its own endpoint, its own concurrency story and
/// its own answer to "what does an absent language mean?". Replacing the set
/// makes the answer obvious — absent means removed.
/// </summary>
public class TranslationInput
{
    [Required, StringLength(8)]
    public string LanguageCode { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Title { get; set; } = string.Empty;

    [StringLength(ContentRules.MaxTextLength)]
    public string? Body { get; set; }

    /// <summary>Transliteration for a dhikr, unused elsewhere.</summary>
    [StringLength(ContentRules.MaxTextLength)]
    public string? Secondary { get; set; }

    /// <summary>The narrated virtue for a dhikr, unused elsewhere.</summary>
    [StringLength(ContentRules.MaxTextLength)]
    public string? Virtue { get; set; }
}

public class CategoryInput
{
    [Required, StringLength(ContentRules.MaxCategoryKeyLength, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9][a-z0-9-]*$",
        ErrorMessage = "A key is lower-case letters, digits and hyphens.")]
    public string Key { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    [EnumDataType(typeof(CategoryRhythm))]
    public CategoryRhythm Rhythm { get; set; } = CategoryRhythm.None;

    /// <summary>
    /// The section of the reader's index. Left at <see cref="CategorySection.None"/>
    /// the chapter still appears — under its own heading — so an editor who
    /// forgets sees a chapter waiting to be filed, not one that vanished.
    /// </summary>
    [EnumDataType(typeof(CategorySection))]
    public CategorySection Section { get; set; } = CategorySection.None;

    [EnumDataType(typeof(PrayerAnchor))]
    public PrayerAnchor Anchor { get; set; } = PrayerAnchor.None;

    public bool IsPublished { get; set; }

    /// <summary>At least the source language must be present, or there is nothing to show anyone.</summary>
    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class DhikrInput
{
    [Required]
    public int CategoryId { get; set; }

    public int SortOrder { get; set; }

    [Required, StringLength(ContentRules.MaxTextLength, MinimumLength = 1)]
    public string ArabicText { get; set; } = string.Empty;

    [Range(1, ContentRules.MaxRepeatCount)]
    public int RepeatCount { get; set; } = 1;

    [StringLength(ContentRules.MaxReferenceLength)]
    public string? SourceBook { get; set; }

    [StringLength(ContentRules.MaxReferenceLength)]
    public string? SourceReference { get; set; }

    public HadithGrade? Grade { get; set; }

    [StringLength(ContentRules.MaxReferenceLength)]
    public string? GradedBy { get; set; }

    public bool IsPublished { get; set; }

    public List<TranslationInput> Translations { get; set; } = [];
}

/// <summary>The admin view of a chapter: counts rather than the adhkar themselves.</summary>
public class AdminCategoryOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public CategoryRhythm Rhythm { get; set; }
    public PrayerAnchor Anchor { get; set; }
    public CategorySection Section { get; set; }
    public bool IsPublished { get; set; }

    public int DhikrCount { get; set; }
    public int PublishedDhikrCount { get; set; }

    /// <summary>
    /// Which languages this chapter has been translated into. Shown as chips in
    /// the grid, so a gap in the Turkish column is visible without opening rows.
    /// </summary>
    public List<string> TranslatedLanguages { get; set; } = [];

    public List<TranslationInput> Translations { get; set; } = [];
}

public class AdminDhikrOutput
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string ArabicText { get; set; } = string.Empty;
    public int RepeatCount { get; set; }
    public string? SourceBook { get; set; }
    public string? SourceReference { get; set; }
    public HadithGrade? Grade { get; set; }
    public string? GradedBy { get; set; }
    public bool IsPublished { get; set; }

    /// <summary>
    /// False when the row has no source, which is the one thing that blocks
    /// publishing. Sent so the grid can mark the row before an editor tries.
    /// </summary>
    public bool IsPublishable { get; set; }

    public List<string> TranslatedLanguages { get; set; } = [];
    public List<TranslationInput> Translations { get; set; } = [];
}

/// <summary>A new order for a set of rows, as pairs of id and position.</summary>
public class ReorderInput
{
    [Required, MinLength(1)]
    public List<ReorderEntry> Items { get; set; } = [];
}

public class ReorderEntry
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
}
