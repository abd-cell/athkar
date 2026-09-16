namespace Athkar.Shareds.Enums;

/// <summary>
/// What a home-screen widget shows.
///
/// A closed set rather than a free-form template, because each one is a
/// separate native layout: a widget gets a fraction of a second of CPU and no
/// network, so what it can draw has to be decided at build time and only
/// *chosen* at run time.
/// </summary>
public enum WidgetKind
{
    /// <summary>The next prayer and a countdown to it.</summary>
    NextPrayer = 1,

    /// <summary>The dhikr for this moment, with a tap that opens the session.</summary>
    Dhikr = 2,

    /// <summary>Both, stacked — for the larger sizes only.</summary>
    Combined = 3,
}
