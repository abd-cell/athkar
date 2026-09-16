using System.Text.Json;
using Athkar.Areas.Services.Quran;
using Athkar.DataAccess.Seeders;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Text;

namespace Athkar.Tests;

/// <summary>
/// Guards the seed data pulled from the quran.ai MCP server.
///
/// The seeder itself is a few lines of assignment; what can actually go wrong is
/// the file — a pull that ran before the pagination fix silently dropped 1413
/// ayat, and آية الكرسي with it, without a single error. So these tests read the
/// shipped JSON and check the two things that would not announce themselves:
/// that every block is complete and in the orthography it claims, and that the
/// text still folds the way search expects it to.
/// </summary>
public class QuranicSeedDataTests
{
    private static readonly JsonDocument Data = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "DataAccess", "Seeders", "Data", "athkar_quranic.json")));

    private static IEnumerable<JsonElement> Blocks => Data.RootElement.EnumerateArray();

    /// <summary>
    /// The chapter the seeder builds. A block that vanished from the pull would
    /// otherwise show up as a chapter one item shorter, which nobody counts.
    /// </summary>
    [Fact]
    public void EveryExpectedBlockIsPresent()
    {
        string[] expected =
        [
            "fatiha", "baqarah-opening", "ayat-al-kursi", "baqarah-closing",
            "imran-tafakkur", "kahf-opening", "kahf-closing", "hashr-closing",
            "sajdah", "mulk", "kafirun", "ikhlas", "falaq", "nas",
        ];

        var actual = Blocks.Select(b => b.GetProperty("key").GetString()).ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The ayah count a reference promises is the count it must carry. «2:285-286»
    /// with one ayah in it is exactly what silent pagination looks like.
    /// </summary>
    [Fact]
    public void EachBlockCarriesTheAyatItsReferenceClaims()
    {
        foreach (var block in Blocks)
        {
            var reference = block.GetProperty("reference").GetString()!;
            var ayahs = block.GetProperty("ayahs").EnumerateArray().ToArray();

            var range = reference.Split(':')[1];
            var bounds = range.Split('-');
            var expected = bounds.Length == 1
                ? 1
                : int.Parse(bounds[1]) - int.Parse(bounds[0]) + 1;

            Assert.Equal(expected, ayahs.Length);
            Assert.All(ayahs, a => Assert.False(
                string.IsNullOrWhiteSpace(a.GetProperty("text").GetString()),
                $"{reference}: an ayah came back empty"));
        }
    }

    /// <summary>
    /// The display text is the Uthmani script, not the plain one. The two read
    /// aloud identically and differ only in orthography, so nothing downstream
    /// would complain — but shipping «اللهُ لَا إِلَهَ» where the mushaf has
    /// «ٱللَّهُ لَآ إِلَٰهَ» is precisely the drift the MCP pull exists to prevent.
    /// </summary>
    [Fact]
    public void DisplayTextIsInUthmaniOrthography()
    {
        var kursi = Blocks.Single(b => b.GetProperty("key").GetString() == "ayat-al-kursi");
        var text = kursi.GetProperty("ayahs")[0].GetProperty("text").GetString()!;

        // Alif wasla: present throughout the Uthmani rasm, absent from every
        // plain edition. One character, and the whole distinction.
        Assert.Contains('ٱ', text);
    }

    /// <summary>
    /// The dagger alif, and why the seeder folds the plain edition instead of
    /// the Uthmani one.
    ///
    /// «ٱلْعَٰلَمِينَ» writes its second alif as a superscript mark rather than a letter.
    /// <see cref="ArabicText.Normalize"/> strips that mark — rightly, it is a
    /// diacritic — and the alif disappears with it, leaving «العلمين». Nobody types
    /// that. The plain edition spells the letter out, so folding it yields
    /// «العالمين», which is what a reader actually enters.
    ///
    /// The two therefore do <b>not</b> agree, and this test pins the one case
    /// where the difference decides whether search finds anything at all.
    /// </summary>
    [Fact]
    public void UthmaniAndPlainEditionsFoldDifferentlyWhereTheDaggerAlifStandsIn()
    {
        var fatiha = Blocks.Single(b => b.GetProperty("key").GetString() == "fatiha");
        var ayah = fatiha.GetProperty("ayahs")[1];

        var fromUthmani = ArabicText.Normalize(ayah.GetProperty("text").GetString());
        var fromPlain = ArabicText.Normalize(ayah.GetProperty("search_text").GetString());

        Assert.Equal("الحمد لله رب العلمين", fromUthmani);
        Assert.Equal("الحمد لله رب العالمين", fromPlain);

        // The seeder stores the second. If this ever flips, a reader searching
        // «العالمين» stops finding the Fatiha.
        Assert.NotEqual(fromUthmani, fromPlain);
    }

    /// <summary>
    /// Every ayah ships the plain text the seeder folds for SearchText. One
    /// missing would send that block down the Uthmani fallback silently.
    /// </summary>
    [Fact]
    public void EveryAyahCarriesThePlainEditionTheSeederFolds()
    {
        foreach (var block in Blocks)
        {
            foreach (var ayah in block.GetProperty("ayahs").EnumerateArray())
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(ayah.GetProperty("search_text").GetString()),
                    $"{ayah.GetProperty("ayah").GetString()}: no plain-edition text");
            }
        }
    }

    /// <summary>
    /// Folding has to actually strip the Uthmani marks — the wasla, the dagger
    /// alif, the small high seen and the waqf signs. If any survived, the stored
    /// SearchText would hold characters no keyboard produces.
    /// </summary>
    [Fact]
    public void FoldedTextHoldsNothingAKeyboardCannotType()
    {
        var kursi = Blocks.Single(b => b.GetProperty("key").GetString() == "ayat-al-kursi");
        var folded = ArabicText.Normalize(kursi.GetProperty("ayahs")[0].GetProperty("text").GetString());

        Assert.DoesNotContain('ٱ', folded);
        Assert.DoesNotContain('ٰ', folded);
        Assert.DoesNotContain('ۚ', folded);
    }

    /// <summary>
    /// Everything the seeder writes fits the columns it writes into.
    ///
    /// This is the one that matters. SQL Server caps a nonclustered index key at
    /// 1700 bytes and says nothing when the index is built — it says it on the
    /// INSERT, as error 1946. Al-Mulk and as-Sajdah are thirty ayat each, which
    /// is well past that, so the unfitted seeder threw inside startup and took
    /// the whole API down with it: no prayer times, no reminders, because a
    /// surah was too long for a search column.
    /// </summary>
    [Theory]
    [InlineData("mulk")]
    [InlineData("sajdah")]
    [InlineData("imran-tafakkur")]
    [InlineData("ikhlas")]
    public void WhatTheSeederWritesFitsTheColumnsItWritesInto(string key)
    {
        var block = QuranicAthkarCatalog.Blocks.Single(b => b.Key == key);

        var arabic = TextFit.Fit(
            string.Join(' ', block.Ayahs.Select(a => a.Text)), ContentRules.MaxTextLength);
        var search = TextFit.Fit(
            ArabicText.Normalize(QuranicAthkarSeeder.SearchSource(block)),
            ContentRules.MaxIndexedSearchLength);
        var english = TextFit.Fit(
            QuranicAthkarSeeder.EnglishOf(block), ContentRules.MaxTextLength);

        Assert.InRange(arabic.Length, 1, ContentRules.MaxTextLength);
        Assert.InRange(search.Length, 1, ContentRules.MaxIndexedSearchLength);
        Assert.InRange(english.Length, 1, ContentRules.MaxTextLength);
    }

    /// <summary>
    /// Trimming is only ever allowed to shorten. A block that already fits must
    /// come through byte for byte — the verse itself is not something to round.
    /// </summary>
    [Fact]
    public void ShortBlocksAreNotTrimmedAtAll()
    {
        var ikhlas = QuranicAthkarCatalog.Blocks.Single(b => b.Key == "ikhlas");
        var arabic = string.Join(' ', ikhlas.Ayahs.Select(a => a.Text));

        Assert.Equal(arabic, TextFit.Fit(arabic, ContentRules.MaxTextLength));
    }

    /// <summary>
    /// Every block carries the English the seeder files as its "en" translation.
    /// </summary>
    [Fact]
    public void EveryAyahCarriesAnEnglishRendering()
    {
        foreach (var block in Blocks)
        {
            foreach (var ayah in block.GetProperty("ayahs").EnumerateArray())
            {
                var en = ayah.GetProperty("en").GetString();
                Assert.False(string.IsNullOrWhiteSpace(en),
                    $"{ayah.GetProperty("ayah").GetString()}: no English translation");
            }
        }
    }
}
