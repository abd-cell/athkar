library;

import 'dart:async';

import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/prayer_times.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import 'athkar_ui.dart';

/// The two prayer-time pieces the home screen opens with: the countdown strip
/// and the six-column card.
///
/// Both render nothing at all when no location is set. That is the correct
/// behaviour and not a placeholder: the app promises to work without ever
/// asking for location, so the prayer section is simply absent until the reader
/// either grants it or picks a city.

/// The tinted strip: next prayer, its time, and a live countdown.
class NextPrayerStrip extends StatefulWidget {
  const NextPrayerStrip({super.key, this.onTap});

  final VoidCallback? onTap;

  @override
  State<NextPrayerStrip> createState() => _NextPrayerStripState();
}

class _NextPrayerStripState extends State<NextPrayerStrip> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    // One second, because the design shows seconds. Cheap: it rebuilds a single
    // strip, and it is cancelled the moment the widget leaves the tree.
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final next = PrayerCalculator.nextPrayer(settings);
    if (next == null) return const SizedBox.shrink();

    final (anchor, at) = next;
    final digits = settings.arabicNumerals;

    return InkWell(
      onTap: widget.onTap,
      borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        decoration: BoxDecoration(
          color: tokens.brandTint,
          borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
        ),
        child: Row(
          children: [
            Text(
              context.tr('home.nextPrayer'),
              style: AthkarType.sans(size: 12, color: tokens.brandInk),
            ),
            const SizedBox(width: 10),
            Text(
              '${prayerName(context, anchor)} '
              '${Numerals.time(_hour12(at.hour), at.minute, arabicIndic: digits)}',
              style: AthkarType.sans(size: 14, color: tokens.brandInk, weight: FontWeight.w600),
            ),
            const Spacer(),
            Text(
              context.tr('home.remaining'),
              style: AthkarType.sans(size: 12, color: tokens.brandInk),
            ),
            const SizedBox(width: 8),
            Text(
              Numerals.countdown(at.difference(DateTime.now()), arabicIndic: digits),
              style: AthkarType.sans(size: 14, color: tokens.brandInk, weight: FontWeight.w600),
            ),
          ],
        ),
      ),
    );
  }
}

/// The card with all six times across it, and the convention line underneath.
class PrayerTimesCard extends StatelessWidget {
  const PrayerTimesCard({super.key, this.onMonthlyTap});

  final VoidCallback? onMonthlyTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final timetable = PrayerCalculator.today(settings);
    if (timetable == null) return const SizedBox.shrink();

    final next = PrayerCalculator.nextPrayer(settings)?.$1;
    final digits = settings.arabicNumerals;

    return AthkarCard(
      padding: const EdgeInsets.fromLTRB(18, 16, 18, 16),
      child: Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final (anchor, at) in timetable.ordered)
                Column(
                  children: [
                    Text(
                      prayerName(context, anchor),
                      style: AthkarType.sans(
                        size: 11,
                        color: anchor == next ? tokens.brand : tokens.muted,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      Numerals.time(_hour12(at.hour), at.minute, arabicIndic: digits),
                      style: AthkarType.sans(
                        size: 13,
                        color: anchor == next ? tokens.brand : tokens.ink,
                        weight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
            ],
          ),
          const AthkarRule(margin: EdgeInsets.only(top: 14, bottom: 10)),
          Row(
            children: [
              Text(
                methodName(context, settings.calculationMethod),
                style: AthkarType.sans(size: 11.5, color: tokens.muted),
              ),
              const Spacer(),
              InkWell(
                onTap: onMonthlyTap,
                child: Text(
                  context.tr('home.monthlyView'),
                  style: AthkarType.sans(size: 11.5, color: tokens.brand),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The reader-facing name of a prayer.
String prayerName(BuildContext context, PrayerAnchor anchor) => switch (anchor) {
      PrayerAnchor.fajr => context.tr('prayer.fajr'),
      PrayerAnchor.sunrise => context.tr('prayer.sunrise'),
      PrayerAnchor.dhuhr => context.tr('prayer.dhuhr'),
      PrayerAnchor.asr => context.tr('prayer.asr'),
      PrayerAnchor.maghrib => context.tr('prayer.maghrib'),
      PrayerAnchor.isha => context.tr('prayer.isha'),
      _ => '',
    };

/// The convention's own name, in the reader's language.
///
/// These are the names of specific bodies, but ordinary translatable ones —
/// «كراتشي» is Karachi, «الكويت» is Kuwait — with established English forms,
/// not proper nouns exempt from i18n. An English reader with the Arabic
/// literals could read only "ISNA" and had no way to tell which of the other
/// twelve conventions was in effect.
String methodName(BuildContext context, CalculationMethod method) =>
    context.tr('prayer.method.${method.name}');

/// The design prints a 12-hour clock with no meridiem — «١٢:١٧», «٤:٥٢» — which
/// is how prayer times are read aloud and how every printed timetable sets them.
int _hour12(int hour) {
  final wrapped = hour % 12;
  return wrapped == 0 ? 12 : wrapped;
}
