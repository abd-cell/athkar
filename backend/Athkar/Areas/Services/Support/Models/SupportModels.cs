using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Support.Models;

public class FaqOutput
{
    public int Id { get; set; }
    public FaqCategory Category { get; set; }
    public int SortOrder { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class AdminFaqOutput
{
    public int Id { get; set; }
    public FaqCategory Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public List<string> TranslatedLanguages { get; set; } = [];
    public List<TranslationInput> Translations { get; set; } = [];
}

public class FaqInput
{
    [EnumDataType(typeof(FaqCategory))]
    public FaqCategory Category { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;

    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class FeedbackInput
{
    [EnumDataType(typeof(FeedbackKind))]
    public FeedbackKind Kind { get; set; } = FeedbackKind.Suggestion;

    [Required, StringLength(ContentRules.MaxTextLength, MinimumLength = 5)]
    public string Message { get; set; } = string.Empty;

    /// <summary>The dhikr this is about, when the reader started from one.</summary>
    public int? DhikrId { get; set; }

    /// <summary>Optional, and the only place a reader may volunteer contact details.</summary>
    [EmailAddress, StringLength(256)]
    public string? ContactEmail { get; set; }

    [StringLength(32)]
    public string? AppVersion { get; set; }
}

public class FeedbackOutput
{
    public int Id { get; set; }
    public FeedbackKind Kind { get; set; }
    public FeedbackStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? DhikrId { get; set; }

    /// <summary>The Arabic of the dhikr complained about, so a reviewer sees it without navigating.</summary>
    public string? DhikrText { get; set; }

    public string? ContactEmail { get; set; }
    public string? AppVersion { get; set; }
    public string LanguageCode { get; set; } = "ar";
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string? RepliedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FeedbackReplyInput
{
    [Required, StringLength(ContentRules.MaxTextLength, MinimumLength = 1)]
    public string Reply { get; set; } = string.Empty;

    [EnumDataType(typeof(FeedbackStatus))]
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Answered;
}
