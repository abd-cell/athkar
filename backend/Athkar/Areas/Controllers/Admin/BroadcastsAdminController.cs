using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Notifications;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/broadcasts")]
public class BroadcastsAdminController : BaseApiController
{
    private readonly IBroadcastService service;

    public BroadcastsAdminController(IBroadcastService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<PageOutput<BroadcastOutput>>> List([FromQuery] PageInput input) =>
        service.List(input);

    [HttpGet("{id:int}")]
    public Task<BaseResponse<BroadcastOutput>> Get(int id) => service.Get(id);

    [HttpPost]
    public Task<BaseResponse<BroadcastOutput>> Create([FromBody] BroadcastInput input) =>
        service.Create(input);

    [HttpPut("{id:int}")]
    public Task<BaseResponse<BroadcastOutput>> Update(int id, [FromBody] BroadcastInput input) =>
        service.Update(id, input);

    /// <summary>Queues it. The worker does the fan-out; this call returns at once.</summary>
    [HttpPost("{id:int}/send")]
    public Task<BaseResponse<BroadcastOutput>> Send(int id) => service.Send(id);

    [HttpPost("{id:int}/cancel")]
    public Task<BaseResponse<BroadcastOutput>> Cancel(int id) => service.Cancel(id);

    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.Delete(id);
}
