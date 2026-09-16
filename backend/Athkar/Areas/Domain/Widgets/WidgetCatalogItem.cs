using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Widgets;

/// <summary>
/// One entry in the widget gallery — the list the reader browses before they
/// place anything.
///
/// The load-bearing thing to understand here is what this row is *not*: it is
/// not a design. A widget is drawn by code compiled into the app, because a
/// widget gets a fraction of a second of CPU and no network; nothing the admin
/// types could become a new layout on a phone that already shipped. What this
/// row carries is everything *around* the drawing — whether the entry is
/// offered at all, what it is called in each language, where it sits in the
/// gallery, and which badges it wears.
///
/// <see cref="Key"/> is therefore a contract, not a label. The app holds a
/// renderer per key and silently skips a key it has never heard of, which is
/// what lets the catalogue be edited from the console and still reach an
/// install from last year without crashing it. An admin inventing a key gets an
/// entry nobody can see; an admin hiding a key withdraws it from every phone at
/// the next sync.
/// </summary>
public class WidgetCatalogItem : AuditableEntity
{
    /// <summary>
    /// The renderer this entry names. Stable, lower-case, snake_case, and
    /// matched against the app's registry — see
    /// <c>app/athkar_app/lib/features/widgets/widget_designs.dart</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public WidgetSurface Surface { get; set; } = WidgetSurface.Home;

    public WidgetFamily Family { get; set; } = WidgetFamily.Prayer;

    /// <summary>
    /// How many designs of this widget the admin is willing to offer.
    ///
    /// A *cap*, not a count: the app knows how many it can actually draw and
    /// takes the smaller of the two. Raising this on the server cannot conjure a
    /// design into an installed app, but lowering it can take one away — which
    /// is the direction that needs to work without an app update.
    /// </summary>
    public int DesignCount { get; set; } = 1;

    /// <summary>Which design a reader who has never chosen one sees. Zero-based.</summary>
    public int DefaultDesign { get; set; }

    /// <summary>Wears the «حصرية» badge in the gallery.</summary>
    public bool IsExclusive { get; set; }

    /// <summary>
    /// Wears the «جديد» badge. A flag rather than a date comparison on
    /// <see cref="BaseEntity.CreationDate"/>, because "new" is an editorial
    /// decision — a widget re-drawn from scratch is new to the reader and an
    /// entry seeded on day one never was.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Off hides the entry from the gallery. The reader who already placed it
    /// keeps a working widget: withdrawing a design is not the same as breaking
    /// a home screen, and the master switch on <see cref="WidgetSettings"/> is
    /// what does the latter.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<WidgetCatalogItemTranslation> Translations { get; set; } = [];
}
