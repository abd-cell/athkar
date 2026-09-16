namespace Athkar.Shareds.Enums;

/// <summary>
/// How a category's session counter behaves over time.
///
/// This is a property of the *content*, not of a preference: morning adhkar are
/// a thing you finish once a day and start again tomorrow, whereas istighfar has
/// no such boundary. The app resets a <see cref="Daily"/> category at the user's
/// local midnight without asking the server.
/// </summary>
public enum CategoryRhythm
{
    /// <summary>Progress is kept until the user clears it.</summary>
    None = 0,

    /// <summary>Progress resets at the device's local midnight.</summary>
    Daily = 1,

    /// <summary>Progress resets at the start of each Hijri month (Ramadan plans and the like).</summary>
    Monthly = 2,
}
