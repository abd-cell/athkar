using System.ComponentModel.DataAnnotations;

namespace Athkar.Areas.Services.Localization.Models;

public class LanguageOutput
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public bool IsRtl { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public int Version { get; set; }

    /// <summary>How many interface strings have been overridden in this language.</summary>
    public int StringCount { get; set; }

    public LanguageOutput() { }

    public LanguageOutput(Domain.Localization.AppLanguage e)
    {
        Id = e.Id;
        Code = e.Code;
        NativeName = e.NativeName;
        EnglishName = e.EnglishName;
        IsRtl = e.IsRtl;
        IsEnabled = e.IsEnabled;
        IsDefault = e.IsDefault;
        SortOrder = e.SortOrder;
        Version = e.Version;
    }
}

public class LanguageInput
{
    [Required, StringLength(8, MinimumLength = 2)]
    [RegularExpression("^[a-z]{2,3}$", ErrorMessage = "A language code is two or three lower-case letters.")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string NativeName { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string EnglishName { get; set; } = string.Empty;

    public bool IsRtl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// The interface-copy overlay for one language, as a flat map.
///
/// A map and not a list of rows because that is how both clients consume it —
/// the app merges it over its compiled-in strings, the CMS renders it as a
/// table — and because a key is unique per language by definition.
/// </summary>
public class UiStringsOutput
{
    public string LanguageCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public Dictionary<string, string> Strings { get; set; } = [];
}

public class UiStringsInput
{
    /// <summary>
    /// Replaces the whole overlay for the language. A key that is absent here is
    /// removed, which is how a client is told to fall back to its built-in copy
    /// rather than keeping an override nobody wanted.
    /// </summary>
    [Required]
    public Dictionary<string, string> Strings { get; set; } = [];
}
