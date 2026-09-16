using Athkar.Shareds.Enums;

namespace Athkar.Shareds.Security;

/// <summary>Reads the authenticated staff identity from the current request.</summary>
public interface ISecurityManager
{
    /// <summary>Current staff user id, or null when unauthenticated.</summary>
    int? UserId { get; }

    /// <summary>Current staff user id, or throws <see cref="Models.AppException"/> when absent.</summary>
    int RequireUserId();

    IReadOnlyCollection<Roles> Roles { get; }

    bool IsInRole(Roles role);

    /// <summary>True when the caller holds <paramref name="role"/> or anything above it.</summary>
    bool IsAtLeast(Roles role);

    /// <summary>The active login session key (jti), used to revoke tokens on logout.</summary>
    string? SessionKey { get; }
}
