/// Which widget keys this build understands.
///
/// Split out of the drawing code on purpose. Two very different callers need
/// this same fact and only one of them is a screen:
///
/// * the gallery, to decide what to show and how many designs to offer;
/// * [WidgetBridge], to decide what to hand the launcher — and that runs from a
///   background sync, with no element tree and no `BuildContext` anywhere.
///
/// So the *list of keys* lives here in `core`, as data, and the *renderers* live
/// in `features/widgets/widget_designs.dart`. Adding a widget means touching
/// both, and the app's own test asserts that they agree with the catalogue the
/// server ships.
library;

class WidgetKeys {
  const WidgetKeys._();

  /// How many designs this build implements for a key. Zero means unknown.
  ///
  /// The server's `designCount` is a *cap* on this, never a replacement for it:
  /// the gallery takes the smaller of the two, so a server ahead of the app
  /// cannot advertise a design that would render blank.
  static int designsFor(String key) => switch (key) {
        'today_prayers' => 4,
        'prayer_times' => 4,
        'prayer_track' => 2,
        'date_only' => 4,
        'prayer_calendar' => 3,
        'assorted_adhkar' => 3,
        'quran_verse' => 2,
        'occasion_countdown' => 3,
        'night_thirds' => 2,
        'comprehensive' => 2,
        'custom_pinned' => 2,
        _ => lock.contains(key) ? 1 : 0,
      };

  static bool knows(String key) => designsFor(key) > 0;

  /// The height a preview of this key wants, in logical pixels.
  ///
  /// Set per key rather than per design: a launcher gives a widget a height in
  /// grid cells, and a design that changed its own height would be showing the
  /// reader a shape the launcher will not grant it.
  static double heightFor(String key) => switch (key) {
        'today_prayers' => 132,
        'prayer_times' => 126,
        'prayer_calendar' => 150,
        'assorted_adhkar' => 130,
        'quran_verse' => 130,
        'custom_pinned' => 130,
        'comprehensive' => 150,
        'occasion_countdown' => 118,
        'date_only' => 118,
        // Three rows: Isha, midnight, the last third. At the lock-screen
        // default of 74 the third one is cut off, which reads as a bug rather
        // than as a smaller widget.
        'night_thirds' => 104,
        'prayer_track' => 106,
        _ => 74,
      };

  /// Every lock-screen key this build draws.
  ///
  /// One design each, and no colour of their own: both platforms render a lock
  /// widget as a monochrome glyph over whatever the reader's wallpaper is, so a
  /// design that depended on a fill would be drawn as a flat blob on the phone
  /// and as a designed panel in the gallery — a preview that lies.
  static const lock = {
    'lock_next_prayer',
    'lock_previous_prayer',
    'lock_prev_next',
    'lock_prev_next_bar',
    'lock_all_times',
    'lock_three_times',
    'lock_fajr_dhuhr',
    'lock_asr_maghrib_isha',
    'lock_prayer_counter',
    'lock_date',
    'lock_date_next_prayer',
    'lock_date_prev_next',
    'lock_day_and_date',
    'lock_day_number',
    'lock_day',
    'lock_daily_adhkar',
    'lock_assorted_adhkar',
    'lock_dua',
    'lock_quran_verse',
    'lock_mushaf_page',
    'lock_moon_phase',
    'lock_ramadan_countdown',
  };

  /// The keys whose widget is a row of prayer times.
  ///
  /// Named because the native layout has a five-column row it only shows for
  /// these: [WidgetBridge] fills it, and everything else leaves it hidden.
  static const withPrayerRow = {
    'today_prayers',
    'prayer_times',
    'prayer_calendar',
    'comprehensive',
  };
}
