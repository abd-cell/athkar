import 'package:flutter/material.dart';

import '../core/hijri_date.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/prayer_times.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import '../widgets/prayer_strip.dart';

/// The month's timetable as one table.
///
/// Computed on the fly for all thirty days — which is instant, because the
/// calculation is arithmetic and happens on this device. An app that fetches a
/// month of prayer times from a server is solving a problem it created.
class MonthlyScreen extends StatefulWidget {
  const MonthlyScreen({super.key});

  @override
  State<MonthlyScreen> createState() => _MonthlyScreenState();
}

class _MonthlyScreenState extends State<MonthlyScreen> {
  late DateTime _month = DateTime(DateTime.now().year, DateTime.now().month);

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final days = PrayerCalculator.forMonth(settings, _month);
    final today = DateTime.now();
    final digits = settings.arabicNumerals;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('prayer.monthly'))),
      body: days.isEmpty
          ? AthkarEmptyState(
              title: context.tr('prayer.noLocation'),
              body: context.tr('prayer.noLocationHint'),
              icon: Icons.place_outlined,
            )
          : Column(
              children: [
                _MonthHeader(
                  month: _month,
                  onPrevious: () => setState(
                    () => _month = DateTime(_month.year, _month.month - 1),
                  ),
                  onNext: () => setState(
                    () => _month = DateTime(_month.year, _month.month + 1),
                  ),
                ),
                const _ColumnHeader(),
                Expanded(
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(
                      AthkarSpacing.page, 0, AthkarSpacing.page, 24),
                    itemCount: days.length,
                    separatorBuilder: (_, __) => const AthkarRule(
                      margin: EdgeInsets.symmetric(vertical: 2),
                    ),
                    itemBuilder: (context, index) {
                      final day = days[index];
                      final isToday = day.date.year == today.year &&
                          day.date.month == today.month &&
                          day.date.day == today.day;

                      return Container(
                        color: isToday ? tokens.brandTint : null,
                        padding: const EdgeInsets.symmetric(vertical: 9, horizontal: 4),
                        child: Row(
                          children: [
                            SizedBox(
                              width: 26,
                              child: Text(
                                Numerals.format(day.date.day, arabicIndic: digits),
                                style: AthkarType.sans(
                                  size: 12,
                                  color: isToday ? tokens.brand : tokens.muted,
                                  weight: isToday ? FontWeight.w600 : FontWeight.w400,
                                ),
                              ),
                            ),
                            for (final (_, at) in day.ordered)
                              Expanded(
                                child: Text(
                                  Numerals.time(
                                    at.hour % 12 == 0 ? 12 : at.hour % 12,
                                    at.minute,
                                    arabicIndic: digits,
                                  ),
                                  textAlign: TextAlign.center,
                                  style: AthkarType.sans(
                                    size: 11.5,
                                    color: isToday ? tokens.brand : tokens.ink,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
              ],
            ),
    );
  }
}

class _MonthHeader extends StatelessWidget {
  const _MonthHeader({
    required this.month,
    required this.onPrevious,
    required this.onNext,
  });

  final DateTime month;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final digits = settings.arabicNumerals;

    // Both calendars, because a reader thinks in one and their phone's calendar
    // is in the other.
    //
    // A Gregorian month straddles two Hijri ones, and naming only the month the
    // 1st falls in reads as an error to anyone looking at it on the 20th: the
    // home screen would be saying «ربيع الآخر» while this header said «ربيع
    // الأول». Printed timetables name the span, so this does too.
    HijriDate hijriOn(DateTime day) => HijriDate.from(
          day,
          offsetDays: settings.hijriOffset,
          languageCode: settings.languageCode,
        );

    final first = hijriOn(month);
    final last = hijriOn(DateTime(month.year, month.month + 1, 0));

    String year(int value) => Numerals.format(value, arabicIndic: digits);

    final title = first.monthName == last.monthName
        ? '${first.monthName} ${year(first.year)}'
        : first.year == last.year
            ? '${first.monthName} – ${last.monthName} ${year(last.year)}'
            : '${first.monthName} ${year(first.year)} – '
                '${last.monthName} ${year(last.year)}';

    return Padding(
      padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 8, AthkarSpacing.page, 12),
      child: Row(
        children: [
          // The chevrons mirror themselves in RTL, so these are named for the
          // direction they point in a left-to-right reading and come out right
          // in both.
          IconButton(onPressed: onPrevious, icon: const Icon(Icons.chevron_left, size: 20)),
          Expanded(
            child: Column(
              children: [
                Text(
                  title,
                  style: AthkarType.amiri(size: 18, color: tokens.ink, weight: FontWeight.w700),
                ),
                Text(
                  '${Numerals.format(month.month, arabicIndic: digits)}/'
                  '${Numerals.format(month.year, arabicIndic: digits)}',
                  style: AthkarType.sans(size: 11.5, color: tokens.muted),
                ),
              ],
            ),
          ),
          IconButton(onPressed: onNext, icon: const Icon(Icons.chevron_right, size: 20)),
        ],
      ),
    );
  }
}

class _ColumnHeader extends StatelessWidget {
  const _ColumnHeader();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    const anchors = [
      PrayerAnchor.fajr,
      PrayerAnchor.sunrise,
      PrayerAnchor.dhuhr,
      PrayerAnchor.asr,
      PrayerAnchor.maghrib,
      PrayerAnchor.isha,
    ];

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AthkarSpacing.page + 4, 0, AthkarSpacing.page + 4, 8),
      child: Row(
        children: [
          const SizedBox(width: 26),
          for (final anchor in anchors)
            Expanded(
              child: Text(
                prayerName(context, anchor),
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 10.5, color: tokens.muted),
              ),
            ),
        ],
      ),
    );
  }
}
