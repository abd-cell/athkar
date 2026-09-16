using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Support;
using Athkar.Areas.Services.Support.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/support")]
public class SupportAdminController : BaseApiController
{
    private readonly ISupportService service;

    public SupportAdminController(ISupportService service) => this.service = service;

    [HttpGet("faq")]
    public Task<BaseResponse<PageOutput<AdminFaqOutput>>> ListFaq([FromQuery] PageInput input) =>
        service.ListFaq(input);

    [HttpPost("faq")]
    public Task<BaseResponse<AdminFaqOutput>> CreateFaq([FromBody] FaqInput input) =>
        service.CreateFaq(input);

    [HttpPut("faq/{id:int}")]
    public Task<BaseResponse<AdminFaqOutput>> UpdateFaq(int id, [FromBody] FaqInput input) =>
        service.UpdateFaq(id, input);

    [HttpDelete("faq/{id:int}")]
    public Task<BaseResponse> DeleteFaq(int id) => service.DeleteFaq(id);

    [HttpGet("feedback")]
    public Task<BaseResponse<PageOutput<FeedbackOutput>>> ListFeedback(
        [FromQuery] FeedbackStatus? status, [FromQuery] PageInput input) =>
        service.ListFeedback(status, input);

    [HttpPost("feedback/{id:int}/reply")]
    public Task<BaseResponse<FeedbackOutput>> Reply(int id, [FromBody] FeedbackReplyInput input) =>
        service.Reply(id, input);
}
