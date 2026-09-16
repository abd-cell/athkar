using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Devices;

/// <summary>
/// Installs, as the console needs to see them.
///
/// Separate from <see cref="IDeviceService"/> deliberately: that one is the
/// app's own endpoint and answers to a device key, this one answers to a staff
/// session and is read-only. Keeping them apart is what stops an admin route
/// ever appearing behind <c>X-Device-Key</c>, which is not authentication.
///
/// This exists because the push manager can say "seven installs hold no token"
/// and an admin could do nothing with the sentence — not see which, not send a
/// test to one, not tell a fortnight-old install from a fresh one. There is no
/// personal data to expose here: the row is a self-minted key, a platform, a
/// language, a zone and a date, and the token is never returned.
/// </summary>
[ScopedInjectable]
public interface IDeviceAdminService
{
    /// <summary>A filtered page of installs, newest contact first.</summary>
    Task<BaseResponse<PageOutput<DeviceAdminOutput>>> List(DeviceQueryInput input);

    /// <summary>One install by id.</summary>
    Task<BaseResponse<DeviceAdminOutput>> Get(int id);

    /// <summary>
    /// What actually reached an install's inbox.
    ///
    /// The delivery log says what the pipeline attempted; this says what the
    /// reader can see. They differ whenever a push failed but the message was
    /// still stored — which is the normal case for a muted or tokenless
    /// install, and the thing hardest to believe without looking.
    /// </summary>
    Task<BaseResponse<PageOutput<DeviceInboxOutput>>> Inbox(int id, PageInput input);

    /// <summary>
    /// The whole FCM registration token for one install.
    ///
    /// Deliberately its own call rather than a column on the list. The token is
    /// a capability — with the server's credentials, whoever holds it can raise
    /// a notification on that phone — so it is read one install at a time, on
    /// purpose, and every read is written to the audit trail. The list carries
    /// only the last few characters, which is what an admin actually needs to
    /// match a row against the Firebase console.
    /// </summary>
    Task<BaseResponse<string>> RevealPushToken(int id);
}
