using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.Areas.Services.Audit;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Shareds.Notifications.Fcm;

namespace Athkar.Areas.Services.Notifications;

public class PushManagerService : IPushManagerService
{
    /// <summary>Matches the dashboard's own definition of an active install.</summary>
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);

    private const int MaxWindowDays = 90;
    private const int RecentFailureCount = 25;

    private readonly IRepository<Device> devices;
    private readonly IRepository<PushDispatch> dispatches;
    private readonly IRepository<Broadcast> broadcasts;
    private readonly IRepository<ReminderCampaign> campaigns;
    private readonly IBroadcastService broadcastService;
    private readonly IPushDispatcher dispatcher;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IFcmSender fcm;

    public PushManagerService(
        IRepository<Device> devices,
        IRepository<PushDispatch> dispatches,
        IRepository<Broadcast> broadcasts,
        IRepository<ReminderCampaign> campaigns,
        IBroadcastService broadcastService,
        IPushDispatcher dispatcher,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IFcmSender fcm)
    {
        this.devices = devices;
        this.dispatches = dispatches;
        this.broadcasts = broadcasts;
        this.campaigns = campaigns;
        this.broadcastService = broadcastService;
        this.dispatcher = dispatcher;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.fcm = fcm;
    }

    public async Task<BaseResponse<PushOverviewOutput>> Overview(int windowDays = 7)
    {
        var days = Math.Clamp(windowDays, 1, MaxWindowDays);
        var now = DateTime.UtcNow;
        var since = now.AddDays(-days);
        var activeSince = now - ActiveWindow;

        return new BaseResponse<PushOverviewOutput>(new PushOverviewOutput
        {
            IsFcmConfigured = fcm.IsConfigured,
            Reach = await Reach(activeSince),
            Queue = await Queue(now),
            Outcomes = await Outcomes(since, days),
            RecentFailures = await RecentFailures(since),
        });
    }

    // ───────────────────────────────── sending ───────────────────────────────

    public async Task<BaseResponse<BroadcastOutput>> Send(BroadcastInput input)
    {
        // Now, never later. A message with a time on it is a campaign, and a
        // campaign needs the broadcasts screen — somewhere it can be read back,
        // edited and withdrawn before it goes.
        input.ScheduledAtUtc = null;

        var created = await broadcastService.Create(input);
        if (!created.Success || created.Data is null) return created;

        // A draft survives a failure here rather than being cleaned up. That is
        // deliberate: the admin's words are the expensive part, and a draft
        // waiting on the broadcasts screen can be sent again, where a tidy
        // rollback would have thrown them away.
        return await broadcastService.Send(created.Data.Id);
    }

    // ────────────────────────────── delivery log ─────────────────────────────

    public async Task<BaseResponse<PageOutput<PushDispatchOutput>>> Dispatches(
        PushDispatchQueryInput input)
    {
        var query = dispatches.Query();

        if (input.DispatchId is { } dispatchId) query = query.Where(d => d.Id == dispatchId);
        if (input.Status is { } status) query = query.Where(d => d.Status == status);
        if (input.BroadcastId is { } broadcastId) query = query.Where(d => d.BroadcastId == broadcastId);
        if (input.CampaignId is { } campaignId) query = query.Where(d => d.CampaignId == campaignId);
        if (input.FromUtc is { } from) query = query.Where(d => d.ScheduledAtUtc >= from);
        if (input.ToUtc is { } to) query = query.Where(d => d.ScheduledAtUtc <= to);

        // Filtered through the navigation rather than by looking the device up
        // first: one query either way, and a device key that matches nothing
        // then yields an empty page instead of a not-found the screen would
        // have to render differently.
        if (input.Platform is { } platform)
            query = query.Where(d => d.Device != null && d.Device.Platform == platform);

        if (!string.IsNullOrWhiteSpace(input.LanguageCode))
        {
            var code = input.LanguageCode.Trim().ToLowerInvariant();
            query = query.Where(d => d.Device != null && d.Device.LanguageCode == code);
        }

        if (!string.IsNullOrWhiteSpace(input.DeviceKey))
        {
            var key = input.DeviceKey.Trim();
            query = query.Where(d => d.Device != null && d.Device.DeviceKey == key);
        }

        var total = await query.CountAsync();

        // Newest first, and by id within an instant: a broadcast writes every
        // one of its rows with the same instant, and an unstable order would
        // shuffle them between pages and show the same row twice.
        var rows = await query
            .OrderByDescending(d => d.ScheduledAtUtc)
            .ThenByDescending(d => d.Id)
            .Paginate(input)
            // Spelled out as conditionals for the same reason RecentFailures
            // does it: exactly one of campaign and broadcast is set on any row,
            // so `!` would throw the moment anything but EF ran the projection.
            .Select(d => new PushDispatchOutput
            {
                Id = d.Id,
                ScheduledAtUtc = d.ScheduledAtUtc,
                SentAtUtc = d.SentAtUtc,
                Status = d.Status,
                Attempts = d.Attempts,
                MessageId = d.MessageId,
                Error = d.Error,
                CampaignId = d.CampaignId,
                BroadcastId = d.BroadcastId,
                Source = d.Campaign == null
                    ? (d.BroadcastId == null ? "—" : "broadcast #" + d.BroadcastId)
                    : d.Campaign.Key,
                DeviceId = d.DeviceId,
                DeviceKey = d.Device == null ? "" : d.Device.DeviceKey,
                Platform = d.Device == null ? DevicePlatform.Unknown : d.Device.Platform,
                LanguageCode = d.Device == null ? "" : d.Device.LanguageCode,
            })
            .ToListAsync();

        await Headlines(rows);

        foreach (var row in rows)
        {
            row.CanRetry = Retryable(row.Status);
            row.CanCancel = row.Status == PushStatus.Pending;
        }

        return new BaseResponse<PageOutput<PushDispatchOutput>>(new PageOutput<PushDispatchOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    /// <summary>
    /// Fills in each row's headline, in the language that row was addressed in.
    ///
    /// Two queries for the whole page rather than a correlated sub-select per
    /// row, which would be a hundred round trips for a page of a hundred.
    ///
    /// Deleted sources are read too, and that is the point rather than an
    /// oversight. This is a history: a broadcast is routinely deleted in the
    /// days after it goes out, and excluding it the way every other query here
    /// does left the log saying a dash where the message used to be — the rows
    /// most worth reading back are exactly the ones somebody has since tidied
    /// away. A live translation still wins over a withdrawn one; the withdrawn
    /// one is what remains when there is nothing else left to show.
    /// </summary>
    private async Task Headlines(List<PushDispatchOutput> rows)
    {
        if (rows.Count == 0) return;

        var campaignIds = rows.Where(r => r.CampaignId is not null)
            .Select(r => r.CampaignId!.Value).Distinct().ToList();

        var broadcastIds = rows.Where(r => r.BroadcastId is not null)
            .Select(r => r.BroadcastId!.Value).Distinct().ToList();

        var campaignText = campaignIds.Count == 0
            ? []
            : await campaigns.Query(includeDeleted: true)
                .Where(c => campaignIds.Contains(c.Id))
                .SelectMany(c => c.Translations,
                    (c, t) => new Headline(c.Id, t.LanguageCode, t.Title, t.IsDeleted))
                .ToListAsync();

        var broadcastText = broadcastIds.Count == 0
            ? []
            : await broadcasts.Query(includeDeleted: true)
                .Where(b => broadcastIds.Contains(b.Id))
                .SelectMany(b => b.Translations,
                    (b, t) => new Headline(b.Id, t.LanguageCode, t.Title, t.IsDeleted))
                .ToListAsync();

        foreach (var row in rows)
        {
            var candidates = row.CampaignId is { } campaignId
                ? campaignText.Where(t => t.SourceId == campaignId).ToList()
                : row.BroadcastId is { } broadcastId
                    ? broadcastText.Where(t => t.SourceId == broadcastId).ToList()
                    : [];

            // Live first, then the row's own language, then anything at all: a
            // dispatch addressed in a language since withdrawn still has to say
            // what it said.
            row.Title = (
                candidates.FirstOrDefault(t => !t.IsDeleted && t.LanguageCode == row.LanguageCode)
                ?? candidates.FirstOrDefault(t => !t.IsDeleted)
                ?? candidates.FirstOrDefault(t => t.LanguageCode == row.LanguageCode)
                ?? candidates.FirstOrDefault())?.Title;
        }
    }

    /// <summary>One source's wording in one language, flattened for the lookup above.</summary>
    private sealed record Headline(int SourceId, string LanguageCode, string Title, bool IsDeleted);

    // ─────────────────────────── retry and cancel ────────────────────────────

    /// <summary>
    /// The one place the rule lives — the CMS reads it off the row rather than
    /// working it out again. A sent dispatch is not retryable because the reader
    /// already has the message; a pending one is not retryable because it has
    /// not been tried yet.
    /// </summary>
    private static bool Retryable(PushStatus status) =>
        status is PushStatus.Failed or PushStatus.TokenExpired or PushStatus.Skipped;

    public async Task<BaseResponse<PushDispatchOutput>> Retry(int id)
    {
        var dispatch = await dispatches.GetByIdAsync(id);
        if (dispatch is null) return BaseResponse<PushDispatchOutput>.Fail(ErrorCode.DispatchNotFound);

        if (!Retryable(dispatch.Status))
            return BaseResponse<PushDispatchOutput>.Fail(ErrorCode.DispatchNotRetryable);

        dispatch.Status = PushStatus.Pending;
        dispatch.Attempts = 0;
        dispatch.Error = null;
        dispatch.MessageId = null;
        dispatch.SentAtUtc = null;

        // Moved to now, not left where it was. The sender drops anything older
        // than the grace window, so a row retried an hour after it failed would
        // be skipped again on the very next pass — and the admin would see the
        // button do nothing at all.
        dispatch.ScheduledAtUtc = DateTime.UtcNow;

        dispatches.Update(dispatch);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.DispatchRetry, nameof(PushDispatch), dispatch.Id);

        return await One(dispatch.Id);
    }

    public async Task<BaseResponse<PushDispatchOutput>> CancelDispatch(int id)
    {
        var dispatch = await dispatches.GetByIdAsync(id);
        if (dispatch is null) return BaseResponse<PushDispatchOutput>.Fail(ErrorCode.DispatchNotFound);

        if (dispatch.Status != PushStatus.Pending)
            return BaseResponse<PushDispatchOutput>.Fail(ErrorCode.DispatchNotCancellable);

        // Skipped, not deleted: the row is the evidence that somebody stopped
        // this on purpose, and a missing row reads as a bug in the sender.
        dispatch.Status = PushStatus.Skipped;
        dispatch.SentAtUtc = DateTime.UtcNow;
        dispatch.Error = "Cancelled by an administrator.";

        dispatches.Update(dispatch);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.DispatchCancel, nameof(PushDispatch), dispatch.Id);

        return await One(dispatch.Id);
    }

    /// <summary>Re-reads one row through the same projection the list uses.</summary>
    private async Task<BaseResponse<PushDispatchOutput>> One(int id)
    {
        var page = await Dispatches(new PushDispatchQueryInput { PageSize = 1, DispatchId = id });
        var row = page.Data?.Data.FirstOrDefault();

        return row is null
            ? BaseResponse<PushDispatchOutput>.Fail(ErrorCode.DispatchNotFound)
            : new BaseResponse<PushDispatchOutput>(row);
    }

    // ──────────────────────────────── run now ────────────────────────────────

    public async Task<BaseResponse<PushRunOutput>> RunNow()
    {
        // The workers' own order, kept: a reminder that materialises in this
        // pass and a broadcast that fans out in it are both then sent by the
        // third, so one press does the whole thing rather than a third of it.
        var output = new PushRunOutput
        {
            Materialised = await dispatcher.MaterialiseReminders(),
            BroadcastsStarted = await dispatcher.StartDueBroadcasts(),
        };

        output.Attempted = await dispatcher.SendDue();

        await auditService.LogAsync(AuditActions.PushRunNow, nameof(PushDispatch), null, null, output);

        return new BaseResponse<PushRunOutput>(output);
    }

    // ───────────────────────────────── reach ─────────────────────────────────

    private async Task<PushReachOutput> Reach(DateTime activeSince)
    {
        // Grouped in the database rather than counted in memory. The obvious
        // version — pull every row and run a foreach — is a full table scan
        // materialised in the web process, and this screen is opened by
        // somebody who is already worried about something.
        //
        // The key is the triple, so each combination of the three flags falls
        // into exactly one group and the buckets below cannot double-count. If
        // they could, the columns would stop summing to the total and the
        // screen would quietly lie.
        var groups = await devices.Query()
            .GroupBy(d => new
            {
                HasToken = d.PushToken != null && d.PushToken != "",
                d.NotificationsEnabled,
                IsActive = d.LastSeenAt >= activeSince,
            })
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();

        var reach = new PushReachOutput();

        foreach (var group in groups)
        {
            reach.TotalDevices += group.Count;

            // Same ladder as the sender's own skip test, in the same order.
            if (!group.Key.HasToken) reach.Tokenless += group.Count;
            else if (!group.Key.NotificationsEnabled) reach.Muted += group.Count;
            else if (!group.Key.IsActive) reach.Stale += group.Count;
            else reach.Reachable += group.Count;
        }

        if (reach.Reachable == 0) return reach;

        // Only the reachable are broken down: the question the breakdown
        // answers is "who would this broadcast land on", so counting a device
        // that cannot receive it would make it the wrong answer.
        var reachable = devices.Query()
            .Where(d => d.PushToken != null && d.PushToken != "")
            .Where(d => d.NotificationsEnabled && d.LastSeenAt >= activeSince);

        reach.ReachableByPlatform = await reachable
            .GroupBy(d => d.Platform)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count);

        reach.ReachableByLanguage = await reachable
            .GroupBy(d => d.LanguageCode)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        return reach;
    }

    // ───────────────────────────────── queue ─────────────────────────────────

    private async Task<PushQueueOutput> Queue(DateTime now)
    {
        var pending = dispatches.Query().Where(d => d.Status == PushStatus.Pending);

        // "Late" is measured against the same grace window the sender itself
        // uses, so this column and the sender cannot disagree about what overdue
        // means.
        var cutoff = now.AddMinutes(-PushRules.DispatchGraceMinutes);

        var scheduled = broadcasts.Query()
            .Where(b => b.Status == BroadcastStatus.Scheduled);

        return new PushQueueOutput
        {
            PendingDispatches = await pending.CountAsync(),
            OverdueDispatches = await pending.CountAsync(d => d.ScheduledAtUtc < cutoff),
            NextDispatchAtUtc = await pending
                .OrderBy(d => d.ScheduledAtUtc)
                .Select(d => (DateTime?)d.ScheduledAtUtc)
                .FirstOrDefaultAsync(),

            ScheduledBroadcasts = await scheduled.CountAsync(),
            NextBroadcastAtUtc = await scheduled
                .Where(b => b.ScheduledAtUtc != null)
                .OrderBy(b => b.ScheduledAtUtc)
                .Select(b => b.ScheduledAtUtc)
                .FirstOrDefaultAsync(),

            ActivePushCampaigns = await campaigns
                .CountAsync(c => c.IsEnabled && c.Delivery == ReminderDelivery.ServerPush),
        };
    }

    // ──────────────────────────────── outcomes ───────────────────────────────

    private async Task<PushOutcomesOutput> Outcomes(DateTime since, int days)
    {
        var counts = await dispatches.Query()
            .Where(d => d.ScheduledAtUtc >= since && d.Status != PushStatus.Pending)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int Of(PushStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var sent = Of(PushStatus.Sent);
        var failed = Of(PushStatus.Failed);
        var expired = Of(PushStatus.TokenExpired);

        var attempted = sent + failed + expired;

        return new PushOutcomesOutput
        {
            WindowDays = days,
            Sent = sent,
            Failed = failed,
            Skipped = Of(PushStatus.Skipped),
            TokenExpired = expired,
            DeliveryRate = attempted == 0 ? 0 : Math.Round((double)sent / attempted, 4),
        };
    }

    // ──────────────────────────────── failures ───────────────────────────────

    private async Task<List<PushFailureOutput>> RecentFailures(DateTime since)
    {
        var rows = await dispatches.Query()
            .Where(d => d.ScheduledAtUtc >= since)
            .Where(d => d.Status == PushStatus.Failed || d.Status == PushStatus.TokenExpired)
            .OrderByDescending(d => d.ScheduledAtUtc)
            .Take(RecentFailureCount)
            // The navigations are spelled out as conditionals rather than
            // dereferenced with `!`: exactly one of campaign and broadcast is
            // ever set, so half of these are null on every row by design. EF
            // translates the conditional to the same left join either way, and
            // the null-forgiving version throws the moment anything but EF —
            // a test double, say — runs the projection over plain objects.
            .Select(d => new
            {
                d.Id,
                d.ScheduledAtUtc,
                d.Status,
                d.Attempts,
                d.Error,
                CampaignKey = d.Campaign == null ? null : d.Campaign.Key,
                BroadcastId = d.BroadcastId,
                Platform = d.Device == null ? DevicePlatform.Unknown : d.Device.Platform,
                LanguageCode = d.Device == null ? "" : d.Device.LanguageCode,
            })
            .ToListAsync();

        return
        [
            .. rows.Select(row => new PushFailureOutput
            {
                Id = row.Id,
                ScheduledAtUtc = row.ScheduledAtUtc,
                Status = row.Status.ToString(),
                Attempts = row.Attempts,
                Error = row.Error,

                // Named rather than an id: an admin reading a failure list needs
                // to recognise the campaign, not look it up.
                Source = row.CampaignKey ?? (row.BroadcastId is { } id ? $"broadcast #{id}" : "—"),
                Platform = row.Platform.ToString(),
                LanguageCode = row.LanguageCode,
            }),
        ];
    }
}
