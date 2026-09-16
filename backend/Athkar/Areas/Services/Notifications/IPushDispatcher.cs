using Athkar.Shareds.Attributes;

namespace Athkar.Areas.Services.Notifications;

/// <summary>
/// The push pipeline, in three steps that are deliberately separate.
///
/// Materialising, sending and fanning out are distinct passes over the database
/// rather than one loop, because each has a different failure mode and a
/// different safe retry. Materialising twice must produce no duplicates (a
/// unique index sees to that); sending twice must not double-notify (a claimed
/// status sees to that); and a broadcast that dies half way must be resumable
/// without re-sending to everyone it already reached.
/// </summary>
[ScopedInjectable]
public interface IPushDispatcher
{
    /// <summary>
    /// Writes the dispatch rows for every server-pushed reminder falling due in
    /// the next horizon. Idempotent. Returns how many rows were created.
    /// </summary>
    Task<int> MaterialiseReminders(CancellationToken ct = default);

    /// <summary>
    /// Sends every dispatch that is due and still pending, writes the inbox
    /// rows, and records the outcome. Returns how many were attempted.
    /// </summary>
    Task<int> SendDue(CancellationToken ct = default);

    /// <summary>
    /// Turns scheduled broadcasts into dispatch rows and marks them sending.
    /// Returns how many broadcasts were started.
    /// </summary>
    Task<int> StartDueBroadcasts(CancellationToken ct = default);
}
