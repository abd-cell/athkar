using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Recitations;

/// <summary>
/// A reciter — «ماهر المعيقلي» — and the recordings of his that the reader can
/// listen to.
///
/// The server keeps the <b>catalogue</b> and nothing else. No audio is stored or
/// relayed here: a recitation is a folder of MP3s on its publisher's own
/// servers (<see cref="Recitation.ServerUrl"/>), and the app streams or
/// downloads from there. Hosting a few hundred gigabytes of recitation to save
/// a reader one DNS lookup would make this endowment an audio CDN, which is a
/// cost it cannot carry and a copy of someone else's work it has no licence to
/// make.
///
/// Rides the catalogue like the radio stations do: reciters are published
/// content, and <c>AppConfiguration.ContentVersion</c> is the whole sync
/// protocol.
/// </summary>
public class Reciter : AuditableEntity
{
    /// <summary>
    /// Stable slug ("mp3quran-102"). Unique and never translated — what the
    /// app keys a bookmark or a download on, so correcting a name loses nobody
    /// their place.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The publisher's own id for this reciter, when the row came from a sync.
    /// Null for a reciter an editor entered by hand. This, not the name, is how
    /// a second sync finds the row the first one wrote.
    /// </summary>
    public int? ExternalId { get; set; }

    /// <summary>
    /// A portrait, as a URL. Optional and never synced: the publisher does not
    /// supply one, a photograph carries rights of its own, and some readers
    /// would rather not see one at all. An editor who has a picture they are
    /// allowed to use sets it; everyone else gets the reciter's initial.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>Shown in the «قراء مختارون» row at the top of the listening tab.</summary>
    public bool IsFeatured { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// An unpublished reciter is invisible to the app. A sync always writes new
    /// reciters unpublished — which of two hundred reciters a waqf app offers
    /// is an editorial choice, not the publisher's.
    /// </summary>
    public bool IsPublished { get; set; }

    public ICollection<ReciterTranslation> Translations { get; set; } = [];

    public ICollection<Recitation> Recitations { get; set; } = [];
}

public class ReciterTranslation : TranslationEntity
{
    public int ReciterId { get; set; }
    public Reciter? Reciter { get; set; }

    public string Name { get; set; } = string.Empty;
}
