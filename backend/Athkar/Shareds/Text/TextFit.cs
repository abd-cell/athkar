using System.Text;

namespace Athkar.Shareds.Text;

/// <summary>
/// Trimming text to the column it is about to be written into.
///
/// Cross-cutting rather than owned by a feature, because the reason it exists is
/// the schema: <c>Dhikr.ArabicText</c> takes 4000 characters and
/// <c>Dhikr.SearchText</c> can only *index* 850 (see
/// <see cref="Athkar.Shareds.Constants.ContentRules.MaxIndexedSearchLength"/>).
/// Anything importing text it did not author — a whole surah, a chapter of حصن
/// المسلم — has to respect both, and an unfitted insert does not fail politely:
/// SQL Server raises error 1946 from inside the save.
/// </summary>
public static class TextFit
{
    /// <summary>
    /// At most <paramref name="limit"/> characters, cut on a word boundary when
    /// there is one within reach. Text already short enough comes back
    /// untouched — a verse is not something to round.
    /// </summary>
    public static string Fit(string text, int limit)
    {
        if (text.Length <= limit) return text;

        var cut = text.LastIndexOf(' ', limit - 1);
        return text[..(cut > limit / 2 ? cut : limit)].TrimEnd();
    }
}
