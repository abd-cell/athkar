import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

import 'notifications.dart';

/// Handles a push that arrives while the app is in the background or shut.
///
/// A top-level function by requirement, not by choice: Flutter spins up a fresh
/// isolate for this, so it shares nothing with the running app — no singletons,
/// no settings, no catalogue.
///
/// It deliberately does almost nothing. The message carries a `notification`
/// block, so the system has already drawn the notification by the time this
/// runs; drawing a second one here is the classic way to ship an app that
/// double-notifies. What it is for is the work that must happen even when
/// nobody opens the app — and so far that is only the assertion below, kept
/// because the handler's absence is invisible and its presence is not.
@pragma('vm:entry-point')
Future<void> athkarBackgroundMessageHandler(RemoteMessage message) async {
  await Firebase.initializeApp();

  assert(() {
    debugPrint('[push] background message ${message.messageId}: ${message.data}');
    return true;
  }());
}

/// Firebase Cloud Messaging, for the half of the notification story the server
/// owns: broadcasts an admin composed, and fixed-time reminders.
///
/// Everything anchored to a prayer time is *not* here — it is scheduled locally
/// in `reminder_scheduler.dart`, because the server would need coordinates it
/// deliberately never receives. See `docs/BUSINESS_LOGIC.md` §5.
///
/// Every method degrades quietly. A build with no Firebase configuration, a
/// reader who declined notifications, a device with no Play Services: each
/// simply produces no token, and the app carries on with local reminders and
/// the in-app inbox. Push is a courtesy, never a dependency.
class PushService {
  PushService._();

  static final instance = PushService._();

  var _ready = false;
  String? _token;

  /// Called whenever the token changes after startup. Set by [initialize] so
  /// this file stays free of any knowledge of the API.
  Future<void> Function(String? token)? _onTokenChanged;

  /// The FCM registration token, or null when push is unavailable for any
  /// reason. Sent to the server on registration; null is an ordinary value.
  String? get token => _token;

  /// Sets up Firebase and asks for the token.
  ///
  /// [onTapped] receives the `route` from a notification's data payload, which
  /// is the only thing the server sends that the app acts on — everything a
  /// screen needs is already on the device.
  Future<void> initialize({
    void Function(String route)? onTapped,
    Future<void> Function(String? token)? onTokenChanged,
  }) async {
    if (_ready) return;

    _onTokenChanged = onTokenChanged;

    try {
      await Firebase.initializeApp();

      // Registered before anything else asks Firebase for something, because
      // FCM refuses a handler set after the first message has been handled.
      FirebaseMessaging.onBackgroundMessage(athkarBackgroundMessageHandler);

      final messaging = FirebaseMessaging.instance;

      // Notification permission is asked for once, by whichever of push and
      // local notifications gets there first — they are the same OS permission.
      await messaging.requestPermission();

      // A token can rotate at any time. When it does the device row has to be
      // updated, or the next campaign pushes into a void — FCM will happily
      // accept a send to a token it has already retired, and the reader simply
      // stops hearing from the app.
      //
      // The callback is awaited rather than fired and forgotten so a failure is
      // at least observable; there is nothing useful to do about one here,
      // because the next launch re-registers with the current token anyway.
      messaging.onTokenRefresh.listen((value) async {
        _token = value;
        await _publish(value);
      });

      FirebaseMessaging.onMessageOpenedApp.listen((message) {
        if (message.data['route'] case final String route when route.isNotEmpty) {
          onTapped?.call(route);
        }
      });

      // The tap that *launched* the app, which `onMessageOpenedApp` never sees:
      // the app was not running to listen. Without this, tapping a broadcast
      // from a shut phone opens the home screen and the reader is left to find
      // the chapter the notification was about themselves.
      final launcher = await messaging.getInitialMessage();
      if (launcher?.data['route'] case final String route when route.isNotEmpty) {
        onTapped?.call(route);
      }

      // A message arriving while the app is open is shown through the local
      // notifications plugin, so it looks identical to a scheduled reminder and
      // lands on the same channel.
      FirebaseMessaging.onMessage.listen(_showForeground);

      _ready = true;

      // Last, and allowed to come back empty. Everything above is wired
      // whatever happens here, so a token that is late — the usual case on an
      // iPhone's first launch — still reaches the server through
      // `onTokenRefresh` or the next `refresh()`, instead of taking the whole
      // push setup down with it until the app is killed and reopened.
      _token = await _readToken(messaging);
    } catch (error) {
      // The commonest reason to be here is a development build with no
      // `google-services.json`, which is a supported configuration.
      assert(() {
        debugPrint('[push] unavailable: $error');
        return true;
      }());
    }
  }

  /// Re-reads the token and publishes it if it moved.
  ///
  /// Called when the app returns to the foreground: a reader who grants or
  /// revokes notification permission does it in the OS settings, outside this
  /// process, and `onTokenRefresh` does not fire for a revocation at all.
  Future<void> refresh() async {
    if (!_ready) return;

    try {
      final current = await _readToken(FirebaseMessaging.instance);
      if (current == _token) return;

      _token = current;
      await _publish(current);
    } catch (error) {
      assert(() {
        debugPrint('[push] refresh failed: $error');
        return true;
      }());
    }
  }

  /// The FCM token, or null when there is none to be had yet.
  ///
  /// On iOS an FCM token is a wrapper round an APNs token, and APNs delivers
  /// that asynchronously — on a first launch, typically a moment *after* the
  /// reader answers the permission prompt. Asked before then, `getToken()`
  /// throws `apns-token-not-set`. So on iOS this waits a few seconds for APNs
  /// first, and gives up quietly: when the APNs token does land, FCM mints its
  /// token and `onTokenRefresh` publishes it.
  static Future<String?> _readToken(FirebaseMessaging messaging) async {
    try {
      if (!kIsWeb && defaultTargetPlatform == TargetPlatform.iOS) {
        String? apns;
        for (var attempt = 0; attempt < 10 && apns == null; attempt++) {
          apns = await messaging.getAPNSToken();
          if (apns == null) await Future<void>.delayed(const Duration(milliseconds: 500));
        }
        if (apns == null) return null;
      }

      return await messaging.getToken();
    } catch (error) {
      assert(() {
        debugPrint('[push] no token yet: $error');
        return true;
      }());
      return null;
    }
  }

  Future<void> _publish(String? token) async {
    final notify = _onTokenChanged;
    if (notify == null) return;

    try {
      await notify(token);
    } catch (error) {
      // Push is a courtesy, never a dependency: a server that cannot be
      // reached must not take a launch or a resume down with it.
      assert(() {
        debugPrint('[push] could not publish token: $error');
        return true;
      }());
    }
  }

  Future<void> _showForeground(RemoteMessage message) async {
    final notification = message.notification;
    if (notification == null) return;

    await LocalNotifications.instance.schedule(
      // A moment from now rather than immediately: `zonedSchedule` refuses a
      // time in the past, and this keeps one code path for every notification
      // the app raises.
      id: message.hashCode & 0x7FFFFFFF,
      when: DateTime.now().add(const Duration(seconds: 1)),
      title: notification.title ?? '',
      body: notification.body ?? '',
      channelId: _channelOf(message),
      route: message.data['route'] as String?,
    );
  }

  /// Which channel a pushed message belongs on.
  ///
  /// The server names one — it is the channel the campaign was authored
  /// against, and Android fixes a channel's sound the moment it is created, so
  /// guessing here would raise a prayer call on the announcements channel and
  /// play the wrong sound on a reader's phone. The kind is only the fallback for
  /// a server older than this field, and an unknown id falls back too rather
  /// than being passed through: a channel the app never created is dropped by
  /// Android silently.
  static String _channelOf(RemoteMessage message) {
    if (message.data['channel'] case final String channel
        when LocalNotifications.knownChannels.contains(channel)) {
      return channel;
    }

    return message.data['kind'] == 'reminder'
        ? LocalNotifications.remindersChannel
        : LocalNotifications.announcementsChannel;
  }
}
