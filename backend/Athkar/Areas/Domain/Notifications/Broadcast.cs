using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Notifications;

/// <summary>
/// A one-off message an admin writes and sends to a slice of the installed base.
///
/// Separate from <see cref="Reminders.ReminderCampaign"/> because the two are
/// different kinds of object despite sharing a delivery pipe: a campaign is a
/// rule that keeps producing sends, a broadcast is a single event with a status
/// and a result. Merging them would put a "sent at" on something that is never
/// finished and a recurrence on something that happens once.
/// </summary>
public class Broadcast : AuditableEntity
{
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Draft;

    public Audience Audience { get; set; } = Audience.All;
    public string? TargetLanguageCode { get; set; }
    public DevicePlatform? TargetPlatform { get; set; }

    /// <summary>The single device, when <see cref="Audience"/> is <see cref="Audience.Device"/>.</summary>
    public string? TargetDeviceKey { get; set; }

    /// <summary>
    /// When to send. Null means "as soon as the worker next looks", which is how
    /// the CMS's Send button works — it schedules for now rather than sending
    /// inline, so one code path carries every message.
    /// </summary>
    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    /// <summary>Where tapping it goes ("category/12"). Null for text-only.</summary>
    public string? Route { get; set; }

    public string AndroidChannelId { get; set; } = Shareds.Constants.PushRules.Channels.Announcements;

    // ── Result, filled in by the dispatcher ──

    /// <summary>How many devices the audience resolved to when the fan-out ran.</summary>
    public int RecipientCount { get; set; }

    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>
    /// Devices deliberately not tried — notifications off, or no push token.
    /// Counted separately so a low <see cref="SentCount"/> can be read as
    /// "most readers have push off" rather than as a broken integration.
    /// </summary>
    public int SkippedCount { get; set; }

    public ICollection<BroadcastTranslation> Translations { get; set; } = [];
}
