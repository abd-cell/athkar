using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Quran;
using Athkar.Areas.Services.Devices;
using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The push token's life after registration.
///
/// FCM rotates a registration token on its own schedule — a restore onto a new
/// phone, a long idle stretch, its own housekeeping — and the app hears about it
/// through a callback that can fire at any moment. Before this endpoint existed
/// the new token waited for the next cold launch, and every server push in
/// between went to an address nobody was at. Nothing failed: FCM accepts a send
/// to a token it has already retired, so the only symptom was a reader quietly
/// hearing less from the app.
/// </summary>
public class PushTokenTests
{
    private const string Key = "11111111-1111-1111-1111-111111111111";

    private readonly InMemoryRepository<Device> devices = new();
    private readonly InMemoryRepository<DeviceNotification> notifications = new();
    private readonly InMemoryRepository<AppConfiguration> configurations = new();
    private readonly InMemoryRepository<QuranPackage> packages = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeDeviceContext deviceContext = new() { DeviceKey = Key };

    private DeviceService Service() =>
        new(devices, notifications, configurations, packages, unitOfWork, deviceContext);

    private Device Seeded(string? token = "old-token") =>
        new()
        {
            DeviceKey = Key,
            PushToken = token,
            LanguageCode = "ar",
            TimeZoneId = "Asia/Riyadh",
            NotificationsEnabled = true,
            Platform = DevicePlatform.Android,
            LastSeenAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    private static PushTokenInput Input(string? token = "new-token", bool enabled = true) =>
        new() { DeviceKey = Key, PushToken = token, NotificationsEnabled = enabled };

    [Fact]
    public async Task A_rotated_token_replaces_the_stored_one()
    {
        var device = Seeded();
        devices.Seed(device);

        var response = await Service().UpdatePushToken(Input());

        Assert.True(response.Success);
        Assert.Equal("new-token", device.PushToken);
    }

    [Fact]
    public async Task A_null_token_clears_the_stored_one()
    {
        // What revoking notification permission produces. It has to reach the
        // server, or the device keeps consuming a slot in every fan-out.
        var device = Seeded();
        devices.Seed(device);

        await Service().UpdatePushToken(Input(token: null, enabled: false));

        Assert.Null(device.PushToken);
        Assert.False(device.NotificationsEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_token_is_stored_as_null_rather_than_as_a_string(string token)
    {
        // The dispatcher tests `PushToken != null`; an empty string would pass
        // that and then be rejected by FCM on every single send.
        var device = Seeded();
        devices.Seed(device);

        await Service().UpdatePushToken(Input(token));

        Assert.Null(device.PushToken);
    }

    [Fact]
    public async Task The_token_is_trimmed()
    {
        var device = Seeded();
        devices.Seed(device);

        await Service().UpdatePushToken(Input("  padded-token  "));

        Assert.Equal("padded-token", device.PushToken);
    }

    [Fact]
    public async Task An_unknown_device_is_refused_rather_than_created()
    {
        // A refresh that raced the first registration. Creating a row from it
        // would leave a device with no language and no timezone in every future
        // fan-out — and the app registers on every launch, so the token lands a
        // moment later anyway.
        var response = await Service().UpdatePushToken(Input());

        Assert.Equal(ErrorCode.DeviceNotFound, response.ErrorCode);
        Assert.Empty(devices.All);
    }

    [Fact]
    public async Task A_malformed_device_key_is_refused()
    {
        var input = Input();
        input.DeviceKey = "not-a-uuid";

        var response = await Service().UpdatePushToken(input);

        Assert.Equal(ErrorCode.InvalidDeviceKey, response.ErrorCode);
    }

    [Fact]
    public async Task Nothing_but_the_token_and_the_switch_is_touched()
    {
        // The endpoint exists precisely so a rotation cannot write back stale
        // copies of settings it knows nothing about. A reader who changed their
        // language on another screen must not have it reverted by a token that
        // happened to rotate afterwards.
        var device = Seeded();
        devices.Seed(device);

        await Service().UpdatePushToken(Input());

        Assert.Equal("ar", device.LanguageCode);
        Assert.Equal("Asia/Riyadh", device.TimeZoneId);
        Assert.Equal(DevicePlatform.Android, device.Platform);
    }

    [Fact]
    public async Task A_rotation_does_not_count_as_the_reader_opening_the_app()
    {
        // LastSeenAt drives the active-installs figure, which is supposed to
        // mean "somebody used this". A token rotating says the OS is awake.
        var device = Seeded();
        var before = device.LastSeenAt;
        devices.Seed(device);

        await Service().UpdatePushToken(Input());

        Assert.Equal(before, device.LastSeenAt);
    }

    [Fact]
    public async Task A_forgotten_device_is_not_revived_by_a_token()
    {
        // Register revives deliberately — reopening the app is a reader coming
        // back. A background token refresh is not, and undoing "delete my data"
        // from one would be the worst kind of surprise.
        var device = Seeded();
        device.IsDeleted = true;
        devices.Seed(device);

        var response = await Service().UpdatePushToken(Input());

        Assert.Equal(ErrorCode.DeviceNotFound, response.ErrorCode);
        Assert.True(device.IsDeleted);
        Assert.Equal("old-token", device.PushToken);
    }
}
