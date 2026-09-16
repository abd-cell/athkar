using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Support;
using Athkar.Areas.Services.Support.Models;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Public;

[Route("api/v1/support")]
public class SupportController : BaseApiController
{
    private readonly ISupportService service;

    public SupportController(ISupportService service) => this.service = service;

    [HttpGet("faq")]
    public Task<BaseResponse<List<FaqOutput>>> Faq([FromQuery] string? language) =>
        service.Faq(language);

    [HttpPost("feedback")]
    public Task<BaseResponse> Feedback([FromBody] FeedbackInput input) =>
        service.SubmitFeedback(input);
}
