using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Staff.Models;

/// <summary>How the console asks for staff sessions.</summary>
public class SessionQueryInput : PageInput
{
    /// <summary>One member of staff's sessions — every device they are signed in on.</summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Include sessions whose refresh window has lapsed.
    ///
    /// Off by default: the question this screen answers is "who can act right
    /// now", and a lapsed row cannot act. It is still worth being able to ask —
    /// a sign-in from somewhere unexpected is interesting after it ends, not
    /// only while it lasts.
    /// </summary>
    public bool IncludeExpired { get; set; }
}

/// <summary>
/// One signed-in staff session.
///
/// Neither the session key nor the refresh token is here, and neither ever will
/// be: both are bearer credentials, and a screen that prints one turns "look at
/// who is signed in" into "take over their account". The row id is enough to
/// revoke by, and it grants nothing.
/// </summary>
public class SessionOutput
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<Roles> Roles { get; set; } = [];

    /// <summary>As the browser reported it. Untrusted text — the CMS must not render it as markup.</summary>
    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    public DateTime SignedInAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public DateTime RefreshExpiresAt { get; set; }

    /// <summary>Past its refresh window: it can no longer be renewed into a live token.</summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// The session making this very request.
    ///
    /// Marked so the CMS can say so plainly. Revoking it is allowed — "sign out
    /// everywhere" has to include here — but it should never happen by accident
    /// because one row looked like any other.
    /// </summary>
    public bool IsCurrent { get; set; }
}
