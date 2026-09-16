namespace Athkar.Shareds.Enums;

/// <summary>
/// A moment in the day a reminder can hang off, rather than a clock time.
///
/// Anchored reminders are scheduled <b>on the device</b> — the server never
/// computes a prayer time, because it would need the user's coordinates to do it
/// and the app promises to work without ever asking for them. See
/// <c>docs/BUSINESS_LOGIC.md</c> §5.
/// </summary>
public enum PrayerAnchor
{
    None = 0,
    Fajr = 1,
    Sunrise = 2,
    Dhuhr = 3,
    Asr = 4,
    Maghrib = 5,
    Isha = 6,

    /// <summary>The user's stated bedtime — not a prayer, but the anchor sleep adhkar belong to.</summary>
    Bedtime = 7,

    /// <summary>Islamic midnight: the midpoint between Maghrib and the following Fajr.</summary>
    IslamicMidnight = 8,

    /// <summary>The start of the last third of the night.</summary>
    LastThirdOfNight = 9,
}
