using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Staff;

/// <summary>
/// One role held by one user. A table rather than a column on
/// <see cref="User"/> because the audit trail wants to record a role being
/// granted and withdrawn as events, and a flags column erases that history.
/// </summary>
public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public Roles Role { get; set; }
}
