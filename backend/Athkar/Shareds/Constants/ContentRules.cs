namespace Athkar.Shareds.Constants;

/// <summary>
/// The numbers the content model refuses to be talked out of. Gathered here
/// rather than scattered as literals so a reviewer can see the whole shape of
/// "what a dhikr may be" on one screen.
/// </summary>
public static class ContentRules
{
    /// <summary>
    /// The language the content is authored in. Everything else is a translation
    /// of it, and it cannot be disabled or deleted.
    /// </summary>
    public const string SourceLanguage = "ar";

    /// <summary>Largest repeat count a dhikr may ask for. A hundred is the largest in the sources.</summary>
    public const int MaxRepeatCount = 1000;

    public const int MaxCategoryKeyLength = 64;
    public const int MaxTextLength = 4000;

    /// <summary>
    /// How much of <c>Dhikr.SearchText</c> can actually be indexed.
    ///
    /// SQL Server caps a nonclustered index key at 1700 bytes, and it does not
    /// say so when the index is created — it says so on the INSERT, as error
    /// 1946, which is how a long dhikr turns into a failed save rather than a
    /// design review. At two bytes per character that is 850, so anything folded
    /// past this point is trimmed: the column exists to be searched, not to be
    /// read, and a row still matches on everything up to here.
    /// </summary>
    public const int MaxIndexedSearchLength = 850;
    public const int MaxReferenceLength = 200;

    /// <summary>
    /// How many open submissions one device may have with the support desk at a
    /// time. Anonymous devices cannot be blocked by account, so the limit is the
    /// only brake there is.
    /// </summary>
    public const int MaxOpenFeedbackPerDevice = 5;
}
