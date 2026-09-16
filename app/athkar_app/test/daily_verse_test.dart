import 'package:athkar_app/core/daily_verse.dart';
import 'package:flutter_test/flutter_test.dart';

/// The verse-of-the-day list is the one piece of content the app carries in a
/// constant, and a typo in it is invisible: the lookup simply walks past a
/// reference the package cannot resolve, so a wrong surah number shows the
/// *next* verse rather than an error.
void main() {
  group('the daily verse references', () {
    test('name a real surah', () {
      for (final (surah, _) in DailyVerse.references) {
        expect(surah, inInclusiveRange(1, 114));
      }
    });

    test('name a verse, counting from one', () {
      for (final (_, ayah) in DailyVerse.references) {
        expect(ayah, greaterThanOrEqualTo(1));
      }
    });

    test('are distinct, so the year does not repeat one early', () {
      expect(DailyVerse.references.toSet(), hasLength(DailyVerse.references.length));
    });
  });
}
