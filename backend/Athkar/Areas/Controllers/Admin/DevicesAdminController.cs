using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Devices;
using Athkar.Areas.Services.Devices.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The installs, for the console.
///
/// Read-only, and it will stay that way: everything an admin can do *to* an
/// install is already a message — a broadcast addressed to its key. There is no
/// edit here because there is nothing on the row that belongs to anyone but the
/// reader who set it.
///
/// Note the route and the attribute together: this is staff-authenticated, not
/// device-authenticated. <c>X-Device-Key</c> says which install is calling and
/// is not authentication (<c>docs/BUSINESS_LOGIC.md</c> §2), so an admin view
/// of every install must never sit behind it.
/// </summary>
[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/devices")]
public class DevicesAdminController : BaseApiController
{
    private readonly IDeviceAdminService service;

    public DevicesAdminController(IDeviceAdminService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<PageOutput<DeviceAdminOutput>>> List([FromQuery] DeviceQueryInput input) =>
        service.List(input);

    [HttpGet("{id:int}")]
    public Task<BaseResponse<DeviceAdminOutput>> Get(int id) => service.Get(id);

    [HttpGet("{id:int}/inbox")]
    public Task<BaseResponse<PageOutput<DeviceInboxOutput>>> Inbox(int id, [FromQuery] PageInput input) =>
        service.Inbox(id, input);

    /// <summary>
    /// One install's whole FCM token.
    ///
    /// A POST rather than a GET, and one install at a time: this is an action
    /// with a consequence — it is written to the audit trail — not a view. The
    /// list carries only the last few characters.
    /// </summary>
    [HttpPost("{id:int}/push-token")]
    public Task<BaseResponse<string>> RevealPushToken(int id) => service.RevealPushToken(id);
}
