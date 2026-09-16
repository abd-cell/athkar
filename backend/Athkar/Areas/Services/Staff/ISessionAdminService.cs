using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Staff;

/// <summary>
/// Who is signed in to the console, and the ability to end it.
///
/// The session rows already exist — every request re-checks one, which is what
/// makes logout immediate (see <c>OnTokenValidated</c> in <c>Program.cs</c>).
/// What was missing is anyone being able to *look*: a signed token is valid
/// until it expires, so "an editor left" and "somebody is signed in from a
/// machine nobody recognises" were both invisible and neither could be acted
/// on. This is the smallest thing that fixes that — a list and a revoke.
/// </summary>
[ScopedInjectable]
public interface ISessionAdminService
{
    /// <summary>Live staff sessions, most recently used first.</summary>
    Task<BaseResponse<PageOutput<SessionOutput>>> List(SessionQueryInput input);

    /// <summary>
    /// Ends one session. The next request carrying its token fails, because
    /// every request looks the row up rather than trusting the signature.
    ///
    /// An admin may not revoke a session belonging to someone above them on the
    /// ladder: the account that can create and delete staff is not one that a
    /// lesser role gets to lock out.
    /// </summary>
    Task<BaseResponse> Revoke(int id);

    /// <summary>
    /// Ends every session for one member of staff — "sign out everywhere", for
    /// a lost laptop or a leaver. Returns how many were ended.
    /// </summary>
    Task<BaseResponse<int>> RevokeAllFor(int userId);
}
