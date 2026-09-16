using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Notifications.Models;

public class BroadcastInput
{
    [EnumDataType(typeof(Audience))]
    public Audience Audience { get; set; } = Audience.All;

    [StringLength(8)]
    public string? TargetLanguageCode { get; set; }

    public DevicePlatform? TargetPlatform { get; set; }

    [StringLength(64)]
    public string? TargetDeviceKey { get; set; }

    /// <summary>Null sends as soon as the worker next looks.</summary>
    public DateTime? ScheduledAtUtc { get; set; }

    [StringLength(200)]
    public string? Route { get; set; }

    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class BroadcastOutput
{
    public int Id { get; set; }
    public BroadcastStatus Status { get; set; }
    public Audience Audience { get; set; }
    public string? TargetLanguageCode { get; set; }
    public DevicePlatform? TargetPlatform { get; set; }
    public string? TargetDeviceKey { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? Route { get; set; }

    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<TranslationInput> Translations { get; set; } = [];

    public BroadcastOutput() { }

    public BroadcastOutput(Domain.Notifications.Broadcast e)
    {
        Id = e.Id;
        Status = e.Status;
        Audience = e.Audience;
        TargetLanguageCode = e.TargetLanguageCode;
        TargetPlatform = e.TargetPlatform;
        TargetDeviceKey = e.TargetDeviceKey;
        ScheduledAtUtc = e.ScheduledAtUtc;
        SentAtUtc = e.SentAtUtc;
        Route = e.Route;
        RecipientCount = e.RecipientCount;
        SentCount = e.SentCount;
        FailedCount = e.FailedCount;
        SkippedCount = e.SkippedCount;
        CreatedAt = e.CreationDate;
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
