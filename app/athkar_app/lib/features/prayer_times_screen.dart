import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/prayer_times.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import '../widgets/prayer_strip.dart';
import 'location_screen.dart';
import 'monthly_screen.dart';

/// Today's times, and everything that decides them.
///
/// The settings live on this screen rather than behind the general settings
/// list, because a reader who disagrees with a time is looking at that time
/// when they decide to change something.
class PrayerTimesScreen extends StatelessWidget {
  const PrayerTimesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    if (!settings.hasLocation) {
      return Scaffold(
        backgroundColor: tokens.paper,
        body: AthkarEmptyState(
          title: context.tr('prayer.noLocation'),
          body: context.tr('prayer.locationOptional'),
          icon: Icons.place_outlined,
          action: FilledButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const LocationScreen()),
            ),
            child: Text(context.tr('prayer.chooseCity')),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            AthkarSpacing.page, 14, AthkarSpacing.page, 30),
          children: [
            Row(
              children: [
                Text(
                  context.tr('prayer.title'),
                  style: AthkarType.amiri(size: 22, color: tokens.ink, weight: FontWeight.w700),
                ),
                const Spacer(),
                AthkarChip(
                  label: settings.cityName ?? '',
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const LocationScreen()),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            const NextPrayerStrip(),
            const SizedBox(height: 12),
            const _TodayList(),
            const SizedBox(height: 12),
            AthkarCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  AthkarListRow(
                    label: context.tr('prayer.method'),
                    value: methodName(context, settings.calculationMethod),
                    onTap: () => _pickMethod(context, settings),
                  ),
                  const AthkarRule(margin: EdgeInsets.zero),
                  AthkarListRow(
                    label: context.tr('prayer.madhab'),
                    value: settings.madhab == Madhab.hanafi
                        ? context.tr('prayer.madhab.hanafi')
                        : context.tr('prayer.madhab.standard'),
                    onTap: () => _pickMadhab(context, settings),
                  ),
                  const AthkarRule(margin: EdgeInsets.zero),
                  AthkarListRow(
                    label: context.tr('prayer.monthly'),
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => const MonthlyScreen()),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            const _Adjustments(),
          ],
        ),
      ),
    );
  }

  Future<void> _pickMethod(BuildContext context, Settings settings) async {
    final chosen = await showModalBottomSheet<CalculationMethod>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _PickerSheet(
        title: context.tr('prayer.method'),
        options: [
          for (final method in CalculationMethod.values)
            (method, methodName(context, method), method == settings.calculationMethod),
        ],
      ),
    );

    if (chosen == null) return;

    await settings.setCalculationMethod(chosen);
    // Every prayer-anchored reminder just moved, so the week's queue is wrong
    // until it is rebuilt. Forgetting this is the bug where the times on screen
    // are right and the notifications are yesterday's.
    if (context.mounted) await AppStateScope.read(context).rescheduleReminders();
  }

  Future<void> _pickMadhab(BuildContext context, Settings settings) async {
    final chosen = await showModalBottomSheet<Madhab>(
      context: context,
      builder: (context) => _PickerSheet(
        title: context.tr('prayer.madhab'),
        options: [
          (Madhab.standard, context.tr('prayer.madhab.standard'),
              settings.madhab == Madhab.standard),
          (Madhab.hanafi, context.tr('prayer.madhab.hanafi'),
              settings.madhab == Madhab.hanafi),
        ],
      ),
    );

    if (chosen == null) return;

    await settings.setMadhab(chosen);
    if (context.mounted) await AppStateScope.read(context).rescheduleReminders();
  }
}

/// The six times as rows, which is easier to read against a wall clock than the
/// home screen's single line.
class _TodayList extends StatelessWidget {
  const _TodayList();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final timetable = PrayerCalculator.today(settings);
    if (timetable == null) return const SizedBox.shrink();

    final next = PrayerCalculator.nextPrayer(settings)?.$1;

    return AthkarCard(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          for (final (index, entry) in timetable.ordered.indexed) ...[
            if (index > 0) const AthkarRule(margin: EdgeInsets.zero),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      prayerName(context, entry.$1),
                      style: AthkarType.sans(
                        size: 13.5,
                        color: entry.$1 == next ? tokens.brand : tokens.ink,
                        weight: entry.$1 == next ? FontWeight.w600 : FontWeight.w500,
                      ),
                    ),
                  ),
                  Text(
                    Numerals.time(
                      entry.$2.hour % 12 == 0 ? 12 : entry.$2.hour % 12,
                      entry.$2.minute,
                      arabicIndic: settings.arabicNumerals,
                    ),
                    style: AthkarType.sans(
                      size: 14,
                      color: entry.$1 == next ? tokens.brand : tokens.ink,
                      weight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// ±minutes per prayer.
///
/// The feature that decides whether somebody keeps the app: a timetable that
/// disagrees with the mosque down the road by two minutes is, to its reader,
/// simply wrong.
class _Adjustments extends StatelessWidget {
  const _Adjustments();

  static const _prayers = [
    ('fajr', PrayerAnchor.fajr),
    ('sunrise', PrayerAnchor.sunrise),
    ('dhuhr', PrayerAnchor.dhuhr),
    ('asr', PrayerAnchor.asr),
    ('maghrib', PrayerAnchor.maghrib),
    ('isha', PrayerAnchor.isha),
  ];

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final adjustments = settings.adjustments;

    return AthkarCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            context.tr('prayer.adjust'),
            style: AthkarType.sans(size: 13.5, color: tokens.ink, weight: FontWeight.w500),
          ),
          const SizedBox(height: 3),
          Text(
            context.tr('prayer.adjustHint'),
            style: AthkarType.sans(size: 11.5, color: tokens.muted),
          ),
          const SizedBox(height: 8),
          for (final (key, anchor) in _prayers)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                children: [
                  Expanded(child: Text(
                    prayerName(context, anchor),
                    style: AthkarType.sans(size: 13, color: tokens.ink),
                  )),
                  _StepButton(
                    icon: Icons.remove,
                    onTap: () => _adjust(context, settings, key, -1),
                  ),
                  SizedBox(
                    width: 44,
                    child: Text(
                      Numerals.format(
                        adjustments[key] ?? 0,
                        arabicIndic: settings.arabicNumerals,
                      ),
                      textAlign: TextAlign.center,
                      style: AthkarType.sans(
                        size: 13.5,
                        color: (adjustments[key] ?? 0) == 0 ? tokens.muted : tokens.brand,
                        weight: FontWeight.w600,
                      ),
                    ),
                  ),
                  _StepButton(
                    icon: Icons.add,
                    onTap: () => _adjust(context, settings, key, 1),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _adjust(BuildContext context, Settings settings, String key, int delta) async {
    await settings.setAdjustment(key, (settings.adjustments[key] ?? 0) + delta);
    if (context.mounted) await AppStateScope.read(context).rescheduleReminders();
  }
}

class _StepButton extends StatelessWidget {
  const _StepButton({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        width: AthkarSpacing.tapTarget,
        height: AthkarSpacing.tapTarget,
        alignment: Alignment.center,
        child: Icon(icon, size: 16, color: tokens.ink),
      ),
    );
  }
}

/// A list of options in a sheet, used by both pickers above.
class _PickerSheet<T> extends StatelessWidget {
  const _PickerSheet({required this.title, required this.options});

  final String title;
  final List<(T, String, bool)> options;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 20, AthkarSpacing.page, 8),
            child: Text(
              title,
              style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
            ),
          ),
          Flexible(
            child: ListView(
              shrinkWrap: true,
              children: [
                for (final (value, label, selected) in options)
                  AthkarListRow(
                    label: label,
                    onTap: () => Navigator.of(context).pop(value),
                    trailing: selected
                        ? Icon(Icons.check, size: 18, color: tokens.brand)
                        : const SizedBox.shrink(),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
      ),
    );
  }
}
