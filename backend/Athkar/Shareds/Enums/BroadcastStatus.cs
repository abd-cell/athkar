namespace Athkar.Shareds.Enums;

/// <summary>
/// Where a one-off admin message is in its life.
///
/// <see cref="Sending"/> is a real state and not a transient: a fan-out over
/// every device takes long enough that the CMS has to be able to show it, and
/// the worker uses it to claim a row so two passes cannot send the same message
/// twice.
/// </summary>
public enum BroadcastStatus
{
    Draft = 0,
    Scheduled = 1,
    Sending = 2,
    Sent = 3,

    /// <summary>Every recipient failed. A partial failure is still <see cref="Sent"/>, with counts.</summary>
    Failed = 4,

    Cancelled = 5,
}
