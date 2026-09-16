import 'package:athkar_app/core/hijri_date.dart';
import 'package:athkar_app/core/occasions.dart';
import 'package:flutter_test/flutter_test.dart';

/// The countdowns the occasion widgets print.
///
/// The rule this file exists to protect: **the countdown must agree with the
/// date printed two centimetres above it on the same widget.** The reader's
/// Hijri correction shifts their calendar against the arithmetic one, so a
/// countdown computed by converting a Hijri date back to a Gregorian one would
/// quietly ignore the correction and disagree with the app's own date — on a
/// home screen, where nobody can check it against anything.
void main() {
  final day = DateTime(2026, 9, 13);

  group('the walk lands on the right day', () {
    test('Ramadan is the first day of the ninth month', () {
      final ahead = Occasions.toRamadan(day);

      expect(ahead, isNotNull);

      final arrival = HijriDate.from(day.add(Duration(days: ahead!)));
      expect(arrival.month, 9);
      expect(arrival.day, 1);
    });

    test('Eid al-Fitr is the first of Shawwal', () {
      final ahead = Occasions.toEidAlFitr(day);
      final arrival = HijriDate.from(day.add(Duration(days: ahead!)));

      expect(arrival.month, 10);
      expect(arrival.day, 1);
    });

    test('Eid al-Adha is the tenth of Dhu al-Hijjah', () {
      final ahead = Occasions.toEidAlAdha(day);
      final arrival = HijriDate.from(day.add(Duration(days: ahead!)));

      expect(arrival.month, 12);
      expect(arrival.day, 10);
    });
  });

  test('the countdown follows the reader’s own calendar correction', () {
    // A reader who has set their calendar a day forward is a day closer to
    // Ramadan. If this ever stops being true, their widget is printing a date
    // and a countdown that contradict each other.
    final plain = Occasions.toRamadan(day);
    final shifted = Occasions.toRamadan(day, offsetDays: 1);

    expect(shifted, isNotNull);
    expect(shifted, plain! - 1);
  });

  test('an occasion today reads as zero rather than as a year away', () {
    // Walked from today inclusive: the morning of Eid must not tell the reader
    // there are 354 days left.
    final ahead = Occasions.toRamadan(day)!;
    final onTheDay = day.add(Duration(days: ahead));

    expect(Occasions.toRamadan(onTheDay), 0);
  });

  test('the memo does not answer one day with another day’s numbers', () {
    // The walk is memoised on the day it started from, because the gallery
    // rebuilds on a one-second ticker. A memo keyed too loosely would leave
    // yesterday's count on screen until the app was restarted.
    final today = Occasions.toRamadan(day)!;
    final tomorrow = Occasions.toRamadan(day.add(const Duration(days: 1)))!;

    expect(tomorrow, today - 1);
    expect(Occasions.toRamadan(day), today);
  });
}
