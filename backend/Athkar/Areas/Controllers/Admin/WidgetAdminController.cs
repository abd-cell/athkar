using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Widgets;
using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/widget")]
public class WidgetAdminController : BaseApiController
{
    private readonly IWidgetService service;

    public WidgetAdminController(IWidgetService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<WidgetSettingsOutput>> Get() => service.Get();

    [HttpPut]
    public Task<BaseResponse<WidgetSettingsOutput>> Update([FromBody] WidgetSettingsInput input) =>
        service.Update(input);
}
