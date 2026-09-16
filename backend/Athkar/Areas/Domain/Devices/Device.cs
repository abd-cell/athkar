using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Devices;

/// <summary>
/// One anonymous install.
///
/// This row is the whole of what the server knows about a reader, and the list
/// of columns is the privacy policy made executable: a key the app generated
/// for itself, a push token, a language, a timezone, a platform. No name, no
/// email, no phone, no coordinates — prayer times are computed on the device
/// precisely so that the last of those never has to be sent. See
/// <c>docs/BUSINESS_LOGIC.md</c> §2.
///
/// The key is minted by the app on first launch and kept in local storage. It is
/// therefore an identifier for an *install*, not for a person: clearing the app
/// data makes a new one, and two phones are two devices with nothing joining
/// them. That is the intended behaviour and not a limitation to work around.
/// </summary>
public class Device : BaseEntity
{
    /// <summary>A UUID the app generated for itself. Unique; the only way to address a device.</summary>
    public string DeviceKey { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }

    /// <summary>
    /// The FCM registration token, or null when the reader has not granted
    /// notification permission — which is a normal, permanent state for a great
    /// many installs and must never be treated as an error.
    /// </summary>
    public string? PushToken { get; set; }

    /// <summary>Chosen interface language. Decides which translation a push is worded in.</summary>
    public string LanguageCode { get; set; } = "ar";

    /// <summary>
    /// IANA zone id ("Asia/Riyadh"). The server needs it for exactly one thing:
    /// turning a campaign's local time of day into the UTC instant to send at.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Whether the reader wants scheduled reminders at all. Set from the app's
    /// own settings screen, and checked before every dispatch — the OS-level
    /// permission is not visible here, so this is the server's only chance to
    /// not waste a send.
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    public string? AppVersion { get; set; }

    /// <summary>
    /// Coarse locale region reported by the device ("SA", "JO"). Two letters,
    /// never derived from an IP address, and used only for aggregate counts in
    /// the dashboard.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Last time the app called in. Drives the active-installs figure, and lets
    /// a long-silent device be excluded from a fan-out rather than pushed to
    /// forever.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The content version this device last synced. Compared against
    /// <c>AppConfiguration.ContentVersion</c> to decide whether it has anything
    /// to fetch — the app asks on every launch and the usual answer is "nothing".
    /// </summary>
    public int SyncedContentVersion { get; set; }
}
