using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Devices.Models;

/// <summary>
/// How the console asks for installs.
///
/// Every filter here is one of the four states the push manager keeps apart —
/// tokenless, muted, silent, reachable — because the reason to open this screen
/// is almost always a number on that one, and landing on "all seven hundred
/// devices" would make the admin do the filtering by eye.
/// </summary>
public class DeviceQueryInput : PageInput
{
    /// <summary>One install by id, so a single read goes through the same projection.</summary>
    public int? Id { get; set; }

    public DevicePlatform? Platform { get; set; }

    public string? LanguageCode { get; set; }

    /// <summary>True: holds an FCM token. False: the tokenless column, in full.</summary>
    public bool? HasPushToken { get; set; }

    /// <summary>The reader's own switch, not the OS permission — which is not visible here.</summary>
    public bool? NotificationsEnabled { get; set; }

    /// <summary>Seen inside the active window (30 days), matching the dashboard's definition.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// One install, as the console may see it.
///
/// The whole row and nothing added: there is nothing else to add. An install is
/// a key it minted for itself, a platform, a language, a zone and a date — see
/// <c>docs/BUSINESS_LOGIC.md</c> §2. The token itself is deliberately *not*
/// here: an admin needs to know whether one exists, never what it is, and a
/// credential that can raise a notification on somebody's phone has no reason
/// to be on a screen or in a log.
/// </summary>
public class DeviceAdminOutput
{
    public int Id { get; set; }
    public string DeviceKey { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>Whether a token exists — never the token.</summary>
    public bool HasPushToken { get; set; }

    /// <summary>
    /// The last few characters of the token, or null when there is none.
    ///
    /// Enough to tell one token from another, and to check a row against what
    /// the Firebase console shows, without putting a working credential on a
    /// screen. The whole token is a separate, audited read — see
    /// <c>IDeviceAdminService.RevealPushToken</c>.
    /// </summary>
    public string? PushTokenTail { get; set; }

    public bool NotificationsEnabled { get; set; }

    public DateTime LastSeenAt { get; set; }
    public DateTime FirstSeenAt { get; set; }

    public int SyncedContentVersion { get; set; }

    /// <summary>
    /// Which of the four states this install is in, decided on the server so the
    /// screen and the sender cannot disagree. The ladder is the sender's own,
    /// in its order: no token, then muted, then silent, then reachable.
    /// </summary>
    public DeviceReach Reach { get; set; }

    /// <summary>Unread inbox rows — the messages this install has been given but not opened.</summary>
    public int UnreadCount { get; set; }
}

/// <summary>
/// Why a broadcast would or would not land on an install.
///
/// Not an enum in <c>Shareds/Enums</c> on purpose: it is derived at read time
/// from three columns, never stored, so it has no number to be a contract.
/// </summary>
public enum DeviceReach
{
    /// <summary>No token: permission declined, or never asked for.</summary>
    Tokenless = 0,

    /// <summary>Holds a token, but the reader turned reminders off.</summary>
    Muted = 1,

    /// <summary>Holds a token and wants reminders, but has not called in for 30 days.</summary>
    Silent = 2,

    /// <summary>A push would go out and be expected to arrive.</summary>
    Reachable = 3,
}

/// <summary>One row of an install's inbox, as delivered.</summary>
public class DeviceInboxOutput
{
    public int Id { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string? Route { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
