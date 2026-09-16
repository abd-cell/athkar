import 'package:athkar_app/core/true_heading.dart';
import 'package:flutter_test/flutter_test.dart';

/// The arithmetic around the declination.
///
/// Not the geophysics — that comes from the platform's own World Magnetic
/// Model, and testing it here would only be testing a number we copied. What is
/// worth testing is the part written in this codebase: applying a correction,
/// wrapping the circle, and getting the direction of a turn the right way
/// round.
void main() {
  group('TrueHeading.apply', () {
    test('adds an easterly declination', () {
      expect(TrueHeading.apply(10, 3.1), closeTo(13.1, 0.001));
    });

    test('subtracts a westerly one', () {
      expect(TrueHeading.apply(100, -12.9), closeTo(87.1, 0.001));
    });

    test('wraps past north in both directions', () {
      expect(TrueHeading.apply(359, 3), closeTo(2, 0.001));
      expect(TrueHeading.apply(2, -5), closeTo(357, 0.001));
    });

    test('a missing declination leaves the reading alone', () {
      // The honest fallback: an uncorrected compass, not a fabricated
      // correction. The screen says so when this happens.
      expect(TrueHeading.apply(123.4, null), closeTo(123.4, 0.001));
    });

    test('always lands inside a full turn', () {
      for (var heading = 0.0; heading < 360; heading += 17) {
        for (final declination in [-20.0, -1.0, 0.0, 1.0, 20.0]) {
          final result = TrueHeading.apply(heading, declination);

          expect(result, greaterThanOrEqualTo(0));
          expect(result, lessThan(360));
        }
      }
    });
  });

  group('TrueHeading.difference', () {
    test('is positive when the target is to the right', () {
      expect(TrueHeading.difference(100, 90), closeTo(10, 0.001));
    });

    test('is negative when the target is to the left', () {
      expect(TrueHeading.difference(80, 90), closeTo(-10, 0.001));
    });

    test('takes the short way round north', () {
      // Facing 350°, a target at 10° is twenty degrees to the right — not 340
      // degrees to the left, which is what the naive subtraction gives.
      expect(TrueHeading.difference(10, 350), closeTo(20, 0.001));
      expect(TrueHeading.difference(350, 10), closeTo(-20, 0.001));
    });

    test('never asks for more than half a turn', () {
      for (var target = 0.0; target < 360; target += 13) {
        for (var heading = 0.0; heading < 360; heading += 29) {
          final turn = TrueHeading.difference(target, heading);

          expect(turn, greaterThan(-180.0001));
          expect(turn, lessThanOrEqualTo(180.0001));
        }
      }
    });

    test('is zero when already facing the target', () {
      expect(TrueHeading.difference(147, 147), closeTo(0, 0.001));
    });
  });
}
