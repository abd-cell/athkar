using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Staff.Models;

public class LoginInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class RefreshInput
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>What the CMS holds after a successful sign-in.</summary>
public class AuthOutput
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessExpiresAt { get; set; }

    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshExpiresAt { get; set; }

    public StaffOutput User { get; set; } = new();
}

public class StaffOutput
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "ar";
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<Roles> Roles { get; set; } = [];

    public StaffOutput() { }

    public StaffOutput(Domain.Staff.User user)
    {
        Id = user.Id;
        Email = user.Email;
        FullName = user.FullName;
        LanguageCode = user.LanguageCode;
        IsActive = user.IsActive;
        LastLoginAt = user.LastLoginAt;
        Roles = [.. user.Roles.Where(r => !r.IsDeleted).Select(r => r.Role).Order()];
    }
}

public class StaffInput
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Required when creating; leave null on an edit to keep the current password.</summary>
    [StringLength(128, MinimumLength = 8)]
    public string? Password { get; set; }

    [StringLength(8)]
    public string LanguageCode { get; set; } = "ar";

    public bool IsActive { get; set; } = true;

    [Required, MinLength(1)]
    public List<Roles> Roles { get; set; } = [];
}
