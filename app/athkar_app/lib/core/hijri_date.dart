import 'package:hijri/hijri_calendar.dart';

import 'numerals.dart';

/// The Hijri date, with the ±1 day the reader is allowed to set.
///
/// The correction is not a workaround: the start of a Hijri month depends on
/// sighting, sighting differs between countries, and an app that insists on one
/// arithmetic answer is simply wrong for a large share of its readers. The
/// offset lets them agree with their own mosque.
class HijriDate {
  const HijriDate({
    required this.day,
    required this.month,
    required this.year,
    required this.monthName,
    required this.weekdayName,
  });

  final int day;
  final int month;
  final int year;
  final String monthName;
  final String weekdayName;

  static const _monthsAr = [
    'محرّم', 'صفر', 'ربيع الأول', 'ربيع الآخر', 'جمادى الأولى', 'جمادى الآخرة',
    'رجب', 'شعبان', 'رمضان', 'شوّال', 'ذو القعدة', 'ذو الحجة',
  ];

  static const _monthsEn = [
    'Muharram', 'Safar', 'Rabi al-Awwal', 'Rabi al-Thani', 'Jumada al-Ula',
    'Jumada al-Akhira', 'Rajab', 'Shaban', 'Ramadan', 'Shawwal',
    'Dhu al-Qadah', 'Dhu al-Hijjah',
  ];

  static const _weekdaysAr = [
    'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد',
  ];

  static const _weekdaysEn = [
    'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday',
  ];

  /// The Hijri date for [date], shifted by [offsetDays].
  static HijriDate from(DateTime date, {int offsetDays = 0, String languageCode = 'ar'}) {
    final shifted = date.add(Duration(days: offsetDays));
    final hijri = HijriCalendar.fromDate(shifted);

    final months = languageCode == 'ar' ? _monthsAr : _monthsEn;
    final weekdays = languageCode == 'ar' ? _weekdaysAr : _weekdaysEn;

    return HijriDate(
      day: hijri.hDay,
      month: hijri.hMonth,
      year: hijri.hYear,
      monthName: months[(hijri.hMonth - 1).clamp(0, 11)],
      // DateTime.weekday is 1..7 from Monday, which is the order the lists above
      // are written in.
      weekdayName: weekdays[(date.weekday - 1).clamp(0, 6)],
    );
  }

  /// Whether this date falls in Ramadan, for the seasonal content to key off.
  bool get isRamadan => month == 9;

  /// «الأربعاء · ٢١ ربيع الأول ١٤٤٨», exactly as the design prints it.
  String format({required bool arabicNumerals}) {
    String digits(int value) => Numerals.format(value, arabicIndic: arabicNumerals);
    return '$weekdayName · ${digits(day)} $monthName ${digits(year)}';
  }
}
