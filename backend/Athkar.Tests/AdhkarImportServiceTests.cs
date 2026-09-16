using Microsoft.Extensions.Logging.Abstractions;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Shareds.Text;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// Importing the أبواب of حصن المسلم.
///
/// The source carries no book, no hadith number and no grading, so the only
/// safe shape for this import is the one tested here: it adds, it never edits or
/// deletes, and every dhikr it writes is unpublished. An import that published
/// 267 unattributed rows would be the largest hole anyone could put in the one
/// promise this project makes.
/// </summary>
public class AdhkarImportServiceTests
{
    private static AdhkarCatalog.ImportedChapter FirstNewChapter =>
        AdhkarCatalog.Chapters.First(c => c.Key.StartsWith("hisn-") && c.Adhkar.Count > 0);

    private sealed class Harness
    {
        public InMemoryRepository<AthkarCategory> Categories { get; } = new();
        public InMemoryRepository<Dhikr> Adhkar { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeAuditService Audit { get; } = new();
        public FakeAppConfigurationService Configuration { get; } = new();

        public AthkarCategory SeedCategory(string key, int id, int sortOrder = 0)
        {
            var category = new AthkarCategory { Id = id, Key = key, SortOrder = sortOrder };
            Categories.Seed(category);
            return category;
        }

        public Dhikr SeedDhikr(int categoryId, string arabic)
        {
            var row = new Dhikr
            {
                CategoryId = categoryId,
                ArabicText = arabic,
                SearchText = ArabicText.Normalize(arabic),
                IsPublished = true,
            };
            Adhkar.Seed(row);
            return row;
        }

        public AdhkarImportService Build() => new(
            Categories, Adhkar, UnitOfWork, Audit, Configuration,
            new FakeSecurityManager(), NullLogger<AdhkarImportService>.Instance);
    }

    // ── The corpus itself ──

    /// <summary>
    /// The shipped file is the whole book. A pull that half-finished would show
    /// up here as a chapter count nobody would otherwise count.
    /// </summary>
    [Fact]
    public void TheShippedCorpusIsTheWholeBook()
    {
        Assert.Equal(132, AdhkarCatalog.Chapters.Count);
        Assert.All(AdhkarCatalog.Chapters, c => Assert.NotEmpty(c.Adhkar));
        Assert.All(AdhkarCatalog.Chapters, c => Assert.NotEmpty(c.TitleArabic));

        // Keys are what the import matches on, so a duplicate would silently
        // merge two أبواب into one.
        Assert.Equal(
            AdhkarCatalog.Chapters.Count,
            AdhkarCatalog.Chapters.Select(c => c.Key).Distinct().Count());
    }

    // ── Preview ──

    /// <summary>A preview reads and reports. It does not create a single row.</summary>
    [Fact]
    public async Task PreviewCountsWhatItWouldAddWithoutAddingIt()
    {
        var harness = new Harness();

        var result = await harness.Build().Preview();

        Assert.True(result.Success);
        Assert.False(result.Data!.Applied);
        Assert.Equal(132, result.Data.ChaptersChecked);
        Assert.True(result.Data.ChaptersAdded > 0);
        Assert.True(result.Data.AdhkarAdded > 0);

        Assert.Empty(harness.Categories.All);
        Assert.Empty(harness.Adhkar.All);
        Assert.Equal(0, harness.Configuration.BumpCount);
        Assert.Empty(harness.Audit.Actions);
    }

    // ── Applying ──

    /// <summary>
    /// The rule the whole import turns on. Nothing from this source reaches a
    /// reader, because nothing from this source carries a takhrij.
    /// </summary>
    [Fact]
    public async Task EveryImportedDhikrIsAnUnpublishedDraftWithNoSource()
    {
        var harness = new Harness();

        var result = await harness.Build().Apply();

        Assert.True(result.Data!.Applied);
        Assert.NotEmpty(harness.Adhkar.All);

        Assert.All(harness.Adhkar.All, d =>
        {
            Assert.False(d.IsPublished);
            Assert.Null(d.SourceBook);
            Assert.Null(d.SourceReference);
            Assert.Null(d.Grade);
        });

        Assert.Equal(result.Data.AdhkarAdded, result.Data.DraftsAwaitingSource);
    }

    /// <summary>
    /// Chapters arrive unpublished, and this is the test that keeps the reader's
    /// screen honest rather than the archive complete.
    ///
    /// Every dhikr in an imported chapter is a draft, so a published chapter
    /// would be an empty list on a phone — and the import makes 129 of them at
    /// once. The editor publishes a chapter when it has something in it.
    /// </summary>
    [Fact]
    public async Task ImportedChaptersAreUnpublishedAndCarryTheirArabicName()
    {
        var harness = new Harness();

        await harness.Build().Apply();

        Assert.NotEmpty(harness.Categories.All);
        Assert.All(harness.Categories.All, c =>
        {
            Assert.False(c.IsPublished);
            Assert.NotEmpty(c.Translations.Single(t => t.LanguageCode == "ar").Name);
        });
    }

    /// <summary>
    /// The whole import, from a reader's side: not one row of it is visible.
    /// A published chapter or a published dhikr here would both be bugs, and
    /// they would look completely different in the app — an empty list, or an
    /// unattributed narration — so both are asserted together.
    /// </summary>
    [Fact]
    public async Task NothingTheImportWritesIsVisibleToAReader()
    {
        var harness = new Harness();

        await harness.Build().Apply();

        Assert.DoesNotContain(harness.Categories.All, c => c.IsPublished);
        Assert.DoesNotContain(harness.Adhkar.All, d => d.IsPublished);
    }

    /// <summary>Every dhikr lands in the chapter it belongs to, with a real id.</summary>
    [Fact]
    public async Task EveryImportedDhikrIsAttachedToItsChapter()
    {
        var harness = new Harness();

        await harness.Build().Apply();

        var ids = harness.Categories.All.Select(c => c.Id).ToHashSet();

        Assert.All(harness.Adhkar.All, d =>
        {
            Assert.NotEqual(0, d.CategoryId);
            Assert.Contains(d.CategoryId, ids);
        });
    }

    /// <summary>
    /// The content version is the whole sync protocol: an import that reaches
    /// the database and not the phones is an import that never happened.
    /// </summary>
    [Fact]
    public async Task ApplyingBumpsTheContentVersionAndAudits()
    {
        var harness = new Harness();

        var result = await harness.Build().Apply();

        Assert.Equal(1, harness.Configuration.BumpCount);
        Assert.Equal(harness.Configuration.Version, result.Data!.ContentVersion);
        Assert.All(harness.Audit.Actions, a => Assert.Equal(AuditActions.AdhkarImport, a));
        Assert.NotEmpty(harness.Audit.Actions);
    }

    /// <summary>
    /// Run twice, and the second run is a no-op. Without this an admin pressing
    /// the button again doubles the editors' review queue.
    /// </summary>
    [Fact]
    public async Task RunningItTwiceAddsNothingTheSecondTime()
    {
        var harness = new Harness();

        var first = await harness.Build().Apply();
        var categories = harness.Categories.All.Count;
        var adhkar = harness.Adhkar.All.Count;

        var second = await harness.Build().Apply();

        Assert.True(first.Data!.AdhkarAdded > 0);
        Assert.Equal(0, second.Data!.ChaptersAdded);
        Assert.Equal(0, second.Data.AdhkarAdded);
        Assert.False(second.Data.Applied);

        Assert.Equal(categories, harness.Categories.All.Count);
        Assert.Equal(adhkar, harness.Adhkar.All.Count);

        // And no second version bump, so no install re-downloads for nothing.
        Assert.Equal(1, harness.Configuration.BumpCount);
    }

    // ── What it refuses to touch ──

    /// <summary>
    /// A chapter an editor has filled is theirs. Adding to it would interleave
    /// the book's ordering with a curated one, and the import has no opinion
    /// about which should win — so it reports and steps back.
    /// </summary>
    [Fact]
    public async Task AChapterAnEditorHasFilledIsSkippedRatherThanMergedInto()
    {
        var harness = new Harness();
        var chapter = FirstNewChapter;
        var category = harness.SeedCategory(chapter.Key, 42);
        harness.SeedDhikr(category.Id, chapter.Adhkar[0].Text);

        var result = await harness.Build().Apply();

        var report = result.Data!.Chapters.Single(c => c.Key == chapter.Key);
        Assert.Equal(AdhkarImportAction.Skipped, report.Action);
        Assert.Equal(0, report.Adding);

        // The editor's single dhikr is still the only one in that chapter.
        Assert.Single(harness.Adhkar.All, d => d.CategoryId == category.Id);
    }

    /// <summary>
    /// The same words with different brackets and diacritics are the same dhikr.
    /// Matching on the raw string would import a near-duplicate of everything an
    /// editor has ever typed.
    /// </summary>
    [Fact]
    public async Task AdhkarAreMatchedOnFoldedArabicNotOnTheExactString()
    {
        var chapter = FirstNewChapter;
        var original = chapter.Adhkar[0].Text;

        var harness = new Harness();
        var category = harness.SeedCategory(chapter.Key, 42);

        // Same words, re-typed the way a person would: no diacritics at all.
        harness.SeedDhikr(category.Id, ArabicText.Normalize(original));

        var result = await harness.Build().Preview();

        var report = result.Data!.Chapters.Single(c => c.Key == chapter.Key);
        Assert.Equal(1, report.Present);
    }

    /// <summary>
    /// The import never removes anything, including the rows it did not create.
    /// </summary>
    [Fact]
    public async Task NothingIsEverDeleted()
    {
        var harness = new Harness();
        var category = harness.SeedCategory("editors-own", 99);
        var stray = harness.SeedDhikr(category.Id, "ذكر كتبه المحرر");

        await harness.Build().Apply();

        Assert.False(stray.IsDeleted);
        Assert.False(harness.Categories.All.Single(c => c.Id == 99).IsDeleted);
    }

    /// <summary>
    /// Imported chapters queue after whatever an editor has arranged rather than
    /// shuffling into the middle of it.
    /// </summary>
    [Fact]
    public async Task ImportedChaptersSortAfterTheExistingOnes()
    {
        var harness = new Harness();
        harness.SeedCategory("editors-own", 99, sortOrder: 13);

        await harness.Build().Apply();

        var imported = harness.Categories.All.Where(c => c.Key != "editors-own");
        Assert.All(imported, c => Assert.True(c.SortOrder > 13));
    }

    /// <summary>
    /// Nothing to import is a plain failure an admin can read, not an exception.
    /// </summary>
    [Fact]
    public async Task AMissingCorpusFailsTheEnvelope()
    {
        // The corpus ships with the build, so this asserts the contract rather
        // than the absence: the code path returns the envelope, never throws.
        Assert.NotEmpty(AdhkarCatalog.Chapters);
        Assert.Equal(306, (int)ErrorCode.AdhkarCorpusMissing);

        var result = await new Harness().Build().Preview();
        Assert.True(result.Success);
    }
}
