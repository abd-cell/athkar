using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Recitations;

/// <summary>
/// One complete recording by one reciter — what the publisher calls a
/// «مصحف»: «حفص عن عاصم - مرتل», «المصحف المجود», «ورش عن نافع».
///
/// A reciter often has several, and they are not interchangeable: a different
/// riwaya is a different reading of the text, and the reader must choose it
/// knowingly. That is why the riwaya is part of <see cref="RecitationTranslation.Name"/>
/// and the app shows it on the reciter's page as a selector rather than
/// silently picking one.
/// </summary>
public class Recitation : AuditableEntity
{
    public int ReciterId { get; set; }
    public Reciter? Reciter { get; set; }

    /// <summary>The publisher's id for this recording («moshaf id»). How a re-sync finds it.</summary>
    public int? ExternalId { get; set; }

    /// <summary>
    /// The folder the surah files sit in, ending in a slash. Surah <c>n</c> is
    /// <c>ServerUrl + n:000 + ".mp3"</c>. https only, for the reason the radio
    /// gives: a cleartext stream is refused by the phone, where nobody in the
    /// console would see it fail.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// The surahs this recording actually has, comma-separated in order
    /// ("1,2,3,…"). Not every recording is complete, and a surah list that
    /// offered a file the server does not have would be a row that plays
    /// nothing.
    /// </summary>
    public string SurahList { get; set; } = string.Empty;

    /// <summary>
    /// The publisher's id for this recording in its ayah-timing service, when
    /// it has one. Null means there is no timing, and the app says so
    /// («لا يوجد توقيت لهذه التلاوة») rather than offering a verse tracker that
    /// cannot track. Timing belongs to a <i>recording</i>, not to a reciter:
    /// one of a reciter's recordings can have it and another not.
    /// </summary>
    public int? TimingReadId { get; set; }

    /// <summary>
    /// Who publishes the audio — shown beside the player. Credit is the
    /// condition the publisher offers its recordings on, and it is also what
    /// tells the reader whose servers they are listening from.
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Per recording, not only per reciter: an editor can offer a reciter's
    /// murattal and withhold a recording they have not listened to.
    /// </summary>
    public bool IsPublished { get; set; }

    public ICollection<RecitationTranslation> Translations { get; set; } = [];
}

public class RecitationTranslation : TranslationEntity
{
    public int RecitationId { get; set; }
    public Recitation? Recitation { get; set; }

    /// <summary>«حفص عن عاصم - مرتل». Carries the riwaya — see the class note.</summary>
    public string Name { get; set; } = string.Empty;
}
