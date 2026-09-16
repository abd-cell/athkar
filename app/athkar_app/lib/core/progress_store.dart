import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// What the reader has done, kept entirely on this device.
///
/// Counters, streaks and totals never leave the phone — there is no account to
/// attach them to and no analytics endpoint to send them at. That is a design
/// decision, not a gap: see the privacy section of `docs/BUSINESS_LOGIC.md`.
///
/// The streak is deliberately **encouraging and never reproachful**. It records
/// the longest run and the current one, and nothing anywhere reads "you broke
/// your streak" — a person who missed a day of dhikr does not need an app to
/// point it out.
class ProgressStore {
  ProgressStore._(this._prefs);

  final SharedPreferences _prefs;

  static const _kSessionState = 'progress.session';
  static const _kCompletions = 'progress.completions';
  static const _kTotalDhikr = 'progress.totalDhikr';
  static const _kSessionCount = 'progress.sessions';
  static const _kCurrentStreak = 'progress.streak.current';
  static const _kLongestStreak = 'progress.streak.longest';
  static const _kLastActiveDay = 'progress.streak.lastDay';
  static const _kTasbih = 'tasbih.counters';

  static Future<ProgressStore> load() async =>
      ProgressStore._(await SharedPreferences.getInstance());

  // ─────────────────────────── a session in progress ───────────────────────────

  /// Where the reader stopped in a chapter, as `{dhikrIndex, count}`.
  ///
  /// Stored per chapter and per *day* for a daily chapter, which is what makes
  /// "resume where I left off" and "start fresh tomorrow" the same mechanism
  /// rather than two features that disagree at midnight.
  (int index, int count)? sessionState(int categoryId, {required bool daily}) {
    final raw = _prefs.getString('$_kSessionState.$categoryId');
    if (raw == null) return null;

    try {
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      if (daily && decoded['day'] != _today) return null;

      return (decoded['index'] as int? ?? 0, decoded['count'] as int? ?? 0);
    } on FormatException {
      return null;
    }
  }

  Future<void> saveSessionState(int categoryId, int index, int count) =>
      _prefs.setString(
        '$_kSessionState.$categoryId',
        jsonEncode({'day': _today, 'index': index, 'count': count}),
      );

  Future<void> clearSessionState(int categoryId) =>
      _prefs.remove('$_kSessionState.$categoryId');

  // ─────────────────────────── completions ───────────────────────────

  /// Chapters finished today, by id.
  Set<int> get completedToday {
    final raw = _prefs.getString(_kCompletions);
    if (raw == null) return {};

    try {
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      if (decoded['day'] != _today) return {};

      return {for (final id in (decoded['ids'] as List<dynamic>? ?? [])) id as int};
    } on FormatException {
      return {};
    }
  }

  /// Records a finished chapter, and moves the streak on if this is the first
  /// one today.
  Future<void> markCompleted(int categoryId, int dhikrCount) async {
    final ids = completedToday..add(categoryId);

    await _prefs.setString(
      _kCompletions,
      jsonEncode({'day': _today, 'ids': ids.toList()}),
    );

    await _prefs.setInt(_kSessionCount, sessionCount + 1);
    await _prefs.setInt(_kTotalDhikr, totalDhikr + dhikrCount);
    await _touchStreak();
  }

  int get sessionCount => _prefs.getInt(_kSessionCount) ?? 0;
  int get totalDhikr => _prefs.getInt(_kTotalDhikr) ?? 0;
  int get currentStreak => _prefs.getInt(_kCurrentStreak) ?? 0;
  int get longestStreak => _prefs.getInt(_kLongestStreak) ?? 0;

  /// Advances the streak.
  ///
  /// The gap is measured in days, so a reader in a different timezone or one
  /// who reads at 23:59 and again at 00:01 is treated the way they would expect
  /// rather than the way the clock would.
  Future<void> _touchStreak() async {
    final last = _prefs.getString(_kLastActiveDay);
    if (last == _today) return;

    final yesterday = _dayKey(DateTime.now().subtract(const Duration(days: 1)));
    final next = last == yesterday ? currentStreak + 1 : 1;

    await _prefs.setInt(_kCurrentStreak, next);
    await _prefs.setString(_kLastActiveDay, _today);

    if (next > longestStreak) await _prefs.setInt(_kLongestStreak, next);
  }

  // ─────────────────────────── tasbih ───────────────────────────

  /// The free-standing counters, by name.
  Map<String, int> get tasbihCounters {
    final raw = _prefs.getString(_kTasbih);
    if (raw == null) return const {};

    try {
      final decoded = jsonDecode(raw) as Map<String, dynamic>;
      return {for (final entry in decoded.entries) entry.key: entry.value as int};
    } on FormatException {
      return const {};
    }
  }

  Future<void> setTasbihCounter(String name, int value) async {
    final next = Map<String, int>.from(tasbihCounters)..[name] = value;
    await _prefs.setString(_kTasbih, jsonEncode(next));
  }

  static String get _today => _dayKey(DateTime.now());

  static String _dayKey(DateTime date) =>
      '${date.year}-${date.month.toString().padLeft(2, '0')}-'
      '${date.day.toString().padLeft(2, '0')}';
}
