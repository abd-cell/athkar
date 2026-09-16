using Athkar.Areas.Services.Notifications.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Notifications;

/// <summary>
/// The push pipeline as the CMS sees it: what it can reach, what is queued,
/// what became of what was sent — and one way to send something now.
///
/// The reading half exists because nothing else could answer the question an
/// admin actually asks after sending: *did it arrive, and if not, which of the
/// four possible reasons was it.*
///
/// The sending half is deliberately thin. It does not implement a second way to
/// push; it composes a broadcast and hands it to
/// <see cref="IBroadcastService"/>, so one code path still carries every
/// message and a quick send appears in the same history, with the same
/// counters and the same audit trail, as one written on the broadcasts screen.
/// </summary>
[ScopedInjectable]
public interface IPushManagerService
{
    /// <summary>Reach, queue and outcomes in one call — the manager screen loads once.</summary>
    Task<BaseResponse<PushOverviewOutput>> Overview(int windowDays = 7);

    /// <summary>
    /// Composes a message and queues it immediately.
    ///
    /// Any schedule on the input is ignored: a message with a time on it is a
    /// campaign, and campaigns belong on the broadcasts screen where they can
    /// be edited and withdrawn before they go. This is for the one an admin
    /// wants out now — a test to a single device most of all.
    /// </summary>
    Task<BaseResponse<BroadcastOutput>> Send(BroadcastInput input);

    /// <summary>
    /// A filtered page of the dispatch table — the log behind the summary.
    ///
    /// The overview carries twenty-five recent failures because that is what
    /// fits on a screen somebody is scanning. This is for the question that
    /// comes next: everything that went to one install, or everything one
    /// broadcast produced, or every token Firebase retired last night.
    /// </summary>
    Task<BaseResponse<PageOutput<PushDispatchOutput>>> Dispatches(PushDispatchQueryInput input);

    /// <summary>
    /// Queues a settled dispatch to be tried again, now.
    ///
    /// Refused for a dispatch that succeeded — the reader has already been
    /// notified, and a second copy is a worse outcome than the first failure —
    /// and for one still pending, which has not failed yet. The retry resets
    /// the attempt count and moves the instant to now, because the grace window
    /// would otherwise judge an old row late and skip it again immediately.
    /// </summary>
    Task<BaseResponse<PushDispatchOutput>> Retry(int id);

    /// <summary>
    /// Withdraws a dispatch that has not gone out. Recorded as skipped rather
    /// than deleted: the row is the evidence that it was stopped on purpose.
    /// </summary>
    Task<BaseResponse<PushDispatchOutput>> CancelDispatch(int id);

    /// <summary>
    /// Runs the three pipeline passes once, in order, instead of waiting for
    /// the workers.
    ///
    /// The manager screen can already tell an admin that the sender is stopped;
    /// this is what makes that diagnosis actionable. It is the same dispatcher
    /// the workers call, so running it by hand while they are also running is
    /// safe — every pass is idempotent by design.
    /// </summary>
    Task<BaseResponse<PushRunOutput>> RunNow();
}
