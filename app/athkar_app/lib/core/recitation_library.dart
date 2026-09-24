import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// A surah of a recording, as the reader's own lists refer to it.
///
/// Keyed on the reciter's stable key and the recording's id, so a reciter
/// whose name an editor corrects keeps his place in somebody's bookmarks.
@immutable
class SurahRef {
  const SurahRef({required this.reciterKey, required this.recitationId, required this.surah});

  final String reciterKey;
  final int recitationId;
  final int surah;

  String get id => '$reciterKey|$recitationId|$surah';

  static SurahRef? parse(String raw) {
    final parts = raw.split('|');
    if (parts.length != 3) return null;
    final recitationId = int.tryParse(parts[1]);
    final surah = int.tryParse(parts[2]);
    if (recitationId == null || surah == null) return null;
    return SurahRef(reciterKey: parts[0], recitationId: recitationId, surah: surah);
  }

  @override
  bool operator ==(Object other) => other is SurahRef && other.id == id;

  @override
  int get hashCode => id.hashCode;
}

/// Where the reader left off, so the listening tab can offer to continue.
@immutable
class ListeningResume {
  const ListeningResume({required this.ref, required this.position});

  final SurahRef ref;
  final Duration position;
}

/// The reader's own listening state: bookmarks, and where they stopped.
///
/// All of it stays on the device. This app does not learn what anybody
/// listens to — there is no endpoint that could be told, which is the only
/// kind of promise about that worth making.
class RecitationLibrary extends ChangeNotifier {
  RecitationLibrary._();

  static final RecitationLibrary instance = RecitationLibrary._();

  static const _kBookmarks = 'listen.bookmarks';
  static const _kResume = 'listen.resume';

  SharedPreferences? _prefs;
  var _bookmarks = <String>[];

  Future<void> load() async {
    final prefs = _prefs ??= await SharedPreferences.getInstance();
    _bookmarks = prefs.getStringList(_kBookmarks) ?? [];
    notifyListeners();
  }

  // ── bookmarks ──

  /// Most recent first.
  List<SurahRef> get bookmarks => [
        for (final raw in _bookmarks.reversed)
          if (SurahRef.parse(raw) case final ref?) ref,
      ];

  bool isBookmarked(SurahRef ref) => _bookmarks.contains(ref.id);

  Future<void> toggleBookmark(SurahRef ref) async {
    if (!_bookmarks.remove(ref.id)) _bookmarks.add(ref.id);
    notifyListeners();
    await _prefs?.setStringList(_kBookmarks, _bookmarks);
  }

  // ── where the reader stopped ──

  ListeningResume? get resume {
    final raw = _prefs?.getString(_kResume);
    if (raw == null) return null;

    try {
      final json = jsonDecode(raw) as Map<String, dynamic>;
      final ref = SurahRef.parse(json['ref'] as String? ?? '');
      if (ref == null) return null;
      return ListeningResume(
        ref: ref,
        position: Duration(milliseconds: json['ms'] as int? ?? 0),
      );
    } on FormatException {
      return null;
    }
  }

  /// Saved every few seconds while playing, and on pause. Not notified: the
  /// continue card reads it when the tab is drawn, and a rebuild per second
  /// of listening would be the whole tab repainting for nothing.
  Future<void> saveResume(SurahRef ref, Duration position) async {
    await _prefs?.setString(
      _kResume,
      jsonEncode({'ref': ref.id, 'ms': position.inMilliseconds}),
    );
  }

  Future<void> clearResume() async {
    await _prefs?.remove(_kResume);
    notifyListeners();
  }
}
