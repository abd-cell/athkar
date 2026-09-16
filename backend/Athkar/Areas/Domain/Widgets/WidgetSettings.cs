using Athkar.Areas.Domain.Content;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Widgets;

/// <summary>
/// What the admin decides about home-screen widgets.
///
/// Exactly one live row, like <see cref="Configuration.AppConfiguration"/> —
/// the seeder creates it and the service only updates it.
///
/// The reason this is admin-owned at all: a widget is the one surface of this
/// app that a reader sees **without opening it**, so it is also the one whose
/// content nobody can correct in the moment. An admin who has withdrawn a
/// disputed dhikr needs it gone from the home screen too, and the only way to
/// reach that is a setting the app syncs.
///
/// What it deliberately does *not* carry: the text itself. The widget draws
/// from the catalogue already on the device, so it keeps working with the radio
/// off — these are the rules for choosing, not the content.
/// </summary>
public class WidgetSettings : AuditableEntity
{
    /// <summary>
    /// The master switch. Off hides the widget from the app's editor and makes
    /// the native widget render a quiet placeholder rather than stale content.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>What a reader gets before they choose anything.</summary>
    public WidgetKind DefaultKind { get; set; } = WidgetKind.NextPrayer;

    /// <summary>
    /// Which kinds the reader may pick. Both false is a valid, if severe,
    /// configuration — it is the same as switching widgets off, and the service
    /// says so rather than silently correcting it.
    /// </summary>
    public bool AllowPrayerWidget { get; set; } = true;

    public bool AllowDhikrWidget { get; set; } = true;

    public WidgetTheme Theme { get; set; } = WidgetTheme.System;

    /// <summary>
    /// How often the app refreshes the widget's values, in minutes.
    ///
    /// Clamped rather than free: under fifteen minutes Android coalesces the
    /// updates anyway and the only measurable effect is battery, which is the
    /// complaint a widget earns fastest.
    /// </summary>
    public int RefreshMinutes { get; set; } = 30;

    public bool ShowHijriDate { get; set; } = true;

    /// <summary>Whether the prayer widget counts down or just names the time.</summary>
    public bool ShowCountdown { get; set; } = true;

    /// <summary>
    /// Which chapter the dhikr widget draws from.
    ///
    /// Null means "follow the time of day", which is the good default and what
    /// the home screen already does. Setting it pins the widget to one chapter
    /// — how a Ramadan plan or a themed month is run without an app update.
    /// </summary>
    public int? DhikrCategoryId { get; set; }
    public AthkarCategory? DhikrCategory { get; set; }

    /// <summary>
    /// The gallery entry a reader gets before they have chosen one — matched
    /// against <see cref="WidgetCatalogItem.Key"/>.
    ///
    /// A key rather than a foreign key, deliberately. The app resolves it
    /// against its own renderer registry, and a build that has never heard of
    /// this key must fall back rather than fail; a relational reference would
    /// imply a guarantee the app cannot honour. Null means "the first entry the
    /// reader's app can actually draw", which is the honest default for an
    /// install that may be a release behind this server.
    /// </summary>
    public string? DefaultWidgetKey { get; set; }

    // ── What the reader is allowed to change for themselves ──
    //
    // These are permissions, not preferences: the reader's own choice lives on
    // their phone and never reaches this server. They are here because a
    // customisation can break the one promise the widget makes — a transparent
    // background over a photographic wallpaper can render a dhikr unreadable,
    // and an unreadable dhikr on a home screen is worse than no widget — so the
    // project keeps a way to withdraw one without shipping an app update.

    /// <summary>Whether the reader may tint the widget's background.</summary>
    public bool AllowBackgroundColor { get; set; } = true;

    /// <summary>Whether the reader may drop the panel and let the wallpaper through.</summary>
    public bool AllowTransparency { get; set; } = true;

    /// <summary>Whether the reader may put one of their own photographs behind it.</summary>
    public bool AllowBackgroundImage { get; set; } = true;

    /// <summary>
    /// Whether the reader may pin their own text — an ayah, a du'a, a line they
    /// want in front of them all day.
    ///
    /// The one place in this app where a widget shows words that carry no
    /// takhrij. It stays honest because the text is the reader's own, typed on
    /// their own phone, never uploaded and never shown to anyone else; the app
    /// labels it as theirs rather than dressing it as catalogue content.
    /// </summary>
    public bool AllowCustomWidget { get; set; } = true;

    /// <summary>
    /// How much of their own text the reader may pin. A widget is a few square
    /// centimetres, so this is a legibility limit before it is a storage one.
    /// </summary>
    public int CustomWidgetMaxLength { get; set; } = 280;

    /// <summary>
    /// Bumped on every change, so a device can tell whether what it last pushed
    /// to the launcher is still current without diffing every field.
    /// </summary>
    public int Version { get; set; } = 1;
}
