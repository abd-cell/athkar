namespace Athkar.Shareds.Enums;

/// <summary>
/// Where a widget lives.
///
/// Separate from <see cref="WidgetKind"/> because the surface, not the content,
/// decides what a widget may be: a lock-screen widget on both platforms is a
/// glyph-sized strip with no colour of its own, so a design that works on a home
/// screen cannot simply be offered there.
/// </summary>
public enum WidgetSurface
{
    /// <summary>The launcher's home screen (Android) or Today view (iOS).</summary>
    Home = 1,

    /// <summary>The lock screen. Monochrome, tiny, and no background of its own.</summary>
    Lock = 2,
}
