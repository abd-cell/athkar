using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Staff;
using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

/// <summary>Sign-in for the control panel. The app never calls any of this.</summary>
[Route("api/v1/auth")]
public class AuthController : BaseApiController
{
    private readonly IAuthService service;

    public AuthController(IAuthService service) => this.service = service;

    [HttpPost("login")]
    public Task<BaseResponse<AuthOutput>> Login([FromBody] LoginInput input) => service.Login(input);

    [HttpPost("refresh")]
    public Task<BaseResponse<AuthOutput>> Refresh([FromBody] RefreshInput input) => service.Refresh(input);

    [AppAuthorize]
    [HttpPost("logout")]
    public Task<BaseResponse> Logout() => service.Logout();

    [AppAuthorize]
    [HttpGet("me")]
    public Task<BaseResponse<StaffOutput>> Me() => service.Me();
}
