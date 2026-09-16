import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The one place the app's calendar and the server's bit set disagree.
///
/// The server numbers its days from Sunday at bit 0; Dart's `DateTime.weekday`
/// runs Monday = 1 … Sunday = 7. Getting this wrong shifts every weekly
/// reminder by a day, which is the sort of bug that takes a week to notice.
void main() {
  group('WeekDays.includes', () {
    test('every day is in the full mask', () {
      for (var weekday = DateTime.monday; weekday <= DateTime.sunday; weekday++) {
        expect(WeekDays.includes(WeekDays.all, weekday), isTrue, reason: 'weekday $weekday');
      }
    });

    test('no day is in the empty mask', () {
      for (var weekday = DateTime.monday; weekday <= DateTime.sunday; weekday++) {
        expect(WeekDays.includes(WeekDays.none, weekday), isFalse, reason: 'weekday $weekday');
      }
    });

    test('Sunday is bit 0, matching the server', () {
      expect(WeekDays.includes(1, DateTime.sunday), isTrue);
      expect(WeekDays.includes(1, DateTime.monday), isFalse);
    });

    test('Monday is bit 1', () {
      expect(WeekDays.includes(2, DateTime.monday), isTrue);
      expect(WeekDays.includes(2, DateTime.sunday), isFalse);
    });

    test('Friday is bit 5', () {
      expect(WeekDays.includes(1 << 5, DateTime.friday), isTrue);
      expect(WeekDays.includes(1 << 5, DateTime.thursday), isFalse);
    });

    test('Monday and Thursday together', () {
      const mask = 2 | 16;

      expect(WeekDays.includes(mask, DateTime.monday), isTrue);
      expect(WeekDays.includes(mask, DateTime.thursday), isTrue);
      expect(WeekDays.includes(mask, DateTime.tuesday), isFalse);
      expect(WeekDays.includes(mask, DateTime.sunday), isFalse);
    });
  });
}
