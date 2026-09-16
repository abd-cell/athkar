using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Athkar.Shareds.Models.Config;

namespace Athkar.Shareds.Notifications.Fcm;

/// <summary>
/// <see cref="IFcmSender"/> over the Firebase Admin SDK.
///
/// The app is initialised once, lazily, and a failure to initialise is recorded
/// rather than thrown: a bad credentials file should cost push, not the process.
/// </summary>
public class FcmSender : IFcmSender
{
    private const string FirebaseAppName = "athkar";

    private readonly FcmSettings settings;
    private readonly ILogger<FcmSender> logger;
    private readonly FirebaseApp? app;

    public FcmSender(IOptions<FcmSettings> options, IHostEnvironment environment, ILogger<FcmSender> logger)
    {
        settings = options.Value;
        this.logger = logger;

        if (!settings.Enabled)
        {
            logger.LogInformation("FCM is disabled by configuration; push will be skipped.");
            return;
        }

        try
        {
            var credential = LoadCredential(environment);
            if (credential is null)
            {
                logger.LogWarning("FCM is enabled but no credentials were found; push will be skipped.");
                return;
            }

            app = FirebaseApp.GetInstance(FirebaseAppName)
                  ?? FirebaseApp.Create(new AppOptions { Credential = credential }, FirebaseAppName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FCM failed to initialise; push will be skipped.");
        }
    }

    public bool IsConfigured => app is not null;

    public async Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<FcmMessage> messages, CancellationToken ct = default)
    {
        if (messages.Count == 0) return [];

        if (!IsConfigured)
            return [.. messages.Select(_ => FcmSendResult.Failed("FCM is not configured."))];

        var messaging = FirebaseMessaging.GetMessaging(app);
        var results = new List<FcmSendResult>(messages.Count);

        foreach (var batch in Chunk(messages, Math.Clamp(settings.BatchSize, 1, 500)))
        {
            // SendEachAsync rather than SendMulticastAsync: one bad token must
            // not fail its 499 neighbours, and the per-message responses come
            // back in the order sent, which is how each result is matched to the
            // device row it belongs to.
            try
            {
                var response = await messaging.SendEachAsync([.. batch.Select(Build)], ct);
                results.AddRange(response.Responses.Select(Interpret));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FCM batch send failed for {Count} messages.", batch.Count);
                results.AddRange(batch.Select(_ => FcmSendResult.Failed(ex.Message)));
            }
        }

        return results;
    }

    private static Message Build(FcmMessage message) => new()
    {
        // `Token`, not `Fid`, and the SDK's own deprecation notice is a trap:
        // it marks `Token` obsolete and points at `Fid`, but the two are not the
        // same value. `Fid` is a *Firebase Installation ID* — a different,
        // shorter identifier — and FCM answers a registration token sent in that
        // field with `NotRegistered`. Which looks exactly like a reader who
        // uninstalled the app, so the dispatcher dutifully retires a perfectly
        // good token and the install goes quiet for good.
        //
        // Verified against the REST API directly: the same token FCM rejected
        // through `Fid` was accepted (HTTP 200) as `token`.
#pragma warning disable CS0618
        Token = message.Token,
#pragma warning restore CS0618
        Notification = new Notification { Title = message.Title, Body = message.Body },
        Data = message.Data,
        Android = new AndroidConfig
        {
            Priority = Priority.High,
            Notification = new AndroidNotification { ChannelId = message.AndroidChannelId },
        },
        Apns = new ApnsConfig
        {
            Aps = new Aps { Sound = "default", MutableContent = true },

            // `interruption-level` has no typed home on Aps in this SDK, so it
            // goes in the custom data alongside it — which is where APNs reads
            // it from either way.
            CustomData = message.TimeSensitive
                ? new Dictionary<string, object> { ["interruption-level"] = "time-sensitive" }
                : null,
        },
    };

    private static FcmSendResult Interpret(SendResponse response)
    {
        if (response.IsSuccess) return FcmSendResult.Sent(response.MessageId);

        var error = response.Exception;
        var reason = error?.Message ?? "Unknown FCM error.";

        // The two codes that mean the install is gone rather than unreachable.
        return error?.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument
            ? FcmSendResult.Expired(reason)
            : FcmSendResult.Failed(reason);
    }

    // FromJson/FromFile are marked obsolete in favour of a CredentialFactory that
    // this version of Google.Apis.Auth (1.67.0, pulled transitively by
    // FirebaseAdmin 3.6) does not yet ship. Suppressed rather than worked around
    // with reflection; revisit when the dependency moves.
#pragma warning disable CS0618
    private GoogleCredential? LoadCredential(IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(settings.CredentialsJson))
            return GoogleCredential.FromJson(settings.CredentialsJson);

        if (string.IsNullOrWhiteSpace(settings.CredentialsPath)) return null;

        var path = Path.IsPathRooted(settings.CredentialsPath)
            ? settings.CredentialsPath
            : Path.Combine(environment.ContentRootPath, settings.CredentialsPath);

        return File.Exists(path) ? GoogleCredential.FromFile(path) : null;
    }
#pragma warning restore CS0618

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return [.. source.Skip(i).Take(size)];
    }
}
