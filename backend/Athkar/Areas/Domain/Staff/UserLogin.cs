using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Staff;

/// <summary>
/// One live session, one row.
///
/// Every request re-checks that the row still exists (see the JWT
/// <c>OnTokenValidated</c> handler in <c>Program.cs</c>), which is what makes
/// logout immediate: a signed token is otherwise valid until it expires, and
/// "sign out everywhere" has to mean something for an account that can publish
/// content to a million phones.
/// </summary>
public class UserLogin : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>The token's jti. Ties an access token to this row.</summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>Opaque randomness; looked up, never parsed. Rotated on every refresh.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshExpiresAt { get; set; }

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
