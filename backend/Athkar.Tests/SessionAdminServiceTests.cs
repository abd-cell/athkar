using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Services.Staff;
using Athkar.Areas.Services.Staff.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// Who is signed in to the console, and who may end it.
///
/// The tests that matter here are about the two credentials and the ladder. A
/// session row carries a refresh token and a session key, either of which *is*
/// the account to whoever holds it — so the screen that lists sessions must not
/// be a way to collect them. And revoking has a direction: reaching down or
/// sideways is ordinary administration, reaching up is an editor locking out
/// the person who could undo it.
/// </summary>
public class SessionAdminServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly InMemoryRepository<UserLogin> logins = new();
    private readonly InMemoryRepository<User> users = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();

    private SessionAdminService Service(FakeSecurityManager? caller = null) =>
        new(logins, users, unitOfWork, audit, caller ?? new FakeSecurityManager());

    private User Staff(params Shareds.Enums.Roles[] roles)
    {
        var user = new User { Email = $"{Guid.NewGuid():N}@athkari.app", FullName = "Staff" };
        foreach (var role in roles) user.Roles.Add(new UserRole { Role = role });

        users.Seed(user);
        return user;
    }

    private UserLogin Session(User user, int daysLeft = 30, string key = "session")
    {
        var login = new UserLogin
        {
            UserId = user.Id,
            User = user,
            SessionKey = key,
            RefreshToken = "refresh-token-nobody-should-see",
            RefreshExpiresAt = Now.AddDays(daysLeft),
            LastUsedAt = Now,
        };

        logins.Seed(login);
        return login;
    }

    [Fact]
    public async Task Neither_credential_on_a_session_row_is_returned()
    {
        var user = Staff(Shareds.Enums.Roles.Admin);
        Session(user, key: "the-session-key");

        var row = (await Service().List(new SessionQueryInput())).Data!.Data.Single();

        var json = System.Text.Json.JsonSerializer.Serialize(row);
        Assert.DoesNotContain("refresh-token-nobody-should-see", json);
        Assert.DoesNotContain("the-session-key", json);

        // The id is what revoke takes, and it grants nothing.
        Assert.True(row.Id > 0);
    }

    [Fact]
    public async Task Lapsed_sessions_are_out_of_the_way_but_can_be_asked_for()
    {
        // "Who can act right now" is the question the screen opens on. A sign-in
        // from somewhere unexpected is still worth finding after it has ended.
        var user = Staff(Shareds.Enums.Roles.Admin);
        Session(user);
        Session(user, daysLeft: -1);

        Assert.Equal(1, (await Service().List(new SessionQueryInput())).Data!.TotalRows);

        var all = await Service().List(new SessionQueryInput { IncludeExpired = true });
        Assert.Equal(2, all.Data!.TotalRows);
        Assert.Contains(all.Data.Data, row => row.IsExpired);
    }

    [Fact]
    public async Task The_session_making_the_request_is_marked_as_such()
    {
        // Revoking it is allowed — "sign out everywhere" has to include here —
        // but it must never happen because one row looked like any other.
        var user = Staff(Shareds.Enums.Roles.SuperAdmin);
        Session(user, key: "mine");
        Session(user, key: "theirs");

        var caller = new FakeSecurityManager(user.Id, Shareds.Enums.Roles.SuperAdmin)
        {
            SessionKey = "mine",
        };

        var rows = (await Service().List(new SessionQueryInput())).Data!.Data;
        var current = rows.Where(r => r.IsCurrent).ToList();
        Assert.Empty(current);

        rows = (await Service(caller).List(new SessionQueryInput())).Data!.Data;
        Assert.Single(rows.Where(r => r.IsCurrent));
    }

    [Fact]
    public async Task An_admin_may_not_revoke_a_superadmins_session()
    {
        var superAdmin = Staff(Shareds.Enums.Roles.SuperAdmin);
        var session = Session(superAdmin);

        var caller = new FakeSecurityManager(userId: 99, Shareds.Enums.Roles.Admin);

        var response = await Service(caller).Revoke(session.Id);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.CannotRevokeHigherRole, response.ErrorCode);
        Assert.False(session.IsDeleted);
    }

    [Fact]
    public async Task Revoking_ends_the_session_and_is_recorded()
    {
        var editor = Staff(Shareds.Enums.Roles.Editor);
        var session = Session(editor);

        var caller = new FakeSecurityManager(userId: 99, Shareds.Enums.Roles.Admin);

        var response = await Service(caller).Revoke(session.Id);

        Assert.True(response.Success);
        Assert.True(session.IsDeleted);
        Assert.Contains(AuditActions.SessionRevoke, audit.Actions);
    }

    [Fact]
    public async Task Signing_out_everywhere_ends_every_session_that_account_holds()
    {
        // For a lost laptop or a leaver. The count comes back because "3 ended"
        // and "0 ended" mean very different things to whoever pressed it.
        var editor = Staff(Shareds.Enums.Roles.Editor);
        Session(editor, key: "laptop");
        Session(editor, key: "phone");
        Session(editor, key: "desktop");

        var caller = new FakeSecurityManager(userId: 99, Shareds.Enums.Roles.SuperAdmin);

        var response = await Service(caller).RevokeAllFor(editor.Id);

        Assert.True(response.Success);
        Assert.Equal(3, response.Data);
        Assert.All(logins.All, login => Assert.True(login.IsDeleted));
    }

    [Fact]
    public async Task Anyone_may_end_their_own_session()
    {
        // The ladder check must not stop a superadmin signing themselves out of
        // a machine they no longer trust.
        var superAdmin = Staff(Shareds.Enums.Roles.SuperAdmin);
        var session = Session(superAdmin);

        var caller = new FakeSecurityManager(superAdmin.Id, Shareds.Enums.Roles.SuperAdmin);

        Assert.True((await Service(caller).Revoke(session.Id)).Success);
    }
}
