import 'package:flutter/material.dart';

import '../core/bootstrap.dart';
import '../core/l10n.dart';
import '../core/notifications.dart';
import '../core/numerals.dart';
import '../core/reminder_scheduler.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import 'battery_help_screen.dart';

/// The reminders, as the reader sees them.
///
/// Each row shows what it is anchored to and when it will actually next fire —
/// a real computed instant, not a description of a rule. That is the difference
/// between a reader trusting the feature and wondering whether it works.
class RemindersScreen extends StatefulWidget {
  const RemindersScreen({super.key});

  @override
  State<RemindersScreen> createState() => _RemindersScreenState();
}

class _RemindersScreenState extends State<RemindersScreen> {
  var _hasPermission = true;

  @override
  void initState() {
    super.initState();
    _check();
  }

  Future<void> _check() async {
    final granted = await LocalNotifications.instance.hasPermission();
    if (mounted) setState(() => _hasPermission = granted);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('reminders.title'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 14, AthkarSpacing.page, 30),
        children: [
          Text(
            context.tr('reminders.subtitle'),
            style: AthkarType.sans(size: 12, color: tokens.muted, height: 1.6),
          ),

          if (!_hasPermission) ...[
            const SizedBox(height: 14),
            AthkarCard(
              radius: AthkarSpacing.smallCardRadius,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    context.tr('reminders.permissionNeeded'),
                    style: AthkarType.sans(
                      size: 13.5, color: tokens.ink, weight: FontWeight.w500),
                  ),
                  const SizedBox(height: 10),
                  FilledButton(
                    onPressed: () async {
                      await LocalNotifications.instance.requestPermission();
                      await _check();
                      if (mounted) await state.rescheduleReminders();
                    },
                    child: Text(context.tr('reminders.permissionAsk')),
                  ),
                ],
              ),
            ),
          ],

          const SizedBox(height: 14),
          if (state.reminders.isEmpty)
            AthkarEmptyState(
              title: context.tr('notifications.empty'),
              body: context.tr('categories.emptyHint'),
              icon: Icons.notifications_none,
            )
          else
            AthkarCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  for (final (index, reminder) in state.reminders.indexed) ...[
                    if (index > 0) const AthkarRule(margin: EdgeInsets.zero),
                    _ReminderRow(reminder: reminder),
                  ],
                ],
              ),
            ),

          const SizedBox(height: 16),
          // The most-asked question an app like this gets, and it deserves a
          // real answer rather than a support email.
          AthkarCard(
            radius: AthkarSpacing.smallCardRadius,
            padding: EdgeInsets.zero,
            child: AthkarListRow(
              label: context.tr('reminders.whyNot'),
              icon: Icons.battery_alert_outlined,
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const BatteryHelpScreen()),
              ),
            ),
          ),

          const SizedBox(height: 14),
          AthkarCard(
            radius: AthkarSpacing.smallCardRadius,
            padding: EdgeInsets.zero,
            child: AthkarListRow(
              label: context.tr('reminders.bedtime'),
              icon: Icons.bedtime_outlined,
              value: Numerals.clock(settings.bedtime, arabicIndic: settings.arabicNumerals),
              onTap: () => _pickBedtime(context, settings, state),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _pickBedtime(BuildContext context, Settings settings, AppState state) async {
    final parts = settings.bedtime.split(':');

    final chosen = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(
        hour: int.tryParse(parts.first) ?? 22,
        minute: int.tryParse(parts.last) ?? 30,
      ),
    );

    if (chosen == null) return;

    await settings.setBedtime(
      '${chosen.hour.toString().padLeft(2, '0')}:'
      '${chosen.minute.toString().padLeft(2, '0')}',
    );
    await state.rescheduleReminders();
  }
}

class _ReminderRow extends StatelessWidget {
  const _ReminderRow({required this.reminder});

  final DeviceReminder reminder;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final muted = settings.isReminderMuted(reminder.key);
    final next = muted ? null : ReminderScheduler.nextOccurrence(settings, reminder);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  reminder.title,
                  style: AthkarType.sans(
                    size: 13.5, color: tokens.ink, weight: FontWeight.w500),
                ),
                const SizedBox(height: 3),
                Text(
                  _describe(context, reminder, next, settings.arabicNumerals),
                  style: AthkarType.sans(size: 11.5, color: tokens.muted),
                ),
              ],
            ),
          ),
          Switch(
            value: !muted,
            onChanged: reminder.isUserAdjustable
                ? (enabled) async {
                    await settings.setReminderMuted(reminder.key, !enabled);
                    await state.rescheduleReminders();
                  }
                : null,
          ),
        ],
      ),
    );
  }

  /// The rule, and then the actual next time — «بعد الشروق بـ٣٠ دقيقة · ٦:٤١».
  String _describe(
    BuildContext context,
    DeviceReminder reminder,
    DateTime? next,
    bool digits,
  ) {
    final rule = reminder.kind == ReminderKind.fixedTime
        ? context.tr('reminders.atTime', {
            'time': Numerals.clock(reminder.localTime ?? '', arabicIndic: digits),
          })
        : _anchored(context, reminder, digits);

    if (next == null) return rule;

    final time = Numerals.time(
      next.hour % 12 == 0 ? 12 : next.hour % 12,
      next.minute,
      arabicIndic: digits,
    );

    return '$rule · $time';
  }

  /// An anchored reminder's rule.
  ///
  /// Zero gets its own sentence. «بعد الفجر بـ٠ دقيقة» is what a template with a
  /// minute count in it produces for a reminder that fires *at* the prayer, and
  /// it reads like a mistake — which, for a reader deciding whether to trust the
  /// alert, is worse than it sounds.
  String _anchored(BuildContext context, DeviceReminder reminder, bool digits) {
    final anchor = context.tr('reminders.anchor.${reminder.anchor.name}');

    if (reminder.offsetMinutes == 0) {
      return context.tr('reminders.atAnchor', {'anchor': anchor});
    }

    return context.tr(
      reminder.offsetMinutes < 0 ? 'reminders.offsetBefore' : 'reminders.offsetAfter',
      {
        'anchor': anchor,
        'minutes': Numerals.format(reminder.offsetMinutes.abs(), arabicIndic: digits),
      },
    );
  }
}
