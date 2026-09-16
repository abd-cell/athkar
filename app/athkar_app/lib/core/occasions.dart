import 'hijri_date.dart';

/// How many days remain to the occasions a countdown widget names.
///
/// Computed by walking forward from today and converting each day, rather than
/// by converting a Hijri date back to a Gregorian one. That sounds like the
/// long way round, and it is deliberate: the reader's [HijriDate.offsetDays]
/// correction shifts their calendar against the arithmetic one, and a reverse
/// conversion would quietly ignore it — so a reader who has set their calendar
/// a day back would see a countdown that disagrees with the date printed two
/// centimetres above it, on the same widget.
///
/// Walking the reader's own calendar cannot disagree with itself.
///
/// The walk is memoised on the day it started from, because the widget gallery
/// rebuilds on a one-second ticker and four hundred date conversions a second
/// is a real cost for an answer that changes at midnight.
class Occasions {
  const Occasions._();

  /// A year and a half of look-ahead. Long enough that every occasion below is
  /// always ahead of us, short enough to stay cheap.
  static const _horizon = 400;

  static _Memo? _memo;

  /// Days from [from] to the next 1 Ramadan, or null if the walk never reaches
  /// it — which cannot happen with the horizon above, but a widget must not
  /// throw on a home screen.
  static int? toRamadan(DateTime from, {int offsetDays = 0, String languageCode = 'ar'}) =>
      _resolve(from, offsetDays, languageCode).ramadan;

  /// Days to the next 1 Shawwal — Eid al-Fitr.
  static int? toEidAlFitr(DateTime from, {int offsetDays = 0, String languageCode = 'ar'}) =>
      _resolve(from, offsetDays, languageCode).eidAlFitr;

  /// Days to the next 10 Dhu al-Hijjah — Eid al-Adha.
  static int? toEidAlAdha(DateTime from, {int offsetDays = 0, String languageCode = 'ar'}) =>
      _resolve(from, offsetDays, languageCode).eidAlAdha;

  static _Memo _resolve(DateTime from, int offsetDays, String languageCode) {
    final day = DateTime(from.year, from.month, from.day);

    final cached = _memo;
    if (cached != null && cached.day == day && cached.offsetDays == offsetDays) {
      return cached;
    }

    int? ramadan;
    int? eidAlFitr;
    int? eidAlAdha;

    for (var ahead = 0; ahead <= _horizon; ahead++) {
      final hijri = HijriDate.from(
        day.add(Duration(days: ahead)),
        offsetDays: offsetDays,
        languageCode: languageCode,
      );

      if (ramadan == null && hijri.month == 9 && hijri.day == 1) ramadan = ahead;
      if (eidAlFitr == null && hijri.month == 10 && hijri.day == 1) eidAlFitr = ahead;
      if (eidAlAdha == null && hijri.month == 12 && hijri.day == 10) eidAlAdha = ahead;

      if (ramadan != null && eidAlFitr != null && eidAlAdha != null) break;
    }

    return _memo = _Memo(
      day: day,
      offsetDays: offsetDays,
      ramadan: ramadan,
      eidAlFitr: eidAlFitr,
      eidAlAdha: eidAlAdha,
    );
  }
}

class _Memo {
  const _Memo({
    required this.day,
    required this.offsetDays,
    required this.ramadan,
    required this.eidAlFitr,
    required this.eidAlAdha,
  });

  final DateTime day;
  final int offsetDays;
  final int? ramadan;
  final int? eidAlFitr;
  final int? eidAlAdha;
}
