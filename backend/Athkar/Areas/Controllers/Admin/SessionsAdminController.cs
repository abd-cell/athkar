using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Staff;
using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>
/// Staff sessions: who is signed in to the console, and ending it.
///
/// Not to be confused with the installs under <c>admin/devices</c>. A reader's
/// app has no session at all — it is anonymous and holds no token that grants
/// anything (<c>docs/BUSINESS_LOGIC.md</c> §2). Everything here is a member of
/// staff signed in to the CMS.
/// </summary>
[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/sessions")]
public class SessionsAdminController : BaseApiController
{
    private readonly ISessionAdminService service;

    public SessionsAdminController(ISessionAdminService service) => this.service = service;

    [HttpGet]
    public Task<BaseResponse<PageOutput<SessionOutput>>> List([FromQuery] SessionQueryInput input) =>
        service.List(input);

    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Revoke(int id) => service.Revoke(id);

    [HttpDelete("user/{userId:int}")]
    public Task<BaseResponse<int>> RevokeAllFor(int userId) => service.RevokeAllFor(userId);
}
