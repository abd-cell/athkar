using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Widgets;
using Athkar.Shareds.Enums;

namespace Athkar.DataAccess.Seeders;

/// <summary>
/// The widget gallery's shipped contents.
///
/// Unlike every other seeder here, this one is **additive on every startup**
/// rather than all-or-nothing. The reason is the direction the data flows: a
/// catalogue entry is a name for a renderer compiled into the app, so a release
/// that adds a widget adds a key, and a deployment that already has rows would
/// never see it under the usual "seed only when the table is empty" rule. The
/// new key would exist in every installed app and in no database.
///
/// What it will not do is touch a row that is already there. An admin who has
/// renamed a widget, reordered the gallery, hidden an entry or translated it
/// into a third language has made a decision, and a seeder that overwrites it
/// on the next restart is the kind of bug that takes a month to attribute.
///
/// Deletions are respected too: the key filter includes soft-deleted rows, so an
/// entry an admin withdrew stays withdrawn instead of reappearing every boot.
/// </summary>
public static class WidgetCatalogSeeder
{
    /// <summary>
    /// One shipped entry: the key the app's renderer registry answers to, and
    /// the wording it arrives under before anybody edits it.
    /// </summary>
    private readonly record struct Entry(
        string Key,
        WidgetSurface Surface,
        WidgetFamily Family,
        int DesignCount,
        string Arabic,
        string English,
        string? ArabicSubtitle = null,
        string? EnglishSubtitle = null,
        bool IsExclusive = false,
        bool IsNew = false);

    /// <summary>
    /// The entry a reader meets first when the admin has not chosen one.
    ///
    /// The day and the five times: the shape every reader of a prayer app
    /// recognises, and the only one that is useful before they have told the
    /// app anything about themselves.
    /// </summary>
    public const string DefaultKey = "today_prayers";

    public static async Task SeedAsync(DatabaseService db)
    {
        // Straight off the DbSet rather than through a repository, so
        // soft-deleted rows are counted too — see the class comment.
        var known = await db.WidgetCatalogItems
            .Select(x => x.Key)
            .ToListAsync();

        var missing = Catalogue.Where(entry => !known.Contains(entry.Key)).ToList();

        // Nothing new to add. The default is not revisited either — see
        // AdoptDefault for why that is deliberate.
        if (missing.Count == 0) return;

        // Appended after whatever is already there, per surface, so adding a
        // widget in a new release never rearranges a gallery an admin has
        // already put in the order they wanted.
        var next = await db.WidgetCatalogItems
            .GroupBy(x => x.Surface)
            .Select(g => new { Surface = g.Key, Max = g.Max(x => x.SortOrder) })
            .ToDictionaryAsync(x => x.Surface, x => x.Max + 1);

        foreach (var entry in missing)
        {
            var order = next.TryGetValue(entry.Surface, out var value) ? value : 0;
            next[entry.Surface] = order + 1;

            var item = new WidgetCatalogItem
            {
                Key = entry.Key,
                Surface = entry.Surface,
                Family = entry.Family,
                DesignCount = entry.DesignCount,
                DefaultDesign = 0,
                IsExclusive = entry.IsExclusive,
                IsNew = entry.IsNew,
                IsEnabled = true,
                SortOrder = order,
            };

            item.Translations.Add(new WidgetCatalogItemTranslation
            {
                LanguageCode = "ar",
                Title = entry.Arabic,
                Subtitle = entry.ArabicSubtitle,
            });

            item.Translations.Add(new WidgetCatalogItemTranslation
            {
                LanguageCode = "en",
                Title = entry.English,
                Subtitle = entry.EnglishSubtitle,
            });

            db.WidgetCatalogItems.Add(item);
        }

        await db.SaveChangesAsync();
        await AdoptDefault(db);
    }

    /// <summary>
    /// Points the settings row at <see cref="DefaultKey"/> on the one boot that
    /// brings the gallery into existence, and never again.
    ///
    /// Needed because the settings row predates the catalogue: a deployment
    /// upgrading into this feature already has a row, so without this the
    /// column would stay empty and the console would open on no default at all.
    ///
    /// The guard is "this run created catalogue entries", not "the key is
    /// null", and the difference matters in both directions. Null is itself a
    /// meaningful setting — "whatever the reader's own build can draw first" —
    /// so an admin who clears it must not have it filled back in on the next
    /// restart. And keying off <c>ModificationDate</c> instead would strand
    /// every deployment whose settings row had been saved once for some
    /// unrelated reason, which is most of them.
    ///
    /// Every deployment gets that one boot exactly once, so every deployment
    /// gets a default exactly once.
    /// </summary>
    private static async Task AdoptDefault(DatabaseService db)
    {
        var settings = await db.WidgetSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (settings is null || settings.DefaultWidgetKey is not null) return;

        if (!await db.WidgetCatalogItems.AnyAsync(x => x.Key == DefaultKey && x.IsEnabled)) return;

        settings.DefaultWidgetKey = DefaultKey;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Every key the app can draw, in the order a reader meets them.
    ///
    /// The design counts are **caps**, and they are set to what the app actually
    /// implements today. Raising one here cannot conjure a design into a phone;
    /// the app takes the smaller of this number and its own, so a cap ahead of
    /// the app is merely ignored and a cap behind it withdraws a design. Only
    /// the second direction is useful, which is why these track the app rather
    /// than aspire ahead of it.
    /// </summary>
    private static readonly Entry[] Catalogue =
    [
        // ── Home screen ──
        new("today_prayers", WidgetSurface.Home, WidgetFamily.PrayerAndDate, 4,
            "اليوم ومواقيت الصلاة", "Today and prayer times",
            "اسم اليوم والتاريخان والمواقيت الخمسة", "The day, both dates and all five times"),

        new("prayer_times", WidgetSurface.Home, WidgetFamily.Prayer, 4,
            "مواقيت الصلاة", "Prayer times",
            "المواقيت الخمسة والوقت المتبقي", "The five times and the time left",
            IsNew: true),

        new("prayer_track", WidgetSurface.Home, WidgetFamily.Tracker, 2,
            "مسار الصلوات", "Prayer track",
            "ما صلّيته اليوم في سطر واحد", "What you have prayed today, in one line",
            IsNew: true),

        new("date_only", WidgetSurface.Home, WidgetFamily.Date, 4,
            "التاريخ فقط", "Date only",
            "الهجري والميلادي، بلا مواقيت", "Hijri and Gregorian, with no times"),

        new("prayer_calendar", WidgetSurface.Home, WidgetFamily.PrayerAndDate, 3,
            "الصلاة والتقويم", "Prayer and calendar",
            "أسبوع كامل بجانب الصلاة القادمة", "A full week beside the next prayer"),

        new("assorted_adhkar", WidgetSurface.Home, WidgetFamily.Dhikr, 3,
            "أذكار منوعة", "Assorted adhkar",
            "ذكر يتبدّل، بتخريجه", "A dhikr that changes, with its attribution"),

        new("quran_verse", WidgetSurface.Home, WidgetFamily.Quran, 2,
            "آيات قرآنية", "Qur'an verses",
            "آية مع اسم السورة ورقمها", "An ayah with its surah and number"),

        new("occasion_countdown", WidgetSurface.Home, WidgetFamily.Countdown, 3,
            "العد التنازلي للمناسبات", "Occasion countdown",
            "رمضان وعيد الفطر وعيد الأضحى", "Ramadan and the two Eids"),

        new("night_thirds", WidgetSurface.Home, WidgetFamily.Moon, 2,
            "أثلاث الليل", "Night thirds",
            "منتصف الليل والثلث الأخير", "Midnight and the last third"),

        new("comprehensive", WidgetSurface.Home, WidgetFamily.PrayerAndDate, 2,
            "ويدجت شامل", "All in one",
            "المواقيت والتاريخ وأثلاث الليل معاً", "Times, date and night thirds together",
            IsExclusive: true),

        new("custom_pinned", WidgetSurface.Home, WidgetFamily.Dhikr, 2,
            "ويدجت اختياري", "Your own text",
            "ثبّت آية أو دعاءً أو نصّك الخاص", "Pin an ayah, a du'a, or your own words"),

        // ── Lock screen ──
        new("lock_next_prayer", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "الصلاة القادمة", "Next prayer"),

        new("lock_previous_prayer", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "الصلاة السابقة", "Previous prayer"),

        new("lock_prev_next", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "الصلاة السابقة والقادمة", "Previous and next prayer"),

        new("lock_prev_next_bar", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "السابقة والقادمة مع شريط", "Previous and next, with a bar"),

        new("lock_all_times", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "جميع المواقيت", "All prayer times"),

        new("lock_three_times", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "ثلاث مواقيت", "Three times"),

        new("lock_fajr_dhuhr", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "الفجر والظهر", "Fajr and Dhuhr"),

        new("lock_asr_maghrib_isha", WidgetSurface.Lock, WidgetFamily.Prayer, 1,
            "العصر والمغرب والعشاء", "Asr, Maghrib and Isha"),

        new("lock_prayer_counter", WidgetSurface.Lock, WidgetFamily.Tracker, 1,
            "عداد الصلاة", "Prayer counter"),

        new("lock_date", WidgetSurface.Lock, WidgetFamily.Date, 1,
            "التاريخ الهجري أو الميلادي", "Hijri or Gregorian date"),

        new("lock_date_next_prayer", WidgetSurface.Lock, WidgetFamily.PrayerAndDate, 1,
            "التاريخ والصلاة القادمة", "Date and next prayer"),

        new("lock_date_prev_next", WidgetSurface.Lock, WidgetFamily.PrayerAndDate, 1,
            "التاريخ مع السابقة والقادمة", "Date with previous and next"),

        new("lock_day_and_date", WidgetSurface.Lock, WidgetFamily.Date, 1,
            "اليوم والتاريخ", "Day and date"),

        new("lock_day_number", WidgetSurface.Lock, WidgetFamily.Date, 1,
            "رقم اليوم", "Day number"),

        new("lock_day", WidgetSurface.Lock, WidgetFamily.Date, 1,
            "اليوم", "The day"),

        new("lock_daily_adhkar", WidgetSurface.Lock, WidgetFamily.Dhikr, 1,
            "أذكار اليوم", "Today's adhkar"),

        new("lock_assorted_adhkar", WidgetSurface.Lock, WidgetFamily.Dhikr, 1,
            "أذكار منوعة", "Assorted adhkar"),

        new("lock_dua", WidgetSurface.Lock, WidgetFamily.Dhikr, 1,
            "أدعية بخط جميل", "Du'a in a fine hand"),

        new("lock_quran_verse", WidgetSurface.Lock, WidgetFamily.Quran, 1,
            "آيات قرآنية", "Qur'an verses"),

        new("lock_mushaf_page", WidgetSurface.Lock, WidgetFamily.Quran, 1,
            "صفحة المصحف", "Mushaf page"),

        new("lock_moon_phase", WidgetSurface.Lock, WidgetFamily.Moon, 1,
            "حالة القمر", "Moon phase"),

        new("lock_ramadan_countdown", WidgetSurface.Lock, WidgetFamily.Countdown, 1,
            "العد التنازلي لرمضان", "Countdown to Ramadan"),
    ];
}
