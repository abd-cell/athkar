namespace Athkar.Shareds.Enums;

/// <summary>
/// The prayer-time convention, i.e. which authority's twilight angles to use.
///
/// Carried by the server only as the *default* a fresh install starts on — the
/// times themselves are computed on the device. The numbers match the app's own
/// mapping onto the `adhan` package; changing one means changing both.
/// </summary>
public enum CalculationMethod
{
    UmmAlQura = 1,
    MuslimWorldLeague = 2,
    Egyptian = 3,
    Karachi = 4,
    Kuwait = 5,
    Qatar = 6,
    Dubai = 7,

    /// <summary>Diyanet İşleri Başkanlığı.</summary>
    Turkey = 8,

    /// <summary>ISNA.</summary>
    NorthAmerica = 9,

    Singapore = 10,
    Tehran = 11,
    MoonsightingCommittee = 12,

    /// <summary>
    /// دائرة الإفتاء العام / وزارة الأوقاف الأردنية — Fajr 18°, Isha 18°.
    ///
    /// Appended at 13 rather than slotted next to its neighbours: these numbers
    /// are the contract across all three stacks, and reordering a member
    /// silently re-labels every stored row.
    ///
    /// It exists because no other convention in this list is right for Jordan.
    /// Umm al-Qura puts Isha at a fixed 90 minutes after Maghrib — a Saudi rule
    /// — which in Amman is eight minutes late; the Muslim World League's 17° is
    /// four minutes early. Neither is a rounding difference: it is the
    /// difference between praying at the mosque's time and praying alone.
    /// </summary>
    Jordan = 13,
}
