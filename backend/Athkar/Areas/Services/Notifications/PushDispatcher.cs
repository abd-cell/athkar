using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Domain.Reminders;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Notifications;
using Athkar.Shareds.Text;
using Athkar.Shareds.Notifications.Fcm;

namespace Athkar.Areas.Services.Notifications;

public class PushDispatcher : IPushDispatcher
{
    private readonly IRepository<ReminderCampaign> campaigns;
    private readonly IRepository<Broadcast> broadcasts;
    private readonly IRepository<PushDispatch> dispatches;
    private readonly IRepository<Device> devices;
    private readonly IRepository<DeviceNotification> inbox;
    private readonly IUnitOfWork unitOfWork;
    private readonly IFcmSender fcm;
    private readonly ILogger<PushDispatcher> logger;

    public PushDispatcher(
        IRepository<ReminderCampaign> campaigns,
        IRepository<Broadcast> broadcasts,
        IRepository<PushDispatch> dispatches,
        IRepository<Device> devices,
        IRepository<DeviceNotification> inbox,
        IUnitOfWork unitOfWork,
        IFcmSender fcm,
        ILogger<PushDispatcher> logger)
    {
        this.campaigns = campaigns;
        this.broadcasts = broadcasts;
        this.dispatches = dispatches;
        this.devices = devices;
        this.inbox = inbox;
        this.unitOfWork = unitOfWork;
        this.fcm = fcm;
        this.logger = logger;
    }

    // ───────────────────────────── materialising ─────────────────────────────

    public async Task<int> MaterialiseReminders(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddMinutes(PushRules.DispatchHorizonMinutes);

        var due = await campaigns.Query()
            .Where(c => c.IsEnabled && c.Delivery == ReminderDelivery.ServerPush)
            .Where(c => c.Kind == ReminderKind.FixedTime)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var created = 0;

        foreach (var campaign in due)
        {
            var audience = await Audience(campaign).ToListAsync(ct);
            if (audience.Count == 0) continue;

            // Existing rows for this campaign anywhere in the window, read once
            // rather than probed per device: the unique index is the real
            // guarantee, but letting it fire thousands of times a pass would
            // turn one query into thousands of caught exceptions.
            var already = await dispatches.Query()
                .Where(d => d.CampaignId == campaign.Id && d.ScheduledAtUtc > now && d.ScheduledAtUtc <= horizon)
                .Select(d => new { d.DeviceId, d.ScheduledAtUtc })
                .ToListAsync(ct);

            var seen = already
                .Select(row => (row.DeviceId, row.ScheduledAtUtc))
                .ToHashSet();

            foreach (var device in audience)
            {
                var zone = ReminderSchedule.ZoneOrUtc(device.TimeZoneId);

                foreach (var instant in ReminderSchedule.Occurrences(campaign, zone, now, horizon))
                {
                    if (!seen.Add((device.Id, instant))) continue;

                    await dispatches.AddAsync(new PushDispatch
                    {
                        CampaignId = campaign.Id,
                        DeviceId = device.Id,
                        ScheduledAtUtc = instant,
                    });

                    created++;
                }
            }
        }

        if (created > 0) await unitOfWork.SaveAsync();
        return created;
    }

    // ──────────────────────────────── sending ────────────────────────────────

    public async Task<int> SendDue(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var floor = now.AddMinutes(-PushRules.DispatchGraceMinutes);

        var due = await dispatches.Query()
            .Where(d => d.Status == PushStatus.Pending && d.ScheduledAtUtc <= now)
            .Include(d => d.Device)
            .Include(d => d.Campaign!).ThenInclude(c => c.Translations)
            .Include(d => d.Broadcast!).ThenInclude(b => b.Translations)
            .OrderBy(d => d.ScheduledAtUtc)
            .Take(2000)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var messages = new List<FcmMessage>();
        var pending = new List<PushDispatch>();

        foreach (var dispatch in due)
        {
            // Too late to be useful. A reminder for morning adhkar arriving at
            // noon tells the reader the app is not to be relied on, so it is
            // dropped rather than delivered.
            if (dispatch.ScheduledAtUtc < floor)
            {
                Close(dispatch, PushStatus.Skipped, "Missed its window.");
                continue;
            }

            var device = dispatch.Device;
            if (device is null || device.IsDeleted)
            {
                Close(dispatch, PushStatus.Skipped, "The device is gone.");
                continue;
            }

            var wording = Wording(dispatch)?.For(device.LanguageCode);
            if (wording is null)
            {
                Close(dispatch, PushStatus.Skipped, "The message has no wording.");
                continue;
            }

            // The inbox row is written whether or not the push itself lands.
            // A reader who had notifications off, or whose phone was in a
            // tunnel, should still find the message when they next open the app
            // — the push is a courtesy, the inbox is the delivery.
            //
            // Once only, though: an admin retrying a failed push must not put a
            // second copy of the same message in a reader's inbox, and the
            // reader never saw the failure that prompted it.
            if (!dispatch.InboxWritten)
            {
                await inbox.AddAsync(new DeviceNotification
                {
                    DeviceId = device.Id,
                    Kind = dispatch.CampaignId is not null
                        ? NotificationKind.Reminder
                        : NotificationKind.Broadcast,
                    Title = wording.Value.Title,
                    Body = wording.Value.Body,
                    LanguageCode = device.LanguageCode,
                    Route = Route(dispatch),
                });

                dispatch.InboxWritten = true;
                dispatches.Update(dispatch);
            }

            if (!device.NotificationsEnabled || string.IsNullOrWhiteSpace(device.PushToken))
            {
                Close(dispatch, PushStatus.Skipped, "Notifications are off for this device.");
                continue;
            }

            var channel = Channel(dispatch);

            messages.Add(new FcmMessage
            {
                Token = device.PushToken,

                // Marked here rather than stored marked: the inbox row above is
                // rendered inside the app, where a Directionality settles it, and
                // control characters in stored text would be there forever. Only
                // the copy going to the OS needs to say which way it runs.
                Title = BidiText.ForNotification(wording.Value.Title),
                Body = BidiText.ForNotification(wording.Value.Body),
                AndroidChannelId = channel,
                TimeSensitive = channel == PushRules.Channels.Prayer,
                Data = Payload(dispatch),
            });
            pending.Add(dispatch);
        }

        if (messages.Count > 0)
        {
            var results = await fcm.SendAsync(messages, ct);

            for (var i = 0; i < pending.Count; i++)
            {
                var dispatch = pending[i];
                var result = i < results.Count
                    ? results[i]
                    : FcmSendResult.Failed("No result was returned for this message.");

                dispatch.Attempts++;

                if (result.Success)
                {
                    Close(dispatch, PushStatus.Sent, null, result.MessageId);
                }
                else if (result.TokenExpired)
                {
                    Close(dispatch, PushStatus.TokenExpired, result.Error);

                    // The install is gone. Clearing the token here is what stops
                    // every later campaign paying for it again.
                    if (dispatch.Device is { } device)
                    {
                        device.PushToken = null;
                        devices.Update(device);
                    }
                }
                else if (dispatch.Attempts >= PushRules.MaxAttempts)
                {
                    Close(dispatch, PushStatus.Failed, result.Error);
                }
                else
                {
                    // Left pending on purpose: the next pass retries it, and the
                    // grace window above is what stops that going on forever.
                    dispatch.Error = Truncate(result.Error);
                    dispatches.Update(dispatch);
                }
            }
        }

        await unitOfWork.SaveAsync();
        await SettleBroadcasts(due, ct);

        return due.Count;
    }

    // ────────────────────────────── broadcasts ──────────────────────────────

    public async Task<int> StartDueBroadcasts(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var due = await broadcasts.Query()
            .Where(b => b.Status == BroadcastStatus.Scheduled)
            .Where(b => b.ScheduledAtUtc == null || b.ScheduledAtUtc <= now)
            .Include(b => b.Translations)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        foreach (var broadcast in due)
        {
            var audience = await Audience(broadcast).ToListAsync(ct);

            foreach (var device in audience)
                await dispatches.AddAsync(new PushDispatch
                {
                    BroadcastId = broadcast.Id,
                    DeviceId = device.Id,
                    // Now, not the scheduled instant: a broadcast that was
                    // queued while the worker was down should go out when it
                    // starts, not be immediately judged late by the grace window.
                    ScheduledAtUtc = now,
                });

            broadcast.RecipientCount = audience.Count;
            broadcast.Status = audience.Count == 0 ? BroadcastStatus.Sent : BroadcastStatus.Sending;
            broadcast.SentAtUtc = audience.Count == 0 ? now : null;
            broadcasts.Update(broadcast);

            logger.LogInformation("Broadcast {Id} fanned out to {Count} devices.",
                broadcast.Id, audience.Count);
        }

        await unitOfWork.SaveAsync();
        return due.Count;
    }

    /// <summary>
    /// Closes out any broadcast whose dispatches have all finished, and rolls
    /// their outcomes up into the counts the CMS shows.
    /// </summary>
    private async Task SettleBroadcasts(IEnumerable<PushDispatch> touched, CancellationToken ct)
    {
        var ids = touched
            .Where(d => d.BroadcastId is not null)
            .Select(d => d.BroadcastId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return;

        foreach (var id in ids)
        {
            if (await dispatches.AnyAsync(d => d.BroadcastId == id && d.Status == PushStatus.Pending))
                continue;

            var broadcast = await broadcasts.GetByIdAsync(id);
            if (broadcast is null || broadcast.Status != BroadcastStatus.Sending) continue;

            var outcomes = await dispatches.Query()
                .Where(d => d.BroadcastId == id)
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int CountOf(PushStatus status) =>
                outcomes.FirstOrDefault(o => o.Status == status)?.Count ?? 0;

            broadcast.SentCount = CountOf(PushStatus.Sent);
            broadcast.SkippedCount = CountOf(PushStatus.Skipped);
            broadcast.FailedCount = CountOf(PushStatus.Failed) + CountOf(PushStatus.TokenExpired);

            // Failed only when nothing at all got through. A broadcast where
            // most readers simply have push off is a successful broadcast with
            // an honest skip count, not a failure.
            broadcast.Status = broadcast.SentCount == 0 && broadcast.FailedCount > 0
                ? BroadcastStatus.Failed
                : BroadcastStatus.Sent;
            broadcast.SentAtUtc = DateTime.UtcNow;
            broadcasts.Update(broadcast);
        }

        await unitOfWork.SaveAsync();
    }

    // ─────────────────────────────── helpers ───────────────────────────────

    private IQueryable<Device> Audience(ReminderCampaign campaign) =>
        Reachable()
            .Where(d => campaign.Audience != Shareds.Enums.Audience.Language ||
                        d.LanguageCode == campaign.TargetLanguageCode)
            .Where(d => campaign.Audience != Shareds.Enums.Audience.Platform ||
                        d.Platform == campaign.TargetPlatform);

    private IQueryable<Device> Audience(Broadcast broadcast) =>
        broadcast.Audience switch
        {
            Shareds.Enums.Audience.Device =>
                devices.Query().Where(d => d.DeviceKey == broadcast.TargetDeviceKey),
            Shareds.Enums.Audience.Language =>
                Reachable().Where(d => d.LanguageCode == broadcast.TargetLanguageCode),
            Shareds.Enums.Audience.Platform =>
                Reachable().Where(d => d.Platform == broadcast.TargetPlatform),
            _ => Reachable(),
        };

    /// <summary>
    /// Devices worth queueing for. Muted installs are excluded here rather than
    /// at send time so a campaign's recipient count means what it says; a device
    /// with no token is kept, because the inbox row is still worth writing.
    /// </summary>
    private IQueryable<Device> Reachable() =>
        devices.Query().Where(d => d.NotificationsEnabled);

    private static LocalizedText? Wording(PushDispatch dispatch)
    {
        var translations = dispatch.Campaign is { } campaign
            ? campaign.Translations.Where(t => !t.IsDeleted)
                .ToDictionary(t => t.LanguageCode, t => (t.Title, t.Body))
            : dispatch.Broadcast?.Translations.Where(t => !t.IsDeleted)
                .ToDictionary(t => t.LanguageCode, t => (t.Title, t.Body));

        if (translations is null || translations.Count == 0) return null;

        return new LocalizedText(translations);
    }

    private static string? Route(PushDispatch dispatch) =>
        dispatch.Campaign?.CategoryId is { } categoryId
            ? $"category/{categoryId}"
            : dispatch.Broadcast?.Route;

    private static string Channel(PushDispatch dispatch) =>
        dispatch.Campaign?.AndroidChannelId
        ?? dispatch.Broadcast?.AndroidChannelId
        ?? PushRules.Channels.Announcements;

    /// <summary>
    /// The data payload the app routes on. Strings only — that is all FCM
    /// carries — and deliberately minimal: everything else is already on the
    /// device, so a push says which screen, never what to put on it.
    /// </summary>
    private static Dictionary<string, string> Payload(PushDispatch dispatch)
    {
        var payload = new Dictionary<string, string>
        {
            ["kind"] = dispatch.CampaignId is not null ? "reminder" : "broadcast",

            // Named rather than inferred. A message raised in the foreground is
            // drawn by the app, and Android fixes a channel's sound the moment
            // the channel is created — so guessing here would play the wrong
            // sound, and there is no way to correct it afterwards on a phone
            // that has already installed the app.
            ["channel"] = Channel(dispatch),
        };

        if (Route(dispatch) is { } route) payload["route"] = route;
        if (dispatch.CampaignId is { } campaignId) payload["campaignId"] = campaignId.ToString();
        if (dispatch.BroadcastId is { } broadcastId) payload["broadcastId"] = broadcastId.ToString();

        return payload;
    }

    private void Close(PushDispatch dispatch, PushStatus status, string? error, string? messageId = null)
    {
        dispatch.Status = status;
        dispatch.SentAtUtc = DateTime.UtcNow;
        dispatch.MessageId = messageId;
        dispatch.Error = Truncate(error);
        dispatches.Update(dispatch);
    }

    /// <summary>Some FCM errors are paragraphs, and the column is a thousand characters.</summary>
    private static string? Truncate(string? error) =>
        error is null ? null : error.Length <= 1000 ? error : error[..1000];
}
