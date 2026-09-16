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
  bool needsSync(String language, int serverVersion) =>
      languageCode != language || version != serverVersion || isEmpty;

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

  Future<void> save(Catalog catalog) async {
    await _prefs.setString(
      _kCatalog,
      jsonEncode([for (final category in catalog.categories) category.toJson()]),
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
