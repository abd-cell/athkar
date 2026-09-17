import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';
import 'cities.dart';

/// Everything the reader has chosen, and everything the app remembers.
///
/// A single [ChangeNotifier] over `SharedPreferences`, with no state-management
/// package — the app has one store, it is read by nearly every screen, and a
/// dependency would buy nothing here.
///
/// The privacy promise is visible in what this holds: coordinates, streaks,
/// favourites and counters all live in this file on this phone and are never
/// sent anywhere. The only things the server ever learns are in
/// `DeviceService.register`, and they are listed there.
class Settings extends ChangeNotifier {
  Settings._(this._prefs);

  final SharedPreferences _prefs;

  static const _kDeviceKey = 'device.key';
  static const _kLanguage = 'ui.language';
  static const _kTheme = 'ui.theme';
  static const _kFontScale = 'ui.fontScale';
  static const _kTashkeel = 'ui.tashkeel';
  static const _kTranslation = 'ui.translation';
  static const _kArabicNumerals = 'ui.arabicNumerals';
  static const _kHaptics = 'ui.haptics';
  static const _kCalculationMethod = 'prayer.method';
  static const _kMadhab = 'prayer.madhab';
  static const _kAdjustments = 'prayer.adjustments';
  static const _kLatitude = 'prayer.latitude';
  static const _kLongitude = 'prayer.longitude';
  static const _kCityName = 'prayer.city';
  static const _kCountry = 'prayer.country';
  static const _kHijriOffset = 'calendar.hijriOffset';
  static const _kQuranAsPages = 'quran.asPages';
  static const _kTasbihDhikr = 'tasbih.dhikrId';
  static const _kFavourites = 'content.favourites';
  static const _kConfig = 'cache.configuration';
  static const _kStringsOverlay = 'cache.strings';
  static const _kBedtime = 'reminders.bedtime';
  static const _kMutedReminders = 'reminders.muted';
  static const _kWidgetKind = 'widget.kind';
  static const _kWidgetSettings = 'widget.settings';
  static const _kWidgetCatalog = 'widget.catalog';
  static const _kWidgetDesigns = 'widget.designs';
  static const _kWidgetSelected = 'widget.selected';
  static const _kWidgetBackground = 'widget.background';
  static const _kWidgetOpacity = 'widget.opacity';
  static const _kWidgetImage = 'widget.image';
  static const _kWidgetCustomText = 'widget.customText';
  static const _kWidgetCustomAttribution = 'widget.customAttribution';

  static Future<Settings> load() async =>
      Settings._(await SharedPreferences.getInstance());

  // ─────────────────────────── identity ───────────────────────────

  /// This install's key, minted once and kept.
  ///
  /// Generated here rather than asked for: it identifies an install, never a
  /// person, and clearing the app's data quite deliberately produces a new one.
  String get deviceKey {
    final existing = _prefs.getString(_kDeviceKey);
    if (existing != null) return existing;

    final key = _mintUuid();
    _prefs.setString(_kDeviceKey, key);
    return key;
  }

  /// Drops this install's key so the next read of [deviceKey] mints a new
  /// one.
  ///
  /// Called after a confirmed «حذف بياناتي من الخادم»: without it, the next
  /// launch's own registration call re-sends the same key, and the server
  /// un-deletes the very row the reader just asked it to forget.
  Future<void> resetDeviceKey() async {
    await _prefs.remove(_kDeviceKey);
  }

  // ─────────────────────────── appearance ───────────────────────────

  String get languageCode => _prefs.getString(_kLanguage) ?? 'ar';

  Future<void> setLanguage(String code) async {
    await _prefs.setString(_kLanguage, code);
    notifyListeners();
  }

  ThemeMode get themeMode => switch (_prefs.getString(_kTheme)) {
        'light' => ThemeMode.light,
        'dark' => ThemeMode.dark,
        _ => ThemeMode.system,
      };

  Future<void> setThemeMode(ThemeMode mode) async {
    await _prefs.setString(_kTheme, mode.name);
    notifyListeners();
  }

  /// Multiplier on the size of narrated text — the dhikr itself, never the
  /// interface around it. Labels and buttons stay where the design put them.
  double get fontScale => _prefs.getDouble(_kFontScale) ?? 1.0;

  Future<void> setFontScale(double value) async {
    await _prefs.setDouble(_kFontScale, value.clamp(0.8, 1.8));
    notifyListeners();
  }

  bool get showTashkeel => _prefs.getBool(_kTashkeel) ?? true;

  Future<void> setShowTashkeel(bool value) async {
    await _prefs.setBool(_kTashkeel, value);
    notifyListeners();
  }

  /// Off by default in Arabic, on by default in every other language — a reader
  /// who chose English is reading the app *for* the translation.
  bool get showTranslation =>
      _prefs.getBool(_kTranslation) ?? languageCode != 'ar';

  Future<void> setShowTranslation(bool value) async {
    await _prefs.setBool(_kTranslation, value);
    notifyListeners();
  }

  bool get arabicNumerals => _prefs.getBool(_kArabicNumerals) ?? languageCode == 'ar';

  Future<void> setArabicNumerals(bool value) async {
    await _prefs.setBool(_kArabicNumerals, value);
    notifyListeners();
  }

  /// Whether the mushaf opens as printed pages rather than as flowing verses.
  ///
  /// Defaults to true, and the default is the point: a package that carries the
  /// page layer *is* the mushaf as the press set it — 604 pages broken where
  /// the King Fahd Complex broke them — and that is the thing a reader knows by
  /// sight and navigates by memory. Opening it as reflowed text would be
  /// offering a worse version of what is already on the device.
  ///
  /// A package without the page layer ignores this: `Mushaf.isAvailable` is
  /// false there and the toggle is not offered, so the verse view is all there
  /// is. See `docs/BUSINESS_LOGIC.md` §7.4.
  bool get quranAsPages => _prefs.getBool(_kQuranAsPages) ?? true;

  Future<void> setQuranAsPages(bool value) async {
    await _prefs.setBool(_kQuranAsPages, value);
    notifyListeners();
  }

  /// Which published dhikr the counter is counting, by id.
  ///
  /// Null is not "unset waiting to be fixed" — it is the built-in
  /// «سبحان الله وبحمده», which is what the counter shows on an install whose
  /// catalogue has not synced yet. An id that the catalogue no longer carries
  /// resolves back to that same default on read, so a dhikr an editor retires
  /// leaves the screen working rather than blank.
  int? get tasbihDhikrId => _prefs.getInt(_kTasbihDhikr);

  Future<void> setTasbihDhikr(int? id) async {
    id == null
        ? await _prefs.remove(_kTasbihDhikr)
        : await _prefs.setInt(_kTasbihDhikr, id);
    notifyListeners();
  }

  bool get haptics => _prefs.getBool(_kHaptics) ?? true;

  Future<void> setHaptics(bool value) async {
    await _prefs.setBool(_kHaptics, value);
    notifyListeners();
  }

  // ─────────────────────────── prayer times ───────────────────────────

  /// The convention the reader's times are computed with.
  ///
  /// Three answers in order of authority, and the middle one is the fix for a
  /// silent nightly error: a reader in Amman was being given Umm al-Qura, whose
  /// Isha is a fixed ninety minutes after Maghrib because that is the rule in
  /// Makkah. In Amman it is eight minutes late, with nothing on screen to say
  /// so.
  ///
  /// 1. What the reader chose. An answer, never overridden.
  /// 2. What their country's own authority publishes.
  /// 3. The configured default, for a country we have not been told about.
  ///
  /// Resolved on read rather than written on move, so an install that already
  /// has a location corrects itself on the next launch instead of waiting for
  /// the reader to pick their city again.
  CalculationMethod get calculationMethod {
    if (_prefs.getInt(_kCalculationMethod) case final chosen?) {
      return CalculationMethod.fromValue(chosen);
    }

    if (methodForCountry(country) case final local?) return local;

    return config.defaultCalculationMethod;
  }

  /// The reader's country: what they told us, else the nearest listed city's.
  String? get country =>
      _prefs.getString(_kCountry) ?? nearestCity(latitude, longitude)?.country;

  Future<void> setCalculationMethod(CalculationMethod method) async {
    await _prefs.setInt(_kCalculationMethod, method.value);
    notifyListeners();
  }

  /// Whether the reader has picked a convention themselves.
  ///
  /// The distinction matters when the location changes: an untouched setting is
  /// the app's guess and should follow the new country, while a deliberate
  /// choice is an answer and must not be quietly overwritten.
  bool get hasChosenCalculationMethod => _prefs.getInt(_kCalculationMethod) != null;

  Madhab get madhab => Madhab.fromValue(_prefs.getInt(_kMadhab) ?? config.defaultMadhab.value);

  Future<void> setMadhab(Madhab value) async {
    await _prefs.setInt(_kMadhab, value.value);
    notifyListeners();
  }

  /// Per-prayer correction in minutes, keyed by the prayer's name.
  ///
  /// The feature that makes the app usable next to a particular mosque, and the
  /// one almost every competitor either omits or buries.
  Map<String, int> get adjustments {
    final raw = _prefs.getString(_kAdjustments);
    if (raw == null) return const {};

    final decoded = jsonDecode(raw) as Map<String, dynamic>;
    return {for (final entry in decoded.entries) entry.key: entry.value as int};
  }

  Future<void> setAdjustment(String prayer, int minutes) async {
    final next = Map<String, int>.from(adjustments)
      ..[prayer] = minutes.clamp(-60, 60);
    await _prefs.setString(_kAdjustments, jsonEncode(next));
    notifyListeners();
  }

  /// Where the device is, when it knows. Null until the reader either grants
  /// location or picks a city — and the app works, with prayer times hidden,
  /// until then.
  double? get latitude => _prefs.getDouble(_kLatitude);
  double? get longitude => _prefs.getDouble(_kLongitude);
  String? get cityName => _prefs.getString(_kCityName);

  bool get hasLocation => latitude != null && longitude != null;

  /// Moves the reader, and with them the convention their country publishes.
  ///
  /// [country] is the whole point of the signature: the city list has always
  /// known it, and dropping it is what left a reader in Amman on Umm al-Qura —
  /// Isha eight minutes late, every night, with nothing on screen to say so.
  ///
  /// A convention the reader chose themselves is left alone — see
  /// [calculationMethod], which puts an explicit choice above everything.
  Future<void> setLocation(
    double lat,
    double lon,
    String city, {
    String? country,
  }) async {
    await _prefs.setDouble(_kLatitude, lat);
    await _prefs.setDouble(_kLongitude, lon);
    await _prefs.setString(_kCityName, city);

    // Stored rather than used to write a method: the convention is resolved on
    // read, so recording the country is enough and is exact where the
    // nearest-city guess would only be close.
    if (country != null) await _prefs.setString(_kCountry, country);

    notifyListeners();
  }

  /// ±1 day on the Hijri date. Sighting differs by country, and an app that
  /// insists otherwise is simply wrong for half its readers.
  int get hijriOffset => _prefs.getInt(_kHijriOffset) ?? 0;

  Future<void> setHijriOffset(int days) async {
    await _prefs.setInt(_kHijriOffset, days.clamp(-2, 2));
    notifyListeners();
  }

  /// When the reader usually goes to sleep, as "HH:mm". The anchor sleep adhkar
  /// hang off — see [PrayerAnchor.bedtime].
  String get bedtime => _prefs.getString(_kBedtime) ?? '22:30';

  Future<void> setBedtime(String value) async {
    await _prefs.setString(_kBedtime, value);
    notifyListeners();
  }

  // ─────────────────────────── content ───────────────────────────

  Set<int> get favourites =>
      (_prefs.getStringList(_kFavourites) ?? []).map(int.parse).toSet();

  bool isFavourite(int dhikrId) => favourites.contains(dhikrId);

  Future<void> toggleFavourite(int dhikrId) async {
    final next = favourites;
    next.contains(dhikrId) ? next.remove(dhikrId) : next.add(dhikrId);
    await _prefs.setStringList(_kFavourites, [for (final id in next) '$id']);
    notifyListeners();
  }

  /// Reminder campaigns the reader has silenced, by key.
  ///
  /// Stored as the exceptions rather than as the full set, so a campaign the
  /// admin adds later arrives switched on — which is what an admin adding one
  /// means by it.
  Set<String> get mutedReminders =>
      (_prefs.getStringList(_kMutedReminders) ?? []).toSet();

  bool isReminderMuted(String key) => mutedReminders.contains(key);

  Future<void> setReminderMuted(String key, bool muted) async {
    final next = mutedReminders;
    muted ? next.add(key) : next.remove(key);
    await _prefs.setStringList(_kMutedReminders, next.toList());
    notifyListeners();
  }

  /// Which home-screen widget the reader picked.
  ///
  /// Only a *preference*: what actually renders is this run through
  /// [WidgetSettings.resolveKind], so a kind the admin has withdrawn falls back
  /// rather than lingering on a home screen.
  WidgetKind get widgetKind => WidgetKind.fromName(_prefs.getString(_kWidgetKind));

  Future<void> setWidgetKind(WidgetKind value) async {
    await _prefs.setString(_kWidgetKind, value.name);
    notifyListeners();
  }

  /// The admin's widget rules, cached so the widget survives a cold, offline start.
  WidgetSettings get widgetSettings {
    final raw = _prefs.getString(_kWidgetSettings);
    if (raw == null) return WidgetSettings.fallback;

    try {
      return WidgetSettings.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } on FormatException {
      return WidgetSettings.fallback;
    }
  }

  Future<void> setWidgetSettings(WidgetSettings value) async {
    await _prefs.setString(_kWidgetSettings, jsonEncode(value.toJson()));
    notifyListeners();
  }

  /// The gallery, cached so the screen opens offline with something in it.
  ///
  /// Holds the settings too, because the two arrive together and a gallery
  /// cached beside stale permissions would offer the reader a customisation the
  /// admin had already withdrawn.
  WidgetCatalog get widgetCatalog {
    final raw = _prefs.getString(_kWidgetCatalog);
    if (raw == null) return WidgetCatalog.empty;

    try {
      return WidgetCatalog.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } on FormatException {
      return WidgetCatalog.empty;
    }
  }

  Future<void> setWidgetCatalog(WidgetCatalog value) async {
    await _prefs.setString(_kWidgetCatalog, jsonEncode(value.toJson()));
    // The settings ride along, so the two can never drift apart on disk.
    await _prefs.setString(_kWidgetSettings, jsonEncode(value.settings.toJson()));
    notifyListeners();
  }

  // ────────────────── the reader's own widget choices ──────────────────
  //
  // None of this reaches the server, and none of it needs to: a widget is drawn
  // on the phone from content already on the phone, so which design the reader
  // likes is not a fact anybody else has any use for. It is also the reason
  // there is no "sync my widgets" — there is nothing to sync.

  /// Which gallery entry the reader put on their home screen.
  ///
  /// Null until they choose, which is not the same as "none": the app resolves
  /// null to the admin's default and then to whatever it can draw, so a reader
  /// who never opens the gallery still gets a working widget. See
  /// [WidgetCatalog.resolveSelection].
  String? get selectedWidgetKey => _prefs.getString(_kWidgetSelected);

  Future<void> setSelectedWidgetKey(String key) async {
    await _prefs.setString(_kWidgetSelected, key);
    notifyListeners();
  }

  /// Which design of one gallery entry the reader last chose.
  ///
  /// Stored per key rather than as a list, so a widget that is withdrawn and
  /// later restored comes back to the design the reader had picked, and one
  /// they have never opened simply has no entry.
  int widgetDesign(String key, {int fallback = 0}) =>
      _designs()[key] ?? fallback;

  Future<void> setWidgetDesign(String key, int design) async {
    final designs = _designs()..[key] = design;
    await _prefs.setString(_kWidgetDesigns, jsonEncode(designs));
    notifyListeners();
  }

  Map<String, int> _designs() {
    final raw = _prefs.getString(_kWidgetDesigns);
    if (raw == null) return {};

    try {
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      return {
        for (final entry in decoded.entries)
          if (entry.value is int) entry.key: entry.value as int,
      };
    } on FormatException {
      return {};
    }
  }

  /// The colour the reader tinted their widgets with, as an ARGB value.
  ///
  /// Null means "follow the app's own palette", which is the default and what
  /// most readers will never change.
  int? get widgetBackgroundColor => _prefs.getInt(_kWidgetBackground);

  Future<void> setWidgetBackgroundColor(int? value) async {
    if (value == null) {
      await _prefs.remove(_kWidgetBackground);
    } else {
      await _prefs.setInt(_kWidgetBackground, value);
    }
    notifyListeners();
  }

  /// How opaque the widget's panel is, from 0 (invisible) to 1 (solid).
  ///
  /// Clamped on read rather than on write, so a value stored by an older build
  /// — or by a build where the admin allowed a range this one does not — cannot
  /// produce an unreadable widget.
  double get widgetOpacity {
    final stored = _prefs.getDouble(_kWidgetOpacity);
    if (stored == null) return 1;
    return stored.clamp(WidgetAppearance.minOpacity, 1).toDouble();
  }

  Future<void> setWidgetOpacity(double value) async {
    await _prefs.setDouble(
        _kWidgetOpacity, value.clamp(WidgetAppearance.minOpacity, 1).toDouble());
    notifyListeners();
  }

  /// A photograph the reader put behind the prayer widget, by file path.
  String? get widgetImagePath => _prefs.getString(_kWidgetImage);

  Future<void> setWidgetImagePath(String? path) async {
    if (path == null) {
      await _prefs.remove(_kWidgetImage);
    } else {
      await _prefs.setString(_kWidgetImage, path);
    }
    notifyListeners();
  }

  /// The reader's own pinned text, and what they say it is.
  ///
  /// The one text in this app that carries no takhrij, which is exactly why the
  /// attribution line exists: the widget prints it under the words as the
  /// reader's own note rather than letting them sit where a source would.
  String get widgetCustomText => _prefs.getString(_kWidgetCustomText) ?? '';

  String get widgetCustomAttribution =>
      _prefs.getString(_kWidgetCustomAttribution) ?? '';

  Future<void> setWidgetCustom(String text, String attribution) async {
    await _prefs.setString(_kWidgetCustomText, text.trim());
    await _prefs.setString(_kWidgetCustomAttribution, attribution.trim());
    notifyListeners();
  }

  // ─────────────────────────── cached server state ───────────────────────────

  /// The platform configuration, cached so a cold start paints the right brand
  /// before the network answers — or without it ever answering.
  AppConfig get config {
    final raw = _prefs.getString(_kConfig);
    if (raw == null) return AppConfig.fallback;

    try {
      return AppConfig.fromJson(jsonDecode(raw) as Map<String, dynamic>);
    } on FormatException {
      return AppConfig.fallback;
    }
  }

  Future<void> setConfig(AppConfig value) async {
    await _prefs.setString(_kConfig, jsonEncode(value.toJson()));
    notifyListeners();
  }

  /// The CMS's interface-copy overlay for the current language.
  Map<String, String> get stringsOverlay {
    final raw = _prefs.getString('$_kStringsOverlay.$languageCode');
    if (raw == null) return const {};

    try {
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      return {for (final entry in decoded.entries) entry.key: '${entry.value}'};
    } on FormatException {
      return const {};
    }
  }

  Future<void> setStringsOverlay(String languageCode, Map<String, String> strings) async {
    await _prefs.setString('$_kStringsOverlay.$languageCode', jsonEncode(strings));
    notifyListeners();
  }

  // ─────────────────────────── plumbing ───────────────────────────

  /// A version-4 UUID from `Random.secure`.
  ///
  /// Hand-rolled rather than pulled in as a dependency: this is the only place
  /// in the app that needs one, and it needs it exactly once per install.
  static String _mintUuid() {
    final random = DateTime.now().microsecondsSinceEpoch;
    final bytes = List<int>.generate(
      16,
      (i) => (random >> (i % 8 * 8) ^ (random * (i + 7))) & 0xFF,
    );

    bytes[6] = (bytes[6] & 0x0F) | 0x40; // version 4
    bytes[8] = (bytes[8] & 0x3F) | 0x80; // variant 1

    String hex(int start, int end) =>
        bytes.sublist(start, end).map((b) => b.toRadixString(16).padLeft(2, '0')).join();

    return '${hex(0, 4)}-${hex(4, 6)}-${hex(6, 8)}-${hex(8, 10)}-${hex(10, 16)}';
  }
}

/// Puts the store in the tree so any screen can read it without a package.
class SettingsScope extends InheritedNotifier<Settings> {
  const SettingsScope({super.key, required Settings settings, required super.child})
      : super(notifier: settings);

  static Settings of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<SettingsScope>()!.notifier!;

  /// For callers that want the store but must not rebuild when it changes —
  /// an event handler, say, rather than a `build`.
  static Settings read(BuildContext context) =>
      context.getInheritedWidgetOfExactType<SettingsScope>()!.notifier!;
}
