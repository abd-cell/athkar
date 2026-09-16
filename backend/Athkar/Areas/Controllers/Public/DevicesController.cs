using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Devices;
using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

/// <summary>
/// The anonymous install's own row and inbox. Identified by the
/// <c>X-Device-Key</c> header, which is not an authentication token — see
/// <c>IDeviceContext</c> for what it is and is not.
/// </summary>
[Route("api/v1/devices")]
public class DevicesController : BaseApiController
{
    private readonly IDeviceService service;

    public DevicesController(IDeviceService service) => this.service = service;

    [HttpPost("register")]
    public Task<BaseResponse<DeviceOutput>> Register([FromBody] DeviceRegistrationInput input) =>
        service.Register(input);

    /// <summary>
    /// A token rotation, which FCM can spring at any moment. Separate from
    /// registration so it can be called the instant it happens rather than at
    /// the next cold launch.
    /// </summary>
    [HttpPost("push-token")]
    public Task<BaseResponse> UpdatePushToken([FromBody] PushTokenInput input) =>
        service.UpdatePushToken(input);

    [HttpGet("notifications")]
    public Task<BaseResponse<PageOutput<NotificationOutput>>> Notifications([FromQuery] PageInput input) =>
        service.Notifications(input);

    [HttpPost("notifications/{id:int}/read")]
    public Task<BaseResponse> MarkRead(int id) => service.MarkRead(id);

    [HttpPost("notifications/read-all")]
    public Task<BaseResponse> MarkAllRead() => service.MarkAllRead();

    /// <summary>The whole of what "delete my data" can mean for an account that never existed.</summary>
    [HttpDelete]
    public Task<BaseResponse> Forget() => service.Forget();
}
