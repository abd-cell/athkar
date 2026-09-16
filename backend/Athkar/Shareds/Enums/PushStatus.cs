namespace Athkar.Shareds.Enums;

/// <summary>The outcome of one attempt to push to one device.</summary>
public enum PushStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,

    /// <summary>
    /// Not attempted, and that is the correct outcome — the device has muted
    /// notifications or holds no push token. Distinct from
    /// <see cref="Failed"/> so the delivery rate in the CMS counts what was
    /// actually tried.
    /// </summary>
    Skipped = 3,

    /// <summary>
    /// FCM reported the token as gone. The token is cleared from the device row
    /// when this happens, so the next pass does not try it again.
    /// </summary>
    TokenExpired = 4,
}
