namespace Athkar.Shareds.Enums;

/// <summary>
/// What a catalogue entry is *about*, used only to group the gallery.
///
/// The reader scrolling thirty widgets is looking for a kind of thing — "the
/// one with the calendar", "the one with a dhikr" — not for a key, so the
/// gallery is grouped by this rather than left in one flat list.
///
/// Deliberately not <see cref="WidgetKind"/>: that enum is the *contract with
/// the native layouts* and adding to it means adding a layout. This one is a
/// label, and a new family costs nothing.
/// </summary>
public enum WidgetFamily
{
    /// <summary>Prayer times, next/previous prayer, countdowns to them.</summary>
    Prayer = 1,

    /// <summary>Hijri and Gregorian dates, the day's name and number.</summary>
    Date = 2,

    /// <summary>Both together — the shape most readers place first.</summary>
    PrayerAndDate = 3,

    /// <summary>A dhikr, a du'a, the day's adhkar.</summary>
    Dhikr = 4,

    /// <summary>An ayah or a page of the mushaf.</summary>
    Quran = 5,

    /// <summary>Days remaining to Ramadan, the two Eids, or a chosen date.</summary>
    Countdown = 6,

    /// <summary>The moon's phase, and the night thirds that hang off it.</summary>
    Moon = 7,

    /// <summary>What the reader has prayed and read, rather than what is coming.</summary>
    Tracker = 8,
}
