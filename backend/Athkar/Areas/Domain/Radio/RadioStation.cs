using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Radio;

/// <summary>
/// A live audio station the reader can listen to — «إذاعة القرآن الكريم» and its
/// like.
///
/// This is the one piece of reader-facing content the app does not hold a copy
/// of: a stream is not a corpus, it is a URL that plays now or does not play at
/// all. Everything else about the row exists so that a URL going dead is an
/// edit in the console rather than an app release — which is why the station
/// lives here and not in a constant in the app.
///
/// It rides the catalogue rather than an endpoint of its own: stations are
/// published content, they change when content changes, and
/// <c>AppConfiguration.ContentVersion</c> is already the whole sync protocol.
/// </summary>
public class RadioStation : AuditableEntity
{
    /// <summary>
    /// Stable slug ("quran-radio"). Unique, never translated, and what the app
    /// keys a "recently played" entry on — a station's name can be corrected
    /// without the reader losing their place.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The stream itself. Plain HTTP is refused on the way in: iOS blocks a
    /// cleartext stream under ATS and Android blocks it by default, so an
    /// http:// station is not a station that plays — it is a support ticket.
    /// </summary>
    public string StreamUrl { get; set; } = string.Empty;

    /// <summary>
    /// The station's own logo, as a URL. Optional — a station without one gets
    /// the app's radio glyph, which is a great deal better than a broken image
    /// on the home screen.
    /// </summary>
    public string? LogoUrl { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// An unpublished station is invisible to the app. The switch is how a dead
    /// stream is taken off every phone at the next sync.
    /// </summary>
    public bool IsPublished { get; set; }

    public ICollection<RadioStationTranslation> Translations { get; set; } = [];
}
