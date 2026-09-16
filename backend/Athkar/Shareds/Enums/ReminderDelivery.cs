namespace Athkar.Shareds.Enums;

/// <summary>
/// Which side actually raises the notification — the one split that keeps this
/// system honest about its two conflicting promises.
///
/// The product promises reminders that work with no network and land at exactly
/// the right minute; it also promises an admin who can change the wording and
/// the schedule from the CMS without shipping an app update. Neither delivery
/// route gives both, so the campaign says which one it is and the split is
/// visible in the CMS rather than hidden in code.
/// </summary>
public enum ReminderDelivery
{
    /// <summary>
    /// The server sends it over FCM at the scheduled instant. The admin owns the
    /// timing completely, and the device needs a network connection to hear it.
    /// Suits anything tied to a clock or to an occasion.
    /// </summary>
    ServerPush = 1,

    /// <summary>
    /// The device schedules it locally, in advance, from the campaign it last
    /// synced. Fires offline and to the minute — which is the only way to hang a
    /// reminder off a prayer time, because prayer times are computed on the
    /// device from coordinates the server never sees.
    /// </summary>
    DeviceLocal = 2,
}
