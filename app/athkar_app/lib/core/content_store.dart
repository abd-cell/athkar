import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../models/models.dart';

/// The catalogue, cached on the device.
///
/// The app's contract with its reader is that everything works offline, so the
/// corpus is not fetched per screen — it is synced once and read locally from
/// then on. A few hundred kilobytes of JSON in preferences is the right storage
/// for that: it is read whole at launch, written whole on a sync, and never
/// queried.
///
/// The Qur'an is the exception and lives in SQLite, because it is two orders of
/// magnitude larger and *is* queried. See `quran_store.dart`.
class ContentStore {
  ContentStore._(this._prefs);

  final SharedPreferences _prefs;

  static const _kCatalog = 'content.catalog';
  static const _kRadios = 'content.radios';
  static const _kReciters = 'content.reciters';
  static const _kVersion = 'content.version';
  static const _kLanguage = 'content.language';

  static Future<ContentStore> load() async =>
      ContentStore._(await SharedPreferences.getInstance());

  /// The version currently on the device, or null when nothing has synced.
  int? get version => _prefs.getInt(_kVersion);

  /// Which language the cache holds. A language switch invalidates it — the
  /// cached payload is one language's text, not all of them.
  String? get languageCode => _prefs.getString(_kLanguage);

  bool get isEmpty => categories.isEmpty;

  /// Whether a sync is needed for [language] at [serverVersion].
  ///
  /// A cache that has never held the reciters is out of date whatever its
  /// version says: an install that updates to a build which knows about them
  /// would otherwise be told "nothing has changed" by a server whose version
  /// has not moved since, and show an empty listening tab until an editor
  /// happened to publish something.
  bool needsSync(String language, int serverVersion) =>
      languageCode != language ||
      version != serverVersion ||
      !isComplete;

  /// Whether the cache holds every part of the payload this build reads.
  bool get isComplete => !isEmpty && _prefs.containsKey(_kReciters);

  List<AthkarCategory> get categories {
    final raw = _prefs.getString(_kCatalog);
    if (raw == null) return const [];

    try {
      final decoded = jsonDecode(raw) as List<dynamic>;
      return [
        for (final entry in decoded) AthkarCategory.fromJson(entry as Map<String, dynamic>),
      ];
    } on FormatException {
      // A cache this app cannot read is a cache it should stop trying to read.
      _prefs.remove(_kCatalog);
      return const [];
    }
  }

  /// The live stations, as last synced. Cached so the list is on the home
  /// screen at launch with no network; only pressing play needs one.
  List<RadioStation> get radios {
    final raw = _prefs.getString(_kRadios);
    if (raw == null) return const [];

    try {
      final decoded = jsonDecode(raw) as List<dynamic>;
      return [
        for (final entry in decoded) RadioStation.fromJson(entry as Map<String, dynamic>),
      ];
    } on FormatException {
      _prefs.remove(_kRadios);
      return const [];
    }
  }

  /// The published reciters, as last synced — browsable offline.
  List<Reciter> get reciters {
    final raw = _prefs.getString(_kReciters);
    if (raw == null) return const [];

    try {
      final decoded = jsonDecode(raw) as List<dynamic>;
      return [
        for (final entry in decoded) Reciter.fromJson(entry as Map<String, dynamic>),
      ];
    } on FormatException {
      _prefs.remove(_kReciters);
      return const [];
    }
  }

  Reciter? reciterByKey(String key) {
    for (final reciter in reciters) {
      if (reciter.key == key) return reciter;
    }
    return null;
  }

  Future<void> save(Catalog catalog) async {
    await _prefs.setString(
      _kCatalog,
      jsonEncode([for (final category in catalog.categories) category.toJson()]),
    );
    // Written even when empty: an admin withdrawing the last station must take
    // the section off the home screen, not leave yesterday's cache on it.
    await _prefs.setString(
      _kRadios,
      jsonEncode([for (final station in catalog.radios) station.toJson()]),
    );
    // Written even when empty, for the same reason as the stations.
    await _prefs.setString(
      _kReciters,
      jsonEncode([for (final reciter in catalog.reciters) reciter.toJson()]),
    );
    await _prefs.setInt(_kVersion, catalog.version);
    await _prefs.setString(_kLanguage, catalog.languageCode);
  }

  AthkarCategory? byKey(String key) {
    for (final category in categories) {
      if (category.key == key) return category;
    }
    return null;
  }

  AthkarCategory? byId(int id) {
    for (final category in categories) {
      if (category.id == id) return category;
    }
    return null;
  }

  Dhikr? dhikrById(int id) {
    for (final category in categories) {
      for (final dhikr in category.adhkar) {
        if (dhikr.id == id) return dhikr;
      }
    }
    return null;
  }

  /// The chapters the reader is most likely to want right now.
  ///
  /// Driven by the chapter's own anchor rather than by a setting: morning
  /// adhkar belong to the morning whoever is reading, and asking somebody to
  /// configure that would be asking them to state the obvious.
  List<AthkarCategory> suggestedFor(DateTime now) {
    final hour = now.hour;

    final wanted = switch (hour) {
      >= 4 && < 11 => PrayerAnchor.sunrise,
      >= 11 && < 15 => PrayerAnchor.dhuhr,
      >= 15 && < 19 => PrayerAnchor.asr,
      >= 19 && < 22 => PrayerAnchor.maghrib,
      _ => PrayerAnchor.bedtime,
    };

    final matching = [for (final c in categories) if (c.anchor == wanted) c];
    if (matching.isNotEmpty) return matching;

    // Nothing is anchored to this hour, which is normal for most of the day.
    // Falling back to the first published chapters is better than an empty
    // card, and the reader can always open the index.
    return categories.take(3).toList();
  }
}
