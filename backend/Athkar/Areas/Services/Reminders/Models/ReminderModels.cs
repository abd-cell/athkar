using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Reminders.Models;

public class ReminderInput
{
    [Required, StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9][a-z0-9-]*$",
        ErrorMessage = "A key is lower-case letters, digits and hyphens.")]
    public string Key { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    [EnumDataType(typeof(ReminderKind))]
    public ReminderKind Kind { get; set; } = ReminderKind.FixedTime;

    [EnumDataType(typeof(ReminderDelivery))]
    public ReminderDelivery Delivery { get; set; } = ReminderDelivery.DeviceLocal;

    /// <summary>"HH:mm" in the device's own timezone. Required when <see cref="Kind"/> is fixed-time.</summary>
    public TimeOnly? LocalTime { get; set; }

    [EnumDataType(typeof(PrayerAnchor))]
    public PrayerAnchor Anchor { get; set; } = PrayerAnchor.None;

    [Range(-720, 720)]
    public int OffsetMinutes { get; set; }

    public WeekDays Days { get; set; } = WeekDays.All;

    [EnumDataType(typeof(Audience))]
    public Audience Audience { get; set; } = Audience.All;

    [StringLength(8)]
    public string? TargetLanguageCode { get; set; }

    public DevicePlatform? TargetPlatform { get; set; }

    public bool IsEnabled { get; set; } = true;
    public bool IsUserAdjustable { get; set; } = true;

    /// <summary>
    /// Which Android channel this rings on. One of
    /// <see cref="Shareds.Constants.PushRules.Channels"/>; blank keeps the
    /// reminders channel.
    ///
    /// It is a real choice rather than a detail: a reader who wants the call to
    /// prayer audible but the adhkar reminders silent can only have that if the
    /// two land on different channels, and a channel's settings cannot be
    /// changed after Android creates it.
    /// </summary>
    [StringLength(64)]
    public string? AndroidChannelId { get; set; }

    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class ReminderOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryKey { get; set; }
    public ReminderKind Kind { get; set; }
    public ReminderDelivery Delivery { get; set; }
    public TimeOnly? LocalTime { get; set; }
    public PrayerAnchor Anchor { get; set; }
    public int OffsetMinutes { get; set; }
    public WeekDays Days { get; set; }
    public Audience Audience { get; set; }
    public string? TargetLanguageCode { get; set; }
    public DevicePlatform? TargetPlatform { get; set; }
    public string AndroidChannelId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsUserAdjustable { get; set; }
    public int Version { get; set; }

    public List<TranslationInput> Translations { get; set; } = [];

    public ReminderOutput() { }

    public ReminderOutput(Domain.Reminders.ReminderCampaign e)
    {
        Id = e.Id;
        Key = e.Key;
        CategoryId = e.CategoryId;
        CategoryKey = e.Category?.Key;
        Kind = e.Kind;
        Delivery = e.Delivery;
        LocalTime = e.LocalTime;
        Anchor = e.Anchor;
        OffsetMinutes = e.OffsetMinutes;
        Days = e.Days;
        Audience = e.Audience;
        TargetLanguageCode = e.TargetLanguageCode;
        TargetPlatform = e.TargetPlatform;
        AndroidChannelId = e.AndroidChannelId;
        IsEnabled = e.IsEnabled;
        IsUserAdjustable = e.IsUserAdjustable;
        Version = e.Version;
        Translations =
        [
            .. e.Translations.Where(t => !t.IsDeleted).Select(t => new TranslationInput
            {
                LanguageCode = t.LanguageCode,
                Title = t.Title,
                Body = t.Body,
            }),
        ];
    }
}

/// <summary>
/// One reminder as the <b>app</b> needs it in order to schedule it locally:
/// already resolved into the reader's language, with no audience or delivery
/// fields, because a campaign that reached this list is by definition one this
/// device is meant to raise itself.
/// </summary>
public class DeviceReminderOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public ReminderKind Kind { get; set; }
    public TimeOnly? LocalTime { get; set; }
    public PrayerAnchor Anchor { get; set; }
    public int OffsetMinutes { get; set; }
    public WeekDays Days { get; set; }
    public bool IsUserAdjustable { get; set; }
    public string AndroidChannelId { get; set; } = string.Empty;

    /// <summary>Compare against what was last scheduled; re-schedule only when it moves.</summary>
    public int Version { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
