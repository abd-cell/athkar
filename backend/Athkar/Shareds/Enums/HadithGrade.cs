namespace Athkar.Shareds.Enums;

/// <summary>
/// The verdict on a dhikr's chain of narration.
///
/// The set is closed and deliberately short: this project publishes nothing
/// below <see cref="Hasan"/>, so there is no member for weak or fabricated —
/// content like that is not stored and then filtered, it is never entered.
/// <see cref="QuranVerse"/> is here because a Qur'anic dhikr has a reference
/// (surah and ayah) but no chain to grade, and leaving the field null would make
/// "unverified" and "revelation" the same value.
/// </summary>
public enum HadithGrade
{
    Sahih = 1,
    Hasan = 2,

    /// <summary>Authentic by corroboration — sound only once its supporting chains are counted.</summary>
    SahihLighayrihi = 3,

    /// <summary>Good by corroboration.</summary>
    HasanLighayrihi = 4,

    /// <summary>Qur'an. Cited by surah and ayah; there is no chain to grade.</summary>
    QuranVerse = 5,

    /// <summary>
    /// Agreed upon — al-Bukhari and Muslim both narrate it. A stronger claim
    /// than <see cref="Sahih"/> and worth its own word to a reader who knows the
    /// vocabulary.
    /// </summary>
    MuttafaqAlayh = 6,
}
