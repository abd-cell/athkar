namespace Athkar.Shareds.Text;

/// <summary>
/// Bidirectional text, for the notifications this server draws on somebody
/// else's phone.
///
/// A push that arrives while the app is closed is rendered by Android or iOS
/// straight from the message — the app never sees it, so it cannot lay it out.
/// The shade takes its base direction from the *device's* locale, which means an
/// Arabic broadcast on a phone set to English is laid out left-to-right. The
/// letters still read correctly; what moves is everything neutral around them: a
/// full stop jumps to the far edge, and «صحيح البخاري 6405» comes out as
/// «6405 صحيح البخاري».
///
/// So the direction is stated in the text itself, with the invisible marks that
/// exist for exactly this. They travel with the string and cost nothing.
///
/// Every control character here is written as an escape rather than pasted in.
/// They are invisible by definition: a literal one in source cannot be seen,
/// selected, or grepped for, and a stray copy would be undiscoverable.
///
/// The app applies the identical rules in
/// <c>app/athkar_app/lib/core/bidi_text.dart</c> for the notifications it raises
/// itself, and <c>bidi_text_test.dart</c> holds the same cases as
/// <c>BidiTextTests</c>. The two must stay in step: a reminder scheduled on the
/// phone and a broadcast pushed from here land in the same shade seconds apart,
/// and they should not be laid out by different rules.
/// </summary>
public static class BidiText
{
    /// <summary>First-strong isolate: "work this run's direction out on its own".</summary>
    private const char Fsi = '\u2068';

    /// <summary>Pop directional isolate — closes <see cref="Fsi"/>.</summary>
    private const char Pdi = '\u2069';

    /// <summary>Right-to-left mark: invisible, strong, and enough to lead a line.</summary>
    private const char Rlm = '\u200F';

    /// <summary>Left-to-right mark.</summary>
    private const char Lrm = '\u200E';

    /// <summary>
    /// Whether a run reads right-to-left, by the first strong character in it.
    ///
    /// Neutrals are skipped rather than counted: a takhrij line opening with a
    /// digit or a guillemet is still an Arabic line, and letting the first
    /// character decide would lay it out backwards.
    /// </summary>
    public static bool IsRtl(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        foreach (var c in text)
        {
            if (IsStrongRtl(c)) return true;
            if (char.IsLetter(c)) return false;
        }

        return false;
    }

    /// <summary>
    /// Wraps a value so it cannot change the direction of the sentence it is
    /// placed in — a name, a number, anything substituted at run time.
    /// </summary>
    public static string Isolate(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"{Fsi}{value}{Pdi}";

    /// <summary>
    /// Marks each line of a notification with its own base direction.
    ///
    /// Per line, because one notification can carry both: an English
    /// announcement with an Arabic narration beneath it wants opposite
    /// directions in the same body, and a single mark would have to be wrong
    /// about one of them. Blank lines are left untouched — a mark would turn the
    /// paragraph break into a line with content.
    /// </summary>
    public static string ForNotification(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            // Already marked — leave it. This runs on a push before it is sent,
            // and the app runs its own copy again when that push arrives with
            // the app in the foreground. Without this the text collects a mark
            // per pass.
            if (lines[i][0] == Rlm || lines[i][0] == Lrm) continue;

            lines[i] = $"{(IsRtl(lines[i]) ? Rlm : Lrm)}{lines[i]}";
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// The strong right-to-left blocks: Hebrew, Arabic and its supplements,
    /// Syriac, Thaana, NKo, and the presentation forms.
    ///
    /// Spelled out rather than derived from <c>CharUnicodeInfo</c>, which exposes
    /// a character's general category but not its bidi class — the one property
    /// this needs.
    /// </summary>
    private static bool IsStrongRtl(char c) =>
        !IsArabicNumeral(c) &&
        c is >= '֐' and <= '׿'   // Hebrew
            or >= '؀' and <= 'ۿ' // Arabic
            or >= '܀' and <= 'ݏ' // Syriac
            or >= 'ݐ' and <= 'ݿ' // Arabic Supplement
            or >= 'ހ' and <= '޿' // Thaana
            or >= '߀' and <= '߿' // NKo
            or >= 'ࡠ' and <= 'ࣿ' // Syriac Supplement, Arabic Extended-A
            or >= 'יִ' and <= '﷿' // Hebrew and Arabic presentation forms
            or >= 'ﹰ' and <= '﻿';

    /// <summary>
    /// The digits that live inside the Arabic block without being strong.
    ///
    /// «٦٤٠٥» is an Arabic-Indic number, and Unicode classes it as a number
    /// rather than as right-to-left text — so it does not decide a line's
    /// direction, and a takhrij reference standing on its own is not an Arabic
    /// line. Excluded explicitly because the block ranges above would otherwise
    /// swallow them, and the app's `intl` does not count them either: the two
    /// implementations have to agree or the mirror is worthless.
    /// </summary>
    private static bool IsArabicNumeral(char c) =>
        c is >= '٠' and <= '٩'   // Arabic-Indic digits
            or '٫' or '٬'        // decimal and thousands separators
            or >= '۰' and <= '۹'; // extended Arabic-Indic digits
}
