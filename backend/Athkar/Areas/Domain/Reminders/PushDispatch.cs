using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Notifications;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Reminders;

/// <summary>
/// One intended push to one device at one instant.
///
/// Written ahead of time rather than sent from a loop, for three reasons that
/// each cost a production incident to learn: a restart must not replay a send
/// or skip one, the CMS has to be able to show what is queued and what failed,
/// and a device in a timezone the admin never thought about needs its own row
/// with its own instant. The unique index on
/// (campaign, device, scheduled instant) is what makes the dispatcher safe to
/// run twice.
/// </summary>
public class PushDispatch : BaseEntity
{
    /// <summary>The recurring campaign this came from, if any.</summary>
    public int? CampaignId { get; set; }
    public ReminderCampaign? Campaign { get; set; }

    /// <summary>The one-off broadcast this came from, if any. Exactly one of the two is set.</summary>
    public int? BroadcastId { get; set; }
    public Broadcast? Broadcast { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>The instant to send at, in UTC, already resolved from the device's timezone.</summary>
    public DateTime ScheduledAtUtc { get; set; }

    public PushStatus Status { get; set; } = PushStatus.Pending;

    public DateTime? SentAtUtc { get; set; }

    public int Attempts { get; set; }

    /// <summary>FCM's id for a successful send; useful when reconciling against Firebase's own reporting.</summary>
    public string? MessageId { get; set; }

    /// <summary>Why it failed. Truncated on write — some FCM errors are paragraphs.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Whether the inbox row for this dispatch has been written.
    ///
    /// The inbox is the delivery and the push only the courtesy, so the row is
    /// written on the first attempt whatever FCM then says. That makes a retry
    /// dangerous without this flag: re-attempting a failed push would put a
    /// second copy of the same message in the reader's inbox, and the reader —
    /// who never saw the failure — would see only the duplicate.
    /// </summary>
    public bool InboxWritten { get; set; }
}
