namespace Athkar.Shareds.Enums;

/// <summary>What a message from a reader is about.</summary>
public enum FeedbackKind
{
    Suggestion = 1,
    Complaint = 2,

    /// <summary>
    /// A correction to the text, a reference or a grading. Its own kind because
    /// it is the one that must reach the reviewing scholar rather than support,
    /// and the one the CMS sorts to the top.
    /// </summary>
    Correction = 3,

    Praise = 4,
}
