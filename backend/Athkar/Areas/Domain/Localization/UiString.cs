using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Localization;

/// <summary>
/// One piece of interface copy, in one language.
///
/// The app ships with a full set of strings compiled in and treats these as an
/// *overlay*: anything present here wins, anything absent falls back to the
/// build. That ordering is what lets a typo in a button be fixed the same day
/// without a store review, while an install that has never reached the network
/// still renders complete sentences.
/// </summary>
public class UiString : BaseEntity
{
    public int LanguageId { get; set; }
    public AppLanguage? Language { get; set; }

    /// <summary>Dotted key matching the app's own maps: "home.greeting.morning".</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
