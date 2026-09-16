using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Services.Staff.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;
using Athkar.Shareds.Security.Token;

namespace Athkar.Areas.Services.Staff;

/// <summary>
/// Sign-in for the control panel. The app never calls any of this.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IRepository<User> users;
    private readonly IRepository<UserLogin> logins;
    private readonly IUnitOfWork unitOfWork;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenGenerator tokenGenerator;
    private readonly ISecurityManager securityManager;
    private readonly IHttpContextAccessor http;

    public AuthService(
        IRepository<User> users,
        IRepository<UserLogin> logins,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ISecurityManager securityManager,
        IHttpContextAccessor http)
    {
        this.users = users;
        this.logins = logins;
        this.unitOfWork = unitOfWork;
        this.passwordHasher = passwordHasher;
        this.tokenGenerator = tokenGenerator;
        this.securityManager = securityManager;
        this.http = http;
    }

    public async Task<BaseResponse<AuthOutput>> Login(LoginInput input)
    {
        var email = input.Email.Trim().ToLowerInvariant();

        var user = await users.Query()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email);

        // One answer for "no such account" and "wrong password". Telling them
        // apart turns the login form into a directory of who works here.
        if (user is null || !passwordHasher.Verify(input.Password, user.PasswordHash))
            return BaseResponse<AuthOutput>.Fail(ErrorCode.InvalidCredentials);

        if (!user.IsActive)
            return BaseResponse<AuthOutput>.Fail(ErrorCode.AccountDisabled);

        user.LastLoginAt = DateTime.UtcNow;
        users.Update(user);

        var session = await IssueSession(user);
        await unitOfWork.SaveAsync();

        return new BaseResponse<AuthOutput>(Describe(user, session));
    }

    public async Task<BaseResponse<AuthOutput>> Refresh(RefreshInput input)
    {
        var token = input.RefreshToken?.Trim();
        if (string.IsNullOrEmpty(token))
            return BaseResponse<AuthOutput>.Fail(ErrorCode.SessionExpired);

        var session = await logins.Query()
            .Include(l => l.User!).ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(l => l.RefreshToken == token);

        if (session?.User is null || session.RefreshExpiresAt <= DateTime.UtcNow)
            return BaseResponse<AuthOutput>.Fail(ErrorCode.SessionExpired);

        if (!session.User.IsActive)
            return BaseResponse<AuthOutput>.Fail(ErrorCode.AccountDisabled);

        // Rotate both halves. The session row is reused so anything holding its
        // id keeps working, but neither token that arrived here is valid again.
        session.SessionKey = Guid.NewGuid().ToString("N");
        session.RefreshToken = tokenGenerator.CreateRefreshToken();
        session.RefreshExpiresAt = tokenGenerator.RefreshExpiry;
        session.LastUsedAt = DateTime.UtcNow;
        logins.Update(session);

        await unitOfWork.SaveAsync();

        return new BaseResponse<AuthOutput>(Describe(session.User, session));
    }

    public async Task<BaseResponse> Logout()
    {
        var sessionKey = securityManager.SessionKey;
        if (string.IsNullOrEmpty(sessionKey)) return new BaseResponse();

        var session = await logins.FirstOrDefaultAsync(l => l.SessionKey == sessionKey);
        if (session is not null)
        {
            logins.SoftDelete(session);
            await unitOfWork.SaveAsync();
        }

        return new BaseResponse();
    }

    public async Task<BaseResponse<StaffOutput>> Me()
    {
        var userId = securityManager.RequireUserId();

        var user = await users.Query()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user is null
            ? BaseResponse<StaffOutput>.Fail(ErrorCode.NotFound)
            : new BaseResponse<StaffOutput>(new StaffOutput(user));
    }

    private async Task<UserLogin> IssueSession(User user)
    {
        var request = http.HttpContext?.Request;

        var session = new UserLogin
        {
            UserId = user.Id,
            SessionKey = Guid.NewGuid().ToString("N"),
            RefreshToken = tokenGenerator.CreateRefreshToken(),
            RefreshExpiresAt = tokenGenerator.RefreshExpiry,
            UserAgent = request?.Headers.UserAgent.FirstOrDefault(),
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        };

        await logins.AddAsync(session);
        return session;
    }

    private AuthOutput Describe(User user, UserLogin session) => new()
    {
        AccessToken = tokenGenerator.CreateAccessToken(
            user.Id, session.SessionKey, user.Roles.Where(r => !r.IsDeleted).Select(r => r.Role)),
        AccessExpiresAt = tokenGenerator.AccessExpiry,
        RefreshToken = session.RefreshToken,
        RefreshExpiresAt = session.RefreshExpiresAt,
        User = new StaffOutput(user),
    };
}
