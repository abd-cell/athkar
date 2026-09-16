using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Staff;

/// <summary>
/// A member of staff. The only kind of account this system has.
///
/// Readers of the app are anonymous devices and never appear here — there is no
/// sign-up, no password and no email anywhere near a reader. See
/// <c>docs/BUSINESS_LOGIC.md</c> §2 for why that constraint shapes everything
/// else, including why the CMS is the only client that authenticates.
/// </summary>
public class User : AuditableEntity
{
    /// <summary>Lower-cased on save; the login identifier.</summary>
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Argon2id, parameters and salt included. See <c>PasswordHasher</c>.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// A disabled account keeps its rows — the audit trail refers to it — but
    /// cannot sign in, and its live sessions are revoked when the flag is set.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Which language the CMS opens in for this user.</summary>
    public string LanguageCode { get; set; } = "ar";

    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<UserLogin> Logins { get; set; } = [];
}
