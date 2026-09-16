using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Services.Notifications;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Text;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Notifications.Fcm;
using Athkar.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace Athkar.Tests;

/// <summary>
/// How the dispatcher reacts to what Firebase says.
///
/// This is the half of the push pipeline that cannot be checked by reading it:
/// the interesting behaviour is all in the response handling — a retired token
/// has to retire the device row, a transient failure has to be retried and then
/// given up on, and a reader with notifications off has to be recorded as
/// skipped rather than failed. None of that can be provoked against the real
/// service on demand, so the sender is scripted instead.
/// </summary>
public class PushDispatcherTests
{
    private readonly InMemoryRepository<ReminderCampaign> campaigns = new();
    private readonly InMemoryRepository<Broadcast> broadcasts = new();
    private readonly InMemoryRepository<PushDispatch> dispatches = new();
    private readonly InMemoryRepository<Device> devices = new();
    private readonly InMemoryRepository<DeviceNotification> inbox = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeFcmSender fcm = new();

    private PushDispatcher Dispatcher() =>
        new(campaigns, broadcasts, dispatches, devices, inbox, unitOfWork, fcm,
            NullLogger<PushDispatcher>.Instance);

    /// <summary>
    /// One pending dispatch, with its navigations wired the way EF's Include
    /// would have left them.
    /// </summary>
    private PushDispatch Due(
        string? token = "token",
        bool notificationsEnabled = true,
        int attempts = 0,
        double dueMinutesAgo = 1)
    {
        var device = new Device
        {
            DeviceKey = Guid.NewGuid().ToString(),
            PushToken = token,
            NotificationsEnabled = notificationsEnabled,
            LanguageCode = "ar",
            Platform = DevicePlatform.Android,
        };
        devices.Seed(device);

        var campaign = new ReminderCampaign
        {
            Key = "morning-adhkar",
            IsEnabled = true,
            Delivery = ReminderDelivery.ServerPush,
            AndroidChannelId = PushRules.Channels.Reminders,
        };
        campaign.Translations.Add(new ReminderCampaignTranslation
        {
            LanguageCode = "ar",
            Title = "أذكار الصباح",
            Body = "حان وقت أذكار الصباح",
        });
        campaigns.Seed(campaign);

        var dispatch = new PushDispatch
        {
            CampaignId = campaign.Id,
            Campaign = campaign,
            DeviceId = device.Id,
            Device = device,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-dueMinutesAgo),
            Status = PushStatus.Pending,
            Attempts = attempts,
        };
        dispatches.Seed(dispatch);

        return dispatch;
    }

    // ───────────────────────────── the happy path ────────────────────────────

    [Fact]
    public async Task A_successful_send_closes_the_row_and_records_the_message_id()
    {
        var dispatch = Due();
        fcm.Script(FcmSendResult.Sent("projects/x/messages/1"));

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Sent, dispatch.Status);
        Assert.Equal("projects/x/messages/1", dispatch.MessageId);
        Assert.Equal(1, dispatch.Attempts);
        Assert.NotNull(dispatch.SentAtUtc);
    }

    [Fact]
    public async Task The_inbox_row_is_written_whatever_the_push_does()
    {
        // The design the whole notification story turns on: the push is a
        // courtesy, the inbox is the delivery. A reader whose phone was in a
        // tunnel must still find the message when they next open the app.
        var dispatch = Due();
        fcm.Script(FcmSendResult.Failed("UNAVAILABLE"));

        await Dispatcher().SendDue();

        Assert.Single(inbox.All);
        Assert.Equal("أذكار الصباح", inbox.All[0].Title);
        Assert.Equal(dispatch.DeviceId, inbox.All[0].DeviceId);
    }

    // ────────────────────────────── token pruning ────────────────────────────

    [Fact]
    public async Task A_retired_token_is_cleared_from_the_device()
    {
        // The one outcome that says something about the *device* rather than
        // the attempt. Without this every later campaign pays to discover the
        // same install is gone.
        var dispatch = Due();
        fcm.Script(FcmSendResult.Expired("UNREGISTERED"));

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.TokenExpired, dispatch.Status);
        Assert.Null(dispatch.Device!.PushToken);
    }

    [Fact]
    public async Task A_device_whose_token_was_retired_is_skipped_next_time_rather_than_retried()
    {
        // The proof that clearing the token actually saves the later send: the
        // same device, a fresh dispatch, and FCM is never called.
        var first = Due();
        fcm.Script(FcmSendResult.Expired("UNREGISTERED"));
        await Dispatcher().SendDue();

        var second = new PushDispatch
        {
            CampaignId = first.CampaignId,
            Campaign = first.Campaign,
            DeviceId = first.DeviceId,
            Device = first.Device,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
            Status = PushStatus.Pending,
        };
        dispatches.Seed(second);

        var before = fcm.Sent.Count;
        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Skipped, second.Status);
        Assert.Equal(before, fcm.Sent.Count);
    }

    [Fact]
    public async Task A_transient_failure_does_not_clear_the_token()
    {
        // A network blip is not an uninstall. Clearing on any failure would
        // silently unsubscribe readers every time Firebase had a bad minute.
        var dispatch = Due();
        fcm.Script(FcmSendResult.Failed("UNAVAILABLE"));

        await Dispatcher().SendDue();

        Assert.Equal("token", dispatch.Device!.PushToken);
    }

    // ──────────────────────────────── retrying ───────────────────────────────

    [Fact]
    public async Task A_transient_failure_is_left_pending_for_the_next_pass()
    {
        var dispatch = Due();
        fcm.Script(FcmSendResult.Failed("UNAVAILABLE"));

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Pending, dispatch.Status);
        Assert.Equal(1, dispatch.Attempts);
        Assert.Equal("UNAVAILABLE", dispatch.Error);
    }

    [Fact]
    public async Task A_failure_on_the_last_attempt_closes_the_row()
    {
        var dispatch = Due(attempts: PushRules.MaxAttempts - 1);
        fcm.Script(FcmSendResult.Failed("INTERNAL"));

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Failed, dispatch.Status);
        Assert.Equal(PushRules.MaxAttempts, dispatch.Attempts);
    }

    // ──────────────────────────────── skipping ───────────────────────────────

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("token", false)]
    public async Task A_device_that_cannot_receive_is_skipped_without_an_attempt(
        string? token, bool notificationsEnabled)
    {
        // Skipped, not Failed: these are correct outcomes. Counting them as
        // failures would make the delivery rate a measure of how many readers
        // want push rather than of whether push works.
        var dispatch = Due(token: token, notificationsEnabled: notificationsEnabled);

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Skipped, dispatch.Status);
        Assert.Empty(fcm.Sent);
    }

    [Fact]
    public async Task A_dispatch_that_missed_its_window_is_dropped_rather_than_delivered()
    {
        // A morning-adhkar reminder arriving at noon tells the reader the app is
        // not to be relied on.
        var dispatch = Due(dueMinutesAgo: PushRules.DispatchGraceMinutes + 5);

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Skipped, dispatch.Status);
        Assert.Empty(fcm.Sent);
    }

    [Fact]
    public async Task A_dispatch_that_is_not_due_yet_is_left_alone()
    {
        var dispatch = Due(dueMinutesAgo: -30);

        await Dispatcher().SendDue();

        Assert.Equal(PushStatus.Pending, dispatch.Status);
        Assert.Equal(0, dispatch.Attempts);
        Assert.Empty(fcm.Sent);
    }

    // ──────────────────────────────── payload ────────────────────────────────

    [Fact]
    public async Task The_message_carries_the_reader_s_language_and_the_campaign_s_channel()
    {
        Due();

        await Dispatcher().SendDue();

        var message = Assert.Single(fcm.Sent);

        // The wording, led by a right-to-left mark. A push that arrives while
        // the app is closed is drawn by the OS from this string alone, and the
        // shade takes its base direction from the *device's* locale — so the
        // text has to say which way it runs or an Arabic reminder is laid out
        // left-to-right on a phone set to English. See BidiTextTests.
        Assert.Equal(BidiText.ForNotification("أذكار الصباح"), message.Title);
        Assert.StartsWith("\u200F", message.Title);

        Assert.Equal(PushRules.Channels.Reminders, message.AndroidChannelId);
        Assert.Equal("reminder", message.Data["kind"]);
    }
}
