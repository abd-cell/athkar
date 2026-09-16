import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The client half of the admin's widget rules.
///
/// `resolveKind` is what makes withdrawing a kind actually reach a home screen.
/// The server corrects its own *default*; this corrects the **reader's saved
/// preference**, which the server has never seen. Without it, somebody who
/// chose the dhikr widget last month keeps it after the admin takes it away —
/// on the one surface nobody can correct in the moment.
void main() {
  WidgetSettings rules({
    bool enabled = true,
    bool prayer = true,
    bool dhikr = true,
    WidgetKind defaultKind = WidgetKind.nextPrayer,
  }) =>
      WidgetSettings(
        isEnabled: enabled,
        defaultKind: defaultKind,
        allowPrayerWidget: prayer,
        allowDhikrWidget: dhikr,
        theme: WidgetTheme.system,
        refreshMinutes: 30,
        showHijriDate: true,
        showCountdown: true,
        allowBackgroundColor: true,
        allowTransparency: true,
        allowBackgroundImage: true,
        allowCustomWidget: true,
        customWidgetMaxLength: 280,
        version: 1,
      );

  group('allowedKinds', () {
    test('lists what the admin offers, in a stable order', () {
      expect(rules().allowedKinds, [WidgetKind.nextPrayer, WidgetKind.dhikr]);
      expect(rules(prayer: false).allowedKinds, [WidgetKind.dhikr]);
      expect(rules(dhikr: false).allowedKinds, [WidgetKind.nextPrayer]);
      expect(rules(prayer: false, dhikr: false).allowedKinds, isEmpty);
    });
  });

  group('resolveKind', () {
    test('honours the reader when their choice is still offered', () {
      expect(rules().resolveKind(WidgetKind.dhikr), WidgetKind.dhikr);
      expect(rules().resolveKind(WidgetKind.nextPrayer), WidgetKind.nextPrayer);
    });

    test('falls back when the admin withdraws the chosen kind', () {
      expect(rules(dhikr: false).resolveKind(WidgetKind.dhikr), WidgetKind.nextPrayer);
      expect(rules(prayer: false).resolveKind(WidgetKind.nextPrayer), WidgetKind.dhikr);
    });

    test('prefers the admin default when falling back', () {
      final settings = rules(defaultKind: WidgetKind.dhikr);

      // `combined` is never offered on its own, so it always falls back — and
      // it should land on what the admin nominated, not on the first in the list.
      expect(settings.resolveKind(WidgetKind.combined), WidgetKind.dhikr);
    });

    test('falls back to something offered when even the default is withdrawn', () {
      final settings = rules(prayer: false, defaultKind: WidgetKind.nextPrayer);

      expect(settings.resolveKind(WidgetKind.nextPrayer), WidgetKind.dhikr);
    });

    test('never returns a kind that is not offered', () {
      for (final prayer in [true, false]) {
        for (final dhikr in [true, false]) {
          if (!prayer && !dhikr) continue; // the master switch covers this

          final settings = rules(prayer: prayer, dhikr: dhikr);

          for (final preferred in WidgetKind.values) {
            expect(
              settings.allowedKinds,
              contains(settings.resolveKind(preferred)),
              reason: 'prayer=$prayer dhikr=$dhikr preferred=$preferred',
            );
          }
        }
      }
    });

    test('degrades to the default rather than throwing when nothing is offered', () {
      // Reachable only with widgets switched off, where nothing renders anyway —
      // but it must not be an exception on a launch path.
      final settings = rules(enabled: false, prayer: false, dhikr: false);

      expect(settings.resolveKind(WidgetKind.dhikr), settings.defaultKind);
    });
  });

  group('serialisation', () {
    test('survives a round trip, so the offline cache is faithful', () {
      const original = WidgetSettings(
        isEnabled: false,
        defaultKind: WidgetKind.dhikr,
        allowPrayerWidget: false,
        allowDhikrWidget: true,
        theme: WidgetTheme.transparent,
        refreshMinutes: 120,
        showHijriDate: false,
        showCountdown: false,
        allowBackgroundColor: false,
        allowTransparency: false,
        allowBackgroundImage: false,
        allowCustomWidget: false,
        customWidgetMaxLength: 120,
        dhikrCategoryId: 7,
        version: 9,
      );

      final restored = WidgetSettings.fromJson(original.toJson());

      expect(restored.isEnabled, isFalse);
      expect(restored.defaultKind, WidgetKind.dhikr);
      expect(restored.theme, WidgetTheme.transparent);
      expect(restored.refreshMinutes, 120);
      expect(restored.dhikrCategoryId, 7);
      // The reader-facing permissions ride in the same cache: a widget drawn
      // offline must obey the rules that were in force when they last arrived,
      // not the shipped defaults.
      expect(restored.allowCustomWidget, isFalse);
      expect(restored.allowTransparency, isFalse);
      expect(restored.customWidgetMaxLength, 120);
      expect(restored.version, 9);
    });

    test('an empty payload yields the shipped defaults', () {
      final settings = WidgetSettings.fromJson(const {});

      expect(settings.isEnabled, isTrue);
      expect(settings.defaultKind, WidgetKind.nextPrayer);
      expect(settings.refreshMinutes, 30);
      expect(settings.dhikrCategoryId, isNull);
    });
  });
}
