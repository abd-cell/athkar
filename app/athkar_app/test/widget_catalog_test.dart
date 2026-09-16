import 'package:athkar_app/features/widgets/widget_designs.dart';
import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The contract between the console and this build.
///
/// The server names widgets; the app draws them. Everything tested here guards
/// one direction of that: a console a release *ahead* of the app must not be
/// able to make the gallery advertise something that would render blank, and a
/// console *behind* it must still be able to withdraw a design without waiting
/// for an app update.
///
/// Both failures are silent on a home screen, which is why they are worth a
/// test rather than a careful reading.
void main() {
  WidgetCatalogItem item({
    String key = 'prayer_times',
    int designCount = 4,
    int defaultDesign = 0,
    WidgetSurface surface = WidgetSurface.home,
  }) =>
      WidgetCatalogItem(
        id: 1,
        key: key,
        surface: surface,
        family: WidgetFamily.prayer,
        title: 'مواقيت الصلاة',
        designCount: designCount,
        defaultDesign: defaultDesign,
        isExclusive: false,
        isNew: false,
        sortOrder: 0,
      );

  group('designsAvailable', () {
    test('a server ahead of the app cannot advertise a design it cannot draw', () {
      expect(item(designCount: 40).designsAvailable(4), 4);
    });

    test('a server behind the app withdraws designs without an app update', () {
      expect(item(designCount: 2).designsAvailable(4), 2);
    });

    test('never falls to zero, because a widget with no design is a blank box', () {
      expect(item(designCount: 0).designsAvailable(0), 1);
    });
  });

  group('the renderer registry', () {
    test('knows every key the server ships', () {
      // The seeded catalogue and this build have to agree, or a reader syncing
      // a fresh install gets an entry the gallery silently drops.
      const seeded = [
        'today_prayers',
        'prayer_times',
        'prayer_track',
        'date_only',
        'prayer_calendar',
        'assorted_adhkar',
        'quran_verse',
        'occasion_countdown',
        'night_thirds',
        'comprehensive',
        'custom_pinned',
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
      ];

      for (final key in seeded) {
        expect(WidgetDesigns.knows(key), isTrue, reason: 'no renderer for "$key"');
      }
    });

    test('does not claim to know a key it has never heard of', () {
      // The gallery drops these rather than drawing an empty box, which is what
      // lets the console add next year's widget today.
      expect(WidgetDesigns.knows('widget_from_a_later_release'), isFalse);
      expect(WidgetDesigns.designsFor('widget_from_a_later_release'), 0);
    });
  });

  group('resolveSelection', () {
    // The rule that decides what is actually on a reader's home screen. Every
    // branch of it fails silently: the launcher keeps drawing *something*, and
    // the reader has no way to tell that it is the wrong something.
    WidgetCatalog gallery({String? adminDefault}) => WidgetCatalog(
          settings: WidgetSettings.fromJson({'defaultWidgetKey': adminDefault}),
          items: [
            item(key: 'today_prayers'),
            item(key: 'prayer_times'),
            item(key: 'lock_day', surface: WidgetSurface.lock),
          ],
        );

    bool everything(String key) => true;

    test('the reader keeps what they chose', () {
      final chosen = gallery(adminDefault: 'today_prayers')
          .resolveSelection(chosen: 'prayer_times', canDraw: everything);

      expect(chosen!.key, 'prayer_times');
    });

    test('a reader who has chosen nothing gets the admin default', () {
      final chosen = gallery(adminDefault: 'prayer_times')
          .resolveSelection(chosen: null, canDraw: everything);

      expect(chosen!.key, 'prayer_times');
    });

    test('with no default at all, the first entry the build can draw', () {
      final chosen = gallery().resolveSelection(chosen: null, canDraw: everything);

      expect(chosen!.key, 'today_prayers');
    });

    test('a choice the admin has withdrawn stops being the reader\'s', () {
      // The gallery no longer offers it, so neither may the home screen — the
      // same rule that takes a withdrawn dhikr off every surface.
      final chosen = gallery(adminDefault: 'prayer_times')
          .resolveSelection(chosen: 'a_withdrawn_widget', canDraw: everything);

      expect(chosen!.key, 'prayer_times');
    });

    test('a default this build cannot draw falls through to one it can', () {
      // The server may be a release ahead of the app. A default naming a
      // renderer this build lacks has to degrade, not leave the launcher blank.
      final chosen = gallery(adminDefault: 'today_prayers').resolveSelection(
        chosen: null,
        canDraw: (key) => key != 'today_prayers',
      );

      expect(chosen!.key, 'prayer_times');
    });

    test('lock-screen entries are never the home-screen selection', () {
      final chosen = gallery().resolveSelection(chosen: 'lock_day', canDraw: everything);

      expect(chosen!.surface, WidgetSurface.home);
    });

    test('null when the gallery holds nothing this build understands', () {
      // A real state on an install several releases old. The caller has a
      // sentence for it; drawing an empty box instead would look like a crash.
      final chosen = gallery().resolveSelection(chosen: null, canDraw: (_) => false);

      expect(chosen, isNull);
    });
  });

  group('the cached gallery', () {
    test('survives a round trip, so the screen opens offline', () {
      const original = WidgetCatalog(
        settings: WidgetSettings.fallback,
        items: [],
      );

      final restored = WidgetCatalog.fromJson(original.toJson());

      expect(restored.isEmpty, isTrue);
      expect(restored.settings.allowCustomWidget, isTrue);
    });

    test('keeps the rules beside the gallery they are browsed under', () {
      final catalog = WidgetCatalog(
        settings: WidgetSettings.fallback,
        items: [item(), item(key: 'lock_day', surface: WidgetSurface.lock)],
      );

      final restored = WidgetCatalog.fromJson(catalog.toJson());

      expect(restored.forSurface(WidgetSurface.home).single.key, 'prayer_times');
      expect(restored.forSurface(WidgetSurface.lock).single.key, 'lock_day');
    });

    test('an unreadable cache degrades to an empty gallery, never to a throw', () {
      // Launch reads this. A cache written by an older build has to produce a
      // screen with a sentence on it, not an exception in the splash.
      final restored = WidgetCatalog.fromJson(const {'items': 'not a list at all'});

      expect(restored.isEmpty, isTrue);
    });
  });
}
