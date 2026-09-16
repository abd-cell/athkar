using Microsoft.AspNetCore.Authorization;
using Athkar.Shareds.Enums;

namespace Athkar.Shareds.Attributes;

/// <summary>
/// Role-aware authorization. Usage: <c>[AppAuthorize]</c> (any authenticated
/// admin) or <c>[AppAuthorize(Roles.SuperAdmin)]</c> to narrow further.
///
/// Every authenticated caller in this system is a member of staff: the app's own
/// users are anonymous devices and never hold a token. See
/// <c>docs/BUSINESS_LOGIC.md</c> §2.
/// </summary>
public sealed class AppAuthorizeAttribute : AuthorizeAttribute
{
    public AppAuthorizeAttribute(params Roles[] roles)
    {
        if (roles.Length > 0)
            Roles = string.Join(',', roles.Select(r => r.ToString()));
    }
}
