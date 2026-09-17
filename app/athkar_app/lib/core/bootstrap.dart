import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_timezone/flutter_timezone.dart';

import '../l10n/strings_ar.dart';
import '../l10n/strings_en.dart';
import '../models/models.dart';
import '../services/services.dart';
import 'api_client.dart';
import 'content_store.dart';
import 'hijri_date.dart';
import 'notifications.dart';
import 'progress_store.dart';
import 'push_service.dart';
import 'quran_store.dart';
import 'reminder_scheduler.dart';
import 'settings.dart';
import 'widget_bridge.dart';
import 'widget_keys.dart';

/// Everything the app holds at runtime, and the one method that brings it up to
/// date.
///
/// The rule this whole class exists to enforce: **the app is usable before the
/// network is consulted, and remains usable if it never answers.** Launch reads
/// the caches and paints; the sync happens afterwards and quietly, and every
/// step of it is allowed to fail without anything visible going wrong.
class AppState extends ChangeNotifier {
  AppState._({
    required this.settings,
    required this.content,
    required this.progress,
    required this.quran,
  });

  final Settings settings;
  final ContentStore content;
  final ProgressStore progress;
  final QuranStore quran;

  /// The reminders this device schedules for itself, as last synced.
  List<DeviceReminder> reminders = const [];

  /// The admin's widget rules. Read from the cache at launch, refreshed on sync.
  WidgetSettings get widgetSettings => settings.widgetSettings;

  /// The widget gallery, as last synced. Empty until the first answer arrives.
  WidgetCatalog get widgetCatalog => settings.widgetCatalog;

  /// The gallery entry that is actually on this reader's home screen.
  ///
  /// Their own choice, corrected to something the admin still offers and this
  /// build can still draw; then the admin's default; then whatever is first.
  /// Null only when the gallery holds nothing this build understands, which is
  /// a real state on an install several releases old.
  WidgetCatalogItem? get selectedWidget => widgetCatalog.resolveSelection(
        chosen: settings.selectedWidgetKey,
        canDraw: WidgetKeys.knows,
      );

  /// An ayah out of the catalogue, for the Qur'an widget.
  ///
  /// Drawn from published content rather than compiled in, which is the rule
  /// this project does not bend: what a widget shows must be traceable to a
  /// source an editor can correct.
  Dhikr? get widgetVerse {
    for (final category in content.categories) {
      for (final dhikr in category.adhkar) {
        if (dhikr.grade == HadithGrade.quranVerse) return dhikr;
      }
    }
    return null;
  }

  /// The chapter the widget draws its dhikr from.
  ///
  /// The admin may pin one; otherwise it follows the time of day, which is what
  /// the home screen already does — so the widget and the app agree without
  /// anybody configuring it twice.
  AthkarCategory? get widgetCategory {
    final pinned = widgetSettings.dhikrCategoryId;
    if (pinned != null) return content.byId(pinned);

    final suggested = content.suggestedFor(DateTime.now());
    return suggested.isEmpty ? null : suggested.first;
  }

  /// Today's Hijri date, formatted to the reader's settings.
  String hijriToday(Settings settings) => HijriDate.from(
        DateTime.now(),
        offsetDays: settings.hijriOffset,
        languageCode: settings.languageCode,
      ).format(arabicNumerals: settings.arabicNumerals);

  /// True while a sync is in flight, so a screen can show it without blocking.
  var isSyncing = false;

  /// Loads the local state. Fast, offline, and enough to paint the whole app.
  static Future<AppState> load() async {
    final state = AppState._(
      settings: await Settings.load(),
      content: await ContentStore.load(),
      progress: await ProgressStore.load(),
      quran: await QuranStore.load(),
    );

    ApiClient.instance
      ..deviceKey = state.settings.deviceKey
      ..languageCode = state.settings.languageCode
      ..strings = state.settings.stringsOverlay;

    return state;
  }

  /// Registers the device and pulls anything that has changed.
  ///
  /// Ordered so the cheap checks come first and each step is independent: a
  /// failed catalogue sync must not stop reminders being scheduled from what is
  /// already cached.
  Future<void> sync({void Function(String route)? onNotificationTapped}) async {
    if (isSyncing) return;

    isSyncing = true;
    notifyListeners();

    try {
      await _configuration();
      await _registerDevice(onNotificationTapped: onNotificationTapped);
      await _catalog();
      await _strings();
      await _reminders();
      await _widget();
      await _adoptLegacyQuran();
    } finally {
      isSyncing = false;
      notifyListeners();
    }
  }

  Future<void> _configuration() async {
    final response = await Api.configuration.get();
    if (response.success && response.data != null) {
      await settings.setConfig(response.data!);
    }
  }

  Future<void> _registerDevice({void Function(String route)? onNotificationTapped}) async {
    await LocalNotifications.instance.initialize(onTapped: onNotificationTapped);
    await PushService.instance.initialize(
      onTapped: onNotificationTapped,
      onTokenChanged: _publishPushToken,
    );

    final timeZone = await _timeZone();

    await Api.devices.register(
      deviceKey: settings.deviceKey,
      platform: _platform,
      languageCode: settings.languageCode,
      timeZoneId: timeZone,
      notificationsEnabled: await LocalNotifications.instance.hasPermission(),
      pushToken: PushService.instance.token,
      appVersion: appVersion,
      countryCode: _regionCode,
    );
  }

  /// Sends a rotated push token to the server.
  ///
  /// FCM hands the app a new token whenever it likes, and until the server has
  /// it every scheduled push for this install goes to an address nobody is at.
  /// Registration alone would not catch it: that runs at launch, and a rotation
  /// usually happens while the app is open or closed, not as it starts.
  Future<void> _publishPushToken(String? token) async {
    await Api.devices.updatePushToken(
      deviceKey: settings.deviceKey,
      pushToken: token,
      notificationsEnabled: await LocalNotifications.instance.hasPermission(),
    );
  }

  /// Re-checks the push token after the app comes back to the foreground.
  ///
  /// The case `onTokenRefresh` cannot cover: a reader revoking notification
  /// permission in the OS settings. No callback fires for that — the app simply
  /// finds it has no token the next time it looks.
  Future<void> refreshPushToken() => PushService.instance.refresh();

  /// Fetches the catalogue only when the version moved.
  ///
  /// The comparison happens twice — once here against the configuration we just
  /// fetched, and once on the server against the version we send — because the
  /// first saves a request and the second is what makes the answer correct.
  Future<void> _catalog() async {
    final language = settings.languageCode;
    final serverVersion = settings.config.contentVersion;

    if (!content.needsSync(language, serverVersion)) return;

    final response = await Api.content.catalog(
      language: language,
      knownVersion: content.languageCode == language ? content.version : null,
    );

    final catalog = response.data;
    if (!response.success || catalog == null || catalog.isUpToDate) return;

    await content.save(catalog);
  }

  Future<void> _strings() async {
    final response = await Api.languages.strings(settings.languageCode);
    if (response.success && response.data != null) {
      await settings.setStringsOverlay(settings.languageCode, response.data!);
      ApiClient.instance.strings = response.data!;
    }
  }

  /// Refreshes the reminder campaigns and rebuilds this week's notifications.
  ///
  /// Rebuilt from whatever is in hand — the freshly fetched list, or the last
  /// one if the call failed — because a reader whose network is down still
  /// expects tomorrow's morning reminder.
  Future<void> _reminders() async {
    final response = await Api.reminders.forDevice(settings.languageCode);
    if (response.success && response.data != null) reminders = response.data!;

    if (reminders.isNotEmpty) {
      await ReminderScheduler.rebuild(settings, reminders, content, _string);
    }
  }

  /// Refreshes the gallery and the rules, then repaints the launcher.
  ///
  /// One call for both, and a fall back to the rules alone if it fails: a
  /// deployment running an older server has no catalogue endpoint, and a reader
  /// there must still get a working widget rather than an empty gallery and a
  /// blank home screen.
  Future<void> _widget() async {
    final catalog = await Api.widget.catalog(settings.languageCode);

    if (catalog.success && catalog.data != null) {
      await settings.setWidgetCatalog(catalog.data!);
    } else {
      final rules = await Api.widget.get();
      if (rules.success && rules.data != null) {
        await settings.setWidgetSettings(rules.data!);
      }
    }

    await pushWidget();
  }

  /// Hands the widget's current content to the platform.
  ///
  /// Called after a sync, after the reader changes anything the widget shows,
  /// and when the app resumes. Android's own half-hourly refresh is only the
  /// backstop for a phone nobody has opened.
  Future<void> pushWidget() async {
    await WidgetBridge.instance.push(
      settings: settings,
      rules: widgetSettings,
      selection: selectedWidget,
      category: widgetCategory,
      verse: widgetVerse,
      placeholder: _widgetPlaceholder,
      prayerName: _prayerName,
      copy: _string,
    );
  }

  /// Re-schedules everything. Called when anything that moves a reminder's
  /// clock time changes: the location, the calculation method, the madhab, a
  /// manual adjustment, the bedtime, or a reminder being muted.
  ///
  /// **The widget is repainted here too, and first.** Every change in that list
  /// moves what the widget shows as surely as it moves a reminder, and the two
  /// used to be wired separately — so choosing a city rebuilt the notifications
  /// and left «لم يُحدَّد موقع بعد» on the home screen until the next sync or the
  /// next time the app was opened. A reader who has just told the app where they
  /// live and watches their widget keep saying it does not know has been given
  /// every reason to conclude the widget is broken.
  ///
  /// First, because the early return below is real: a device with no campaigns
  /// has nothing to reschedule, and that must not be allowed to swallow the
  /// repaint as it did before.
  Future<void> rescheduleReminders() async {
    await pushWidget();

    if (reminders.isEmpty) return;
    await ReminderScheduler.rebuild(settings, reminders, content, _string);
  }

  /// Switches language and re-syncs everything that is language-shaped.
  ///
  /// The catalogue cache holds one language's text, so a switch invalidates it —
  /// which is why this is a method here rather than a setter on [Settings].
  Future<void> changeLanguage(String code) async {
    await settings.setLanguage(code);
    ApiClient.instance
      ..languageCode = code
      ..strings = settings.stringsOverlay;

    await _catalog();
    await _strings();
    await _reminders();
    // The gallery's names are resolved to one language on the server, so a
    // switch leaves every widget in the old one until this runs.
    await _widget();

    notifyListeners();
  }

  /// Files the mushaf a single-mushaf build left behind under the edition it
  /// actually is.
  ///
  /// That build stored one file and one version number and had no name for the
  /// mushaf, because the server had none to give. Asking without naming an
  /// edition returns the default one — which is the mushaf that build was
  /// downloading — so the file already on disk is claimed rather than
  /// re-downloaded. Last in the sync and allowed to fail: a reader whose network
  /// drops here simply keeps an unclaimed file until the next launch.
  Future<void> _adoptLegacyQuran() async {
    if (!quran.hasLegacy) return;

    final response = await Api.quran.checkVersion();
    if (response.data?.edition case final edition? when edition.isNotEmpty) {
      await quran.adoptLegacy(edition);
    }
  }

  /// Removes the downloaded mushaf and tells the screens.
  ///
  /// It goes through here rather than straight to [QuranStore] because the
  /// Qur'an tab decides what it shows from `quran.isSaved`, and a store that is
  /// not a notifier would leave it painting an index over a file that is gone.
  Future<void> forgetQuran(String edition) async {
    await quran.delete(edition);
    notifyListeners();
  }

  /// Tells anything watching [AppStateScope] that the mushaf on this device
  /// changed shape — downloaded, switched, or (via [forgetQuran]) removed.
  ///
  /// The home screen's «آية اليوم» card is the reason this exists: it loads
  /// once and lives inside an `IndexedStack`, so nothing rebuilds it when the
  /// reader downloads a mushaf from the Qur'an tab unless something notifies.
  void quranChanged() => notifyListeners();

  /// Copy the widget needs, resolved without a `BuildContext`.
  ///
  /// The widget is pushed from background paths — a sync, a resume — where
  /// there is no element tree to read localisations from, so these come
  /// straight from the string maps for the chosen language.
  String _string(String key) {
    final overlay = settings.stringsOverlay[key];
    if (overlay != null) return overlay;

    final built = settings.languageCode == 'en' ? englishStrings : arabicStrings;
    return built[key] ?? arabicStrings[key] ?? '';
  }

  String get _widgetPlaceholder => widgetSettings.isEnabled
      ? _string('prayer.noLocation')
      : _string('widget.disabled');

  String _prayerName(PrayerAnchor anchor) => _string('prayer.${anchor.name}');

  static Future<String> _timeZone() async {
    try {
      return await FlutterTimezone.getLocalTimezone();
    } catch (_) {
      // The server falls back to UTC for an id it does not recognise, so an
      // unavailable timezone costs a server-pushed reminder its local hour and
      // nothing else.
      return 'UTC';
    }
  }

  /// Matches `Shareds/Enums/DevicePlatform.cs`.
  static int get _platform {
    if (kIsWeb) return 3;
    if (Platform.isAndroid) return 1;
    if (Platform.isIOS) return 2;
    return 0;
  }

  /// The device's region, from its own locale. Never derived from an IP address.
  static String? get _regionCode {
    if (kIsWeb) return null;

    final locale = Platform.localeName; // "ar_SA.UTF-8"
    final parts = locale.split(RegExp('[_.]'));
    return parts.length > 1 && parts[1].length == 2 ? parts[1].toUpperCase() : null;
  }

  /// Sent to the server and shown on the About screen. Bumped with the pubspec.
  static const appVersion = '1.0.0';
}
