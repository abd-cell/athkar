using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Recitations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Recitations;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// Refreshing the reciter catalogue from its publisher, and curating it.
///
/// The tests worth having are about the boundary the sync must not cross: it
/// writes drafts and never publishes, it moves the publisher's own facts (the
/// folder, the surah list, the timing) and never an editor's (names, portrait,
/// the published switch), and it does not resurrect what an editor deleted.
/// Which reciters a waqf app offers is an editorial decision; a sync that made
/// it would hand that decision to whoever edits someone else's list.
/// </summary>
public class RecitationSyncServiceTests
{
    private sealed class FakeMp3QuranClient : IMp3QuranClient
    {
        public bool IsEnabled { get; set; } = true;
        public Mp3QuranException? Fault { get; set; }
        public List<Mp3QuranReciter> Catalog { get; } = [];

        public Task<IReadOnlyList<Mp3QuranReciter>> FetchCatalog(CancellationToken cancellation = default)
        {
            if (Fault is not null) throw Fault;
            return Task.FromResult<IReadOnlyList<Mp3QuranReciter>>(Catalog);
        }
    }

    private sealed class Harness
    {
        public FakeMp3QuranClient Client { get; } = new();
        public InMemoryRepository<Reciter> Reciters { get; } = new();
        public InMemoryRepository<ReciterTranslation> ReciterNames { get; } = new();
        public InMemoryRepository<Recitation> Recordings { get; } = new();
        public InMemoryRepository<RecitationTranslation> RecordingNames { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeAuditService Audit { get; } = new();
        public FakeAppConfigurationService Configuration { get; } = new();

        public static Mp3QuranMoshaf Moshaf(int id, string server = "https://server12.mp3quran.net/maher/",
            string surahs = "1,2,3", int? timing = null) =>
            new(id, "حفص عن عاصم - مرتل", "Hafs - Murattal", server, surahs, 3, timing);

        public Harness Publisher(int id, string nameAr, string? nameEn, params Mp3QuranMoshaf[] moshafs)
        {
            Client.Catalog.Add(new Mp3QuranReciter(id, nameAr, nameEn, moshafs));
            return this;
        }

        /// <summary>A reciter the console already holds, as an earlier sync and an editor left him.</summary>
        public Reciter Existing(int externalId, string name, bool published, params Recitation[] recordings)
        {
            var reciter = new Reciter
            {
                Key = $"mp3quran-{externalId}",
                ExternalId = externalId,
                IsPublished = published,
            };
            reciter.Translations.Add(new ReciterTranslation { LanguageCode = "ar", Name = name });
            foreach (var recording in recordings) reciter.Recitations.Add(recording);

            Reciters.Seed(reciter);
            return reciter;
        }

        public static Recitation Recording(int externalId, bool published, string server = "https://server12.mp3quran.net/maher/",
            string surahs = "1,2,3", int? timing = null)
        {
            var recording = new Recitation
            {
                ExternalId = externalId,
                ServerUrl = server,
                SurahList = surahs,
                TimingReadId = timing,
                SourceName = "MP3Quran",
                IsPublished = published,
            };
            recording.Translations.Add(new RecitationTranslation { LanguageCode = "ar", Name = "حفص عن عاصم - مرتل" });
            return recording;
        }

        public RecitationSyncService Sync() => new(
            Client, Reciters, UnitOfWork, Audit, Configuration, new FakeSecurityManager(),
            Options.Create(new Mp3QuranSettings { Enabled = true }));

        public RecitationAdminService Admin() => new(
            Reciters, ReciterNames, Recordings, RecordingNames, UnitOfWork, Audit, Configuration,
            new FakeSecurityManager());
    }

    [Fact]
    public async Task ASwitchedOffSourceIsReportedAsASettingNotAFault()
    {
        var harness = new Harness();
        harness.Client.IsEnabled = false;

        var result = await harness.Sync().Preview();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RecitationSourceDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task AnUnreachablePublisherWritesNothing()
    {
        var harness = new Harness();
        harness.Existing(102, "ماهر المعيقلي", published: true, Harness.Recording(102, published: true));
        harness.Client.Fault = new Mp3QuranException("down");

        var result = await harness.Sync().Apply();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RecitationSourceUnreachable, result.ErrorCode);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
        Assert.Single(harness.Reciters.All);
    }

    [Fact]
    public async Task ThePreviewWritesNothing()
    {
        var harness = new Harness().Publisher(102, "ماهر المعيقلي", "Maher Al Meaqli", Harness.Moshaf(102));

        var result = await harness.Sync().Preview();

        Assert.True(result.Success);
        Assert.False(result.Data!.Applied);
        Assert.Equal(1, result.Data.New);
        Assert.Empty(harness.Reciters.All);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    /// <summary>The rule the whole slice turns on.</summary>
    [Fact]
    public async Task ApplyNeverPublishes()
    {
        var harness = new Harness()
            .Publisher(102, "ماهر المعيقلي", "Maher Al Meaqli", Harness.Moshaf(102, timing: 5), Harness.Moshaf(133));

        var result = await harness.Sync().Apply();

        Assert.True(result.Success);
        Assert.Equal(0, result.Data!.Published);

        var reciter = Assert.Single(harness.Reciters.All);
        Assert.False(reciter.IsPublished);
        Assert.All(reciter.Recitations, recording => Assert.False(recording.IsPublished));

        // Nothing a reader can see moved, so the version must not either.
        Assert.Equal(0, harness.Configuration.BumpCount);
        Assert.Contains(AuditActions.RecitationSync, harness.Audit.Actions);
    }

    [Fact]
    public async Task ANewReciterCarriesBothNamesTheCreditAndItsTiming()
    {
        var harness = new Harness()
            .Publisher(102, "ماهر المعيقلي", "Maher Al Meaqli", Harness.Moshaf(102, timing: 5));

        await harness.Sync().Apply();

        var reciter = Assert.Single(harness.Reciters.All);
        Assert.Equal("mp3quran-102", reciter.Key);
        Assert.Contains(reciter.Translations, t => t.LanguageCode == "ar" && t.Name == "ماهر المعيقلي");
        Assert.Contains(reciter.Translations, t => t.LanguageCode == "en" && t.Name == "Maher Al Meaqli");

        var recording = Assert.Single(reciter.Recitations);
        Assert.Equal(5, recording.TimingReadId);
        Assert.Equal("MP3Quran", recording.SourceName);
    }

    [Fact]
    public async Task OnlyTheReciterTheEditorChoseIsWritten()
    {
        var harness = new Harness()
            .Publisher(102, "ماهر المعيقلي", null, Harness.Moshaf(102))
            .Publisher(54, "عبدالرحمن السديس", null, Harness.Moshaf(54));

        var result = await harness.Sync().Apply(new RecitationSyncInput { ExternalIds = [54] });

        var reciter = Assert.Single(harness.Reciters.All);
        Assert.Equal(54, reciter.ExternalId);
        Assert.Equal(1, result.Data!.Written);
        Assert.Contains(result.Data.Rows, r => r.ExternalId == 102 && !r.Chosen);
    }

    /// <summary>
    /// The publisher's facts move; the editor's do not. A folder URL that went
    /// stale breaks playback, so it is refreshed — but the name an editor
    /// corrected and the switch they flipped are theirs.
    /// </summary>
    [Fact]
    public async Task ARefreshMovesThePublishersFactsAndKeepsTheEditors()
    {
        var harness = new Harness();
        var existing = harness.Existing(102, "الشيخ ماهر المعيقلي", published: true,
            Harness.Recording(102, published: true, server: "https://old.mp3quran.net/maher/"));
        existing.IsFeatured = true;

        harness.Publisher(102, "ماهر المعيقلي", "Maher Al Meaqli",
            Harness.Moshaf(102, server: "https://server12.mp3quran.net/maher/", timing: 5));

        var preview = await harness.Sync().Preview();
        var row = Assert.Single(preview.Data!.Rows);
        Assert.Equal(RecitationSyncStatus.Changed, row.Status);
        Assert.Contains("server", row.Changes);
        Assert.Contains("timing-added", row.Changes);
        Assert.Contains("name-en", row.Changes);

        await harness.Sync().Apply();

        var recording = Assert.Single(existing.Recitations);
        Assert.Equal("https://server12.mp3quran.net/maher/", recording.ServerUrl);
        Assert.Equal(5, recording.TimingReadId);
        Assert.True(recording.IsPublished);

        Assert.True(existing.IsPublished);
        Assert.True(existing.IsFeatured);
        Assert.Contains(existing.Translations, t => t.LanguageCode == "ar" && t.Name == "الشيخ ماهر المعيقلي");
        Assert.Contains(existing.Translations, t => t.LanguageCode == "en" && t.Name == "Maher Al Meaqli");

        // A published recording's URL moved under readers' feet: they must hear about it.
        Assert.Equal(1, harness.Configuration.BumpCount);
    }

    [Fact]
    public async Task ANewRecordingOfAPublishedReciterArrivesAsADraft()
    {
        var harness = new Harness();
        var existing = harness.Existing(102, "ماهر المعيقلي", published: true, Harness.Recording(102, published: true));
        harness.Publisher(102, "ماهر المعيقلي", null, Harness.Moshaf(102), Harness.Moshaf(133, server: "https://server12.mp3quran.net/maher/Almusshaf-Al-Mojawwad/"));

        await harness.Sync().Apply();

        Assert.Equal(2, existing.Recitations.Count);
        Assert.False(existing.Recitations.Single(x => x.ExternalId == 133).IsPublished);
        Assert.Equal(0, harness.Configuration.BumpCount);
    }

    [Fact]
    public async Task AReciterAnEditorDeletedIsNotResurrected()
    {
        var harness = new Harness();
        var deleted = harness.Existing(102, "ماهر المعيقلي", published: false, Harness.Recording(102, published: false));
        deleted.IsDeleted = true;
        harness.Publisher(102, "ماهر المعيقلي", null, Harness.Moshaf(102));

        var result = await harness.Sync().Apply();

        Assert.Equal(1, result.Data!.SkippedDeleted);
        Assert.Equal(0, result.Data.Written);
        Assert.Single(harness.Reciters.All);
    }

    /// <summary>
    /// A folder the publisher stopped listing is reported, never unpublished: an
    /// afternoon's outage at the publisher is not a reason to take a reciter off
    /// every phone.
    /// </summary>
    [Fact]
    public async Task ARecordingThePublisherDroppedIsReportedNotWithdrawn()
    {
        var harness = new Harness();
        var existing = harness.Existing(102, "ماهر المعيقلي", published: true, Harness.Recording(102, published: true));

        var result = await harness.Sync().Apply();

        var row = Assert.Single(result.Data!.Rows);
        Assert.Equal(RecitationSyncStatus.Gone, row.Status);
        Assert.Equal(1, result.Data.Gone);
        Assert.True(existing.IsPublished);
        Assert.True(existing.Recitations.Single().IsPublished);
    }

    [Fact]
    public async Task AReciterCannotBePublishedWithNothingToPlay()
    {
        var harness = new Harness();
        var reciter = harness.Existing(102, "ماهر المعيقلي", published: false, Harness.Recording(102, published: false));

        var result = await harness.Admin().UpdateReciter(reciter.Id, new ReciterInput
        {
            IsPublished = true,
            Translations = [new TranslationInput { LanguageCode = "ar", Title = "ماهر المعيقلي" }],
        });

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationError, result.ErrorCode);
        Assert.False(reciter.IsPublished);
    }

    [Fact]
    public async Task WithdrawingTheLastRecordingWithdrawsTheReciter()
    {
        var harness = new Harness();
        var reciter = harness.Existing(102, "ماهر المعيقلي", published: true, Harness.Recording(102, published: true));
        reciter.Recitations.Single().Id = 7;

        var result = await harness.Admin().UpdateRecitation(reciter.Id, 7, new RecitationInput
        {
            IsPublished = false,
            Translations = [new TranslationInput { LanguageCode = "ar", Title = "حفص عن عاصم - مرتل" }],
        });

        Assert.True(result.Success);
        Assert.False(reciter.IsPublished);
        Assert.Equal(1, harness.Configuration.BumpCount);
    }

    [Theory]
    [InlineData("المصحف المجود - المصحف المجود", "المصحف المجود")]
    [InlineData("حفص عن عاصم - مرتل", "حفص عن عاصم - مرتل")]
    [InlineData("  ورش عن نافع  ", "ورش عن نافع")]
    public void APublisherNameSaidTwiceIsSaidOnce(string raw, string expected) =>
        Assert.Equal(expected, Mp3QuranClient.Tidy(raw));

    [Fact]
    public void ASurahListIsReadDefensively() =>
        Assert.Equal([1, 2, 114], RecitationSurahs.Parse(" 2,1, 114,115,0,x,2 "));
}
