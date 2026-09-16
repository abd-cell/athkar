using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Devices;

[ScopedInjectable]
public interface IDeviceService
{
    /// <summary>
    /// Creates or updates the calling install's row. Idempotent on the device
    /// key — the app calls it on every launch without checking.
    /// </summary>
    Task<BaseResponse<DeviceOutput>> Register(DeviceRegistrationInput input);

    /// <summary>
    /// Updates only the push token, for a rotation the app learns about while
    /// it is running. Refuses an unknown device key rather than creating a row
    /// — see the input type for why.
    /// </summary>
    Task<BaseResponse> UpdatePushToken(PushTokenInput input);

    /// <summary>The calling device's inbox, newest first.</summary>
    Task<BaseResponse<PageOutput<NotificationOutput>>> Notifications(PageInput input);

    Task<BaseResponse> MarkRead(int notificationId);

    Task<BaseResponse> MarkAllRead();

    /// <summary>
    /// Forgets the calling install: clears the push token, soft-deletes the row
    /// and its inbox. The app offers this in settings, and it is the whole of
    /// what "delete my data" can mean for an account that never existed.
    /// </summary>
    Task<BaseResponse> Forget();

    /// <summary>Aggregate counts for the CMS dashboard. Never per-device.</summary>
    Task<BaseResponse<DeviceStatsOutput>> Stats();
}
