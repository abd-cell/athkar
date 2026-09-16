import 'package:flutter/material.dart';

import '../../core/hijri_date.dart';
import '../../core/numerals.dart';
import '../../core/prayer_times.dart';
import '../../core/settings.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../widgets/prayer_strip.dart';

/// Everything a widget preview draws, gathered once.
///
/// The galleries render thirty previews on one scroll. Each of them wants the
/// same half-dozen facts — today's timetable, the next prayer, the Hijri date,
/// the reader's numerals — and computing those inside every renderer would mean
/// running the astronomy thirty times per frame on a list that is already
/// rebuilt by a one-second ticker.
///
/// It is also what keeps the previews *honest*. A gallery of mock widgets
/// showing 5:00 for Fajr in a city the reader has never been to is a catalogue
/// of pictures; these show the reader's own times, so what they are choosing
/// between is what they will get.
@immutable
class WidgetPreviewData {
  const WidgetPreviewData({
    required this.now,
    required this.settings,
    required this.timetable,
    required this.next,
    required this.previous,
    required this.hijri,
    required this.category,
    required this.dhikr,
    required this.verse,
    required this.cityName,
    required this.arabicNumerals,
    required this.languageCode,
  });

  final DateTime now;
  final Settings settings;

  /// Null when the reader has set no location. Every renderer must cope: the
  /// app promises to work without ever asking for one.
  final PrayerTimetable? timetable;

  final (PrayerAnchor, DateTime)? next;
  final (PrayerAnchor, DateTime)? previous;

  final HijriDate hijri;

  final AthkarCategory? category;
  final Dhikr? dhikr;

  /// An ayah out of the catalogue, for the Qur'an widgets.
  ///
  /// Drawn from the same content as everything else rather than hard-coded,
  /// which is the rule this project does not bend: what a widget shows must be
  /// traceable to a source an editor can correct, and a verse compiled into the
  /// app is beyond anybody's reach once it has shipped.
  final Dhikr? verse;

  final String? cityName;
  final bool arabicNumerals;
  final String languageCode;

  bool get hasLocation => timetable != null;

  /// Reads everything a preview needs out of the scopes, once per build.
  static WidgetPreviewData of(BuildContext context) {
    final settings = SettingsScope.of(context);
    final state = AppStateScope.of(context);
    final now = DateTime.now();

    final timetable = PrayerCalculator.today(settings);
    final category = state.widgetCategory;

    return WidgetPreviewData(
      now: now,
      settings: settings,
      timetable: timetable,
      next: PrayerCalculator.nextPrayer(settings),
      previous: _previous(timetable, now),
      hijri: HijriDate.from(
        now,
        offsetDays: settings.hijriOffset,
        languageCode: settings.languageCode,
      ),
      category: category,
      dhikr: category?.adhkar.isNotEmpty == true ? category!.adhkar.first : null,
      verse: _firstVerse(state.content.categories),
      cityName: settings.cityName,
      arabicNumerals: settings.arabicNumerals,
      languageCode: settings.languageCode,
    );
  }

  /// The five obligatory prayers, in order. Sunrise is deliberately absent: it
  /// is a boundary, not a prayer, and a widget that lists it among the five
  /// teaches the wrong thing in the one place nobody reads carefully.
  List<(PrayerAnchor, DateTime)> get fivePrayers {
    final table = timetable;
    if (table == null) return const [];

    return [
      (PrayerAnchor.fajr, table.fajr),
      (PrayerAnchor.dhuhr, table.dhuhr),
      (PrayerAnchor.asr, table.asr),
      (PrayerAnchor.maghrib, table.maghrib),
      (PrayerAnchor.isha, table.isha),
    ];
  }

  /// A clock reading, in the reader's numerals and without a meridiem.
  ///
  /// Twelve-hour because that is what the design prints and what the
  /// screenshots of every prayer app in the region show; the meridiem is
  /// omitted because a prayer's half of the day is never in doubt.
  String clock(DateTime at) => Numerals.time(
        at.hour % 12 == 0 ? 12 : at.hour % 12,
        at.minute,
        arabicIndic: arabicNumerals,
      );

  String number(Object value) => Numerals.format(value, arabicIndic: arabicNumerals);

  String countdownTo(DateTime at) =>
      Numerals.countdown(at.difference(now), arabicIndic: arabicNumerals);

  String get hijriLine => hijri.format(arabicNumerals: arabicNumerals);

  /// «1448-04-02», the compact form the small widgets use where the month name
  /// would not fit.
  String get hijriNumeric => '${number(hijri.year)}-'
      '${number(hijri.month.toString().padLeft(2, '0'))}-'
      '${number(hijri.day.toString().padLeft(2, '0'))}';

  String get gregorianNumeric => '${number(now.year)}-'
      '${number(now.month.toString().padLeft(2, '0'))}-'
      '${number(now.day.toString().padLeft(2, '0'))}';

  String name(BuildContext context, PrayerAnchor anchor) => prayerName(context, anchor);

  /// The weekday, in the reader's language. Taken from the Hijri helper so the
  /// app has one list of weekday names rather than two that can disagree.
  String get weekday => hijri.weekdayName;

  /// The first ayah in the catalogue, or null when none has been published.
  static Dhikr? _firstVerse(List<AthkarCategory> categories) {
    for (final category in categories) {
      for (final dhikr in category.adhkar) {
        if (dhikr.grade == HadithGrade.quranVerse) return dhikr;
      }
    }
    return null;
  }

  static (PrayerAnchor, DateTime)? _previous(PrayerTimetable? table, DateTime now) {
    if (table == null) return null;

    (PrayerAnchor, DateTime)? latest;
    for (final entry in table.ordered) {
      if (entry.$2.isAfter(now)) break;
      latest = entry;
    }

    // Before Fajr the previous prayer is yesterday's Isha, which is what a
    // reader glancing at a lock screen at two in the morning expects to see.
    return latest ?? (PrayerAnchor.isha, table.isha.subtract(const Duration(days: 1)));
  }
}
