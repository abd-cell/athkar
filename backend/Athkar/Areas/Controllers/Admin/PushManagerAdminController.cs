using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Notifications;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The push manager: what the pipeline can reach and what became of what was
/// sent, plus a send-now for a message that needs no schedule.
///
/// Scheduling and editing stay on <c>BroadcastsAdminController</c> — the send
/// here delegates to the same service, so there is one sending path and one
/// history whichever screen an admin uses.
/// </summary>
[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/push")]
public class PushManagerAdminController : BaseApiController
{
    private readonly IPushManagerService service;

    public PushManagerAdminController(IPushManagerService service) => this.service = service;

    [HttpGet("overview")]
    public Task<BaseResponse<PushOverviewOutput>> Overview([FromQuery] int windowDays = 7) =>
        service.Overview(windowDays);

    [HttpPost("send")]
    public Task<BaseResponse<BroadcastOutput>> Send([FromBody] BroadcastInput input) =>
        service.Send(input);

    [HttpGet("dispatches")]
    public Task<BaseResponse<PageOutput<PushDispatchOutput>>> Dispatches(
        [FromQuery] PushDispatchQueryInput input) =>
        service.Dispatches(input);

    [HttpPost("dispatches/{id:int}/retry")]
    public Task<BaseResponse<PushDispatchOutput>> Retry(int id) => service.Retry(id);

    [HttpPost("dispatches/{id:int}/cancel")]
    public Task<BaseResponse<PushDispatchOutput>> CancelDispatch(int id) =>
        service.CancelDispatch(id);

    /// <summary>
    /// Runs the pipeline now. Idempotent by construction — it is the same
    /// dispatcher the workers call — so pressing it while they are running
    /// duplicates nothing.
    /// </summary>
    [HttpPost("run")]
    public Task<BaseResponse<PushRunOutput>> RunNow() => service.RunNow();
}
