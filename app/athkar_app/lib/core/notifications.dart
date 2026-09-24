import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_timezone/flutter_timezone.dart';
import 'package:timezone/data/latest_all.dart' as tz_data;
import 'package:timezone/timezone.dart' as tz;

import 'bidi_text.dart';

/// Local notifications: the channels, the permission, and one method to put a
/// notification on the calendar.
///
/// Everything anchored to a prayer time is raised from here rather than pushed
/// from the server, because the server has no way to know when Maghrib is where
/// the reader is standing — it never receives their coordinates. See
/// `docs/BUSINESS_LOGIC.md` §5.
class LocalNotifications {
  LocalNotifications._();

  static final instance = LocalNotifications._();

  final _plugin = FlutterLocalNotificationsPlugin();
  var _ready = false;

  /// The channels the app creates at first launch.
  ///
  /// **A channel's sound and importance are fixed the moment Android creates
  /// it.** Re-creating one with different settings does nothing — the original
  /// survives an app update and even a reinstall on some versions. So changing
  /// how a reminder sounds means adding a channel with a new id, never editing
  /// one, which is why every id here carries a version suffix.
  static const remindersChannel = 'athkar.reminders.v1';
  static const prayerChannel = 'athkar.prayer.v1';
  static const announcementsChannel = 'athkar.announcements.v1';

  /// Every channel this build creates.
  ///
  /// Checked against before a pushed message is raised on a channel the server
  /// named: Android drops a notification aimed at a channel that does not exist,
  /// silently and with no error anywhere, which is the hardest kind of missing
  /// notification to find.
  static const knownChannels = <String>{
    remindersChannel,
    prayerChannel,
    announcementsChannel,
  };

  /// iOS allows only 64 pending notifications, and a rolling week of reminders
  /// across a handful of campaigns fits comfortably inside that. Scheduling
  /// further ahead would silently drop the far end of the queue.
  static const horizonDays = 7;

  Future<void> initialize({void Function(String route)? onTapped}) async {
    if (_ready) return;

    tz_data.initializeTimeZones();
    tz.setLocalLocation(tz.getLocation(await FlutterTimezone.getLocalTimezone()));

    await _plugin.initialize(
      settings: const InitializationSettings(
        android: AndroidInitializationSettings('@drawable/ic_notification'),
        iOS: DarwinInitializationSettings(
          // Asked for explicitly at the moment the reader turns reminders on,
          // rather than thrown at them during the first launch.
          requestAlertPermission: false,
          requestBadgePermission: false,
          requestSoundPermission: false,
        ),
      ),
      onDidReceiveNotificationResponse: (response) {
        if (response.payload case final route? when route.isNotEmpty) onTapped?.call(route);
      },
    );

    await _createChannels();
    _ready = true;
  }

  Future<void> _createChannels() async {
    final android = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    if (android == null) return;

    await android.createNotificationChannel(const AndroidNotificationChannel(
      remindersChannel,
      'تذكيرات الأذكار',
      description: 'تذكيرات أذكار الصباح والمساء والنوم',
      importance: Importance.high,
    ));

    await android.createNotificationChannel(const AndroidNotificationChannel(
      prayerChannel,
      'الأذان والمواقيت',
      description: 'تنبيه دخول وقت الصلاة',
      importance: Importance.max,
    ));

    await android.createNotificationChannel(const AndroidNotificationChannel(
      announcementsChannel,
      'رسائل التطبيق',
      description: 'رسائل عامة من إدارة التطبيق',
      importance: Importance.defaultImportance,
    ));
  }

  /// Asks for permission, and reports what the reader chose.
  ///
  /// Called from the reminders screen when they switch something on — never on
  /// launch. A permission prompt before the app has shown what it is for is the
  /// fastest way to a permanent "no".
  Future<bool> requestPermission() async {
    if (Platform.isIOS) {
      final ios = _plugin.resolvePlatformSpecificImplementation<
          IOSFlutterLocalNotificationsPlugin>();
      return await ios?.requestPermissions(alert: true, badge: true, sound: true) ?? false;
    }

    final android = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();

    final granted = await android?.requestNotificationsPermission() ?? false;

    // Exact alarms are their own permission on Android 12+, and without it a
    // reminder for 04:52 arrives whenever the system feels like it. Asked for
    // separately because a reader may reasonably grant one and not the other.
    if (granted) await android?.requestExactAlarmsPermission();

    return granted;
  }

  Future<bool> hasPermission() async {
    final android = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    if (android != null) return await android.areNotificationsEnabled() ?? false;

    // iOS gives no synchronous answer, and asking again would re-prompt. The
    // reminders screen shows what it scheduled instead of what iOS thinks.
    return true;
  }

  /// Puts one notification on the calendar.
  ///
  /// [id] must be stable for a given reminder-and-day, because scheduling the
  /// same id twice replaces rather than duplicates — which is exactly how the
  /// scheduler stays idempotent across relaunches. See `reminder_scheduler.dart`.
  Future<void> schedule({
    required int id,
    required DateTime when,
    required String title,
    required String body,
    required String channelId,
    String? route,
  }) async {
    if (when.isBefore(DateTime.now())) return;

    // The one seam every notification passes through — scheduled reminders and
    // pushes redrawn in the foreground alike — so the direction rule is applied
    // once rather than at each call site, where the next one would forget.
    final directedTitle = BidiText.forNotification(title);
    final directedBody = BidiText.forNotification(body);

    await _plugin.zonedSchedule(
      id: id,
      title: directedTitle,
      body: directedBody,
      scheduledDate: tz.TZDateTime.from(when, tz.local),
      notificationDetails: NotificationDetails(
        android: AndroidNotificationDetails(
          channelId,
          channelId,
          importance: Importance.high,
          priority: Priority.high,
          // The dhikr, not a truncated line of it: a reminder whose whole point
          // is a sentence of remembrance should show the sentence.
          styleInformation: BigTextStyleInformation(directedBody),
          // Notifications of a kind stack together rather than filling the
          // shade with seven near-identical rows.
          groupKey: channelId,
        ),
        iOS: DarwinNotificationDetails(
          // iOS has no big-text style: it shows the body truncated and expands
          // to the whole of it, so the narration composed into `body` above
          // arrives the same way on both platforms with nothing extra here.
          threadIdentifier: channelId,
          interruptionLevel: _urgencyOf(channelId),
        ),
      ),
      androidScheduleMode: AndroidScheduleMode.exactAllowWhileIdle,
      payload: route,
    );
  }

  /// How loudly a channel is allowed to interrupt.
  ///
  /// Only the prayer times are *time sensitive* — the iOS level that breaks
  /// through Focus and Do Not Disturb. That is the whole justification for the
  /// level and the limit of it: a prayer time is the one thing here that is
  /// worthless a few minutes late, and a reader who set Focus deliberately
  /// should not also be interrupted for an announcement from us.
  ///
  /// **Needs the Time Sensitive Notifications entitlement on the iOS target.**
  /// Without it iOS quietly downgrades this to `active` — the notification still
  /// arrives, it simply respects Focus.
  static InterruptionLevel _urgencyOf(String channelId) =>
      channelId == prayerChannel ? InterruptionLevel.timeSensitive : InterruptionLevel.active;

  /// Clears everything this app has pending. The scheduler calls it before each
  /// rebuild of the week's queue.
  Future<void> cancelAll() => _plugin.cancelAll();

  Future<void> cancel(int id) => _plugin.cancel(id: id);

  /// What is actually on the calendar. Used by the reminders screen so it can
  /// show the reader the next real time rather than a computed guess.
  Future<List<PendingNotificationRequest>> pending() async {
    try {
      return await _plugin.pendingNotificationRequests();
    } catch (error) {
      assert(() {
        debugPrint('[notifications] could not read pending requests: $error');
        return true;
      }());
      return const [];
    }
  }
}
