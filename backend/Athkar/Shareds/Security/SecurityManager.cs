using System.Security.Claims;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Shareds.Security;

[ScopedInjectable]
public class SecurityManager : ISecurityManager
{
    private readonly IHttpContextAccessor http;

    public SecurityManager(IHttpContextAccessor http) => this.http = http;

    private ClaimsPrincipal? Principal => http.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirst(AppClaims.UserId)?.Value
                      ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public int RequireUserId() => UserId ?? throw new AppException(ErrorCode.Unauthorized);

    public IReadOnlyCollection<Roles> Roles =>
        Principal?.FindAll(ClaimTypes.Role)
            .Select(c => Enum.TryParse<Roles>(c.Value, out var r) ? r : (Roles?)null)
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToArray() ?? [];

    public bool IsInRole(Roles role) => Roles.Contains(role);

    /// <summary>
    /// Roles here are a ladder, not a set of unrelated permissions, so "may an
    /// editor do this?" is a comparison and not a membership test. Written once
    /// here rather than as <c>IsInRole(Admin) || IsInRole(SuperAdmin)</c> spread
    /// through the services, which is the form that silently misses a role added
    /// later.
    /// </summary>
    public bool IsAtLeast(Roles role) => Roles.Any(r => r >= role);

    public string? SessionKey => Principal?.FindFirst(AppClaims.SessionKey)?.Value;
}

public static class AppClaims
{
    public const string UserId = "uid";
    public const string SessionKey = "skey";
}
