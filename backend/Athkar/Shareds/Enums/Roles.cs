namespace Athkar.Shareds.Enums;

/// <summary>
/// Staff roles. Only staff authenticate — the app's users are anonymous devices.
///
/// Ordered by reach, and the numbers are on the wire, so add to the end.
/// </summary>
public enum Roles
{
    /// <summary>Writes and publishes content. Cannot touch staff accounts or platform settings.</summary>
    Editor = 1,

    /// <summary>Everything an editor does, plus reminders, broadcasts and settings.</summary>
    Admin = 2,

    /// <summary>Also manages staff accounts. There is always at least one.</summary>
    SuperAdmin = 3,
}
