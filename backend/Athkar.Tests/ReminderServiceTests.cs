using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Services.Reminders;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Reminders.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The rules a reminder campaign is held to when an admin writes one.
///
/// Two of them exist because breaking them produces a campaign that looks
/// perfectly correct in the CMS and then never reaches anybody: a prayer-
/// anchored campaign the server was asked to push, and a notification channel
/// the app has never created.
/// </summary>
public class ReminderServiceTests
{
    private readonly InMemoryRepository<ReminderCampaign> campaigns = new();
    private readonly InMemoryRepository<ReminderCampaignTranslation> translations = new();
    private readonly InMemoryRepository<AthkarCategory> categories = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();

    private ReminderService Service() =>
        new(campaigns, translations, categories, unitOfWork, audit,
            new FakeLanguageResolver(), new FakeSecurityManager());

    private static ReminderInput Anchored(
        string key = "prayer-fajr",
        PrayerAnchor anchor = PrayerAnchor.Fajr,
        string? channel = PushRules.Channels.Prayer,
        ReminderDelivery delivery = ReminderDelivery.DeviceLocal) => new()
    {
        Key = key,
        Kind = ReminderKind.PrayerAnchored,
        Anchor = anchor,
        OffsetMinutes = 0,
        Delivery = delivery,
        Days = WeekDays.All,
        Audience = Audience.All,
        AndroidChannelId = channel,
        IsEnabled = true,
        Translations = [new TranslationInput { LanguageCode = "ar", Title = "حان وقت الصلاة", Body = "حيّ على الصلاة" }],
    };

    [Fact]
    public async Task A_prayer_reminder_may_choose_the_prayer_channel()
    {
        // The whole point of a separate channel: a reader can leave the call to
        // prayer audible while silencing the adhkar reminders, and Android fixes
        // a channel's sound the moment it is created — so this is the only
        // moment that choice can be made.
        var response = await Service().Create(Anchored());

        Assert.True(response.Success);
        Assert.Equal(PushRules.Channels.Prayer, response.Data!.AndroidChannelId);
    }

    [Fact]
    public async Task A_channel_the_app_never_created_is_refused()
    {
        // Refused here rather than on the phone, where Android would drop the
        // notification or quietly rehome it and nobody could see it happen.
        var response = await Service().Create(Anchored(channel: "athkar.invented.v9"));

        Assert.Equal(ErrorCode.UnknownNotificationChannel, response.ErrorCode);
        Assert.Empty(campaigns.All);
    }

    [Fact]
    public async Task No_channel_means_the_reminders_channel()
    {
        var response = await Service().Create(Anchored(channel: null));

        Assert.True(response.Success);
        Assert.Equal(PushRules.Channels.Reminders, response.Data!.AndroidChannelId);
    }

    [Theory]
    [InlineData(PushRules.Channels.Reminders)]
    [InlineData(PushRules.Channels.Prayer)]
    [InlineData(PushRules.Channels.Announcements)]
    public async Task Every_channel_the_app_creates_is_accepted(string channel)
    {
        // Guards the pairing itself: the server's allowed list and the channels
        // `LocalNotifications` creates at first launch are one contract, and a
        // new channel added on one side only would fail silently on a phone.
        var response = await Service().Create(Anchored(channel: channel));

        Assert.True(response.Success);
        Assert.Equal(channel, response.Data!.AndroidChannelId);
    }

    [Fact]
    public async Task A_prayer_anchored_campaign_is_forced_to_device_local()
    {
        // The rule the whole notification design turns on. The server never
        // receives coordinates, so it cannot know when Fajr is for a reader —
        // an anchored campaign it was asked to push would simply never fire.
        var response = await Service().Create(Anchored(delivery: ReminderDelivery.ServerPush));

        Assert.True(response.Success);
        Assert.Equal(ReminderDelivery.DeviceLocal, response.Data!.Delivery);
    }

    [Fact]
    public async Task An_anchored_campaign_without_an_anchor_is_refused()
    {
        var input = Anchored();
        input.Anchor = PrayerAnchor.None;

        var response = await Service().Create(input);

        Assert.Equal(ErrorCode.ReminderScheduleIncomplete, response.ErrorCode);
    }

    [Fact]
    public async Task A_campaign_that_runs_on_no_day_is_refused()
    {
        var input = Anchored();
        input.Days = WeekDays.None;

        var response = await Service().Create(input);

        Assert.Equal(ErrorCode.ReminderHasNoDays, response.ErrorCode);
    }
}
