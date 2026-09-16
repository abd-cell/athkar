using Microsoft.Extensions.Logging.Abstractions;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Services.Content;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Text;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// Filling the imported drafts' takhrij from حصن المسلم's own footnotes.
///
/// The import leaves 250 rows unattributed and therefore unpublishable, and this
/// is what fills them. The tests worth having are the ones about the boundary it
/// must not cross: it writes attribution, it never publishes, and it never
/// overwrites an editor's own reading — including when the book disagrees with
/// it. A sync that published what it filled would take the single rule this
/// project is built on and route around it in one click.
/// </summary>
public class TakhrijSyncServiceTests
{
    private static TakhrijCatalog.TakhrijEntry FirstEntryWithReference =>
        TakhrijCatalog.Entries.First(e => !string.IsNullOrWhiteSpace(e.Reference));

    private sealed class Harness
    {
        public InMemoryRepository<Dhikr> Adhkar { get; } = new();
        public InMemoryRepository<AthkarCategory> Categories { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeAuditService Audit { get; } = new();

        public Harness() => Categories.Seed(new AthkarCategory { Id = 1, Key = "hisn-1" });

        /// <summary>
        /// A row whose folded text is one the catalog carries, so the sync has
        /// something to match. Folded exactly as the service folds it — the
        /// match is on `SearchText`, not on the vocalised original.
        /// </summary>
        public Dhikr SeedMatching(TakhrijCatalog.TakhrijEntry entry, bool published = false)
        {
            var row = new Dhikr
            {
                Id = Adhkar.All.Count + 1,
                CategoryId = 1,
                ArabicText = entry.Fold,
                SearchText = ArabicText.Normalize(entry.Fold),
                IsPublished = published,
            };
            Adhkar.Seed(row);
            return row;
        }

        public TakhrijSyncService Build() => new(
            Adhkar, Categories, UnitOfWork, Audit,
            new FakeSecurityManager(), NullLogger<TakhrijSyncService>.Instance);
    }

    /// <summary>
    /// The shipped file is the book's footnotes, and every entry in it names a
    /// book. An entry without one would put an empty attribution on a row and
    /// make it look attributed.
    /// </summary>
    [Fact]
    public void EveryCatalogEntryNamesABook()
    {
        Assert.NotEmpty(TakhrijCatalog.Entries);
        Assert.All(TakhrijCatalog.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Book)));
        Assert.All(TakhrijCatalog.Entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Fold)));

        // Matched by folded text, so two entries under one key would make the
        // attribution depend on which one happened to be read first.
        Assert.Equal(
            TakhrijCatalog.Entries.Count,
            TakhrijCatalog.Entries.Select(e => e.Fold).Distinct().Count());
    }

    /// <summary>
    /// Every grading in the file is one the project can publish. The enum has no
    /// member for weak on purpose, and a grade read out of a footnote must not
    /// be the thing that introduces one.
    /// </summary>
    [Fact]
    public void EveryStatedGradingIsOneThisProjectPublishes()
    {
        var allowed = Enum.GetValues<HadithGrade>().Select(g => (int)g).ToHashSet();

        Assert.All(
            TakhrijCatalog.Entries.Where(e => e.Grade.HasValue),
            entry => Assert.Contains(entry.Grade!.Value, allowed));
    }

    /// <summary>A preview reports and writes nothing at all.</summary>
    [Fact]
    public async Task PreviewReportsWithoutWriting()
    {
        var harness = new Harness();
        var row = harness.SeedMatching(FirstEntryWithReference);

        var result = await harness.Build().Preview();

        Assert.True(result.Success);
        Assert.False(result.Data!.Applied);
        Assert.Equal(1, result.Data.Matched);
        Assert.Null(row.SourceBook);
        Assert.Empty(harness.Audit.Actions);
    }

    /// <summary>Applying writes the book and the place in it, from the footnote.</summary>
    [Fact]
    public async Task ApplyFillsTheAttributionFromTheFootnote()
    {
        var entry = FirstEntryWithReference;
        var harness = new Harness();
        var row = harness.SeedMatching(entry);

        var result = await harness.Build().Apply();

        Assert.True(result.Data!.Applied);
        Assert.Equal(entry.Book, row.SourceBook);
        Assert.Equal(entry.Reference, row.SourceReference);
        Assert.Single(harness.Audit.Actions);
    }

    /// <summary>
    /// The rule this whole service is shaped around: it makes rows publishable
    /// and leaves them unpublished. An editor reads the takhrij and publishes;
    /// a file does not get to do that for them.
    /// </summary>
    [Fact]
    public async Task ApplyNeverPublishes()
    {
        var harness = new Harness();
        var row = harness.SeedMatching(FirstEntryWithReference);

        var result = await harness.Build().Apply();

        Assert.False(row.IsPublished);
        Assert.Equal(0, result.Data!.Published);
    }

    /// <summary>
    /// A row an editor already attributed is left exactly as it is, and when the
    /// book's footnote names a different source that disagreement is reported
    /// rather than resolved by overwriting.
    /// </summary>
    [Fact]
    public async Task AnEditorsOwnAttributionSurvivesADisagreement()
    {
        var entry = TakhrijCatalog.Entries.First(e =>
            !string.IsNullOrWhiteSpace(e.Reference) && e.Book != "صحيح مسلم");

        var harness = new Harness();
        var row = harness.SeedMatching(entry);
        row.SourceBook = "صحيح مسلم";
        row.SourceReference = "٢٧٢٣";

        var result = await harness.Build().Apply();

        Assert.Equal("صحيح مسلم", row.SourceBook);
        Assert.Equal("٢٧٢٣", row.SourceReference);
        Assert.Equal(1, result.Data!.Disagreements);
        Assert.Empty(harness.Audit.Actions);
    }

    /// <summary>
    /// The choice is the point of the two steps: a check proposes an attribution
    /// for several rows, the editor takes one, and the rest stay exactly as they
    /// were. A sync that wrote everything the moment it was asked about one row
    /// would be a check that changed things.
    /// </summary>
    [Fact]
    public async Task OnlyTheRowsTheEditorChoseAreWritten()
    {
        var entries = TakhrijCatalog.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Reference))
            .Take(2).ToArray();

        var harness = new Harness();
        var taken = harness.SeedMatching(entries[0]);
        var left = harness.SeedMatching(entries[1]);

        var result = await harness.Build().Apply(new TakhrijSyncInput { DhikrIds = [taken.Id] });

        Assert.Equal(entries[0].Book, taken.SourceBook);
        Assert.Null(left.SourceBook);
        Assert.Equal(1, result.Data!.Filled);
        Assert.Equal(2, result.Data.Matched);
        Assert.Single(harness.Audit.Actions);
    }

    /// <summary>
    /// Running it twice changes nothing the second time — and writes no audit
    /// row saying a field changed from a value to itself.
    /// </summary>
    [Fact]
    public async Task ASecondRunIsANoOp()
    {
        var harness = new Harness();
        harness.SeedMatching(FirstEntryWithReference);

        await harness.Build().Apply();
        var second = await harness.Build().Apply();

        Assert.Equal(0, second.Data!.Matched);
        Assert.Equal(1, second.Data.AlreadyAttributed);
        Assert.Single(harness.Audit.Actions);
    }

    /// <summary>
    /// A dhikr the footnotes do not account for is reported and left alone —
    /// the seven أبواب whose footnotes do not line up land here, and that is the
    /// designed outcome rather than a gap to be filled by guessing.
    /// </summary>
    [Fact]
    public async Task AnUnmatchedDhikrIsLeftForAnEditor()
    {
        var harness = new Harness();
        var row = new Dhikr
        {
            Id = 99,
            CategoryId = 1,
            ArabicText = "نصّ لا وجود له في حواشي الكتاب",
            SearchText = ArabicText.Normalize("نصّ لا وجود له في حواشي الكتاب"),
        };
        harness.Adhkar.Seed(row);

        var result = await harness.Build().Apply();

        Assert.Equal(1, result.Data!.Unmatched);
        Assert.Null(row.SourceBook);
        Assert.False(row.IsPublished);
    }
}
