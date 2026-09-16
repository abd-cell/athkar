using System.Globalization;
using System.Text;

namespace Athkar.Shareds.Text;

/// <summary>
/// Folds Arabic to a form that search can match on.
///
/// Arabic is written with marks a reader supplies from memory and a typist
/// mostly omits: somebody looking for «أذكار الصباح» will type it without a
/// single fatha, with a bare alif for the hamza, and quite possibly with a final
/// ه where the text has ة. Comparing their query to fully vocalised text finds
/// nothing, so both sides are folded through here first and the folded form is
/// stored in its own column (<c>Dhikr.SearchText</c>) rather than computed per
/// query — an index cannot help a function applied to every row.
///
/// The app folds queries with the identical rules in
/// <c>app/athkar_app/lib/core/arabic_text.dart</c>. The two must stay in step:
/// a rule added on one side only makes the app search for something the server
/// never wrote down.
/// </summary>
public static class ArabicText
{
    /// <summary>
    /// The harakat and Qur'anic annotation marks, U+064B–U+065F plus the
    /// dagger alif, the sukun variants and the tatweel. Removed entirely: they
    /// carry pronunciation, never identity.
    /// </summary>
    private static bool IsDiacritic(char c) =>
        c is >= 'ً' and <= 'ٟ'   // fathatan … wavy hamza below
            or 'ـ'                     // tatweel (a stretching glyph, not a letter)
            or 'ٰ'                     // superscript (dagger) alif
            or >= 'ۖ' and <= 'ۭ'; // Qur'anic annotation, sajdah and waqf marks

    /// <summary>
    /// The folded form: no diacritics, one shape per letter family, no
    /// punctuation, single spaces, lower-cased so a Latin query folds too.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var builder = new StringBuilder(raw.Length);
        var lastWasSpace = false;

        foreach (var ch in raw.Normalize(NormalizationForm.FormC))
        {
            if (IsDiacritic(ch)) continue;

            var folded = Fold(ch);

            if (folded == ' ')
            {
                // Collapse runs, and never open with one.
                if (!lastWasSpace && builder.Length > 0) builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            if (folded == '\0') continue;

            builder.Append(folded);
            lastWasSpace = false;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// One character's folded form: <c>' '</c> for anything that separates
    /// words, <c>'\0'</c> for anything to drop outright.
    /// </summary>
    private static char Fold(char c) => c switch
    {
        // Alif family → bare alif. The hamza's seat is orthographic and is the
        // single most common thing a typist gets "wrong".
        'آ' or 'أ' or 'إ' or 'ٱ' => 'ا',

        // Ya family → bare ya: alif maqsura and ya are interchanged freely, and
        // Egyptian keyboards produce ى where Levantine ones produce ي.
        'ى' => 'ي',

        // Ta marbuta → ha, which is how it is typed as often as not.
        'ة' => 'ه',

        // Hamza on waw / on ya, standing alone → the letter underneath.
        'ؤ' => 'و',
        'ئ' => 'ي',
        'ء' => '\0',

        // Arabic-Indic digits → ASCII, so «٣٣» and «33» are one search.
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),

        _ when char.IsWhiteSpace(c) => ' ',
        _ when char.IsPunctuation(c) || char.IsSymbol(c) => ' ',
        _ => char.ToLowerInvariant(c),
    };

    /// <summary>
    /// True when <paramref name="haystack"/>'s folded text contains the folded
    /// <paramref name="needle"/>. For in-memory checks only — a query against
    /// the database compares against the stored <c>SearchText</c> column.
    /// </summary>
    public static bool Contains(string? haystack, string? needle) =>
        Normalize(haystack).Contains(Normalize(needle), StringComparison.Ordinal);
}
