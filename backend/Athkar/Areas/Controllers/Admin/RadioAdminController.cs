using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Radio;
using Athkar.Areas.Services.Radio.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// The station list. Open to editors for the same reason the rest of the
/// content area is: replacing a stream that has gone dead is an editorial
/// errand, not an administrative one.
/// </summary>
[AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/radio")]
public class RadioAdminController : BaseApiController
{
    private readonly IRadioAdminService service;

    public RadioAdminController(IRadioAdminService service) => this.service = service;

    [HttpGet("stations")]
    public Task<BaseResponse<PageOutput<AdminRadioStationOutput>>> List([FromQuery] PageInput input) =>
        service.ListStations(input);

    [HttpGet("stations/{id:int}")]
    public Task<BaseResponse<AdminRadioStationOutput>> Get(int id) => service.GetStation(id);

    [HttpPost("stations")]
    public Task<BaseResponse<AdminRadioStationOutput>> Create([FromBody] RadioStationInput input) =>
        service.CreateStation(input);

    [HttpPut("stations/{id:int}")]
    public Task<BaseResponse<AdminRadioStationOutput>> Update(int id, [FromBody] RadioStationInput input) =>
        service.UpdateStation(id, input);

    [HttpDelete("stations/{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.DeleteStation(id);

    [HttpPost("stations/reorder")]
    public Task<BaseResponse> Reorder([FromBody] ReorderInput input) => service.ReorderStations(input);
}
