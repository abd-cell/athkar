using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Widgets;

/// <summary>
/// What one entry in the gallery is called, per language.
///
/// The subtitle is optional and carries the one line under the name — "the one
/// with the countdown", not a paragraph. A gallery of thirty entries is read by
/// scanning, and anything longer stops being read at all.
/// </summary>
public class WidgetCatalogItemTranslation : TranslationEntity
{
    public int WidgetCatalogItemId { get; set; }
    public WidgetCatalogItem? WidgetCatalogItem { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }
}
