import 'package:adhan/adhan.dart' as adhan;
import 'package:athkar_app/core/cities.dart';
import 'package:athkar_app/core/prayer_times.dart';
import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// Which convention a reader's times are computed with.
///
/// The bug this guards was silent and nightly. Every city in the list carries
/// its country, the picker even shows it — and choosing a city threw it away,
/// leaving the reader on whatever global default the admin had set. For a
/// reader in Amman that was Umm al-Qura, whose Isha is a fixed ninety minutes
/// after Maghrib because that is the rule in Makkah. In Amman it is eight
/// minutes late, and nothing on screen says so.
void main() {
  /// Amman, on a date far enough from the equinox to separate the conventions.
  adhan.PrayerTimes amman(CalculationMethod method) {
    final city = cities.firstWhere((c) => c.nameEn == 'Amman');

    return adhan.PrayerTimes(
      adhan.Coordinates(city.latitude, city.longitude),
      adhan.DateComponents(2026, 9, 13),
      PrayerCalculator.parametersFor(method)..madhab = adhan.Madhab.shafi,
    );
  }

  int minutesAfterMaghrib(adhan.PrayerTimes t) =>
      t.isha.difference(t.maghrib).inMinutes;

  group('the country decides the convention', () {
    test('Jordan gets its own authority, not a neighbour\'s', () {
      expect(methodForCountry('JO'), CalculationMethod.jordan);
    });

    test('the lookup is case-insensitive and safe on nothing', () {
      expect(methodForCountry('jo'), CalculationMethod.jordan);
      expect(methodForCountry(null), isNull);
    });

    test('an unlisted country keeps the configured default', () {
      // Honest: we have not been told what the local authority uses, so we do
      // not invent one.
      expect(methodForCountry('ZZ'), isNull);
    });

    test('every mapped country names a real convention', () {
      for (final entry in countryCalculationMethods.entries) {
        expect(
          CalculationMethod.values,
          contains(entry.value),
          reason: entry.key,
        );
      }
    });
  });

  group('the nearest city carries the country', () {
    test('a coordinate in Amman resolves to Jordan', () {
      // This is what makes the fix self-healing: an install that already had a
      // location, set before any of this existed, gets the right convention on
      // its next launch rather than waiting to be told its city again.
      expect(nearestCity(31.95, 35.91)?.country, 'JO');
      expect(methodForCountry(nearestCity(31.95, 35.91)?.country),
          CalculationMethod.jordan);
    });

    test('a coordinate in Makkah resolves to Saudi', () {
      expect(nearestCity(21.39, 39.86)?.country, 'SA');
    });

    test('no location is not an error', () {
      expect(nearestCity(null, null), isNull);
      expect(nearestCity(31.95, null), isNull);
    });
  });

  group('Jordan is 18°/18°', () {
    test('Isha follows the twilight angle, not a fixed interval', () {
      // The distinction *is* the bug: Umm al-Qura's Isha is clock arithmetic
      // from Maghrib and takes no account of where the reader is standing.
      final jordan = PrayerCalculator.parametersFor(CalculationMethod.jordan);

      expect(jordan.fajrAngle, 18.0);
      expect(jordan.ishaAngle, 18.0);
      expect(jordan.ishaInterval, 0);
    });

    test('Umm al-Qura would put Isha eight minutes late in Amman', () {
      final wrong = amman(CalculationMethod.ummAlQura);
      final right = amman(CalculationMethod.jordan);

      expect(minutesAfterMaghrib(wrong), 90, reason: 'the Makkah fixed interval');
      expect(
        wrong.isha.difference(right.isha).inMinutes,
        greaterThanOrEqualTo(5),
        reason: 'a convention is not a rounding difference',
      );
    });

    test('the Muslim World League would put it early instead', () {
      // Recorded because it was the first thing reached for, and it is also
      // wrong — closer, but wrong in the other direction.
      final mwl = amman(CalculationMethod.muslimWorldLeague);
      final right = amman(CalculationMethod.jordan);

      expect(right.isha.isAfter(mwl.isha), isTrue);
    });

    test('the shared prayers are untouched by the choice', () {
      // Maghrib is sunset and Asr is a shadow ratio; neither depends on the
      // twilight angles the conventions disagree about. If these moved, the
      // change would be doing something it should not.
      final a = amman(CalculationMethod.jordan);
      final b = amman(CalculationMethod.muslimWorldLeague);

      expect(a.maghrib, b.maghrib);
      expect(a.asr, b.asr);
      expect(a.sunrise, b.sunrise);
    });
  });

  group('the enum is a cross-stack contract', () {
    test('Jordan was appended, not slotted in', () {
      // Reordering silently re-labels every stored row on three stacks.
      expect(CalculationMethod.jordan.value, 13);
      expect(CalculationMethod.moonsightingCommittee.value, 12);
      expect(CalculationMethod.ummAlQura.value, 1);
    });

    test('every convention resolves to usable parameters', () {
      for (final method in CalculationMethod.values) {
        final params = PrayerCalculator.parametersFor(method);

        expect(params.fajrAngle, greaterThan(0), reason: method.name);
        expect(
          params.ishaAngle != null && params.ishaAngle! > 0 || params.ishaInterval > 0,
          isTrue,
          reason: '${method.name} defines Isha somehow',
        );
      }
    });
  });
}
