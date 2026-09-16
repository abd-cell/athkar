using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Constants;

namespace Athkar.Areas.Services.Radio.Models;

/// <summary>
/// A station as the reader's app receives it, inside the catalogue payload.
/// </summary>
public class RadioStationOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>Already resolved into the requested language, falling back to the key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The broadcaster, when an editor has named one.</summary>
    public string? Provider { get; set; }

    public string StreamUrl { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A station as the console sends it. Translations replace the set, as everywhere else.</summary>
public class RadioStationInput
{
    [Required, StringLength(ContentRules.MaxCategoryKeyLength, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9][a-z0-9-]*$",
        ErrorMessage = "A key is lower-case letters, digits and hyphens.")]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(ContentRules.MaxUrlLength)]
    [Url]
    public string StreamUrl { get; set; } = string.Empty;

    [StringLength(ContentRules.MaxUrlLength)]
    [Url]
    public string? LogoUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; }

    /// <summary>At least the source language, or the station has no name to show.</summary>
    [Required, MinLength(1)]
    public List<TranslationInput> Translations { get; set; } = [];
}

public class AdminRadioStationOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }

    public List<string> TranslatedLanguages { get; set; } = [];
    public List<TranslationInput> Translations { get; set; } = [];
}
