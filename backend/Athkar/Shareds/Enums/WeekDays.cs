namespace Athkar.Shareds.Enums;

/// <summary>
/// Which days a reminder repeats on, as a bit set.
///
/// A mask rather than a table of rows: a recurrence is one small fact about one
/// campaign, it is always read whole, and "every day" should be a single value
/// and not seven joins.
/// </summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,

    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday,

    /// <summary>The two days whose adhkar and virtues are their own ("Monday and Thursday").</summary>
    MondayAndThursday = Monday | Thursday,
}
