using Athkar.Areas.Domain.Content;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Reminders;

/// <summary>
/// A recurring nudge towards a chapter of adhkar, owned entirely by the admin.
///
/// The one field that decides how this behaves is <see cref="Delivery"/>: a
/// <see cref="ReminderDelivery.ServerPush"/> campaign is fanned out from here
/// over FCM at a computed instant, while a
/// <see cref="ReminderDelivery.DeviceLocal"/> campaign is handed to the app as
/// data and scheduled on the phone. Prayer-anchored campaigns are always the
/// latter, because anchoring requires prayer times and prayer times require
/// coordinates this server deliberately never receives. See
/// <c>docs/BUSINESS_LOGIC.md</c> §5.
/// </summary>
public class ReminderCampaign : AuditableEntity
{
    /// <summary>Stable slug for the record and for deep links: "morning-adhkar".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Where tapping the notification lands. Optional: an announcement-style
    /// reminder may point nowhere in particular.
    /// </summary>
    public int? CategoryId { get; set; }
    public AthkarCategory? Category { get; set; }

    public ReminderKind Kind { get; set; } = ReminderKind.FixedTime;

    public ReminderDelivery Delivery { get; set; } = ReminderDelivery.DeviceLocal;

    /// <summary>
    /// The time of day, in the <b>device's own</b> timezone, for a
    /// <see cref="ReminderKind.FixedTime"/> campaign. Null for an anchored one.
    /// </summary>
    public TimeOnly? LocalTime { get; set; }

    /// <summary>The moment an anchored campaign hangs off. <see cref="PrayerAnchor.None"/> when fixed-time.</summary>
    public PrayerAnchor Anchor { get; set; } = PrayerAnchor.None;

    /// <summary>
    /// Minutes from the anchor, signed: -15 is a quarter of an hour before
    /// Maghrib, +30 is half an hour after sunrise. Ignored when fixed-time.
    /// </summary>
    public int OffsetMinutes { get; set; }

    public WeekDays Days { get; set; } = WeekDays.All;

    public Audience Audience { get; set; } = Audience.All;

    /// <summary>Which language, when <see cref="Audience"/> is <see cref="Audience.Language"/>.</summary>
    public string? TargetLanguageCode { get; set; }

    /// <summary>Which platform, when <see cref="Audience"/> is <see cref="Audience.Platform"/>.</summary>
    public DevicePlatform? TargetPlatform { get; set; }

    /// <summary>
    /// Which Android channel to raise this on. Fixed per campaign because a
    /// channel's sound cannot be changed after the app creates it — see
    /// <c>PushRules.Channels</c>.
    /// </summary>
    public string AndroidChannelId { get; set; } = Shareds.Constants.PushRules.Channels.Reminders;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether a reader may switch this one off, or change its time, from the
    /// app's own reminder screen. Most campaigns are suggestions the reader
    /// owns; a small number (a Ramadan schedule the mosque agreed) are not.
    /// </summary>
    public bool IsUserAdjustable { get; set; } = true;

    /// <summary>
    /// Bumped on every edit. The app compares it against what it last scheduled
    /// locally, so a wording change reaches a device-local reminder without the
    /// app having to re-schedule everything on every launch.
    /// </summary>
    public int Version { get; set; } = 1;

    public ICollection<ReminderCampaignTranslation> Translations { get; set; } = [];
}
