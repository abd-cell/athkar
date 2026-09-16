namespace Athkar.Shareds.Notifications.Fcm;

/// <summary>
/// What became of one push.
///
/// <see cref="TokenExpired"/> is separated from a plain failure because it is
/// the only outcome that says something about the *device* rather than about the
/// attempt: the caller clears the token so no later campaign wastes a slot on
/// an install that is gone.
/// </summary>
public sealed record FcmSendResult(bool Success, bool TokenExpired, string? MessageId, string? Error)
{
    public static FcmSendResult Sent(string? messageId) => new(true, false, messageId, null);
    public static FcmSendResult Failed(string error) => new(false, false, null, error);
    public static FcmSendResult Expired(string error) => new(false, true, null, error);
}
