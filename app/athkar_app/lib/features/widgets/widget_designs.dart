import 'package:flutter/material.dart';

import '../../core/hijri_date.dart';
import '../../core/l10n.dart';
import '../../core/occasions.dart';
import '../../core/widget_keys.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../widgets/athkar_ui.dart';
import 'widget_preview_data.dart';
import 'widget_skin.dart';

/// The renderer registry — every widget this build knows how to draw.
///
/// This file is the app's half of the contract described in
/// `models/widget_catalog.dart`: **the server names the widgets, the app draws
/// them.** A catalogue key with no entry here is skipped by the gallery rather
/// than shown as an empty box, which is what lets the console add a widget for
/// next year's release without breaking last year's install.
///
/// Two numbers therefore exist for every entry and must not be confused. The
/// server's `designCount` is a *cap* the admin can lower to withdraw a design;
/// [designsFor] is what this build can actually draw. The gallery takes the
/// smaller — see [WidgetCatalogItem.designsAvailable] — so a server ahead of
/// the app never advertises a design that would render blank.
class WidgetDesigns {
  const WidgetDesigns._();

  /// Delegates to [WidgetKeys].
  ///
  /// The key table lives in `core` because [WidgetBridge] needs it too, and
  /// that runs from a background sync with no `BuildContext` to reach a
  /// feature's widgets through. This class adds only the drawing.
  static int designsFor(String key) => WidgetKeys.designsFor(key);

  static bool knows(String key) => WidgetKeys.knows(key);

  static double heightFor(String key) => WidgetKeys.heightFor(key);

  /// Draws one design of one key, or null when this build does not know it.
  static Widget? build(
    BuildContext context,
    String key,
    int design,
    WidgetPreviewData data,
    WidgetSkin skin,
  ) {
    if (WidgetKeys.lock.contains(key)) return _lockDesign(context, key, data, skin);

    return switch (key) {
      'today_prayers' => _todayPrayers(context, design, data, skin),
      'prayer_times' => _prayerTimes(context, design, data, skin),
      'prayer_track' => _prayerTrack(context, design, data, skin),
      'date_only' => _dateOnly(context, design, data, skin),
      'prayer_calendar' => _prayerCalendar(context, design, data, skin),
      'assorted_adhkar' => _adhkar(context, design, data, skin),
      'quran_verse' => _quranVerse(context, design, data, skin),
      'occasion_countdown' => _occasions(context, design, data, skin),
      'night_thirds' => _nightThirds(context, design, data, skin),
      'comprehensive' => _comprehensive(context, design, data, skin),
      'custom_pinned' => _customPinned(context, design, data, skin),
      _ => null,
    };
  }

  // ───────────────────────── home: today and prayers ─────────────────────────

  static Widget _todayPrayers(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    if (!data.hasLocation) return _noLocation(context, skin);

    return switch (design) {
      1 => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _label(data.weekday, skin),
                if (data.cityName case final city?) _label(city, skin),
              ],
            ),
            const Spacer(),
            _timesRow(context, data, skin, ruled: true),
            const Spacer(),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _label(data.hijriNumeric, skin),
                _label(data.gregorianNumeric, skin),
              ],
            ),
          ],
        ),
      2 => Row(
          children: [
            Expanded(flex: 4, child: _nextBlock(context, data, skin)),
            VerticalDivider(color: skin.hairline, width: 22, thickness: 1),
            Expanded(flex: 6, child: _timesColumn(context, data, skin)),
          ],
        ),
      3 => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            Text(data.weekday,
                style: AthkarType.amiri(size: 22, color: skin.ink, weight: FontWeight.w700)),
            _label('${data.hijriLine}  ·  ${data.gregorianNumeric}', skin),
            _timesRow(context, data, skin),
          ],
        ),
      _ => Column(
          children: [
            _label(data.hijriNumeric, skin),
            const SizedBox(height: 2),
            // The day's name, set large in Amiri — the one line of this widget
            // a reader takes in without focusing.
            Text(data.weekday,
                style: AthkarType.amiri(size: 26, color: skin.ink, weight: FontWeight.w700)),
            const Spacer(),
            _timesRow(context, data, skin),
            const SizedBox(height: 6),
            Divider(color: skin.hairline, height: 1),
            const SizedBox(height: 6),
            _countdownLine(context, data, skin),
          ],
        ),
    };
  }

  static Widget _prayerTimes(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    if (!data.hasLocation) return _noLocation(context, skin);

    return switch (design) {
      1 => _timesColumn(context, data, skin),
      2 => Row(
          children: [
            Expanded(flex: 5, child: _nextBlock(context, data, skin)),
            const SizedBox(width: 12),
            Expanded(
              flex: 5,
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (final entry in data.fivePrayers.take(3))
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 2),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          _label(data.name(context, entry.$1), skin),
                          _value(data.clock(entry.$2), skin, size: 12),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      3 => Column(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            _timesRow(context, data, skin, only: 3),
            Divider(color: skin.hairline, height: 1),
            _timesRow(context, data, skin, skip: 3),
          ],
        ),
      _ => Column(
          children: [
            if (data.cityName case final city?) _label(city, skin),
            const Spacer(),
            _timesRow(context, data, skin, ruled: true),
            const Spacer(),
            _countdownLine(context, data, skin),
          ],
        ),
    };
  }

  /// What the reader has prayed today, rather than what is coming.
  ///
  /// Deliberately derived from the clock and nothing else: the app keeps no
  /// record of whether a prayer was performed, and a widget that claimed to
  /// know would be inventing it.
  static Widget _prayerTrack(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    if (!data.hasLocation) return _noLocation(context, skin);

    final prayers = data.fivePrayers;
    final passed = prayers.where((entry) => entry.$2.isBefore(data.now)).length;

    if (design == 1) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _label(context.tr('widgets.elapsed'), skin),
              _value('${data.number(passed)}/${data.number(prayers.length)}', skin, size: 13),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: LinearProgressIndicator(
              value: passed / prayers.length,
              minHeight: 7,
              backgroundColor: skin.hairline,
              valueColor: AlwaysStoppedAnimation(skin.accent),
            ),
          ),
          const SizedBox(height: 10),
          _countdownLine(context, data, skin),
        ],
      );
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            for (var index = 0; index < prayers.length; index++)
              Column(
                children: [
                  Container(
                    width: 12,
                    height: 12,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: index < passed ? skin.accent : Colors.transparent,
                      border: Border.all(
                        color: index < passed ? skin.accent : skin.hairline,
                        width: 1.5,
                      ),
                    ),
                  ),
                  const SizedBox(height: 6),
                  _label(data.name(context, prayers[index].$1), skin, size: 10),
                ],
              ),
          ],
        ),
        const SizedBox(height: 12),
        _label('${data.number(passed)}/${data.number(prayers.length)}', skin),
      ],
    );
  }

  // ──────────────────────────── home: the date ────────────────────────────

  static Widget _dateOnly(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    return switch (design) {
      1 => Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const AthkarOrnament(width: 48),
              const SizedBox(height: 8),
              Text(data.hijriLine,
                  textAlign: TextAlign.center,
                  style: AthkarType.amiri(size: 19, color: skin.ink, weight: FontWeight.w700)),
            ],
          ),
        ),
      2 => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(data.hijriLine,
                style: AthkarType.amiri(size: 17, color: skin.ink, weight: FontWeight.w700)),
            const SizedBox(height: 6),
            _label(data.gregorianNumeric, skin),
          ],
        ),
      3 => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(data.weekday,
                style: AthkarType.amiri(size: 24, color: skin.accent, weight: FontWeight.w700)),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                _label(data.hijriNumeric, skin),
                _label(data.gregorianNumeric, skin),
              ],
            ),
          ],
        ),
      // The day's number, set very large. A date widget is glanced at, never
      // read, so one number carries it and everything else is a caption.
      _ => Row(
          children: [
            Text(data.number(data.hijri.day),
                style: AthkarType.sans(size: 46, color: skin.ink, weight: FontWeight.w300)),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(data.hijri.monthName,
                      style: AthkarType.amiri(size: 16, color: skin.ink, weight: FontWeight.w700)),
                  Text(data.weekday,
                      style:
                          AthkarType.amiri(size: 15, color: skin.accent, weight: FontWeight.w700)),
                  const SizedBox(height: 2),
                  _label(data.gregorianNumeric, skin),
                ],
              ),
            ),
          ],
        ),
    };
  }

  static Widget _prayerCalendar(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final week = _weekStrip(context, data, skin);

    return switch (design) {
      1 => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            week,
            const SizedBox(height: 10),
            Divider(color: skin.hairline, height: 1),
            const SizedBox(height: 10),
            if (data.hasLocation)
              _timesRow(context, data, skin)
            else
              _noLocation(context, skin),
          ],
        ),
      2 => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _label(data.hijriLine, skin),
            const SizedBox(height: 10),
            week,
          ],
        ),
      _ => Row(
          children: [
            Expanded(flex: 6, child: week),
            const SizedBox(width: 12),
            Expanded(
              flex: 4,
              child: data.hasLocation
                  ? _nextBlock(context, data, skin)
                  : _noLocation(context, skin),
            ),
          ],
        ),
    };
  }

  // ────────────────────────── home: text widgets ──────────────────────────

  static Widget _adhkar(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final dhikr = data.dhikr;
    if (dhikr == null) return _empty(context, skin, 'categories.empty');

    return switch (design) {
      1 => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (data.category case final category?)
              Text(category.name,
                  style:
                      AthkarType.sans(size: 11, color: skin.accent, weight: FontWeight.w600)),
            const SizedBox(height: 6),
            Expanded(child: _narrated(dhikr.arabicText, skin, size: 17)),
            _label('${data.number(dhikr.repeatCount)}×', skin),
          ],
        ),
      2 => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const AthkarOrnament(width: 44),
            const SizedBox(height: 8),
            Flexible(child: _narrated(dhikr.arabicText, skin, size: 17, centre: true)),
          ],
        ),
      _ => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Flexible(child: _narrated(dhikr.arabicText, skin, size: 18, centre: true)),
            const SizedBox(height: 8),
            // The takhrij, on the widget as on every other surface. It is the
            // one thing this project will not let a dhikr appear without.
            _label(_source(dhikr), skin, size: 10),
          ],
        ),
    };
  }

  static Widget _quranVerse(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final verse = data.verse;
    if (verse == null) return _empty(context, skin, 'widgets.noVerse');

    if (design == 1) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const AthkarOrnament(width: 52),
          const SizedBox(height: 10),
          Flexible(child: _narrated(verse.arabicText, skin, size: 19, centre: true)),
        ],
      );
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Flexible(child: _narrated(verse.arabicText, skin, size: 20, centre: true)),
        const SizedBox(height: 8),
        _label(_source(verse), skin, size: 10),
      ],
    );
  }

  /// The reader's own pinned text.
  ///
  /// The attribution line underneath is not decoration. This is the one place
  /// in the app where words appear with no takhrij, and printing them as the
  /// reader's own note is what keeps that from looking like catalogue content.
  static Widget _customPinned(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final text = data.settings.widgetCustomText;
    if (text.isEmpty) return _empty(context, skin, 'widgets.custom.empty');

    final attribution = data.settings.widgetCustomAttribution;

    if (design == 1) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const AthkarOrnament(width: 44),
          const SizedBox(height: 10),
          Flexible(child: _narrated(text, skin, size: 18, centre: true)),
        ],
      );
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Flexible(child: _narrated(text, skin, size: 19, centre: true)),
        if (attribution.isNotEmpty) ...[
          const SizedBox(height: 8),
          _label(attribution, skin, size: 10),
        ],
      ],
    );
  }

  // ───────────────────── home: countdowns, night, all-in-one ─────────────────────

  static Widget _occasions(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final offset = data.settings.hijriOffset;

    final entries = <(String, int?)>[
      (
        context.tr('widgets.ramadan'),
        Occasions.toRamadan(data.now, offsetDays: offset, languageCode: data.languageCode)
      ),
      (
        context.tr('widgets.eidAlFitr'),
        Occasions.toEidAlFitr(data.now, offsetDays: offset, languageCode: data.languageCode)
      ),
      (
        context.tr('widgets.eidAlAdha'),
        Occasions.toEidAlAdha(data.now, offsetDays: offset, languageCode: data.languageCode)
      ),
    ];

    if (design == 1) {
      // The nearest one, alone and large.
      final nearest = ([...entries]..sort((a, b) => (a.$2 ?? 9999).compareTo(b.$2 ?? 9999))).first;

      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(data.number(nearest.$2 ?? 0),
              style: AthkarType.sans(size: 40, color: skin.accent, weight: FontWeight.w300)),
          _label('${context.tr('widgets.daysTo')} ${nearest.$1}', skin),
        ],
      );
    }

    if (design == 2) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final entry in entries)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  _label(entry.$1, skin),
                  _value(
                      '${data.number(entry.$2 ?? 0)} ${context.tr('widgets.day')}', skin,
                      size: 12),
                ],
              ),
            ),
        ],
      );
    }

    return Row(
      children: [
        for (final entry in entries)
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                _label(entry.$1, skin, size: 10),
                const SizedBox(height: 6),
                Text(data.number(entry.$2 ?? 0),
                    style: AthkarType.sans(size: 24, color: skin.ink, weight: FontWeight.w400)),
              ],
            ),
          ),
      ],
    );
  }

  static Widget _nightThirds(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final table = data.timetable;
    if (table == null) return _noLocation(context, skin);

    final rows = <(String, DateTime)>[
      (context.tr('widgets.midnight'), table.islamicMidnight),
      (context.tr('widgets.lastThird'), table.lastThirdOfNight),
    ];

    if (design == 1) {
      return Row(
        children: [
          for (final row in rows)
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  _label(row.$1, skin, size: 10),
                  const SizedBox(height: 4),
                  _value(data.clock(row.$2), skin, size: 18),
                ],
              ),
            ),
        ],
      );
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _label(context.tr('prayer.isha'), skin),
            _value(data.clock(table.isha), skin, size: 13),
          ],
        ),
        const SizedBox(height: 8),
        for (final row in rows)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _label(row.$1, skin),
                _value(data.clock(row.$2), skin, size: 13),
              ],
            ),
          ),
      ],
    );
  }

  static Widget _comprehensive(
      BuildContext context, int design, WidgetPreviewData data, WidgetSkin skin) {
    final table = data.timetable;
    if (table == null) return _noLocation(context, skin);

    if (design == 1) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(data.hijriLine,
              textAlign: TextAlign.center,
              style: AthkarType.amiri(size: 16, color: skin.ink, weight: FontWeight.w700)),
          const SizedBox(height: 8),
          _timesRow(context, data, skin, ruled: true),
          const Spacer(),
          Divider(color: skin.hairline, height: 1),
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _label('${context.tr('widgets.midnight')} ${data.clock(table.islamicMidnight)}', skin,
                  size: 10),
              _label(
                  '${context.tr('widgets.lastThird')} ${data.clock(table.lastThirdOfNight)}', skin,
                  size: 10),
            ],
          ),
        ],
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _countdownLine(context, data, skin),
            _label('${context.tr('prayer.isha')} ${data.clock(table.isha)}', skin),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _label('${context.tr('widgets.lastThird')} ${data.clock(table.lastThirdOfNight)}', skin,
                size: 10),
            _label('${context.tr('widgets.midnight')} ${data.clock(table.islamicMidnight)}', skin,
                size: 10),
          ],
        ),
        const Spacer(),
        Divider(color: skin.hairline, height: 1),
        const SizedBox(height: 8),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _label(data.hijriNumeric, skin, size: 10),
            _label(data.gregorianNumeric, skin, size: 10),
            if (data.cityName case final city?) _label(city, skin, size: 10),
          ],
        ),
      ],
    );
  }

  // ────────────────────────────── lock screen ──────────────────────────────

  static Widget _lockDesign(
      BuildContext context, String key, WidgetPreviewData data, WidgetSkin skin) {
    final next = data.next;
    final previous = data.previous;
    final table = data.timetable;

    Widget pair(String label, String value) => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            _label(label, skin, size: 10),
            const SizedBox(height: 2),
            _value(value, skin, size: 15),
          ],
        );

    Widget row(List<Widget> children) => Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: children,
        );

    // Everything with a time in it needs a location, and there is nothing
    // honest to draw without one.
    if (table == null && !_locationFree.contains(key)) return _noLocation(context, skin);

    return switch (key) {
      'lock_next_prayer' when next != null =>
        pair(data.name(context, next.$1), data.clock(next.$2)),
      'lock_previous_prayer' when previous != null =>
        pair(data.name(context, previous.$1), data.clock(previous.$2)),
      'lock_prev_next' when next != null && previous != null => row([
          pair(data.name(context, previous.$1), data.clock(previous.$2)),
          pair(data.name(context, next.$1), data.clock(next.$2)),
        ]),
      'lock_prev_next_bar' when next != null && previous != null => Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            row([
              pair(data.name(context, previous.$1), data.clock(previous.$2)),
              pair(data.name(context, next.$1), data.clock(next.$2)),
            ]),
            const SizedBox(height: 6),
            ClipRRect(
              borderRadius: BorderRadius.circular(3),
              child: LinearProgressIndicator(
                value: _between(previous.$2, next.$2, data.now),
                minHeight: 4,
                backgroundColor: skin.hairline,
                valueColor: AlwaysStoppedAnimation(skin.accent),
              ),
            ),
          ],
        ),
      'lock_all_times' => _timesRow(context, data, skin, compact: true),
      'lock_three_times' => _timesRow(context, data, skin, only: 3, compact: true),
      'lock_fajr_dhuhr' when table != null => row([
          pair(context.tr('prayer.fajr'), data.clock(table.fajr)),
          pair(context.tr('prayer.dhuhr'), data.clock(table.dhuhr)),
        ]),
      'lock_asr_maghrib_isha' when table != null => row([
          pair(context.tr('prayer.asr'), data.clock(table.asr)),
          pair(context.tr('prayer.maghrib'), data.clock(table.maghrib)),
          pair(context.tr('prayer.isha'), data.clock(table.isha)),
        ]),
      'lock_prayer_counter' => _prayerTrack(context, 0, data, skin),
      'lock_date' => Center(
          child: _value(data.hijriLine, skin, size: 14),
        ),
      'lock_date_next_prayer' when next != null => row([
          pair(context.tr('widgets.today'), data.hijriNumeric),
          pair(data.name(context, next.$1), data.clock(next.$2)),
        ]),
      'lock_date_prev_next' when next != null && previous != null => row([
          pair(data.name(context, previous.$1), data.clock(previous.$2)),
          pair(context.tr('widgets.today'), data.number(data.hijri.day)),
          pair(data.name(context, next.$1), data.clock(next.$2)),
        ]),
      'lock_day_and_date' => Center(
          child: _value('${data.weekday} · ${data.gregorianNumeric}', skin, size: 13),
        ),
      'lock_day_number' => Center(
          child: Text(data.number(data.hijri.day),
              style: AthkarType.sans(size: 30, color: skin.ink, weight: FontWeight.w300)),
        ),
      'lock_day' => Center(
          child: Text(data.weekday,
              style: AthkarType.amiri(size: 20, color: skin.ink, weight: FontWeight.w700)),
        ),
      'lock_daily_adhkar' || 'lock_assorted_adhkar' || 'lock_dua' =>
        data.dhikr == null
            ? _empty(context, skin, 'categories.empty')
            : _narrated(data.dhikr!.arabicText, skin, size: 14, centre: true, lines: 2),
      'lock_quran_verse' || 'lock_mushaf_page' => data.verse == null
          ? _empty(context, skin, 'widgets.noVerse')
          : _narrated(data.verse!.arabicText, skin, size: 14, centre: true, lines: 2),
      'lock_moon_phase' => Center(
          child: _value(
              '${context.tr('widgets.moon')} · ${data.number(data.hijri.day)}', skin,
              size: 14),
        ),
      'lock_ramadan_countdown' => Center(
          child: _value(
            '${context.tr('widgets.ramadan')} · '
            '${data.number(Occasions.toRamadan(
                  data.now,
                  offsetDays: data.settings.hijriOffset,
                  languageCode: data.languageCode,
                ) ?? 0)} '
            '${context.tr('widgets.day')}',
            skin,
            size: 14,
          ),
        ),
      _ => _noLocation(context, skin),
    };
  }

  /// The lock widgets that draw nothing astronomical, so they work with no
  /// location at all.
  static const _locationFree = {
    'lock_date',
    'lock_day_and_date',
    'lock_day_number',
    'lock_day',
    'lock_daily_adhkar',
    'lock_assorted_adhkar',
    'lock_dua',
    'lock_quran_verse',
    'lock_mushaf_page',
    'lock_moon_phase',
    'lock_ramadan_countdown',
  };

  // ────────────────────────────── the pieces ──────────────────────────────

  /// The five prayers across a row, the next one in the accent.
  static Widget _timesRow(
    BuildContext context,
    WidgetPreviewData data,
    WidgetSkin skin, {
    bool ruled = false,
    bool compact = false,
    int? only,
    int skip = 0,
  }) {
    var prayers = data.fivePrayers.skip(skip).toList();
    if (only != null) prayers = prayers.take(only).toList();
    if (prayers.isEmpty) return _noLocation(context, skin);

    final upcoming = data.next?.$1;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        for (final entry in prayers)
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  data.name(context, entry.$1),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AthkarType.sans(
                    size: compact ? 9.5 : 10.5,
                    color: entry.$1 == upcoming ? skin.accent : skin.muted,
                    weight: FontWeight.w500,
                  ),
                ),
                if (ruled) ...[
                  const SizedBox(height: 4),
                  Container(
                    height: 1,
                    margin: const EdgeInsets.symmetric(horizontal: 6),
                    color: entry.$1 == upcoming ? skin.accent : skin.hairline,
                  ),
                ],
                SizedBox(height: ruled ? 4 : 3),
                Text(
                  data.clock(entry.$2),
                  maxLines: 1,
                  style: AthkarType.sans(
                    size: compact ? 12 : 14,
                    color: entry.$1 == upcoming ? skin.accent : skin.ink,
                    weight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }

  /// The same five, stacked, for the taller shapes.
  static Widget _timesColumn(
      BuildContext context, WidgetPreviewData data, WidgetSkin skin) {
    final upcoming = data.next?.$1;

    return Column(
      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final entry in data.fivePrayers)
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                data.name(context, entry.$1),
                style: AthkarType.sans(
                  size: 11.5,
                  color: entry.$1 == upcoming ? skin.accent : skin.muted,
                  weight: entry.$1 == upcoming ? FontWeight.w600 : FontWeight.w400,
                ),
              ),
              Text(
                data.clock(entry.$2),
                style: AthkarType.sans(
                  size: 12.5,
                  color: entry.$1 == upcoming ? skin.accent : skin.ink,
                  weight: FontWeight.w500,
                ),
              ),
            ],
          ),
      ],
    );
  }

  /// The next prayer, its time, and the countdown — the block a reader reads.
  static Widget _nextBlock(BuildContext context, WidgetPreviewData data, WidgetSkin skin) {
    final next = data.next;
    if (next == null) return _noLocation(context, skin);

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _label(context.tr('home.nextPrayer'), skin, size: 10),
        const SizedBox(height: 2),
        Text(data.name(context, next.$1),
            style: AthkarType.amiri(size: 19, color: skin.accent, weight: FontWeight.w700)),
        Text(data.clock(next.$2),
            style: AthkarType.sans(size: 17, color: skin.ink, weight: FontWeight.w500)),
        const SizedBox(height: 2),
        _label(data.countdownTo(next.$2), skin, size: 10),
      ],
    );
  }

  /// «باقٍ على الفجر ٣:٠٦:٤٠» — composed from a template, never concatenated.
  ///
  /// The anchor name is a bare noun; the sentence supplies the preposition. A
  /// widget that glued «باقٍ على» to «الفجر» by hand is how the app ends up with
  /// two ways of saying the same thing, one of them ungrammatical.
  static Widget _countdownLine(
      BuildContext context, WidgetPreviewData data, WidgetSkin skin) {
    final next = data.next;
    if (next == null) return const SizedBox.shrink();

    return Text(
      '${context.tr('widgets.remainingTo', {'prayer': data.name(context, next.$1)})}'
      '  ${data.countdownTo(next.$2)}',
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: AthkarType.sans(size: 11, color: skin.accent, weight: FontWeight.w500),
    );
  }

  /// Seven days across, Hijri over Gregorian, today ringed.
  static Widget _weekStrip(BuildContext context, WidgetPreviewData data, WidgetSkin skin) {
    final start = data.now.subtract(Duration(days: data.now.weekday % 7));

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Row(
          children: [
            for (var index = 0; index < 7; index++)
              Expanded(
                child: Builder(builder: (context) {
                  final day = start.add(Duration(days: index));
                  final isToday = day.day == data.now.day && day.month == data.now.month;
                  final hijri = HijriDate.from(
                    day,
                    offsetDays: data.settings.hijriOffset,
                    languageCode: data.languageCode,
                  );

                  return Column(
                    children: [
                      Container(
                        width: 22,
                        height: 22,
                        alignment: Alignment.center,
                        decoration: isToday
                            ? BoxDecoration(shape: BoxShape.circle, color: skin.accent)
                            : null,
                        child: Text(
                          data.number(hijri.day),
                          style: AthkarType.sans(
                            size: 11,
                            color: isToday ? skin.panel : skin.ink,
                            weight: FontWeight.w500,
                          ),
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        data.number(day.day),
                        style: AthkarType.sans(size: 9.5, color: skin.muted),
                      ),
                    ],
                  );
                }),
              ),
          ],
        ),
      ],
    );
  }

  static Widget _narrated(String text, WidgetSkin skin,
          {double size = 18, bool centre = false, int lines = 3}) =>
      Text(
        text,
        textAlign: centre ? TextAlign.center : TextAlign.start,
        maxLines: lines,
        overflow: TextOverflow.ellipsis,
        style: AthkarType.amiri(size: size, color: skin.ink, height: 1.7),
      );

  static Widget _label(String text, WidgetSkin skin, {double size = 11}) => Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AthkarType.sans(size: size, color: skin.muted),
      );

  static Widget _value(String text, WidgetSkin skin, {double size = 14}) => Text(
        text,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: AthkarType.sans(size: size, color: skin.ink, weight: FontWeight.w500),
      );

  static Widget _noLocation(BuildContext context, WidgetSkin skin) =>
      _empty(context, skin, 'prayer.noLocation');

  static Widget _empty(BuildContext context, WidgetSkin skin, String key) => Center(
        child: Text(
          context.tr(key),
          textAlign: TextAlign.center,
          style: AthkarType.sans(size: 11.5, color: skin.muted),
        ),
      );

  static String _source(Dhikr dhikr) => [
        if (dhikr.sourceBook != null) dhikr.sourceBook!,
        if (dhikr.sourceReference != null) dhikr.sourceReference!,
      ].join(' · ');

  /// How far through the gap between two prayers the moment [now] is.
  static double _between(DateTime from, DateTime to, DateTime now) {
    final span = to.difference(from).inSeconds;
    if (span <= 0) return 0;

    return (now.difference(from).inSeconds / span).clamp(0, 1).toDouble();
  }
}
