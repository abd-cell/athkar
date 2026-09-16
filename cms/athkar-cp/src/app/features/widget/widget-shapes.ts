import { WidgetSurface } from '../../core/api/models';

/**
 * Which parts of the phone's widget each catalogue entry fills.
 *
 * This is a **mirror of `WidgetBridge._build`** in
 * `app/athkar_app/lib/core/widget_bridge.dart`, and it can be a faithful one
 * for a reason worth stating: the phone renders *one* layout for every entry in
 * the gallery — a date, a headline, a title, an optional row of five prayer
 * times, and a subtitle — and what varies between entries is only which of
 * those slots carry text. So the console does not have to reimplement thirty
 * designs to show an admin what a widget looks like; it has to know which slots
 * are filled, which is this table.
 *
 * The two files are kept in step by hand, and the cost of them drifting is
 * bounded and visible: the console would show an admin a slot that is empty on
 * the phone, or miss one that is not. `widget-shapes.spec.ts` pins the keys the
 * server seeds so a new entry cannot be added here without a shape.
 *
 * The sample values are **i18n keys, not content**. The server has no idea
 * where any reader lives, so the preview cannot show real times; it shows a
 * plausible Makkah day, labelled as a sample, in the console's own language.
 */
export interface WidgetShape {
  /** The Hijri line along the top. */
  readonly date: boolean;

  /** i18n keys for the three text slots. Absent means the slot is empty. */
  readonly headline?: string;
  readonly title?: string;
  readonly subtitle?: string;

  /** The five-column row of prayer times. */
  readonly prayerRow: boolean;

  /**
   * True when the title is narrated text — a dhikr, an ayah, the reader's own
   * words — which the phone sets in Amiri rather than the interface face.
   */
  readonly narrated?: boolean;
}

/**
 * What the phone would draw for this key.
 *
 * Lock-screen entries get no shape at all, and that is not an oversight: this
 * build ships no lock-screen widget on either platform — the Android provider
 * declares `widgetCategory="home_screen"` and there is no WidgetKit extension —
 * so a lock entry is browsable in the app's gallery and placeable nowhere. The
 * console says so rather than drawing a picture of something that does not
 * exist.
 */
export function shapeFor(key: string, surface: WidgetSurface): WidgetShape | null {
  if (surface === WidgetSurface.Lock) return null;

  switch (key) {
    case 'today_prayers':
    case 'prayer_calendar':
      return {
        date: true,
        headline: 'widget.preview.weekday',
        title: 'widget.preview.nextPrayer',
        subtitle: 'widget.preview.remaining',
        prayerRow: true,
      };

    case 'prayer_times':
    case 'comprehensive':
      return {
        date: true,
        headline: 'widget.preview.city',
        title: 'widget.preview.nextPrayer',
        subtitle: 'widget.preview.countdown',
        prayerRow: true,
      };

    case 'prayer_track':
      return {
        date: true,
        headline: 'widget.preview.elapsed',
        title: 'widget.preview.elapsedCount',
        subtitle: 'widget.preview.nextPrayer',
        prayerRow: false,
      };

    case 'date_only':
      return {
        date: true,
        headline: 'widget.preview.weekday',
        title: 'widget.preview.hijri',
        subtitle: 'widget.preview.gregorian',
        prayerRow: false,
      };

    case 'night_thirds':
      return {
        date: true,
        headline: 'anchor.islamicMidnight',
        title: 'widget.preview.midnightAt',
        subtitle: 'widget.preview.lastThirdAt',
        prayerRow: false,
      };

    case 'occasion_countdown':
      return {
        date: true,
        headline: 'widget.preview.ramadan',
        title: 'widget.preview.daysLeft',
        prayerRow: false,
      };

    case 'quran_verse':
      return {
        date: true,
        title: 'widget.preview.verse',
        subtitle: 'widget.preview.verseSource',
        prayerRow: false,
        narrated: true,
      };

    case 'custom_pinned':
      return {
        date: true,
        title: 'widget.preview.custom',
        subtitle: 'widget.preview.customNote',
        prayerRow: false,
        narrated: true,
      };

    // Everything else falls to the dhikr shape, exactly as the phone does. A
    // key this console has never heard of lands here too, which is the right
    // answer: the app's own fallback is the same one.
    default:
      return {
        date: true,
        headline: 'widget.preview.chapter',
        title: 'widget.preview.dhikr',
        subtitle: 'widget.preview.repeat',
        prayerRow: false,
        narrated: true,
      };
  }
}

/** The five obligatory prayers, in the order the row prints them. */
export const PREVIEW_PRAYERS: readonly { readonly name: string; readonly at: string }[] = [
  { name: 'anchor.fajr', at: 'widget.preview.time.fajr' },
  { name: 'anchor.dhuhr', at: 'widget.preview.time.dhuhr' },
  { name: 'anchor.asr', at: 'widget.preview.time.asr' },
  { name: 'anchor.maghrib', at: 'widget.preview.time.maghrib' },
  { name: 'anchor.isha', at: 'widget.preview.time.isha' },
];
