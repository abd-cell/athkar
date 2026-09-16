namespace Athkar.Shareds.Enums;

/// <summary>The sections the help screen groups questions under.</summary>
public enum FaqCategory
{
    General = 0,
    Adhkar = 1,
    PrayerTimes = 2,

    /// <summary>
    /// "Why don't my reminders arrive?" — on Android this is nearly always the
    /// manufacturer's battery manager, and it is the single most asked question
    /// an app like this gets.
    /// </summary>
    Notifications = 3,

    Qibla = 4,
    Quran = 5,
    Privacy = 6,
}
