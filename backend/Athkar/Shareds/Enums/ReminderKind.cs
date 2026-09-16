namespace Athkar.Shareds.Enums;

/// <summary>What decides when a reminder campaign fires.</summary>
public enum ReminderKind
{
    /// <summary>A wall-clock time in the device's own timezone ("21:30").</summary>
    FixedTime = 1,

    /// <summary>An offset from a <see cref="PrayerAnchor"/> ("30 minutes after sunrise").</summary>
    PrayerAnchored = 2,
}
