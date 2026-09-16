using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Staff;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Support;

/// <summary>
/// Something a reader wrote to the project: a suggestion, a complaint, or — the
/// kind that matters most here — a correction to a text or a grading.
///
/// There is no account to reply to, so a reply is delivered as a notification to
/// the device that wrote in, and only while that install still exists. That is a
/// real limitation of anonymity and is stated plainly in the app rather than
/// papered over by asking for an email.
/// </summary>
public class Feedback : BaseEntity
{
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public FeedbackKind Kind { get; set; }

    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The dhikr this is about, when the reader started from one. A correction
    /// that arrives attached to its row is worth several that arrive as prose.
    /// </summary>
    public int? DhikrId { get; set; }

    /// <summary>
    /// An optional way to reach them. The one place in the system a reader may
    /// volunteer contact details, always empty unless they typed it themselves.
    /// </summary>
    public string? ContactEmail { get; set; }

    public string? AppVersion { get; set; }
    public string LanguageCode { get; set; } = "ar";

    // ── Desk side ──

    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }

    public int? RepliedBy { get; set; }
    public User? RepliedByUser { get; set; }
}
