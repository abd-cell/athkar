using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Staff;

[ScopedInjectable]
public interface IAuthService
{
    Task<BaseResponse<AuthOutput>> Login(LoginInput input);

    /// <summary>Trades a refresh token for a new pair. The old refresh token stops working.</summary>
    Task<BaseResponse<AuthOutput>> Refresh(RefreshInput input);

    /// <summary>Ends the calling session. Takes effect on the very next request.</summary>
    Task<BaseResponse> Logout();

    /// <summary>The signed-in user, for the CMS to render its own header.</summary>
    Task<BaseResponse<StaffOutput>> Me();
}
