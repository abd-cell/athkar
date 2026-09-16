using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Staff.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Staff;

public class SessionAdminService : ISessionAdminService
{
    private readonly IRepository<UserLogin> logins;
    private readonly IRepository<User> users;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public SessionAdminService(
        IRepository<UserLogin> logins,
        IRepository<User> users,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.logins = logins;
        this.users = users;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<SessionOutput>>> List(SessionQueryInput input)
    {
        var now = DateTime.UtcNow;
        var current = securityManager.SessionKey;

        var query = logins.Query();

        if (input.UserId is { } userId) query = query.Where(l => l.UserId == userId);
        if (!input.IncludeExpired) query = query.Where(l => l.RefreshExpiresAt > now);

        // Searching staff, not sessions: nobody knows a session by anything
        // about the session itself.
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(l => l.User != null &&
                (l.User.Email.Contains(term) || l.User.FullName.Contains(term)));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(l => l.LastUsedAt)
            .ThenByDescending(l => l.Id)
            .Paginate(input)
            // The session key and the refresh token are both absent by
            // construction, not by filtering later: neither is named here, so
            // neither can be added to the screen by accident.
            .Select(l => new SessionOutput
            {
                Id = l.Id,
                UserId = l.UserId,
                UserName = l.User == null ? "" : l.User.FullName,
                Email = l.User == null ? "" : l.User.Email,
                UserAgent = l.UserAgent,
                IpAddress = l.IpAddress,
                SignedInAt = l.CreationDate,
                LastUsedAt = l.LastUsedAt,
                RefreshExpiresAt = l.RefreshExpiresAt,
                IsExpired = l.RefreshExpiresAt <= now,
                IsCurrent = current != null && l.SessionKey == current,
            })
            .ToListAsync();

        await Roles(rows);

        return new BaseResponse<PageOutput<SessionOutput>>(new PageOutput<SessionOutput>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse> Revoke(int id)
    {
        var session = await logins.GetByIdAsync(id);
        if (session is null) return BaseResponse.Fail(ErrorCode.SessionNotFound);

        if (!await MayRevoke(session.UserId))
            return BaseResponse.Fail(ErrorCode.CannotRevokeHigherRole);

        logins.SoftDelete(session);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.SessionRevoke, nameof(UserLogin), session.Id);

        return new BaseResponse();
    }

    public async Task<BaseResponse<int>> RevokeAllFor(int userId)
    {
        if (!await users.AnyAsync(u => u.Id == userId))
            return BaseResponse<int>.Fail(ErrorCode.NotFound);

        if (!await MayRevoke(userId))
            return BaseResponse<int>.Fail(ErrorCode.CannotRevokeHigherRole);

        var sessions = await logins.Query().Where(l => l.UserId == userId).ToListAsync();

        logins.SoftDeleteRange(sessions);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.SessionRevoke, nameof(User), userId,
            null, new { Revoked = sessions.Count });

        return new BaseResponse<int>(sessions.Count);
    }

    /// <summary>
    /// Whether the caller outranks the account whose session this is.
    ///
    /// Revoking your own is always allowed — "sign out everywhere" has to
    /// include the machine you are sitting at — and so is revoking an equal's,
    /// which is what makes a shared admin account manageable. What is refused is
    /// reaching *up*: an admin locking out the superadmin who could undo it.
    /// </summary>
    private async Task<bool> MayRevoke(int ownerId)
    {
        if (ownerId == securityManager.UserId) return true;

        // Projected rather than Included: the roles are all that is wanted, and
        // a projection is the one shape that behaves the same under EF and
        // under the plain lists the tests run on.
        var held = await users.Query()
            .Where(u => u.Id == ownerId)
            .SelectMany(u => u.Roles.Where(r => !r.IsDeleted), (u, r) => r.Role)
            .ToListAsync();

        var highest = held.DefaultIfEmpty(Shareds.Enums.Roles.Editor).Max();

        return securityManager.IsAtLeast(highest);
    }

    /// <summary>
    /// Roles for everyone on the page, in one query.
    ///
    /// Kept out of the projection deliberately: a collection inside a `Select`
    /// is a join that multiplies the rows, and the paging above would then
    /// count a superadmin's three roles as three sessions.
    /// </summary>
    private async Task Roles(List<SessionOutput> rows)
    {
        if (rows.Count == 0) return;

        var ids = rows.Select(r => r.UserId).Distinct().ToList();

        var pairs = await users.Query()
            .Where(u => ids.Contains(u.Id))
            .SelectMany(u => u.Roles.Where(r => !r.IsDeleted), (u, r) => new { u.Id, r.Role })
            .ToListAsync();

        foreach (var row in rows)
            row.Roles = [.. pairs.Where(p => p.Id == row.UserId).Select(p => p.Role)];
    }
}
