using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Devices;

/// <summary>
/// An inbox row for one device.
///
/// Stored with the text already resolved into the device's language rather than
/// as a reference to the campaign: a reader who switches language should not
/// find their history rewritten, and a broadcast that is later edited or deleted
/// should not blank out what somebody already received.
/// </summary>
public class DeviceNotification : BaseEntity
{
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public NotificationKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The language the two fields above were resolved in, for the record.</summary>
    public string LanguageCode { get; set; } = "ar";

    /// <summary>
    /// Where tapping it goes — the same route string the push payload carries
    /// ("category/42", "dhikr/108"). Null for a message that is only text.
    /// </summary>
    public string? Route { get; set; }

    public DateTime? ReadAt { get; set; }
}
