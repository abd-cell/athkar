namespace Athkar.Shareds.Constants;

/// <summary>Timing and batching constants for the push pipeline.</summary>
public static class PushRules
{
    /// <summary>
    /// How far ahead the dispatcher materialises reminder sends. A window
    /// rather than a full calendar because a campaign can be edited or disabled
    /// at any moment, and rows already written for next month would then be
    /// wrong in a way nobody would notice.
    /// </summary>
    public const int DispatchHorizonMinutes = 30;

    /// <summary>
    /// How late a dispatch may be sent before it is dropped instead. A reminder
    /// for morning adhkar arriving at noon is worse than no reminder — it tells
    /// the reader the app is not to be relied on.
    /// </summary>
    public const int DispatchGraceMinutes = 15;

    /// <summary>Attempts a failed dispatch gets before it is left alone.</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// The Android channels the app creates at first launch. A channel's sound
    /// and importance are fixed once created, so these ids are effectively a
    /// contract with every installed copy: to change how a reminder sounds you
    /// add a channel, you never edit one.
    /// </summary>
    public static class Channels
    {
        public const string Reminders = "athkar.reminders.v1";
        public const string Prayer = "athkar.prayer.v1";
        public const string Announcements = "athkar.announcements.v1";

        /// <summary>Every channel the app creates — what an admin may choose from.</summary>
        public static readonly IReadOnlyList<string> All = [Reminders, Prayer, Announcements];
    }
}
