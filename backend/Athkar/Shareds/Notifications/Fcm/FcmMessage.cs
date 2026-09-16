namespace Athkar.Shareds.Notifications.Fcm;

/// <summary>
/// One push, already resolved into the words a particular device will see.
///
/// The data payload is strings only because that is all FCM carries, and it is
/// what the app routes on: <c>kind</c> plus whichever id the kind implies. See
/// <c>app/athkar_app/lib/core/notification_router.dart</c>.
/// </summary>
public sealed class FcmMessage
{
    public required string Token { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }

    public Dictionary<string, string> Data { get; init; } = [];

    /// <summary>
    /// The Android notification channel to raise this on. Channels are created
    /// by the app and cannot be reconfigured afterwards, so the name here has to
    /// match one the installed app already knows — see
    /// <c>docs/BUSINESS_LOGIC.md</c> §6.
    /// </summary>
    public string? AndroidChannelId { get; init; }

    /// <summary>
    /// Whether this may break through Focus and Do Not Disturb.
    ///
    /// True for prayer times and nothing else. A prayer time is the one thing
    /// this system sends that is worthless a few minutes late, and that is the
    /// whole justification for the level — a reader who silenced their phone
    /// deliberately should not also be interrupted for an announcement.
    ///
    /// On iOS this needs the Time Sensitive Notifications entitlement on the app
    /// target; without it the level is quietly downgraded and the notification
    /// still arrives, merely respecting Focus. On Android the equivalent is the
    /// channel's own importance, which the app fixed when it created it.
    /// </summary>
    public bool TimeSensitive { get; init; }
}
