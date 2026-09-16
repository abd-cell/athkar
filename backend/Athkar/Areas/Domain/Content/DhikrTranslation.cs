using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Content;

/// <summary>
/// One dhikr rendered into one language.
///
/// <see cref="Virtue"/> is here rather than on <see cref="Dhikr"/> because the
/// stated reward is itself narrated text that has to be translated — and
/// because it is optional in a specific way: it is written down only where a
/// sound narration states it, never as encouragement invented by an editor.
/// </summary>
public class DhikrTranslation : TranslationEntity
{
    public int DhikrId { get; set; }
    public Dhikr? Dhikr { get; set; }

    /// <summary>The meaning, in this language.</summary>
    public string Translation { get; set; } = string.Empty;

    /// <summary>
    /// Latin-script pronunciation, for readers who do not read Arabic. Only
    /// meaningful for languages that are not written in the Arabic script, so it
    /// is nullable and usually null.
    /// </summary>
    public string? Transliteration { get; set; }

    /// <summary>The narrated virtue, where one is established. Left null otherwise.</summary>
    public string? Virtue { get; set; }
}
