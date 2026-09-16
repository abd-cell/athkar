using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Quran;
using Athkar.Areas.Services.Quran.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Shareds.Text;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The admin sync against the canonical Qur'an source.
///
/// This is the only path in the system that rewrites narrated text without a
/// person typing the replacement, so what is tested here is mostly what it
/// refuses to do: not write on a preview, not touch a row an editor has taken
/// over, not delete, and not turn a third party's outage into a 500.
/// </summary>
public class QuranAthkarSyncServiceTests
{
    /// <summary>آية الكرسي, as the catalogue names it — a real block, so the locator is the real one.</summary>
    private static QuranicAthkarCatalog.QuranicBlock Kursi =>
        QuranicAthkarCatalog.Blocks.Single(b => b.Key == "ayat-al-kursi");

    private static string KursiLocator => QuranicAthkarCatalog.Locator(Kursi);

    private const string CanonicalArabic = "ٱللَّهُ لَآ إِلَٰهَ إِلَّا هُوَ ٱلْحَىُّ ٱلْقَيُّومُ";
    private const string CanonicalPlain = "الله لا إله إلا هو الحي القيوم";
    private const string CanonicalEnglish = "Allah - there is no deity except Him.";

    private sealed class Harness
    {
        public InMemoryRepository<AthkarCategory> Categories { get; } = new();
        public InMemoryRepository<Dhikr> Adhkar { get; } = new();
        public FakeQuranMcpClient Source { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeAuditService Audit { get; } = new();
        public FakeAppConfigurationService Configuration { get; } = new();

        public Harness(bool withCategory = true)
        {
            if (withCategory)
                Categories.Seed(new AthkarCategory { Id = 7, Key = QuranicAthkarCatalog.CategoryKey });

            Source.With(Kursi.Reference, CanonicalArabic, CanonicalPlain, CanonicalEnglish);
        }

        public Dhikr SeedRow(string arabic, string? english = CanonicalEnglish,
            HadithGrade grade = HadithGrade.QuranVerse, string? locator = null)
        {
            var row = new Dhikr
            {
                CategoryId = 7,
                ArabicText = arabic,
                SearchText = ArabicText.Normalize(arabic),
                SourceBook = QuranicAthkarCatalog.SourceBook,
                SourceReference = locator ?? KursiLocator,
                Grade = grade,
                IsPublished = true,
            };

            row.Translations.Add(new DhikrTranslation { LanguageCode = "ar", Translation = arabic });
            if (english is not null)
                row.Translations.Add(new DhikrTranslation { LanguageCode = "en", Translation = english });

            Adhkar.Seed(row);
            return row;
        }

        public QuranAthkarSyncService Build() => new(
            Source, Categories, Adhkar, UnitOfWork, Audit, Configuration,
            new FakeSecurityManager(), NullLogger<QuranAthkarSyncService>.Instance,
            Options.Create(new QuranMcpSettings { Enabled = true, BaseUrl = "https://mcp.quran.ai" }));
    }

    private static QuranSyncChange ChangeFor(QuranSyncOutput output) =>
        output.Changes.Single(c => c.Key == "ayat-al-kursi");

    // ── The source being unavailable ──

    /// <summary>
    /// Switched off is a setting, not a fault, and the admin gets told which.
    /// A deployment that never configured this makes no outbound request at all.
    /// </summary>
    [Fact]
    public async Task ADisabledSourceIsReportedAsDisabled()
    {
        var harness = new Harness();
        harness.Source.IsEnabled = false;

        var result = await harness.Build().Apply();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.QuranSourceDisabled, result.ErrorCode);
        Assert.Empty(harness.Source.Requested);
    }

    /// <summary>
    /// A third party being down is a failed envelope, never a 500 — the same
    /// contract every other failure in this API keeps.
    /// </summary>
    [Fact]
    public async Task AnUnreachableSourceFailsTheEnvelopeAndWritesNothing()
    {
        var harness = new Harness();
        var row = harness.SeedRow("النص القديم");
        harness.Source.Fault = new QuranMcpException("timed out");

        var result = await harness.Build().Apply();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.QuranSourceUnreachable, result.ErrorCode);
        Assert.Equal("النص القديم", row.ArabicText);
        Assert.Equal(0, harness.Configuration.BumpCount);
    }

    // ── Preview ──

    /// <summary>
    /// A preview reads the source and reports, and that is all it does. An admin
    /// who clicks "check" has not yet agreed to rewrite anything.
    /// </summary>
    [Fact]
    public async Task PreviewReportsTheDifferenceWithoutWritingIt()
    {
        var harness = new Harness();
        var row = harness.SeedRow("الله لا إله إلا هو الحي القيوم");

        var result = await harness.Build().Preview();

        Assert.True(result.Success);
        Assert.False(result.Data!.Applied);
        Assert.Equal(1, result.Data.Changed);

        var change = ChangeFor(result.Data);
        Assert.Equal(QuranSyncAction.Changed, change.Action);
        Assert.Contains("arabic", change.Fields);
        Assert.Equal("الله لا إله إلا هو الحي القيوم", change.StoredArabic);
        Assert.Equal(CanonicalArabic, change.CanonicalArabic);

        // Nothing moved.
        Assert.Equal("الله لا إله إلا هو الحي القيوم", row.ArabicText);
        Assert.Equal(0, harness.Configuration.BumpCount);
        Assert.Empty(harness.Audit.Actions);
    }

    // ── Applying ──

    /// <summary>
    /// The Arabic is replaced with the Uthmani text, and the stored SearchText
    /// comes from the plain edition — not from folding the Uthmani, which would
    /// lose the alif the dagger mark stands in for. See QuranicSeedDataTests.
    /// </summary>
    [Fact]
    public async Task ApplyingWritesTheUthmaniTextAndFoldsThePlainEdition()
    {
        var harness = new Harness();
        var row = harness.SeedRow("الله لا إله إلا هو الحي القيوم");

        var result = await harness.Build().Apply();

        Assert.True(result.Success);
        Assert.True(result.Data!.Applied);
        Assert.Equal(CanonicalArabic, row.ArabicText);
        Assert.Equal(ArabicText.Normalize(CanonicalPlain), row.SearchText);
        Assert.Equal(CanonicalEnglish, row.Translations.Single(t => t.LanguageCode == "en").Translation);
        Assert.Equal(CanonicalArabic, row.Translations.Single(t => t.LanguageCode == "ar").Translation);
    }

    /// <summary>
    /// The content version is the whole sync protocol. An edit that reaches the
    /// database and not the phones is an edit that never happened.
    /// </summary>
    [Fact]
    public async Task ApplyingBumpsTheContentVersionAndAuditsEachRow()
    {
        var harness = new Harness();
        harness.SeedRow("الله لا إله إلا هو الحي القيوم");

        var result = await harness.Build().Apply();

        Assert.Equal(1, harness.Configuration.BumpCount);
        Assert.Equal(harness.Configuration.Version, result.Data!.ContentVersion);
        Assert.Equal(AuditActions.QuranAthkarSync, Assert.Single(harness.Audit.Actions));
    }

    /// <summary>
    /// Text that already matches is left entirely alone — no write, no version
    /// bump, no audit entry. A sync run twice must be indistinguishable from a
    /// sync run once, or the app re-downloads content on every admin's whim.
    /// </summary>
    [Fact]
    public async Task AMatchingRowIsNotTouchedAndIssuesNoNewVersion()
    {
        var harness = new Harness();
        harness.SeedRow(CanonicalArabic, CanonicalEnglish);

        // The seeded row folds its own Arabic; the sync stores the plain fold.
        // Line them up so this test is about "already in sync", not about that.
        harness.Adhkar.All[0].SearchText = ArabicText.Normalize(CanonicalPlain);

        var result = await harness.Build().Apply();

        Assert.True(result.Success);
        Assert.False(result.Data!.Applied);
        Assert.Equal(0, result.Data.Changed);
        Assert.Equal(0, harness.Configuration.BumpCount);
        Assert.Empty(harness.Audit.Actions);
    }

    // ── What it refuses to touch ──

    /// <summary>
    /// An editor who re-attributed a row has taken it out of the sync's reach,
    /// and that is the right outcome: it is no longer the row the source was
    /// asked about. Reported, never corrected.
    /// </summary>
    [Fact]
    public async Task ARowWhoseReferenceAnEditorChangedIsLeftAlone()
    {
        var harness = new Harness();
        var row = harness.SeedRow("النص القديم", locator: "البقرة: ٢٥٦");

        var result = await harness.Build().Apply();

        Assert.Equal("النص القديم", row.ArabicText);

        // Unmatched, so the sync offers to add the block rather than rewrite this row.
        Assert.Equal(1, result.Data!.Added);
        Assert.Equal(0, result.Data.Changed);
    }

    /// <summary>
    /// A row an editor has re-graded is no longer a plain Qur'anic citation.
    /// Nothing here is authoritative enough to overrule a person about that.
    /// </summary>
    [Fact]
    public async Task ARowAnEditorReGradedIsSkippedRatherThanRewritten()
    {
        var harness = new Harness();
        var row = harness.SeedRow("النص القديم", grade: HadithGrade.Sahih);

        var result = await harness.Build().Apply();

        Assert.Equal("النص القديم", row.ArabicText);
        Assert.Equal(1, result.Data!.Skipped);
        Assert.Equal(QuranSyncAction.Skipped, ChangeFor(result.Data).Action);
    }

    /// <summary>
    /// Missing blocks are created, with the attribution already attached — which
    /// is why publishing them cannot trip <see cref="ErrorCode.SourceRequired"/>.
    /// </summary>
    [Fact]
    public async Task AMissingBlockIsAddedWithItsSourceAttached()
    {
        var harness = new Harness();

        var result = await harness.Build().Apply();

        Assert.Equal(1, result.Data!.Added);

        var created = Assert.Single(harness.Adhkar.All);
        Assert.Equal(CanonicalArabic, created.ArabicText);
        Assert.Equal(QuranicAthkarCatalog.SourceBook, created.SourceBook);
        Assert.Equal(KursiLocator, created.SourceReference);
        Assert.Equal(HadithGrade.QuranVerse, created.Grade);
        Assert.True(created.IsPublished);
    }

    /// <summary>
    /// The sync never removes anything. A dhikr that has left the catalogue is
    /// an editor's business, and deleting content on a third party's say-so is
    /// not a power this endpoint has.
    /// </summary>
    [Fact]
    public async Task ARowOutsideTheCatalogueIsNeverDeleted()
    {
        var harness = new Harness();
        var stray = harness.SeedRow("ذكر أضافه المحرر", locator: "الأنعام: ١");

        await harness.Build().Apply();

        Assert.False(stray.IsDeleted);
        Assert.Contains(harness.Adhkar.All, d => d.Id == stray.Id && !d.IsDeleted);
    }

    /// <summary>
    /// Without the chapter there is nothing to reconcile against, and inventing
    /// one here would put a chapter on readers' screens that no editor created.
    /// </summary>
    [Fact]
    public async Task AMissingChapterFailsRatherThanCreatingOne()
    {
        var harness = new Harness(withCategory: false);

        var result = await harness.Build().Apply();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.CategoryNotFound, result.ErrorCode);
    }
}
