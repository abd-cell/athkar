using System.Text;

namespace Athkar.Areas.Services.Quran;

/// <summary>
/// Small text chores shared by the seeder and the admin sync — kept in one place
/// so the two cannot produce different strings from the same source and then
/// report a difference that is really their own.
/// </summary>
public static class QuranText
{
    /// <summary>
    /// Strips inline markup. The English editions carry the translator's footnote
    /// markers as <c>sup</c> tags; a reader that can show the note wants them, a
    /// dhikr card does not.
    ///
    /// A character scan rather than a regular expression: the input is a
    /// translation, not HTML, and the only thing to remove is a balanced tag.
    /// </summary>
    public static string StripMarkup(string text)
    {
        var builder = new StringBuilder(text.Length);
        var depth = 0;

        foreach (var ch in text)
        {
            if (ch == '<') { depth++; continue; }
            if (ch == '>') { if (depth > 0) depth--; continue; }
            if (depth == 0) builder.Append(ch);
        }

        while (builder.ToString().Contains("  ")) builder.Replace("  ", " ");

        return builder.ToString().Trim();
    }
}
