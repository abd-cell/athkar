using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Reminders;
using Athkar.Areas.Services.Reminders.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/reminders")]
public class RemindersAdminController : BaseApiController
{
    private readonly IReminderService service;

    public RemindersAdminController(IReminderService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<PageOutput<ReminderOutput>>> List([FromQuery] PageInput input) =>
        service.List(input);

    [HttpGet("{id:int}")]
    public Task<BaseResponse<ReminderOutput>> Get(int id) => service.Get(id);

    [HttpPost]
    public Task<BaseResponse<ReminderOutput>> Create([FromBody] ReminderInput input) =>
        service.Create(input);

    [HttpPut("{id:int}")]
    public Task<BaseResponse<ReminderOutput>> Update(int id, [FromBody] ReminderInput input) =>
        service.Update(id, input);

    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.Delete(id);
}
