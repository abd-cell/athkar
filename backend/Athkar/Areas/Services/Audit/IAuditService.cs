using Athkar.Shareds.Attributes;

namespace Athkar.Areas.Services.Audit;

/// <summary>
/// Records what a member of staff did.
///
/// Called explicitly by each mutating service after its commit, never by a
/// filter: a filter knows the verb and the route, the service knows that this
/// was the unpublishing of a disputed hadith. On a project whose credibility is
/// its content, the second is the one worth keeping.
/// </summary>
[ScopedInjectable]
public interface IAuditService
{
    /// <summary>
    /// Writes one entry and saves it. Snapshots are serialised to JSON; pass
    /// null for either where the service has nothing meaningful to record.
    /// </summary>
    Task LogAsync(string action, string entityName, int? entityId,
        object? oldValue = null, object? newValue = null);
}
