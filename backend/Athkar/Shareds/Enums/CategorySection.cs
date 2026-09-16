namespace Athkar.Shareds.Enums;

/// <summary>
/// Which of the three sections of the أذكار tab a chapter belongs to.
///
/// The app grouped its index by a list of category keys held in the Flutter
/// source, and said so in its own comment: that is a content decision living in
/// a constant, which this project keeps in the CMS. This is the field that
/// comment was waiting for — an editor files a chapter, and the app draws
/// whatever it is told.
///
/// The numbers are the contract across all three stacks. <see cref="None"/> is
/// deliberately the default: a chapter nobody has filed yet is *unfiled*, and
/// the app shows it in a closing group rather than hiding it. A category can be
/// published into a section that does not exist yet; it can never be published
/// into invisibility.
/// </summary>
public enum CategorySection
{
    /// <summary>Not filed yet. Shown, under its own heading, so it is noticed.</summary>
    None = 0,

    /// <summary>أذكار — what is said at a time: morning, evening, sleep, after prayer.</summary>
    Adhkar = 1,

    /// <summary>أدعية — what is asked for at an occasion: travel, distress, rain.</summary>
    Duas = 2,

    /// <summary>الفضائل — what is said of the merit of dhikr and of prayer upon the Prophet ﷺ.</summary>
    Virtues = 3,
}
