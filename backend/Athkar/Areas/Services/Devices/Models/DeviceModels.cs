using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Devices.Models;

/// <summary>
/// What an install tells the server about itself.
///
/// Sent on first launch and again whenever any of it changes — a language
/// switch, a new push token, a timezone that moved because the reader did. The
/// call is idempotent on the device key, so the app can simply send it and not
/// track whether it has registered before.
/// </summary>
public class DeviceRegistrationInput
{
    /// <summary>
    /// A UUID the app minted for itself on first launch. Not a hardware
    /// identifier, not derived from one, and not stable across a reinstall —
    /// see <c>Device</c> for why that is the intended behaviour.
    /// </summary>
    [Required, StringLength(64, MinimumLength = 8)]
    public string DeviceKey { get; set; } = string.Empty;

    [EnumDataType(typeof(DevicePlatform))]
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;

    /// <summary>Null is normal and permanent for a reader who declined notifications.</summary>
    [StringLength(512)]
    public string? PushToken { get; set; }

    [Required, StringLength(8)]
    public string LanguageCode { get; set; } = "ar";

    /// <summary>IANA zone id. Validated against the host's database; an unknown one falls back to UTC.</summary>
    [Required, StringLength(64)]
    public string TimeZoneId { get; set; } = "UTC";

    public bool NotificationsEnabled { get; set; } = true;

    [StringLength(32)]
    public string? AppVersion { get; set; }

    /// <summary>Two-letter region from the device locale. Never derived from an IP address.</summary>
    [StringLength(2, MinimumLength = 2)]
    public string? CountryCode { get; set; }
}

/// <summary>
/// What the app gets back: its own row, plus the two version numbers that decide
/// whether it has anything to download. Returning them here saves the launch
/// sequence a second round trip.
/// </summary>
public class DeviceOutput
{
    public string DeviceKey { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "ar";
    public string TimeZoneId { get; set; } = "UTC";
    public bool NotificationsEnabled { get; set; }

    /// <summary>The server's current content version. Fetch the catalogue when it differs from yours.</summary>
    public int ContentVersion { get; set; }

    /// <summary>Version of the published Qur'an package, or null when none is published.</summary>
    public int? QuranPackageVersion { get; set; }

    public int UnreadNotifications { get; set; }
}

public class NotificationOutput
{
    public int Id { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Route { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }

    public NotificationOutput() { }

    public NotificationOutput(Domain.Devices.DeviceNotification e)
    {
        Id = e.Id;
        Kind = e.Kind;
        Title = e.Title;
        Body = e.Body;
        Route = e.Route;
        CreatedAt = e.CreationDate;
        IsRead = e.ReadAt is not null;
    }
}

/// <summary>
/// A push token on its own, for when only the token has changed.
///
/// FCM rotates a registration token whenever it feels the need — a restore to a
/// new phone, a long idle period, its own housekeeping — and the app learns
/// about it through <c>onTokenRefresh</c>, which can fire at any moment while
/// the app is running. Waiting for the next cold launch to carry the new token
/// in a full registration means every server push in between goes to a token
/// that is no longer anybody's.
///
/// Deliberately not a <c>DeviceRegistrationInput</c>: a token refresh knows
/// nothing about the language or timezone, and sending stale copies of those
/// back would quietly undo a change the reader just made on another screen.
/// </summary>
public class PushTokenInput
{
    [Required, StringLength(64, MinimumLength = 8)]
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>Null clears the token — what a reader revoking permission produces.</summary>
    [StringLength(512)]
    public string? PushToken { get; set; }

    /// <summary>
    /// The OS-level permission as the app currently sees it. Travels with the
    /// token because the two change together: the same tap that revokes
    /// notifications is what invalidates the token.
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;
}

/// <summary>One row of the installed-base summary the CMS dashboard draws.</summary>
public class DeviceStatsOutput
{
    public int TotalDevices { get; set; }

    /// <summary>Seen in the last 30 days.</summary>
    public int ActiveDevices { get; set; }

    /// <summary>Holding a push token and not muted — the real reach of a broadcast.</summary>
    public int ReachableDevices { get; set; }

    public Dictionary<string, int> ByPlatform { get; set; } = [];
    public Dictionary<string, int> ByLanguage { get; set; } = [];
    public Dictionary<string, int> ByCountry { get; set; } = [];
}
