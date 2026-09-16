using Athkar.Shareds.Attributes;

namespace Athkar.Shareds.Notifications.Fcm;

/// <summary>
/// Pushes to Firebase. Credential-gated: with no service account configured,
/// every send reports a skip and the rest of the notification pipeline — the
/// stored inbox row, the delivery record — carries on working. A development
/// machine without Firebase is a supported configuration, not a broken one.
/// </summary>
[SingletonInjectable]
public interface IFcmSender
{
    /// <summary>False when no credentials are configured, so callers can skip rather than fail.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends a batch, returning one result per message in the order given.
    /// Batches larger than the FCM multicast cap are split internally.
    /// </summary>
    Task<IReadOnlyList<FcmSendResult>> SendAsync(IReadOnlyList<FcmMessage> messages, CancellationToken ct = default);
}
