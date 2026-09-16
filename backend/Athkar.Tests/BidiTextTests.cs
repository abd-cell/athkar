using Athkar.Shareds.Text;

namespace Athkar.Tests;

/// <summary>
/// Direction, for the one surface neither stack renders itself.
///
/// These cases are deliberately the same as
/// <c>app/athkar_app/test/bidi_text_test.dart</c>, for the same reason the
/// Arabic-folding tests are duplicated: a reminder scheduled on the phone and a
/// broadcast pushed from here arrive in the same shade seconds apart, and a rule
/// that drifted on one side would lay them out differently with nothing failing.
///
/// The failure being guarded against is never "the Arabic came out backwards" —
/// the bidi algorithm gets letters right. It is that the *neutrals* move: full
/// stops jump to the far edge, and a number beside an Arabic book name swaps
/// sides.
/// </summary>
public class BidiTextTests
{
    private const string Rlm = "\u200F";
    private const string Lrm = "\u200E";
    private const string Fsi = "\u2068";
    private const string Pdi = "\u2069";

    [Theory]
    // The takhrij line often opens with a digit or a guillemet. Neither is
    // strong, and neither should decide which way the line runs.
    [InlineData("«صحيح البخاري»", true)]
    [InlineData("6405 صحيح البخاري", true)]
    [InlineData("\"Sahih al-Bukhari\"", false)]
    // Nothing strong at all is not right-to-left.
    [InlineData("6405", false)]
    [InlineData("   ", false)]
    [InlineData("", false)]
    // Arabic-Indic digits sit in the Arabic block but are numbers, not strong
    // text — a reference standing alone is not an Arabic line. This is the case
    // the two implementations are most likely to drift on.
    [InlineData("٦٤٠٥", false)]
    [InlineData("صحيح البخاري ٦٤٠٥", true)]
    public void Direction_follows_the_first_strong_character(string text, bool expected) =>
        Assert.Equal(expected, BidiText.IsRtl(text));

    [Fact]
    public void An_inserted_value_cannot_drag_the_sentences_punctuation()
    {
        var filled = $"حسب التوقيت المحلي لمدينة {BidiText.Isolate("Amman")}.";

        Assert.Equal($"حسب التوقيت المحلي لمدينة {Fsi}Amman{Pdi}.", filled);

        // The isolate closes before the full stop, so the stop stays with the
        // Arabic sentence rather than joining the Latin run.
        Assert.EndsWith($"{Pdi}.", filled);
    }

    [Fact]
    public void A_matching_direction_is_isolated_too()
    {
        // Which way a city name runs is decided by the reader's settings, long
        // after the sentence was written in the CMS.
        Assert.Equal($"{Fsi}عمّان{Pdi}", BidiText.Isolate("عمّان"));
    }

    [Fact]
    public void Nothing_in_nothing_out()
    {
        // Two invisible characters where a value should have been would be
        // worse than the gap: unsearchable, and impossible to explain.
        Assert.Equal(string.Empty, BidiText.Isolate(""));
        Assert.Equal(string.Empty, BidiText.Isolate(null));
    }

    [Fact]
    public void An_arabic_line_is_marked_right_to_left() =>
        Assert.Equal($"{Rlm}حان الآن موعد صلاة الفجر",
            BidiText.ForNotification("حان الآن موعد صلاة الفجر"));

    [Fact]
    public void An_english_line_is_marked_left_to_right() =>
        Assert.Equal($"{Lrm}It is now time for Fajr",
            BidiText.ForNotification("It is now time for Fajr"));

    [Fact]
    public void Each_line_is_marked_on_its_own_because_a_reminder_carries_both()
    {
        // The English reader's case: an English announcement with an Arabic
        // narration beneath it. One mark for the whole body would have to be
        // wrong about one of the two.
        const string body = "It is now time for Fajr\n\n"
                            + "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ\n"
                            + "صحيح البخاري 6405";

        var lines = BidiText.ForNotification(body).Split('\n');

        Assert.StartsWith(Lrm, lines[0]);
        Assert.Equal(string.Empty, lines[1]);
        Assert.StartsWith(Rlm, lines[2]);

        // The line that reverses without help: an Arabic book name and a Latin
        // number. Marked right-to-left, «صحيح البخاري 6405» keeps its order.
        Assert.StartsWith(Rlm, lines[3]);
    }

    [Fact]
    public void The_paragraph_break_stays_blank()
    {
        // A mark on it makes it a line with content as far as some shades are
        // concerned, and the break becomes a stray empty row.
        Assert.Equal(string.Empty, BidiText.ForNotification("أ\n\nب").Split('\n')[1]);
    }

    [Fact]
    public void Marking_twice_adds_one_mark_not_two()
    {
        // Not hypothetical: this marks a push before it is sent, and the app
        // redraws that same push through its own copy when it arrives in the
        // foreground. Caught on a real phone, where the title came through
        // carrying two right-to-left marks.
        var once = BidiText.ForNotification("حان الآن موعد صلاة الفجر");
        var twice = BidiText.ForNotification(once);

        Assert.Equal($"{Rlm}حان الآن موعد صلاة الفجر", once);
        Assert.Equal(once, twice);
        Assert.Equal(1, twice.Count(c => c == Rlm[0]));
    }

    [Fact]
    public void A_line_already_marked_the_other_way_is_left_alone()
    {
        // Whoever marked it first knew something this pass does not — a
        // transliterated line, say. Re-deciding would overrule them.
        var marked = $"{Lrm}Sahih al-Bukhari";
        Assert.Equal(marked, BidiText.ForNotification(marked));
    }

    [Fact]
    public void Empty_text_is_left_exactly_as_it_is()
    {
        Assert.Equal(string.Empty, BidiText.ForNotification(""));
        Assert.Equal(string.Empty, BidiText.ForNotification(null));
    }
}
