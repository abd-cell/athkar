using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Services.Devices;
using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The console's view of the installs.
///
/// Two things are worth a test here and the rest is plumbing: that the reach
/// state is derived by the sender's own ladder — so this screen and the push
/// manager's four columns can never disagree about what a given install is —
/// and that a push token leaves the server only through the one call that is
/// audited for it. The second is not a preference: a token is a capability, and
/// a list that carried a hundred of them would be a hundred ways to notify
/// somebody's phone sitting in a browser cache.
/// </summary>
public class DeviceAdminServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly InMemoryRepository<Device> devices = new();
    private readonly InMemoryRepository<DeviceNotification> inbox = new();
    private readonly FakeAuditService audit = new();

    private DeviceAdminService Service() => new(devices, inbox, audit);

    private static Device Install(
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

    [Fact]
    public async Task The_list_carries_a_tail_and_never_a_whole_token()
    {
        devices.Seed(Install(token: "a-real-looking-fcm-token"));

        var row = (await Service().List(new DeviceQueryInput())).Data!.Data.Single();

        Assert.True(row.HasPushToken);

        // Enough to match a row against the Firebase console, useless to send with.
        Assert.Equal("cm-token", row.PushTokenTail);

        // The whole output type, not just the field: a token added to any
        // property later would fail here.
        var json = System.Text.Json.JsonSerializer.Serialize(row);
        Assert.DoesNotContain("a-real-looking-fcm-token", json);
    }

    [Fact]
    public async Task Revealing_a_token_is_written_to_the_audit_trail()
    {
        // The whole token is available — an admin checking an install against
        // Firebase genuinely needs it — but asking for one is an event, not a
        // view, and the trail is what makes it one.
        var device = Install(token: "a-real-looking-fcm-token");
        devices.Seed(device);

        var response = await Service().RevealPushToken(device.Id);

        Assert.True(response.Success);
        Assert.Equal("a-real-looking-fcm-token", response.Data);
        Assert.Contains(Athkar.Shareds.Constants.AuditActions.DevicePushTokenReveal, audit.Actions);
    }

    [Fact]
    public async Task Revealing_a_token_that_is_not_there_says_so_rather_than_returning_nothing()
    {
        // The tokenless case is the common one, and "" would read on screen as
        // a token that exists and is empty.
        var device = Install(token: null);
        devices.Seed(device);

        var response = await Service().RevealPushToken(device.Id);

        Assert.False(response.Success);
        Assert.Empty(audit.Actions);
    }

    [Theory]
    [InlineData(null, true, 1, DeviceReach.Tokenless)]
    [InlineData("token", false, 1, DeviceReach.Muted)]
    [InlineData("token", true, 400, DeviceReach.Silent)]
    [InlineData("token", true, 1, DeviceReach.Reachable)]
    public async Task Reach_follows_the_senders_own_ladder(
        string? token, bool enabled, int lastSeenDaysAgo, DeviceReach expected)
    {
        // Same order as PushManagerService.Reach and PushDispatcher's skip test:
        // no token, then muted, then silent. An install with no token that is
        // also silent is tokenless — one state, never two.
        devices.Seed(Install(token: token, enabled: enabled, lastSeenDaysAgo: lastSeenDaysAgo));

        var row = (await Service().List(new DeviceQueryInput())).Data!.Data.Single();

        Assert.Equal(expected, row.Reach);
    }

    [Fact]
    public async Task Tokenless_installs_can_be_listed_on_their_own()
    {
        // The manager says "7 hold no token". This is the screen that turns
        // that sentence into seven rows somebody can act on.
        devices.Seed(Install(), Install(token: null), Install(token: ""));

        var page = await Service().List(new DeviceQueryInput { HasPushToken = false });

        Assert.Equal(2, page.Data!.TotalRows);
        Assert.All(page.Data.Data, row => Assert.Equal(DeviceReach.Tokenless, row.Reach));
    }

    [Fact]
    public async Task An_installs_inbox_is_what_the_reader_can_actually_see()
    {
        // The delivery log says what the pipeline attempted; this says what
        // arrived. For a muted install the two disagree on every row, and that
        // is the case hardest to believe without looking.
        var device = Install(enabled: false);
        devices.Seed(device);

        inbox.Seed(
            new DeviceNotification { DeviceId = device.Id, Title = "أذكار الصباح" },
            new DeviceNotification { DeviceId = device.Id, Title = "إعلان", ReadAt = Now });

        var page = await Service().Inbox(device.Id, new PageInput());

        Assert.Equal(2, page.Data!.TotalRows);

        var unread = (await Service().List(new DeviceQueryInput())).Data!.Data.Single().UnreadCount;
        Assert.Equal(1, unread);
    }

    [Fact]
    public async Task An_install_that_is_not_there_is_a_not_found_rather_than_an_empty_page()
    {
        var response = await Service().Inbox(404, new PageInput());

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DeviceNotFound, response.ErrorCode);
    }

    [Fact]
    public async Task A_forgotten_install_stays_forgotten()
    {
        // "Delete my data" soft-deletes the row. The console must not be the
        // one place it comes back.
        var device = Install();
        device.IsDeleted = true;
        devices.Seed(device);

        var page = await Service().List(new DeviceQueryInput());

        Assert.Equal(0, page.Data!.TotalRows);
    }
}
