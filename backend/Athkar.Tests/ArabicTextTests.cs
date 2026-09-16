using Athkar.Shareds.Text;

namespace Athkar.Tests;

/// <summary>
/// The folding that makes search work.
///
/// Every case here is a real way somebody types Arabic, not a synthetic one:
/// no diacritics at all, a bare alif for a hamza, a final ha for a ta marbuta,
/// Western digits for Arabic-Indic ones.
/// </summary>
public class ArabicTextTests
{
    [Theory]
    // Diacritics are dropped entirely.
    [InlineData("سُبْحَانَ اللهِ", "سبحان الله")]
    [InlineData("أَذْكَارُ الصَّبَاحِ", "اذكار الصباح")]
    // Every alif shape folds to the bare one.
    [InlineData("آمَنَ", "امن")]
    [InlineData("إِيمَان", "ايمان")]
    [InlineData("ٱلْحَمْدُ", "الحمد")]
    // Alif maqsura and ya are one letter for searching.
    [InlineData("مُوسَى", "موسي")]
    // Ta marbuta folds to ha, which is how it is typed as often as not.
    [InlineData("الصَّلَاة", "الصلاه")]
    // Hamza carriers fold to the letter underneath.
    [InlineData("مُؤْمِن", "مومن")]
    [InlineData("سَائِل", "سايل")]
    // The tatweel is a stretching glyph, not a letter.
    [InlineData("الحــــمد", "الحمد")]
    public void Normalize_folds_the_ways_arabic_is_actually_typed(string input, string expected)
    {
        Assert.Equal(expected, ArabicText.Normalize(input));
    }

    [Theory]
    [InlineData("٣٣", "33")]
    [InlineData("١٠٠", "100")]
    public void Normalize_converts_arabic_indic_digits(string input, string expected)
    {
        Assert.Equal(expected, ArabicText.Normalize(input));
    }

    [Fact]
    public void Normalize_collapses_whitespace_and_punctuation()
    {
        Assert.Equal("لا اله الا الله", ArabicText.Normalize("  لا   إلهَ، إلَّا  اللهُ!  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_handles_nothing_gracefully(string? input)
    {
        Assert.Equal(string.Empty, ArabicText.Normalize(input));
    }

    /// <summary>
    /// The point of the whole exercise: a query typed without a single
    /// diacritic finds fully vocalised text.
    /// </summary>
    [Fact]
    public void A_bare_query_matches_vocalised_text()
    {
        const string corpus = "سُبْحَانَ اللهِ وَبِحَمْدِهِ، سُبْحَانَ اللهِ العَظِيمِ";

        Assert.True(ArabicText.Contains(corpus, "سبحان الله"));
        Assert.True(ArabicText.Contains(corpus, "وبحمده"));
        Assert.False(ArabicText.Contains(corpus, "استغفر"));
    }
}
