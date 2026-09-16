using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Content;

/// <summary>
/// A chapter of adhkar — morning, evening, sleep, travel, distress.
///
/// The category, not the individual dhikr, is what a reader opens and what a
/// session counts through, so the rhythm and the time of day live here.
/// </summary>
public class AthkarCategory : AuditableEntity
{
    /// <summary>
    /// Stable slug ("morning", "evening", "sleep"). Unique, never translated,
    /// and the thing deep links and reminder campaigns refer to — a category's
    /// display name can be corrected without breaking either.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Icon name from the app's own set, not a URL. Keeping it symbolic means
    /// the icon renders in the right weight for the theme and needs no network.
    /// </summary>
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public CategoryRhythm Rhythm { get; set; } = CategoryRhythm.None;

    /// <summary>
    /// Which section of the reader's index this chapter sits in. Set by an
    /// editor rather than derived: nothing in a chapter's data distinguishes
    /// «أذكار السفر» from «دعاء السفر», and the app used to guess from a list of
    /// keys compiled into it.
    /// </summary>
    public CategorySection Section { get; set; } = CategorySection.None;

    /// <summary>
    /// When in the day this chapter belongs, or <see cref="PrayerAnchor.None"/>
    /// for one that belongs to no particular moment. Used by the home screen to
    /// decide which chapter to put in front of the reader right now, without
    /// anybody having to configure it.
    /// </summary>
    public PrayerAnchor Anchor { get; set; } = PrayerAnchor.None;

    /// <summary>
    /// An unpublished category is invisible to the app and exists so a chapter
    /// can be assembled and reviewed over several sittings.
    /// </summary>
    public bool IsPublished { get; set; }

    public ICollection<CategoryTranslation> Translations { get; set; } = [];
    public ICollection<Dhikr> Adhkar { get; set; } = [];
}
