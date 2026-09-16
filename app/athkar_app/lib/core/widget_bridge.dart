import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../models/models.dart';
import 'hijri_date.dart';
import 'numerals.dart';
import 'prayer_times.dart';
import 'occasions.dart';
import 'settings.dart';
import 'widget_keys.dart';

/// Pushes the home-screen widget's content to the platform.
///
/// The app decides *everything* the widget shows and hands it over as finished
/// strings: the prayer name in the reader's language, the clock in their chosen
/// numerals, the Hijri date with their correction applied. The native side only
/// lays those out.
///
/// That division is the whole design. A widget gets a fraction of a second of
/// CPU and no network, so it cannot compute prayer times — and if it tried,
/// there would be two implementations of the calculation to keep in step, which
/// is how a widget ends up disagreeing with the app it belongs to.
///
/// Silent on iOS and the web: a WidgetKit extension is not built yet, and the
/// channel simply is not there. Every method degrades to doing nothing rather
/// than throwing into a launch path.
class WidgetBridge {
  WidgetBridge._();

  static final instance = WidgetBridge._();

  static const _channel = MethodChannel('athkari/widget');

  bool get _isSupported => !kIsWeb && Platform.isAndroid;

  /// Whether the reader has actually placed one on a home screen.
  ///
  /// Worth asking rather than assuming: it lets the editor say "it is on your
  /// home screen" or "add it from your launcher" instead of guessing, and the
  /// two need different words.
  Future<bool> isPlaced() async {
    if (!_isSupported) return false;

    try {
      return await _channel.invokeMethod<bool>('isPlaced') ?? false;
    } on PlatformException {
      return false;
    } on MissingPluginException {
      return false;
    }
  }

  /// Recomputes the widget's content and hands it over.
  ///
  /// Called after a sync, after the reader changes the widget or their
  /// location, and whenever the app resumes — those are the moments its values
  /// can have gone stale. Android's own 30-minute refresh is the backstop for a
  /// phone nobody has touched.
  Future<void> push({
    required Settings settings,
    required WidgetSettings rules,
    required WidgetCatalogItem? selection,
    required AthkarCategory? category,
    required Dhikr? verse,
    required String placeholder,
    required String Function(PrayerAnchor anchor) prayerName,
    required String Function(String key) copy,
  }) async {
    if (!_isSupported) return;

    final payload = _build(
      settings: settings,
      rules: rules,
      selection: selection,
      category: category,
      verse: verse,
      placeholder: placeholder,
      prayerName: prayerName,
      copy: copy,
    );

    try {
      await _channel.invokeMethod<bool>('update', payload);
    } on PlatformException catch (error) {
      assert(() {
        debugPrint('[widget] could not update: ${error.message}');
        return true;
      }());
    } on MissingPluginException {
      // A build without the native half. Nothing to do, and nothing wrong.
    }
  }

  /// Composes what the launcher will draw.
  ///
  /// Keyed on the **entry the reader chose in the gallery**, not on
  /// [WidgetKind]. That is the whole of this method's reason to exist: before
  /// it, a reader could browse thirty widgets, pick one, and find the same two
  /// old layouts on their home screen — the gallery was a picture book. The
  /// enum survives only as the fallback for a server too old to have a
  /// catalogue at all.
  ///
  /// Everything leaves here as **finished strings**: the prayer name in the
  /// reader's language, the clock in their numerals, the Hijri date with their
  /// correction applied. The native side lays them out and computes nothing —
  /// a widget gets a fraction of a second of CPU and no network, and a second
  /// implementation of the astronomy is how a widget ends up disagreeing with
  /// the app it belongs to.
  Map<String, Object?> _build({
    required Settings settings,
    required WidgetSettings rules,
    required WidgetCatalogItem? selection,
    required AthkarCategory? category,
    required Dhikr? verse,
    required String placeholder,
    required String Function(PrayerAnchor anchor) prayerName,
    required String Function(String key) copy,
  }) {
    final digits = settings.arabicNumerals;
    final now = DateTime.now();

    final hijri = HijriDate.from(
      now,
      offsetDays: settings.hijriOffset,
      languageCode: settings.languageCode,
    );

    final date = rules.showHijriDate ? hijri.format(arabicNumerals: digits) : '';

    final timetable = PrayerCalculator.today(settings);
    final next = PrayerCalculator.nextPrayer(settings);

    final key = selection?.key ?? _legacyKey(rules, settings);

    var headline = '';
    var title = '';
    var subtitle = '';

    // The five obligatory prayers. Sunrise is deliberately absent: it is a
    // boundary, not a prayer, and a widget that lists it among the five teaches
    // the wrong thing in the one place nobody reads carefully.
    final prayers = timetable == null
        ? const <(PrayerAnchor, DateTime)>[]
        : [
            (PrayerAnchor.fajr, timetable.fajr),
            (PrayerAnchor.dhuhr, timetable.dhuhr),
            (PrayerAnchor.asr, timetable.asr),
            (PrayerAnchor.maghrib, timetable.maghrib),
            (PrayerAnchor.isha, timetable.isha),
          ];

    String clock(DateTime at) => Numerals.time(
          at.hour % 12 == 0 ? 12 : at.hour % 12,
          at.minute,
          arabicIndic: digits,
        );

    String number(Object value) => Numerals.format(value, arabicIndic: digits);

    switch (key) {
      case 'today_prayers':
      case 'prayer_calendar':
        headline = hijri.weekdayName;
        title = next == null
            ? ''
            : '${prayerName(next.$1)} ${clock(next.$2)}';
        subtitle = next == null || !rules.showCountdown
            ? (settings.cityName ?? '')
            : '${copy('widgets.remainingTo').replaceAll('{prayer}', prayerName(next.$1))}'
                '  ${Numerals.countdown(next.$2.difference(now), arabicIndic: digits)}';

      case 'prayer_times':
      case 'comprehensive':
        headline = settings.cityName ?? '';
        title = next == null ? '' : '${prayerName(next.$1)} ${clock(next.$2)}';
        subtitle = next == null || !rules.showCountdown
            ? ''
            : Numerals.countdown(next.$2.difference(now), arabicIndic: digits);

      case 'prayer_track':
        final passed = prayers.where((entry) => entry.$2.isBefore(now)).length;
        headline = copy('widgets.elapsed');
        title = '${number(passed)}/${number(prayers.length)}';
        subtitle = next == null ? '' : '${prayerName(next.$1)} ${clock(next.$2)}';

      case 'date_only':
        headline = hijri.weekdayName;
        title = hijri.format(arabicNumerals: digits);
        subtitle = '${number(now.year)}-'
            '${number(now.month.toString().padLeft(2, '0'))}-'
            '${number(now.day.toString().padLeft(2, '0'))}';

      case 'night_thirds':
        headline = copy('widgets.midnight');
        title = timetable == null ? '' : clock(timetable.islamicMidnight);
        subtitle = timetable == null
            ? ''
            : '${copy('widgets.lastThird')} ${clock(timetable.lastThirdOfNight)}';

      case 'occasion_countdown':
        final days = Occasions.toRamadan(
          now,
          offsetDays: settings.hijriOffset,
          languageCode: settings.languageCode,
        );
        headline = copy('widgets.ramadan');
        title = days == null ? '' : '${number(days)} ${copy('widgets.day')}';

      case 'quran_verse':
        headline = '';
        title = verse?.arabicText ?? '';
        subtitle = verse == null ? '' : _source(verse);

      case 'custom_pinned':
        // The one text this app shows with no takhrij. It is the reader's own,
        // typed on this phone, and the attribution line keeps it labelled as
        // theirs rather than sitting where a source would.
        headline = '';
        title = settings.widgetCustomText;
        subtitle = settings.widgetCustomAttribution;

      default:
        final dhikr = category?.adhkar.isNotEmpty == true ? category!.adhkar.first : null;
        headline = category?.name ?? '';
        title = dhikr?.arabicText ?? '';
        subtitle = dhikr == null ? '' : '${number(dhikr.repeatCount)}×';
    }

    // The native layout carries one five-column row, shown only for the keys
    // that are actually about the timetable. Empty lists hide it, which is also
    // what a reader with no location gets — and that is correct: the app
    // promises to work without ever asking for one.
    final showRow = WidgetKeys.withPrayerRow.contains(key) && prayers.isNotEmpty;

    return {
      'enabled': rules.isEnabled,
      'key': key,
      'kind': rules.resolveKind(settings.widgetKind).name,
      'theme': rules.theme.name,
      'date': date,
      'headline': headline,
      'title': title,
      'subtitle': subtitle,
      'prayerLabels':
          showRow ? [for (final entry in prayers) prayerName(entry.$1)] : const <String>[],
      'prayerTimes': showRow ? [for (final entry in prayers) clock(entry.$2)] : const <String>[],
      // Which column to pick out, or -1. The launcher cannot work this out for
      // itself: it has no clock it can trust against the reader's adjustments.
      'prayerHighlight': showRow && next != null
          ? prayers.indexWhere((entry) => entry.$1 == next.$1)
          : -1,
      // Shown when there is nothing to say — widgets switched off by the admin,
      // or no location chosen yet so there is no next prayer.
      'placeholder': placeholder,
    };
  }

  /// What to draw when the server is too old to have a catalogue.
  ///
  /// A deployment on the previous release sends no gallery at all, and a reader
  /// there must still get the widget they had rather than a placeholder. The
  /// old two-kind rule is preserved here and nowhere else.
  static String _legacyKey(WidgetSettings rules, Settings settings) =>
      rules.resolveKind(settings.widgetKind) == WidgetKind.dhikr
          ? 'assorted_adhkar'
          : 'prayer_times';

  static String _source(Dhikr dhikr) => [
        if (dhikr.sourceBook != null) dhikr.sourceBook!,
        if (dhikr.sourceReference != null) dhikr.sourceReference!,
      ].join(' · ');
}
