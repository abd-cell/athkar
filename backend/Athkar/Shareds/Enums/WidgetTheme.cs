namespace Athkar.Shareds.Enums;

/// <summary>
/// How a widget is painted.
///
/// Separate from the app's own theme because a home screen is not the app: a
/// reader may run the app dark and still want a light widget on a light
/// wallpaper, and the launcher gives no way to ask.
/// </summary>
public enum WidgetTheme
{
    /// <summary>Follow the phone's light/dark setting.</summary>
    System = 0,
    Light = 1,
    Dark = 2,

    /// <summary>
    /// Parchment with no opaque panel behind it, so the wallpaper shows
    /// through. The design's own preference where the launcher allows it.
    /// </summary>
    Transparent = 3,
}
