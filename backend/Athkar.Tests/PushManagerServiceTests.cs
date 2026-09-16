using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Services.Notifications;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The push manager's read side.
///
/// The screen exists because "4 targeted, 0 delivered" is not a diagnosis, and
/// the tests that matter are the ones about *keeping the reasons apart*: a
/// muted reader, an install that was never asked, one that is gone, and a
/// sender that has stopped all look identical from a broadcast row and need
/// four different responses.
/// </summary>
public class PushManagerServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly InMemoryRepository<Device> devices = new();
    private readonly InMemoryRepository<PushDispatch> dispatches = new();
    private readonly InMemoryRepository<Broadcast> broadcasts = new();
    private readonly InMemoryRepository<ReminderCampaign> campaigns = new();
    private readonly FakeBroadcastService broadcastService = new();
    private readonly FakePushDispatcher dispatcher = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();
    private readonly FakeFcmSender fcm = new();

    private PushManagerService Service() => new(
        devices, dispatches, broadcasts, campaigns, broadcastService, dispatcher, unitOfWork, audit, fcm);

    private static Device Device(
        string? token = "token",
        bool enabled = true,
        int lastSeenDaysAgo = 1,
        DevicePlatform platform = DevicePlatform.Android,
        string language = "ar") =>
        new()
        {
            DeviceKey = Guid.NewGuid().ToString(),
            PushToken = token,
            NotificationsEnabled = enabled,
            LastSeenAt = Now.AddDays(-lastSeenDaysAgo),
            Platform = platform,
            LanguageCode = language,
        };

    private static PushDispatch Dispatch(
        PushStatus status,
        double scheduledMinutesAgo = 10,
        int attempts = 1,
        string? error = null) =>
        new()
        {
            Status = status,
            ScheduledAtUtc = Now.AddMinutes(-scheduledMinutesAgo),
            Attempts = attempts,
            Error = error,
            DeviceId = 1,
        };

    // ───────────────────────────────── reach ─────────────────────────────────

    [Fact]
    public async Task Each_install_lands_in_exactly_one_reach_bucket()
    {
        // The columns have to sum to the total. If an install with no token that
        // is also silent were counted twice, the screen would overstate the
        // shortfall and nobody would notice.
        devices.Seed(
            Device(),
            Device(token: null),
            Device(token: null, lastSeenDaysAgo: 400),
            Device(enabled: false),
            Device(enabled: false, lastSeenDaysAgo: 400),
            Device(lastSeenDaysAgo: 400));

        var reach = (await Service().Overview()).Data!.Reach;

        Assert.Equal(6, reach.TotalDevices);
        Assert.Equal(1, reach.Reachable);
        Assert.Equal(2, reach.Tokenless);
        Assert.Equal(2, reach.Muted);
        Assert.Equal(1, reach.Stale);

        Assert.Equal(
            reach.TotalDevices,
            reach.Reachable + reach.Tokenless + reach.Muted + reach.Stale);
    }

    [Fact]
    public async Task An_empty_token_string_counts_as_no_token()
    {
        // A stored empty string would pass a `!= null` check and then be
        // rejected by FCM on every send — a device that looks reachable and
        // never is.
        devices.Seed(Device(token: ""));

        var reach = (await Service().Overview()).Data!.Reach;

        Assert.Equal(0, reach.Reachable);
        Assert.Equal(1, reach.Tokenless);
    }

    [Fact]
    public async Task Only_reachable_installs_are_broken_down_by_platform_and_language()
    {
        // The breakdown answers "who would this broadcast reach", so counting
        // devices that cannot receive it would make it the wrong answer.
        devices.Seed(
            Device(platform: DevicePlatform.Android, language: "ar"),
            Device(platform: DevicePlatform.Ios, language: "en"),
            Device(platform: DevicePlatform.Ios, language: "en", enabled: false),
            Device(platform: DevicePlatform.Android, token: null));

        var reach = (await Service().Overview()).Data!.Reach;

        Assert.Equal(1, reach.ReachableByPlatform["Android"]);
        Assert.Equal(1, reach.ReachableByPlatform["Ios"]);
        Assert.Equal(1, reach.ReachableByLanguage["ar"]);
        Assert.Equal(1, reach.ReachableByLanguage["en"]);
    }

    // ───────────────────────────────── queue ─────────────────────────────────

    [Fact]
    public async Task Overdue_counts_only_pending_rows_past_the_grace_window()
    {
        var late = PushRules.DispatchGraceMinutes + 5;

        dispatches.Seed(
            Dispatch(PushStatus.Pending, scheduledMinutesAgo: late),
            Dispatch(PushStatus.Pending, scheduledMinutesAgo: 1),
            Dispatch(PushStatus.Pending, scheduledMinutesAgo: -60),

            // Closed rows are history, however late they were.
            Dispatch(PushStatus.Sent, scheduledMinutesAgo: late),
            Dispatch(PushStatus.Failed, scheduledMinutesAgo: late));

        var queue = (await Service().Overview()).Data!.Queue;

        Assert.Equal(3, queue.PendingDispatches);
        Assert.Equal(1, queue.OverdueDispatches);
    }

    [Fact]
    public async Task A_healthy_queue_reports_nothing_overdue()
    {
        // Zero here is the whole point of the column: anything else means the
        // sender worker has stopped, which is otherwise invisible.
        dispatches.Seed(
            Dispatch(PushStatus.Pending, scheduledMinutesAgo: -30),
            Dispatch(PushStatus.Pending, scheduledMinutesAgo: 1));

        var queue = (await Service().Overview()).Data!.Queue;

        Assert.Equal(0, queue.OverdueDispatches);
    }

    [Fact]
    public async Task Only_server_push_campaigns_are_counted()
    {
        // A prayer-anchored campaign is scheduled by the phone and will never
        // produce a dispatch row. Counting it here would have an admin waiting
        // for sends that are not coming.
        campaigns.Seed(
            new ReminderCampaign { Key = "a", IsEnabled = true, Delivery = ReminderDelivery.ServerPush },
            new ReminderCampaign { Key = "b", IsEnabled = true, Delivery = ReminderDelivery.DeviceLocal },
            new ReminderCampaign { Key = "c", IsEnabled = false, Delivery = ReminderDelivery.ServerPush });

        var queue = (await Service().Overview()).Data!.Queue;

        Assert.Equal(1, queue.ActivePushCampaigns);
    }

    [Fact]
    public async Task The_next_scheduled_broadcast_is_the_earliest_one()
    {
        broadcasts.Seed(
            new Broadcast { Status = BroadcastStatus.Scheduled, ScheduledAtUtc = Now.AddHours(5) },
            new Broadcast { Status = BroadcastStatus.Scheduled, ScheduledAtUtc = Now.AddHours(2) },
            new Broadcast { Status = BroadcastStatus.Draft, ScheduledAtUtc = Now.AddMinutes(1) });

        var queue = (await Service().Overview()).Data!.Queue;

        Assert.Equal(2, queue.ScheduledBroadcasts);
        Assert.Equal(Now.AddHours(2), queue.NextBroadcastAtUtc);
    }

    // ──────────────────────────────── outcomes ───────────────────────────────

    [Fact]
    public async Task Skips_are_excluded_from_the_delivery_rate()
    {
        // Counting a reader who has notifications off as a delivery failure
        // would turn the rate into a measure of how many people want push —
        // a real question, but not the one this number answers.
        dispatches.Seed(
            Dispatch(PushStatus.Sent),
            Dispatch(PushStatus.Sent),
            Dispatch(PushStatus.Sent),
            Dispatch(PushStatus.Failed),
            Dispatch(PushStatus.Skipped),
            Dispatch(PushStatus.Skipped),
            Dispatch(PushStatus.Skipped));

        var outcomes = (await Service().Overview()).Data!.Outcomes;

        Assert.Equal(3, outcomes.Sent);
        Assert.Equal(3, outcomes.Skipped);
        Assert.Equal(0.75, outcomes.DeliveryRate);
    }

    [Fact]
    public async Task An_expired_token_counts_against_the_rate()
    {
        // It was attempted and it did not arrive. The device row is cleaned up
        // as a side effect, but the reader still missed the notification.
        dispatches.Seed(
            Dispatch(PushStatus.Sent),
            Dispatch(PushStatus.TokenExpired));

        var outcomes = (await Service().Overview()).Data!.Outcomes;

        Assert.Equal(1, outcomes.TokenExpired);
        Assert.Equal(0.5, outcomes.DeliveryRate);
    }

    [Fact]
    public async Task A_window_with_nothing_attempted_reports_a_rate_of_zero_for_the_screen_to_read_as_absent()
    {
        dispatches.Seed(Dispatch(PushStatus.Skipped), Dispatch(PushStatus.Pending));

        var outcomes = (await Service().Overview()).Data!.Outcomes;

        // The service reports 0; the screen turns "nothing attempted" into «—»,
        // because «٠٪» would read as total failure.
        Assert.Equal(0, outcomes.DeliveryRate);
        Assert.Equal(0, outcomes.Sent);
    }

    [Fact]
    public async Task Pending_rows_are_not_an_outcome()
    {
        dispatches.Seed(Dispatch(PushStatus.Pending), Dispatch(PushStatus.Pending));

        var outcomes = (await Service().Overview()).Data!.Outcomes;

        Assert.Equal(0, outcomes.Sent + outcomes.Failed + outcomes.Skipped + outcomes.TokenExpired);
    }

    [Fact]
    public async Task The_window_excludes_older_dispatches()
    {
        dispatches.Seed(
            Dispatch(PushStatus.Sent, scheduledMinutesAgo: 60),
            Dispatch(PushStatus.Sent, scheduledMinutesAgo: 60 * 24 * 30));

        var outcomes = (await Service().Overview(windowDays: 7)).Data!.Outcomes;

        Assert.Equal(7, outcomes.WindowDays);
        Assert.Equal(1, outcomes.Sent);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(9999, 90)]
    [InlineData(30, 30)]
    public async Task The_window_is_clamped(int asked, int expected)
    {
        var outcomes = (await Service().Overview(asked)).Data!.Outcomes;

        Assert.Equal(expected, outcomes.WindowDays);
    }

    // ──────────────────────────────── failures ───────────────────────────────

    [Fact]
    public async Task Recent_failures_list_both_kinds_and_nothing_else()
    {
        devices.Seed(Device());
        dispatches.Seed(
            Dispatch(PushStatus.Failed, error: "UNAVAILABLE"),
            Dispatch(PushStatus.TokenExpired, error: "UNREGISTERED"),
            Dispatch(PushStatus.Sent),
            Dispatch(PushStatus.Skipped),
            Dispatch(PushStatus.Pending));

        var failures = (await Service().Overview()).Data!.RecentFailures;

        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, f => f.Status == "Failed" && f.Error == "UNAVAILABLE");
        Assert.Contains(failures, f => f.Status == "TokenExpired" && f.Error == "UNREGISTERED");
    }

    [Fact]
    public async Task A_failure_names_its_source_rather_than_numbering_it()
    {
        devices.Seed(Device());

        var campaign = new ReminderCampaign { Key = "morning-adhkar" };
        campaigns.Seed(campaign);

        var fromCampaign = Dispatch(PushStatus.Failed);
        fromCampaign.CampaignId = campaign.Id;
        fromCampaign.Campaign = campaign;

        var fromBroadcast = Dispatch(PushStatus.Failed);
        fromBroadcast.BroadcastId = 7;

        dispatches.Seed(fromCampaign, fromBroadcast);

        var failures = (await Service().Overview()).Data!.RecentFailures;

        Assert.Contains(failures, f => f.Source == "morning-adhkar");
        Assert.Contains(failures, f => f.Source == "broadcast #7");
    }

    // ────────────────────────────────  sending  ──────────────────────────────

    private static BroadcastInput Message(DateTime? scheduledAt = null) => new()
    {
        Audience = Audience.Device,
        TargetDeviceKey = "11111111-1111-1111-1111-111111111111",
        ScheduledAtUtc = scheduledAt,
        Translations = [new Areas.Services.Content.Models.TranslationInput
        {
            LanguageCode = "ar",
            Title = "تجربة",
            Body = "رسالة تجريبية",
        }],
    };

    [Fact]
    public async Task Sending_composes_a_broadcast_and_queues_it()
    {
        // A delegation, not a second implementation: one code path has to carry
        // every message, or a quick send would miss the inbox rows, the
        // counters and the audit trail the broadcasts screen produces.
        var response = await Service().Send(Message());

        Assert.True(response.Success);

        var created = Assert.Single(broadcastService.Created);
        Assert.Equal(Audience.Device, created.Audience);
        Assert.Equal("11111111-1111-1111-1111-111111111111", created.TargetDeviceKey);

        Assert.Equal([1], broadcastService.SentIds);
    }

    [Fact]
    public async Task A_schedule_on_a_send_now_is_ignored()
    {
        // A message with a time on it is a campaign, and a campaign belongs on
        // the broadcasts screen where it can be read back and withdrawn before
        // it goes. Silently honouring a schedule here would leave an admin
        // watching for a message that had not been sent.
        await Service().Send(Message(scheduledAt: Now.AddDays(3)));

        Assert.Null(Assert.Single(broadcastService.Created).ScheduledAtUtc);
    }

    [Fact]
    public async Task A_refused_composition_is_not_queued()
    {
        broadcastService.CreateFails = ErrorCode.BroadcastNotFound;

        var response = await Service().Send(Message());

        Assert.False(response.Success);
        Assert.Empty(broadcastService.SentIds);
    }

    [Fact]
    public async Task A_failure_to_queue_leaves_the_draft_behind()
    {
        // Deliberate. The admin's words are the expensive part, and a draft
        // waiting on the broadcasts screen can be sent again — a tidy rollback
        // would have thrown them away.
        broadcastService.SendFails = ErrorCode.BroadcastAlreadySent;

        var response = await Service().Send(Message());

        Assert.False(response.Success);
        Assert.Single(broadcastService.Created);
    }

    // ────────────────────────────── delivery log ─────────────────────────────

    [Fact]
    public async Task The_log_can_be_narrowed_to_one_install()
    {
        // The reason to open the log is almost always one install: a reader
        // wrote in to say nothing arrives, and the question is what the
        // pipeline thinks it did about that.
        var mine = Device();
        var theirs = Device();
        devices.Seed(mine, theirs);

        dispatches.Seed(
            new PushDispatch { DeviceId = mine.Id, Device = mine, Status = PushStatus.Sent, ScheduledAtUtc = Now },
            new PushDispatch { DeviceId = theirs.Id, Device = theirs, Status = PushStatus.Sent, ScheduledAtUtc = Now });

        var page = await Service().Dispatches(new PushDispatchQueryInput { DeviceKey = mine.DeviceKey });

        Assert.Equal(1, page.Data!.TotalRows);
        Assert.Equal(mine.DeviceKey, page.Data.Data[0].DeviceKey);
    }

    [Fact]
    public async Task A_row_carries_whether_each_action_applies()
    {
        // Decided on the server, read off the row by the CMS. A screen that
        // works the rule out for itself is a second copy of it, and the copy is
        // what drifts.
        var device = Device();
        devices.Seed(device);

        dispatches.Seed(
            new PushDispatch { DeviceId = device.Id, Device = device, Status = PushStatus.Sent, ScheduledAtUtc = Now },
            new PushDispatch { DeviceId = device.Id, Device = device, Status = PushStatus.Failed, ScheduledAtUtc = Now },
            new PushDispatch { DeviceId = device.Id, Device = device, Status = PushStatus.Pending, ScheduledAtUtc = Now });

        var rows = (await Service().Dispatches(new PushDispatchQueryInput())).Data!.Data;

        var sent = rows.Single(r => r.Status == PushStatus.Sent);
        var failed = rows.Single(r => r.Status == PushStatus.Failed);
        var pending = rows.Single(r => r.Status == PushStatus.Pending);

        Assert.False(sent.CanRetry);
        Assert.False(sent.CanCancel);

        Assert.True(failed.CanRetry);
        Assert.False(failed.CanCancel);

        Assert.False(pending.CanRetry);
        Assert.True(pending.CanCancel);
    }

    [Fact]
    public async Task A_row_is_headlined_in_the_language_it_was_addressed_in()
    {
        // The log is read to find out what a reader saw, so the English row
        // must not be labelled with the Arabic wording just because it is first.
        var arabic = Device(language: "ar");
        var english = Device(language: "en");
        devices.Seed(arabic, english);

        var broadcast = new Broadcast { Status = BroadcastStatus.Sent };
        broadcast.Translations.Add(new BroadcastTranslation { LanguageCode = "ar", Title = "تنبيه" });
        broadcast.Translations.Add(new BroadcastTranslation { LanguageCode = "en", Title = "Notice" });
        broadcasts.Seed(broadcast);

        dispatches.Seed(
            new PushDispatch { DeviceId = arabic.Id, Device = arabic, BroadcastId = broadcast.Id, Status = PushStatus.Sent, ScheduledAtUtc = Now },
            new PushDispatch { DeviceId = english.Id, Device = english, BroadcastId = broadcast.Id, Status = PushStatus.Sent, ScheduledAtUtc = Now });

        var rows = (await Service().Dispatches(new PushDispatchQueryInput())).Data!.Data;

        Assert.Equal("تنبيه", rows.Single(r => r.LanguageCode == "ar").Title);
        Assert.Equal("Notice", rows.Single(r => r.LanguageCode == "en").Title);
    }

    [Fact]
    public async Task A_deleted_broadcast_still_says_what_it_said()
    {
        // The log is a history, and a broadcast is routinely deleted in the days
        // after it goes out. Excluding deleted rows the way every other query
        // here does left the log showing a dash exactly where somebody had
        // tidied away the message they were trying to read back.
        var device = Device();
        devices.Seed(device);

        var broadcast = new Broadcast { Status = BroadcastStatus.Sent, IsDeleted = true };
        broadcast.Translations.Add(new BroadcastTranslation
        {
            LanguageCode = "ar",
            Title = "إعلان مسحوب",
            IsDeleted = true,
        });
        broadcasts.Seed(broadcast);

        dispatches.Seed(new PushDispatch
        {
            DeviceId = device.Id,
            Device = device,
            BroadcastId = broadcast.Id,
            Status = PushStatus.Sent,
            ScheduledAtUtc = Now,
        });

        var row = (await Service().Dispatches(new PushDispatchQueryInput())).Data!.Data.Single();

        Assert.Equal("إعلان مسحوب", row.Title);
    }

    // ─────────────────────────── retry and cancel ────────────────────────────

    [Fact]
    public async Task A_sent_dispatch_is_never_retried()
    {
        // The worst outcome this screen could produce: the reader already has
        // the message, and a second copy of a 5am reminder is not a fix for
        // anything.
        var device = Device();
        devices.Seed(device);

        var dispatch = new PushDispatch
        {
            DeviceId = device.Id,
            Device = device,
            Status = PushStatus.Sent,
            ScheduledAtUtc = Now.AddHours(-1),
        };
        dispatches.Seed(dispatch);

        var response = await Service().Retry(dispatch.Id);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DispatchNotRetryable, response.ErrorCode);
        Assert.Equal(PushStatus.Sent, dispatch.Status);
    }

    [Fact]
    public async Task A_retry_moves_the_instant_to_now()
    {
        // Left where it was, the sender's grace window would judge the row late
        // and skip it again on the very next pass — and the button would look
        // like it did nothing.
        var device = Device();
        devices.Seed(device);

        var dispatch = new PushDispatch
        {
            DeviceId = device.Id,
            Device = device,
            Status = PushStatus.Failed,
            Attempts = 3,
            Error = "UNAVAILABLE",
            ScheduledAtUtc = Now.AddHours(-6),
        };
        dispatches.Seed(dispatch);

        var response = await Service().Retry(dispatch.Id);

        Assert.True(response.Success);
        Assert.Equal(PushStatus.Pending, dispatch.Status);
        Assert.Equal(0, dispatch.Attempts);
        Assert.Null(dispatch.Error);
        Assert.True(dispatch.ScheduledAtUtc > Now.AddMinutes(-PushRules.DispatchGraceMinutes));
        Assert.Contains(AuditActions.DispatchRetry, audit.Actions);
    }

    [Fact]
    public async Task Cancelling_records_a_skip_rather_than_removing_the_row()
    {
        // The row is the evidence that somebody stopped this on purpose. A
        // missing row reads as a bug in the sender.
        var device = Device();
        devices.Seed(device);

        var dispatch = new PushDispatch
        {
            DeviceId = device.Id,
            Device = device,
            Status = PushStatus.Pending,
            ScheduledAtUtc = Now.AddMinutes(30),
        };
        dispatches.Seed(dispatch);

        var response = await Service().CancelDispatch(dispatch.Id);

        Assert.True(response.Success);
        Assert.Equal(PushStatus.Skipped, dispatch.Status);
        Assert.False(dispatch.IsDeleted);
        Assert.NotNull(dispatch.Error);
        Assert.Contains(AuditActions.DispatchCancel, audit.Actions);
    }

    [Fact]
    public async Task Only_a_pending_dispatch_can_be_cancelled()
    {
        var device = Device();
        devices.Seed(device);

        var dispatch = new PushDispatch
        {
            DeviceId = device.Id,
            Device = device,
            Status = PushStatus.Sent,
            ScheduledAtUtc = Now,
        };
        dispatches.Seed(dispatch);

        var response = await Service().CancelDispatch(dispatch.Id);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DispatchNotCancellable, response.ErrorCode);
    }

    [Fact]
    public async Task Neither_action_invents_a_dispatch_that_is_not_there()
    {
        var service = Service();

        Assert.Equal(ErrorCode.DispatchNotFound, (await service.Retry(404)).ErrorCode);
        Assert.Equal(ErrorCode.DispatchNotFound, (await service.CancelDispatch(404)).ErrorCode);
    }

    // ──────────────────────────────── run now ────────────────────────────────

    [Fact]
    public async Task Running_by_hand_does_all_three_passes_in_the_workers_order()
    {
        // Order is the whole point: a reminder that materialises in this pass
        // and a broadcast that fans out in it are both then sent by the third.
        // Send first and one press would do a third of the job.
        dispatcher.MaterialiseReturns = 4;
        dispatcher.StartBroadcastsReturns = 1;
        dispatcher.SendDueReturns = 9;

        var response = await Service().RunNow();

        Assert.True(response.Success);
        Assert.Equal(
            ["MaterialiseReminders", "StartDueBroadcasts", "SendDue"],
            dispatcher.Calls);

        Assert.Equal(4, response.Data!.Materialised);
        Assert.Equal(1, response.Data.BroadcastsStarted);
        Assert.Equal(9, response.Data.Attempted);
        Assert.Contains(AuditActions.PushRunNow, audit.Actions);
    }

    // ─────────────────────────────── credentials ─────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_screen_is_told_whether_firebase_is_configured_at_all(bool configured)
    {
        // The first thing the screen reads: with no credentials every other
        // figure on it has one explanation, and studying the queue will never
        // reveal it.
        fcm.IsConfigured = configured;

        var overview = (await Service().Overview()).Data!;

        Assert.Equal(configured, overview.IsFcmConfigured);
    }

    [Fact]
    public async Task An_empty_system_reports_zeroes_rather_than_failing()
    {
        // The screen is opened on a fresh install more often than on a busy one.
        var response = await Service().Overview();

        Assert.True(response.Success);
        Assert.Equal(0, response.Data!.Reach.TotalDevices);
        Assert.Empty(response.Data.RecentFailures);
        Assert.Null(response.Data.Queue.NextDispatchAtUtc);
    }
}
