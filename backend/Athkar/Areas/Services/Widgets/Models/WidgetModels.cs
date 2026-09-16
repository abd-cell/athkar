using System.ComponentModel.DataAnnotations;
using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Widgets.Models;

/// <summary>
/// The widget rules as the app reads them.
///
/// Anonymous, like the rest of the reader-facing surface, and cached on the
/// device — the widget has to keep drawing when the phone is offline.
/// </summary>
public class WidgetSettingsOutput
{
    public bool IsEnabled { get; set; }
    public WidgetKind DefaultKind { get; set; }
    public bool AllowPrayerWidget { get; set; }
    public bool AllowDhikrWidget { get; set; }
    public WidgetTheme Theme { get; set; }
    public int RefreshMinutes { get; set; }
    public bool ShowHijriDate { get; set; }
    public bool ShowCountdown { get; set; }

    /// <summary>Null means the widget follows the time of day.</summary>
    public int? DhikrCategoryId { get; set; }

    /// <summary>
    /// The gallery entry a reader gets before choosing. Null means "whatever
    /// the app can draw first" — see the entity.
    /// </summary>
    public string? DefaultWidgetKey { get; set; }

    /// <summary>What the reader may change for themselves. See the entity.</summary>
    public bool AllowBackgroundColor { get; set; }
    public bool AllowTransparency { get; set; }
    public bool AllowBackgroundImage { get; set; }
    public bool AllowCustomWidget { get; set; }
    public int CustomWidgetMaxLength { get; set; }

    /// <summary>Compare against what was last pushed to the launcher.</summary>
    public int Version { get; set; }

    public WidgetSettingsOutput() { }

    public WidgetSettingsOutput(Domain.Widgets.WidgetSettings e)
    {
        IsEnabled = e.IsEnabled;
        DefaultKind = e.DefaultKind;
        AllowPrayerWidget = e.AllowPrayerWidget;
        AllowDhikrWidget = e.AllowDhikrWidget;
        Theme = e.Theme;
        RefreshMinutes = e.RefreshMinutes;
        ShowHijriDate = e.ShowHijriDate;
        ShowCountdown = e.ShowCountdown;
        DhikrCategoryId = e.DhikrCategoryId;
        AllowBackgroundColor = e.AllowBackgroundColor;
        AllowTransparency = e.AllowTransparency;
        AllowBackgroundImage = e.AllowBackgroundImage;
        AllowCustomWidget = e.AllowCustomWidget;
        CustomWidgetMaxLength = e.CustomWidgetMaxLength;
        DefaultWidgetKey = e.DefaultWidgetKey;
        Version = e.Version;
    }
}

/// <summary>Admin update payload. Every field is replaced — no partial save.</summary>
public class WidgetSettingsInput
{
    public bool IsEnabled { get; set; } = true;

    [EnumDataType(typeof(WidgetKind))]
    public WidgetKind DefaultKind { get; set; } = WidgetKind.NextPrayer;

    public bool AllowPrayerWidget { get; set; } = true;
    public bool AllowDhikrWidget { get; set; } = true;

    [EnumDataType(typeof(WidgetTheme))]
    public WidgetTheme Theme { get; set; } = WidgetTheme.System;

    [Range(WidgetRules.MinRefreshMinutes, WidgetRules.MaxRefreshMinutes)]
    public int RefreshMinutes { get; set; } = 30;

    public bool ShowHijriDate { get; set; } = true;
    public bool ShowCountdown { get; set; } = true;

    public int? DhikrCategoryId { get; set; }

    public bool AllowBackgroundColor { get; set; } = true;
    public bool AllowTransparency { get; set; } = true;
    public bool AllowBackgroundImage { get; set; } = true;
    public bool AllowCustomWidget { get; set; } = true;

    [Range(WidgetRules.MinCustomLength, WidgetRules.MaxCustomLength)]
    public int CustomWidgetMaxLength { get; set; } = 280;

    /// <summary>Blank or null clears it back to "whatever the app can draw first".</summary>
    [StringLength(WidgetRules.MaxKeyLength)]
    public string? DefaultWidgetKey { get; set; }
}

/// <summary>The numbers the widget configuration refuses to be talked out of.</summary>
public static class WidgetRules
{
    /// <summary>
    /// Below this Android coalesces widget updates anyway, so a smaller number
    /// buys nothing but battery drain.
    /// </summary>
    public const int MinRefreshMinutes = 15;

    /// <summary>A widget that refreshes less than daily is showing yesterday.</summary>
    public const int MaxRefreshMinutes = 1440;

    /// <summary>Shorter than this and the reader cannot fit an ayah.</summary>
    public const int MinCustomLength = 40;

    /// <summary>Longer than this and no widget size can render it legibly.</summary>
    public const int MaxCustomLength = 600;

    /// <summary>A catalogue key is a contract with the app's renderer registry.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>
    /// The ceiling on designs per entry. Not a technical limit — a guard against
    /// an admin typing 900 into the cap and the gallery claiming designs the app
    /// cannot draw.
    /// </summary>
    public const int MaxDesignCount = 40;
}
