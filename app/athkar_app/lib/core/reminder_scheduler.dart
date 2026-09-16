import 'package:flutter/foundation.dart';

import '../models/models.dart';
import 'content_store.dart';
import 'notification_narration.dart';
import 'notifications.dart';
import 'prayer_times.dart';
import 'settings.dart';

/// Turns the campaigns the server sent into notifications on this phone's
/// calendar.
///
/// The design constraint that shapes everything here: **iOS allows 64 pending
/// notifications**. A prayer-anchored reminder is a different clock time every
/// day, so it cannot be expressed as a repeating notification — each day is its
/// own entry. A rolling week is therefore the horizon, rebuilt on every launch,
/// and the id scheme is what keeps that rebuild idempotent rather than
/// duplicating a week of reminders each time the app opens.
class ReminderScheduler {
  const ReminderScheduler._();

  /// Notification ids are `campaignId * 100 + dayOffset`, which is stable for a
  /// given reminder and day. Scheduling the same id twice replaces rather than
  /// duplicates, so a rebuild is safe at any moment.
  static int _idFor(DeviceReminder reminder, int dayOffset) =>
      reminder.id * 100 + dayOffset;

  /// Rebuilds the next [LocalNotifications.horizonDays] of reminders.
  ///
  /// Called on launch, after a sync, when the reader changes a reminder, and
  /// when the location or calculation method moves — anything that could change
  /// what time a reminder lands at.
  static Future<int> rebuild(
    Settings settings,
    List<DeviceReminder> reminders,
    ContentStore content,
    String Function(String key)? copy,
  ) async {
    await LocalNotifications.instance.cancelAll();

    var scheduled = 0;
    final now = DateTime.now();

    for (final reminder in reminders) {
      if (settings.isReminderMuted(reminder.key)) continue;

      for (var offset = 0; offset < LocalNotifications.horizonDays; offset++) {
        final day = DateTime(now.year, now.month, now.day).add(Duration(days: offset));

        if (!WeekDays.includes(reminder.days, day.weekday)) continue;

        final when = _resolve(settings, reminder, day);
        if (when == null || !when.isAfter(now)) continue;

        await LocalNotifications.instance.schedule(
          id: _idFor(reminder, offset),
          when: when,
          // Filled here as well as in the body: an admin who writes «{city}»
          // into a headline should not find it rendered literally because this
          // code only looked at one of the two fields.
          title: NotificationNarration.fill(
            reminder.title,
            cityName: settings.cityName,
            copy: copy,
          ),
          // The announcement, and under it a narration with its takhrij drawn
          // from the catalogue on this phone. Composed per day rather than per
          // rebuild, so re-opening the app does not change what tomorrow says.
          body: NotificationNarration.compose(
            reminder: reminder,
            content: content,
            when: when,
            cityName: settings.cityName,
            copy: copy,
          ),
          channelId: reminder.androidChannelId,
          route: reminder.categoryId == null ? null : 'category/${reminder.categoryId}',
        );

        scheduled++;
      }
    }

    assert(() {
      debugPrint('[reminders] scheduled $scheduled notifications over '
          '${LocalNotifications.horizonDays} days');
      return true;
    }());

    return scheduled;
  }

  /// The instant one reminder fires on one day, or null when it cannot be
  /// resolved — an anchored reminder with no location set, say, which is a
  /// normal state and not an error.
  static DateTime? _resolve(Settings settings, DeviceReminder reminder, DateTime day) {
    if (reminder.kind == ReminderKind.fixedTime) {
      final parts = (reminder.localTime ?? '').split(':');
      if (parts.length < 2) return null;

      final hour = int.tryParse(parts[0]);
      final minute = int.tryParse(parts[1]);
      if (hour == null || minute == null) return null;

      return DateTime(day.year, day.month, day.day, hour, minute);
    }

    // Bedtime is the reader's own answer, not an astronomical one.
    if (reminder.anchor == PrayerAnchor.bedtime) {
      final parts = settings.bedtime.split(':');
      if (parts.length < 2) return null;

      final hour = int.tryParse(parts[0]) ?? 22;
      final minute = int.tryParse(parts[1]) ?? 30;

      return DateTime(day.year, day.month, day.day, hour, minute)
          .add(Duration(minutes: reminder.offsetMinutes));
    }

    final timetable = PrayerCalculator.forDay(settings, day);
    final anchor = timetable?.at(reminder.anchor);

    return anchor?.add(Duration(minutes: reminder.offsetMinutes));
  }

  /// When a reminder will next fire, for the reminders screen to show.
  static DateTime? nextOccurrence(Settings settings, DeviceReminder reminder) {
    final now = DateTime.now();

    for (var offset = 0; offset < LocalNotifications.horizonDays; offset++) {
      final day = DateTime(now.year, now.month, now.day).add(Duration(days: offset));
      if (!WeekDays.includes(reminder.days, day.weekday)) continue;

      final when = _resolve(settings, reminder, day);
      if (when != null && when.isAfter(now)) return when;
    }

    return null;
  }
}
