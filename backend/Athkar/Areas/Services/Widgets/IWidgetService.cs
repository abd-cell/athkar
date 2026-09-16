using Athkar.Areas.Services.Widgets.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Widgets;

[ScopedInjectable]
public interface IWidgetService
{
    /// <summary>The current rules. Never fails: an empty table yields the defaults.</summary>
    Task<BaseResponse<WidgetSettingsOutput>> Get();

    /// <summary>Replaces them. Admin-only at the controller; audited here.</summary>
    Task<BaseResponse<WidgetSettingsOutput>> Update(WidgetSettingsInput input);

    /// <summary>
    /// The gallery and the rules together, in the reader's language.
    ///
    /// One call rather than two because the app fetches this on every sync and
    /// the two halves are useless apart — a gallery the reader may not customise
    /// and a set of permissions with nothing to apply them to.
    /// </summary>
    Task<BaseResponse<WidgetCatalogBundleOutput>> Catalog(string? languageCode);

    /// <summary>Every gallery entry, hidden ones included, for the console.</summary>
    Task<BaseResponse<List<AdminWidgetCatalogOutput>>> ListCatalog();

    Task<BaseResponse<AdminWidgetCatalogOutput>> CreateCatalogItem(WidgetCatalogInput input);

    Task<BaseResponse<AdminWidgetCatalogOutput>> UpdateCatalogItem(int id, WidgetCatalogInput input);

    Task<BaseResponse> DeleteCatalogItem(int id);

    /// <summary>Writes the console's drag order back as <c>SortOrder</c>.</summary>
    Task<BaseResponse> ReorderCatalog(WidgetCatalogReorderInput input);
}
