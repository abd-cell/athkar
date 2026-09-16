using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Localization;

/// <summary>
/// A language the app offers.
///
/// A table and not an enum, because the point of the whole localisation slice is
/// that adding Turkish is an afternoon of translation in the CMS rather than a
/// release of three clients. The app fetches this list on launch and renders
/// whatever is in it.
/// </summary>
public class AppLanguage : AuditableEntity
{
    /// <summary>BCP-47 primary subtag, lower-case and unique: "ar", "en", "tr".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What speakers of it call it — "العربية", "Türkçe". Shown in the picker.</summary>
    public string NativeName { get; set; } = string.Empty;

    /// <summary>What the CMS calls it in its own lists.</summary>
    public string EnglishName { get; set; } = string.Empty;

    public bool IsRtl { get; set; }

    /// <summary>
    /// A disabled language stops being offered but keeps its translations, so
    /// turning it off while a translator finishes is not destructive.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The end of every fallback chain. Exactly one row has it, it cannot be
    /// disabled, and a missing translation in any other language resolves here.
    /// </summary>
    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Bumped whenever any string or content translation in this language
    /// changes, so a client can sync one language rather than everything.
    /// </summary>
    public int Version { get; set; } = 1;
}
