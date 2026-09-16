using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Quran;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Text;

namespace Athkar.DataAccess.Seeders;

/// <summary>
/// Seeds the chapter of adhkar that are Qur'an — الفاتحة, آية الكرسي, خواتيم
/// البقرة, الملك, المعوذات and the rest — from files rather than from literals.
///
/// The rest of <see cref="DataSeeder"/> carries its Arabic inline, which is right
/// for hadith: a narration is copied once from a printed collection and then it
/// is settled. Qur'anic text is different in one way that matters here — its
/// orthography is not a typing choice. «ٱللَّهُ لَآ إِلَٰهَ إِلَّا هُوَ» is not the same string as
/// «اللهُ لَا إِلَهَ إِلَّا هُوَ» even though the two read aloud identically, and a hand-typed
/// verse drifts from the mushaf in ways nobody reviewing a C# file would catch.
///
/// So the text comes from <see cref="QuranicAthkarCatalog"/>, pulled from the
/// quran.ai MCP server by <c>tools/quran-mcp/pull.py</c>. This class never
/// invents a verse and never edits one; correcting the content is a re-run of
/// that script, or a sync through <see cref="IQuranAthkarSyncService"/>.
///
/// Like every other step it seeds only into emptiness: if the chapter is already
/// there, an editor owns it and the seeder keeps its hands off.
/// </summary>
public static class QuranicAthkarSeeder
{
    /// <summary>
    /// Never throws.
    ///
    /// Seeding runs inside startup, so an exception here does not degrade one
    /// chapter — it stops the API from coming up at all, and the reader loses
    /// prayer times and reminders over a verse that would not fit in a column.
    /// The content is worth a loud warning and no more than that.
    /// </summary>
    public static async Task SeedAsync(DatabaseService db, ILogger? logger = null)
    {
        try
        {
            await Seed(db, logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Qur'anic adhkar could not be seeded. The rest of the database is unaffected.");
        }
    }

    private static async Task Seed(DatabaseService db, ILogger? logger)
    {
        var blocks = QuranicAthkarCatalog.Blocks;

        // A missing data file is not a reason to fail startup — every other
        // chapter still seeds — but it is worth saying out loud, because the
        // symptom otherwise is a chapter that quietly never appears.
        if (blocks.Count == 0)
        {
            logger?.LogWarning(
                "Qur'anic adhkar were not seeded: DataAccess/Seeders/Data is missing or empty. " +
                "Re-run tools/quran-mcp/pull.py to regenerate it.");
            return;
        }

        // The chapter and its contents are two saves, so the guard cannot be "does
        // the chapter exist" — an interruption between them leaves a chapter that
        // is empty and, under that guard, stays empty for the life of the
        // database. Find or create it, and decide about the adhkar separately.
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Key == QuranicAthkarCatalog.CategoryKey);

        if (category is null)
        {
            category = new AthkarCategory
            {
                Key = QuranicAthkarCatalog.CategoryKey,
                Icon = "book",
                // After «أذكار متفرقة», which DataSeeder leaves at index 13.
                SortOrder = 14,
                Rhythm = CategoryRhythm.None,
                Anchor = PrayerAnchor.None,
                IsPublished = true,
            };
            category.Translations.Add(new CategoryTranslation { LanguageCode = "ar", Name = "أذكار من القرآن" });
            category.Translations.Add(new CategoryTranslation { LanguageCode = "en", Name = "From the Qur'an" });

            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        // Deleted rows count. An editor who emptied this chapter made a decision,
        // and a seeder that restores what somebody removed is a bug that takes a
        // week to notice — so this fills a chapter that was never filled, and
        // never one that was cleared.
        if (await db.Adhkar.AnyAsync(d => d.CategoryId == category.Id)) return;

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Ayahs.Count == 0) continue;

            var arabic = string.Join(' ', block.Ayahs.Select(a => a.Text));

            var dhikr = new Dhikr
            {
                CategoryId = category.Id,
                SortOrder = i,
                ArabicText = TextFit.Fit(arabic, ContentRules.MaxTextLength),
                SearchText = TextFit.Fit(
                    ArabicText.Normalize(SearchSource(block)), ContentRules.MaxIndexedSearchLength),
                RepeatCount = 1,
                SourceBook = QuranicAthkarCatalog.SourceBook,
                SourceReference = QuranicAthkarCatalog.Locator(block),
                Grade = HadithGrade.QuranVerse,
                GradedBy = null,
                IsPublished = true,
            };

            dhikr.Translations.Add(new DhikrTranslation
            {
                LanguageCode = "ar",
                Translation = TextFit.Fit(arabic, ContentRules.MaxTextLength),
            });
            dhikr.Translations.Add(new DhikrTranslation
            {
                LanguageCode = "en",
                Translation = TextFit.Fit(EnglishOf(block), ContentRules.MaxTextLength),
            });

            db.Adhkar.Add(dhikr);
        }

        await db.SaveChangesAsync();

        logger?.LogInformation("Seeded {Count} Qur'anic adhkar from quran.ai MCP data.", blocks.Count);
    }

    /// <summary>
    /// The text folded into <c>Dhikr.SearchText</c> — the plain edition, not the
    /// Uthmani one, and that difference is the whole reason the pull fetches both.
    ///
    /// The Uthmani rasm writes «ٱلْعَٰلَمِينَ» with a dagger alif where the letter would
    /// be. <see cref="ArabicText.Normalize"/> strips that mark, correctly: it is a
    /// diacritic. But the alif it stood in for goes with it, leaving «العلمين» — and
    /// a reader typing «العالمين», as everyone does, matches nothing. The plain
    /// edition spells the letter out, so folding it gives the string a keyboard
    /// actually produces. <c>QuranicSeedDataTests</c> holds the case.
    ///
    /// Where a block has no plain text the Uthmani fold is still better than an
    /// empty column.
    /// </summary>
    public static string SearchSource(QuranicAthkarCatalog.QuranicBlock block) =>
        block.Ayahs.All(a => !string.IsNullOrWhiteSpace(a.SearchText))
            ? string.Join(' ', block.Ayahs.Select(a => a.SearchText))
            : string.Join(' ', block.Ayahs.Select(a => a.Text));

    /// <summary>
    /// The English, with the translator's own footnote markers removed. They
    /// arrive as inline <c>sup</c> tags, which belong in a reader that can show
    /// the note; here they would render as literal angle brackets mid-sentence.
    /// </summary>
    public static string EnglishOf(QuranicAthkarCatalog.QuranicBlock block) =>
        string.Join(' ', block.Ayahs
            .Select(a => a.En)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => QuranText.StripMarkup(t!)));
}
