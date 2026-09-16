import 'package:athkar_app/core/numerals.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Numerals.format', () {
    test('renders Arabic-Indic digits when asked', () {
      expect(Numerals.format(2026, arabicIndic: true), '٢٠٢٦');
      expect(Numerals.format(0, arabicIndic: true), '٠');
    });

    test('leaves the number alone otherwise', () {
      expect(Numerals.format(2026, arabicIndic: false), '2026');
    });

    test('only touches digits', () {
      expect(Numerals.format('12:30', arabicIndic: true), '١٢:٣٠');
    });
  });

  group('Numerals.time', () {
    test('pads the minutes but not the hour, as the design sets it', () {
      expect(Numerals.time(4, 52, arabicIndic: true), '٤:٥٢');
      expect(Numerals.time(12, 7, arabicIndic: true), '١٢:٠٧');
      expect(Numerals.time(4, 52, arabicIndic: false), '4:52');
    });
  });

  group('Numerals.countdown', () {
    test('reads as h:mm:ss', () {
      expect(
        Numerals.countdown(const Duration(hours: 1, minutes: 10, seconds: 24), arabicIndic: false),
        '1:10:24',
      );
    });

    test('a passed deadline shows zero rather than a negative', () {
      // The strip ticks once a second and can cross the prayer time between
      // frames; "-0:00:01" would be a visible glitch.
      expect(
        Numerals.countdown(const Duration(seconds: -5), arabicIndic: false),
        '0:00:00',
      );
    });
  });

  group('clock', () {
    // Stored times arrive in two shapes, and the second one is why this exists:
    // a server-side fixed-time campaign stores «HH:mm:ss». Taking the minute
    // from the *last* segment reads those seconds as minutes, turning «13:36:00»
    // into «12:00» — a plausible enough time that nobody would question it, and
    // a reminder that then appears to fire at the wrong moment.
    test('reads HH:mm', () {
      expect(Numerals.clock('22:30', arabicIndic: false), '10:30');
      expect(Numerals.clock('06:05', arabicIndic: false), '6:05');
    });

    test('reads HH:mm:ss without mistaking the seconds for minutes', () {
      expect(Numerals.clock('13:36:00', arabicIndic: false), '1:36');
      expect(Numerals.clock('06:49:30', arabicIndic: false), '6:49');
    });

    test('renders midnight and noon as 12 rather than 0', () {
      expect(Numerals.clock('00:15', arabicIndic: false), '12:15');
      expect(Numerals.clock('12:15', arabicIndic: false), '12:15');
    });

    test('follows the chosen digits', () {
      expect(Numerals.clock('13:36:00', arabicIndic: true), '١:٣٦');
    });

    test('returns anything unreadable unchanged', () {
      // Untidy beats confidently wrong: a reminder showing a raw value is odd,
      // one showing the wrong time is a missed dhikr.
      for (final bad in ['', 'later', '25:00', '10:75', '10']) {
        expect(Numerals.clock(bad, arabicIndic: false), bad, reason: bad);
      }
    });
  });
}
