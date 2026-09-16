using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Notifications.Models;

/// <summary>
/// What the push manager screen shows.
///
/// The screen exists because "the broadcast said 4 targeted, 0 delivered" is
/// not a diagnosis. Reach, queue and outcome are three different questions with
/// three different fixes — nobody has a token, nothing is due yet, or Firebase
/// is rejecting what we send — and a manager who cannot tell them apart will
/// chase the wrong one.
/// </summary>
public class PushOverviewOutput
{
    public PushReachOutput Reach { get; set; } = new();

    public PushQueueOutput Queue { get; set; } = new();

    /// <summary>Outcomes over the trailing window, for the delivery rate.</summary>
    public PushOutcomesOutput Outcomes { get; set; } = new();

    /// <summary>
    /// Whether Firebase credentials are configured at all.
    ///
    /// First thing the screen reads, because when this is false every other
    /// figure on it has one explanation and no amount of staring at the queue
    /// will reveal it.
    /// </summary>
    public bool IsFcmConfigured { get; set; }

    public List<PushFailureOutput> RecentFailures { get; set; } = [];
}

/// <summary>
/// How many installs a push could actually land on, and where the rest go.
///
/// The three shortfalls are kept apart rather than summed because they are not
/// the same problem: a muted reader made a choice, a tokenless install may
/// never have been asked, and a silent one is probably uninstalled.
/// </summary>
public class PushReachOutput
{
    public int TotalDevices { get; set; }

    /// <summary>Holding a token, not muted, and seen inside the active window.</summary>
    public int Reachable { get; set; }

    /// <summary>Has a token but the reader turned notifications off.</summary>
    public int Muted { get; set; }

    /// <summary>No token: permission declined, or never granted.</summary>
    public int Tokenless { get; set; }

    /// <summary>Has a token but has not called in inside the active window.</summary>
    public int Stale { get; set; }

    public Dictionary<string, int> ReachableByPlatform { get; set; } = [];
    public Dictionary<string, int> ReachableByLanguage { get; set; } = [];
}

/// <summary>What is waiting to go out.</summary>
public class PushQueueOutput
{
    /// <summary>Dispatch rows written and not yet sent.</summary>
    public int PendingDispatches { get; set; }

    /// <summary>
    /// Pending rows whose instant has already passed by more than the grace
    /// window — the shape a stopped worker makes, and the one number on this
    /// screen that means something is wrong rather than merely quiet.
    /// </summary>
    public int OverdueDispatches { get; set; }

    public DateTime? NextDispatchAtUtc { get; set; }

    public int ScheduledBroadcasts { get; set; }
    public DateTime? NextBroadcastAtUtc { get; set; }

    /// <summary>Campaigns that will materialise more rows — server-push only.</summary>
    public int ActivePushCampaigns { get; set; }
}

/// <summary>Outcomes over the trailing window.</summary>
public class PushOutcomesOutput
{
    public int WindowDays { get; set; }

    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int TokenExpired { get; set; }

    /// <summary>
    /// Sent as a share of what was actually attempted — skips excluded.
    ///
    /// Counting a reader who has notifications off as a delivery failure would
    /// make the rate a measure of how many people want push, which is a real
    /// question but not this one.
    /// </summary>
    public double DeliveryRate { get; set; }
}

/// <summary>One recent failure, with enough to tell which kind it is.</summary>
public class PushFailureOutput
{
    public int Id { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string? Error { get; set; }

    /// <summary>The source, named — a campaign key or a broadcast headline.</summary>
    public string Source { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
}

// ─────────────────────────── the delivery log ────────────────────────────

/// <summary>
/// A filtered page of the dispatch table.
///
/// The overview's twenty-five recent failures answer "is something wrong".
/// This answers the question that follows it — *which sends, to what kind of
/// device, and what did Firebase actually say* — and it has to be filterable,
/// because the useful view is almost never "everything".
/// </summary>
public class PushDispatchQueryInput : PageInput
{
    /// <summary>Restrict to one outcome. Null means every outcome, pending included.</summary>
    public PushStatus? Status { get; set; }

    /// <summary>One row by id, so an action can read its own result back through this projection.</summary>
    public int? DispatchId { get; set; }

    public int? BroadcastId { get; set; }
    public int? CampaignId { get; set; }

    public DevicePlatform? Platform { get; set; }
    public string? LanguageCode { get; set; }

    /// <summary>One install's whole history — what the devices screen links to.</summary>
    public string? DeviceKey { get; set; }

    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

/// <summary>One row of the delivery log.</summary>
public class PushDispatchOutput
{
    public int Id { get; set; }

    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public PushStatus Status { get; set; }
    public int Attempts { get; set; }

    /// <summary>FCM's own id for a send that succeeded, for reconciling against Firebase.</summary>
    public string? MessageId { get; set; }

    public string? Error { get; set; }

    /// <summary>Named, not numbered: a campaign key or "broadcast #12".</summary>
    public string Source { get; set; } = string.Empty;

    public int? CampaignId { get; set; }
    public int? BroadcastId { get; set; }

    /// <summary>The wording as it would have gone out, in the device's language.</summary>
    public string? Title { get; set; }

    public int DeviceId { get; set; }
    public string DeviceKey { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the two actions apply, decided here rather than in the CMS.
    /// A screen that works the rule out for itself is a second copy of it, and
    /// the copy is what drifts.
    /// </summary>
    public bool CanRetry { get; set; }
    public bool CanCancel { get; set; }
}

/// <summary>What one manual run of the pipeline did.</summary>
public class PushRunOutput
{
    /// <summary>Reminder dispatch rows written.</summary>
    public int Materialised { get; set; }

    /// <summary>Scheduled broadcasts fanned out to their audience.</summary>
    public int BroadcastsStarted { get; set; }

    /// <summary>Dispatches attempted — sent, skipped or failed.</summary>
    public int Attempted { get; set; }
}
