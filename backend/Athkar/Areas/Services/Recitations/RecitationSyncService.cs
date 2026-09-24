using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Recitations;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Recitations.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Recitations;

/// <summary>
/// Reads the publisher's reciter list and writes the parts an editor chose.
///
/// What a sync is allowed to touch is narrow, and deliberately so:
///
/// <list type="bullet">
/// <item>A <b>new</b> reciter or recording is written unpublished.</item>
/// <item>For a row that exists, only the publisher's own facts move — the
/// folder URL, the surah list, whether there is ayah timing. Those are the
/// facts that break playback when they go stale.</item>
/// <item>Names an editor wrote, the portrait, the featured flag, the order and
/// the published switch are the editor's and are never overwritten. A missing
/// English name is filled; an existing one is not replaced.</item>
/// <item>A reciter an editor deleted is skipped, and a recording the publisher
/// dropped is reported, never unpublished.</item>
/// </list>
/// </summary>
public class RecitationSyncService : IRecitationSyncService
{
    private readonly IMp3QuranClient client;
    private readonly IRepository<Reciter> reciters;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService configuration;
    private readonly ISecurityManager securityManager;
    private readonly Mp3QuranSettings settings;

    public RecitationSyncService(
        IMp3QuranClient client,
        IRepository<Reciter> reciters,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAppConfigurationService configuration,
        ISecurityManager securityManager,
        IOptions<Mp3QuranSettings> options)
    {
        this.client = client;
        this.reciters = reciters;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.configuration = configuration;
        this.securityManager = securityManager;
        settings = options.Value;
    }

    public Task<BaseResponse<RecitationSyncOutput>> Preview() => Run(null, apply: false);

    public Task<BaseResponse<RecitationSyncOutput>> Apply(RecitationSyncInput? input = null) =>
        Run(input, apply: true);

    private async Task<BaseResponse<RecitationSyncOutput>> Run(RecitationSyncInput? input, bool apply)
    {
        if (!client.IsEnabled)
            return BaseResponse<RecitationSyncOutput>.Fail(ErrorCode.RecitationSourceDisabled);

        IReadOnlyList<Mp3QuranReciter> publisher;
        try
        {
            publisher = await client.FetchCatalog();
        }
        catch (Mp3QuranException)
        {
            return BaseResponse<RecitationSyncOutput>.Fail(ErrorCode.RecitationSourceUnreachable);
        }

        // Deleted rows included: a reciter an editor removed must be
        // recognised as removed, not mistaken for one the console never had.
        var existing = await reciters.Query(includeDeleted: true)
            .Include(r => r.Translations)
            .Include(r => r.Recitations).ThenInclude(x => x.Translations)
            .ToListAsync();

        var byExternalId = existing
            .Where(r => r.ExternalId is not null)
            .GroupBy(r => r.ExternalId!.Value)
            // A live row outranks a deleted one with the same id.
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.IsDeleted).First());

        var chosen = input?.ExternalIds is { Count: > 0 } ids ? ids.ToHashSet() : null;

        var output = new RecitationSyncOutput
        {
            Applied = apply,
            Source = settings.SourceName,
            SourceUrl = settings.SourceUrl,
            PublisherReciters = publisher.Count,
            PublisherRecordings = publisher.Sum(r => r.Moshafs.Count),
            TimedRecordings = publisher.Sum(r => r.Moshafs.Count(m => m.TimingReadId is not null)),
        };

        var publishedTouched = false;
        var nextSort = existing.Where(r => !r.IsDeleted).Select(r => r.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        var usedKeys = existing.Where(r => !r.IsDeleted).Select(r => r.Key).ToHashSet();

        foreach (var source in publisher)
        {
            byExternalId.TryGetValue(source.Id, out var row);

            if (row is { IsDeleted: true })
            {
                output.SkippedDeleted++;
                continue;
            }

            var report = new RecitationSyncRow
            {
                ExternalId = source.Id,
                NameAr = source.NameAr,
                NameEn = source.NameEn,
                ReciterId = row?.Id,
                IsPublished = row?.IsPublished ?? false,
                Recordings = source.Moshafs.Count,
                TimedRecordings = source.Moshafs.Count(m => m.TimingReadId is not null),
            };

            if (row is null)
            {
                report.Status = RecitationSyncStatus.New;
                output.New++;
            }
            else
            {
                report.Changes = Differences(row, source, out var gone);
                output.Gone += gone;
                report.Status = report.Changes.Count > 0
                    ? RecitationSyncStatus.Changed
                    : RecitationSyncStatus.Unchanged;

                if (report.Status == RecitationSyncStatus.Changed) output.Changed++;
                else output.Unchanged++;
            }

            report.Chosen = report.Status != RecitationSyncStatus.Unchanged &&
                (chosen is null || chosen.Contains(source.Id));

            if (apply && report.Chosen)
            {
                if (row is null)
                {
                    var created = Create(source, nextSort++, UniqueKey(source.Id, usedKeys));
                    await reciters.AddAsync(created);
                }
                else
                {
                    publishedTouched |= Refresh(row, source);
                    reciters.Update(row);
                }

                output.Written++;
            }

            output.Rows.Add(report);
        }

        // Reciters the console holds that the publisher no longer lists.
        var listed = publisher.Select(r => r.Id).ToHashSet();
        foreach (var row in existing.Where(r =>
                     !r.IsDeleted && r.ExternalId is not null && !listed.Contains(r.ExternalId.Value)))
        {
            var recordings = row.Recitations.Count(x => !x.IsDeleted);
            output.Gone += recordings;
            output.Rows.Add(new RecitationSyncRow
            {
                ExternalId = row.ExternalId!.Value,
                NameAr = NameOf(row, ContentRules.SourceLanguage) ?? row.Key,
                NameEn = NameOf(row, "en"),
                ReciterId = row.Id,
                IsPublished = row.IsPublished,
                Recordings = recordings,
                Status = RecitationSyncStatus.Gone,
                Changes = ["recording-gone"],
            });
        }

        output.Rows =
        [
            .. output.Rows
                .OrderBy(r => r.Status)
                .ThenBy(r => r.NameAr, StringComparer.Ordinal),
        ];

        if (apply && output.Written > 0)
        {
            await unitOfWork.SaveAsync();

            // Only a change to something a reader can already see moves the
            // version. New drafts are invisible until an editor publishes them,
            // and that publish is what bumps it.
            if (publishedTouched) await configuration.BumpContentVersion();

            await auditService.LogAsync(AuditActions.RecitationSync, nameof(Reciter), null, null, new
            {
                output.Source,
                output.New,
                output.Changed,
                output.Written,
                output.Gone,
                output.SkippedDeleted,
                Chosen = chosen?.Order().ToList(),
            });
        }

        return new BaseResponse<RecitationSyncOutput>(output);
    }

    /// <summary>What differs between the console's row and the publisher's, as codes.</summary>
    private static List<string> Differences(Reciter row, Mp3QuranReciter source, out int gone)
    {
        var changes = new List<string>();
        var live = row.Recitations.Where(x => !x.IsDeleted).ToList();

        foreach (var moshaf in source.Moshafs)
        {
            var recording = live.FirstOrDefault(x => x.ExternalId == moshaf.Id);
            if (recording is null)
            {
                changes.Add("recording-new");
                continue;
            }

            if (!string.Equals(recording.ServerUrl, moshaf.Server, StringComparison.Ordinal))
                changes.Add("server");
            if (!string.Equals(recording.SurahList, moshaf.SurahList, StringComparison.Ordinal))
                changes.Add("surahs");
            if (recording.TimingReadId is null && moshaf.TimingReadId is not null)
                changes.Add("timing-added");
            else if (recording.TimingReadId is not null && moshaf.TimingReadId is null)
                changes.Add("timing-removed");
            else if (recording.TimingReadId != moshaf.TimingReadId)
                changes.Add("timing-added");
        }

        var listed = source.Moshafs.Select(m => m.Id).ToHashSet();
        gone = live.Count(x => x.ExternalId is not null && !listed.Contains(x.ExternalId.Value));
        if (gone > 0) changes.Add("recording-gone");

        if (!string.IsNullOrWhiteSpace(source.NameEn) && NameOf(row, "en") is null)
            changes.Add("name-en");

        return [.. changes.Distinct()];
    }

    private Reciter Create(Mp3QuranReciter source, int sortOrder, string key)
    {
        var reciter = new Reciter
        {
            Key = key,
            ExternalId = source.Id,
            SortOrder = sortOrder,
            IsPublished = false,
            CreatedBy = securityManager.UserId,
        };

        reciter.Translations.Add(new ReciterTranslation
        {
            LanguageCode = ContentRules.SourceLanguage,
            Name = source.NameAr,
        });
        if (!string.IsNullOrWhiteSpace(source.NameEn))
            reciter.Translations.Add(new ReciterTranslation { LanguageCode = "en", Name = source.NameEn });

        var order = 0;
        foreach (var moshaf in source.Moshafs)
            reciter.Recitations.Add(Build(moshaf, order++));

        return reciter;
    }

    /// <summary>
    /// Moves the publisher's facts onto an existing row. Returns whether a
    /// published recording of a published reciter moved — the only case a
    /// reader would notice.
    /// </summary>
    private bool Refresh(Reciter row, Mp3QuranReciter source)
    {
        var touched = false;
        var live = row.Recitations.Where(x => !x.IsDeleted).ToList();
        var order = live.Select(x => x.SortOrder).DefaultIfEmpty(-1).Max() + 1;

        foreach (var moshaf in source.Moshafs)
        {
            var recording = live.FirstOrDefault(x => x.ExternalId == moshaf.Id);
            if (recording is null)
            {
                row.Recitations.Add(Build(moshaf, order++));
                continue;
            }

            var moved = recording.ServerUrl != moshaf.Server ||
                        recording.SurahList != moshaf.SurahList ||
                        recording.TimingReadId != moshaf.TimingReadId;

            if (!moved) continue;

            recording.ServerUrl = moshaf.Server;
            recording.SurahList = moshaf.SurahList;
            recording.TimingReadId = moshaf.TimingReadId;
            recording.ModifiedBy = securityManager.UserId;
            recording.ModificationDate = DateTime.UtcNow;

            if (row.IsPublished && recording.IsPublished) touched = true;
        }

        if (!string.IsNullOrWhiteSpace(source.NameEn) && NameOf(row, "en") is null)
        {
            row.Translations.Add(new ReciterTranslation { LanguageCode = "en", Name = source.NameEn });
            if (row.IsPublished) touched = true;
        }

        row.ModifiedBy = securityManager.UserId;
        return touched;
    }

    private Recitation Build(Mp3QuranMoshaf moshaf, int sortOrder)
    {
        var recording = new Recitation
        {
            ExternalId = moshaf.Id,
            ServerUrl = moshaf.Server,
            SurahList = moshaf.SurahList,
            TimingReadId = moshaf.TimingReadId,
            SourceName = settings.SourceName,
            SourceUrl = settings.SourceUrl,
            SortOrder = sortOrder,
            IsPublished = false,
            CreatedBy = securityManager.UserId,
        };

        recording.Translations.Add(new RecitationTranslation
        {
            LanguageCode = ContentRules.SourceLanguage,
            Name = moshaf.NameAr,
        });
        if (!string.IsNullOrWhiteSpace(moshaf.NameEn))
            recording.Translations.Add(new RecitationTranslation { LanguageCode = "en", Name = moshaf.NameEn });

        return recording;
    }

    private static string UniqueKey(int externalId, HashSet<string> used)
    {
        var key = $"mp3quran-{externalId}";
        for (var n = 2; used.Contains(key); n++) key = $"mp3quran-{externalId}-{n}";
        used.Add(key);
        return key;
    }

    private static string? NameOf(Reciter row, string language) =>
        row.Translations.FirstOrDefault(t => !t.IsDeleted && t.LanguageCode == language)?.Name;
}
