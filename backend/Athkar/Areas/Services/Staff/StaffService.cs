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

/// <summary>
/// Staff accounts. Super-admin only at the controller.
///
/// The one invariant worth naming: there is always at least one usable
/// super-admin. Every path that could remove the last one — deleting, disabling,
/// demoting — asks <see cref="WouldStrandTheConsole"/> first, because a CMS
/// nobody can sign into is not recoverable from the CMS.
/// </summary>
public class StaffService : IStaffService
{
    private readonly IRepository<User> users;
    private readonly IRepository<UserRole> roles;
    private readonly IRepository<UserLogin> logins;
    private readonly IUnitOfWork unitOfWork;
    private readonly IPasswordHasher passwordHasher;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public StaffService(
        IRepository<User> users,
        IRepository<UserRole> roles,
        IRepository<UserLogin> logins,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.users = users;
        this.roles = roles;
        this.logins = logins;
        this.unitOfWork = unitOfWork;
        this.passwordHasher = passwordHasher;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<StaffOutput>>> List(PageInput input)
    {
        var query = users.Query().Include(u => u.Roles).AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(u => u.FullName)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<StaffOutput>>(new PageOutput<StaffOutput>
        {
            Data = [.. rows.Select(u => new StaffOutput(u))],
            TotalRows = total,
        });
    }

    public async Task<BaseResponse<StaffOutput>> Create(StaffInput input)
    {
        var email = input.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(input.Password))
            return BaseResponse<StaffOutput>.Fail(ErrorCode.ValidationError,
                "A password is required when creating an account.");

        if (await users.AnyAsync(u => u.Email == email))
            return BaseResponse<StaffOutput>.Fail(ErrorCode.EmailAlreadyRegistered);

        var user = new User
        {
            Email = email,
            FullName = input.FullName.Trim(),
            PasswordHash = passwordHasher.Hash(input.Password),
            LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
            IsActive = input.IsActive,
            CreatedBy = securityManager.UserId,
        };

        foreach (var role in input.Roles.Distinct())
            user.Roles.Add(new UserRole { Role = role });

        await users.AddAsync(user);
        await unitOfWork.SaveAsync();

        var output = new StaffOutput(user);
        await auditService.LogAsync(AuditActions.StaffCreate, nameof(User), user.Id, null, output);

        return new BaseResponse<StaffOutput>(output);
    }

    public async Task<BaseResponse<StaffOutput>> Update(int id, StaffInput input)
    {
        var user = await users.Query()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return BaseResponse<StaffOutput>.Fail(ErrorCode.NotFound);

        var email = input.Email.Trim().ToLowerInvariant();
        if (email != user.Email && await users.AnyAsync(u => u.Email == email))
            return BaseResponse<StaffOutput>.Fail(ErrorCode.EmailAlreadyRegistered);

        var wanted = input.Roles.Distinct().ToHashSet();
        var losingTheKeys = !input.IsActive || !wanted.Contains(Roles.SuperAdmin);

        if (losingTheKeys && await WouldStrandTheConsole(user))
            return BaseResponse<StaffOutput>.Fail(ErrorCode.LastAdministrator);

        var before = new StaffOutput(user);

        user.Email = email;
        user.FullName = input.FullName.Trim();
        user.LanguageCode = input.LanguageCode.Trim().ToLowerInvariant();
        user.IsActive = input.IsActive;

        if (!string.IsNullOrWhiteSpace(input.Password))
            user.PasswordHash = passwordHasher.Hash(input.Password);

        user.ModifiedBy = securityManager.UserId;
        users.Update(user);

        SyncRoles(user, wanted);

        // A disabled account keeps its rows but must lose its sessions in the
        // same breath — otherwise the token in a browser tab outlives the
        // decision to revoke it by up to an hour.
        if (!user.IsActive) await RevokeSessions(user.Id);

        await unitOfWork.SaveAsync();

        var output = new StaffOutput(user);
        await auditService.LogAsync(AuditActions.StaffUpdate, nameof(User), user.Id, before, output);

        return new BaseResponse<StaffOutput>(output);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var user = await users.Query()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return BaseResponse.Fail(ErrorCode.NotFound);

        if (await WouldStrandTheConsole(user))
            return BaseResponse.Fail(ErrorCode.LastAdministrator);

        users.SoftDelete(user);
        await RevokeSessions(user.Id);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.StaffDelete, nameof(User), user.Id,
            new StaffOutput(user), null);

        return new BaseResponse();
    }

    /// <summary>
    /// True when <paramref name="user"/> is the only super-admin who could still
    /// sign in, so whatever is about to happen to them would lock everyone out.
    /// </summary>
    private async Task<bool> WouldStrandTheConsole(User user)
    {
        if (!user.Roles.Any(r => !r.IsDeleted && r.Role == Roles.SuperAdmin)) return false;

        var others = await users.Query()
            .Where(u => u.Id != user.Id && u.IsActive)
            .Join(roles.Query(), u => u.Id, r => r.UserId, (u, r) => r.Role)
            .CountAsync(role => role == Roles.SuperAdmin);

        return others == 0;
    }

    private void SyncRoles(User user, IReadOnlySet<Roles> wanted)
    {
        foreach (var existing in user.Roles.Where(r => !r.IsDeleted && !wanted.Contains(r.Role)))
            roles.SoftDelete(existing);

        var held = user.Roles.Where(r => !r.IsDeleted).Select(r => r.Role).ToHashSet();
        foreach (var role in wanted.Where(r => !held.Contains(r)))
            user.Roles.Add(new UserRole { UserId = user.Id, Role = role });
    }

    private async Task RevokeSessions(int userId)
    {
        var live = await logins.Where(l => l.UserId == userId).ToListAsync();
        logins.SoftDeleteRange(live);
    }
}
