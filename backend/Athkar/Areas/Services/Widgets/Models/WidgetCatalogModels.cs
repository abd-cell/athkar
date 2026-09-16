using System.ComponentModel.DataAnnotations;
using Athkar.Areas.Domain.Widgets;
using Athkar.Areas.Services.Content.Models;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Widgets.Models;

/// <summary>
/// One gallery entry as the app reads it.
///
/// Resolved to a single language before it leaves the server, like every other
/// reader-facing list here — the app holds one language's text at a time and a
/// per-language map would be cached six times over for nothing.
/// </summary>
public class WidgetCatalogOutput
{
    public int Id { get; set; }

    /// <summary>The renderer this entry names. The app skips a key it lacks.</summary>
    public string Key { get; set; } = string.Empty;

    public WidgetSurface Surface { get; set; }
    public WidgetFamily Family { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    /// <summary>A cap on the app's own design count, not a promise of that many.</summary>
    public int DesignCount { get; set; }

    public int DefaultDesign { get; set; }

    public bool IsExclusive { get; set; }
    public bool IsNew { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>The gallery plus the rules it is browsed under, in one call.</summary>
public class WidgetCatalogBundleOutput
{
    public WidgetSettingsOutput Settings { get; set; } = new();

    public List<WidgetCatalogOutput> Items { get; set; } = [];
}

/// <summary>One gallery entry as the console edits it — every language at once.</summary>
public class AdminWidgetCatalogOutput
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public WidgetSurface Surface { get; set; }
    public WidgetFamily Family { get; set; }
    public int DesignCount { get; set; }
    public int DefaultDesign { get; set; }
    public bool IsExclusive { get; set; }
    public bool IsNew { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }

    public List<WidgetCatalogTranslationOutput> Translations { get; set; } = [];

    public AdminWidgetCatalogOutput() { }

    public AdminWidgetCatalogOutput(WidgetCatalogItem e)
    {
        Id = e.Id;
        Key = e.Key;
        Surface = e.Surface;
        Family = e.Family;
        DesignCount = e.DesignCount;
        DefaultDesign = e.DefaultDesign;
        IsExclusive = e.IsExclusive;
        IsNew = e.IsNew;
        IsEnabled = e.IsEnabled;
        SortOrder = e.SortOrder;
        Translations =
        [
            .. e.Translations
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.LanguageCode)
                .Select(t => new WidgetCatalogTranslationOutput
                {
                    LanguageCode = t.LanguageCode,
                    Title = t.Title,
                    Subtitle = t.Subtitle,
                }),
        ];
    }
}

public class WidgetCatalogTranslationOutput
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
}

/// <summary>Admin create/update payload. Translations are replaced wholesale.</summary>
public class WidgetCatalogInput
{
    /// <summary>
    /// Only meaningful on create; an existing entry's key is immutable, because
    /// every phone that has placed the widget is holding it.
    /// </summary>
    [Required, StringLength(WidgetRules.MaxKeyLength)]
    public string Key { get; set; } = string.Empty;

    [EnumDataType(typeof(WidgetSurface))]
    public WidgetSurface Surface { get; set; } = WidgetSurface.Home;

    [EnumDataType(typeof(WidgetFamily))]
    public WidgetFamily Family { get; set; } = WidgetFamily.Prayer;

    [Range(1, WidgetRules.MaxDesignCount)]
    public int DesignCount { get; set; } = 1;

    [Range(0, WidgetRules.MaxDesignCount - 1)]
    public int DefaultDesign { get; set; }

    public bool IsExclusive { get; set; }
    public bool IsNew { get; set; }
    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>Reuses the console's one translations editor, so Body is the subtitle.</summary>
    public List<TranslationInput> Translations { get; set; } = [];
}

/// <summary>The ids of the gallery, in the order the admin dragged them into.</summary>
public class WidgetCatalogReorderInput
{
    [Required, MinLength(1)]
    public List<int> Ids { get; set; } = [];
}
