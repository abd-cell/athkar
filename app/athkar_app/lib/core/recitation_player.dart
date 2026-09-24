import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart' show TimeOfDay;
import 'package:just_audio/just_audio.dart';
import 'package:just_audio_background/just_audio_background.dart';

import '../models/models.dart';
import 'audio_engine.dart';
import 'ayah_timing.dart';
import 'recitation_downloads.dart';
import 'recitation_library.dart';
import 'surah_names.dart';

/// What happens when a surah ends.
enum RecitationRepeat {
  /// Go on to the next surah in the queue, and stop after the last.
  off,

  /// Recite this surah again.
  surah,

  /// Go on, and start the queue over after the last surah.
  all,
}

/// A stretch of ayat recited over and over — how a reader memorises.
@immutable
class AyahRange {
  const AyahRange({required this.from, required this.to, this.times = 0});

  final int from;
  final int to;

  /// How many times through. Zero: until the reader stops it.
  final int times;
}

/// Playing recitations.
///
/// A singleton for the reason the radio is one: there is one pair of speakers,
/// and the mini player, the reciter's page and the full player must all agree
/// about what is on. It plays through the app's one shared player
/// ([AudioEngine]) and gives up its session the moment the radio takes the
/// player over.
///
/// A surah is played from the saved file when the reader downloaded it, and
/// streamed from the publisher otherwise — the same queue can mix the two.
class RecitationPlayer extends ChangeNotifier {
  RecitationPlayer._() {
    _subscriptions
      ..add(_player.playerStateStream.listen(_onState))
      ..add(_player.currentIndexStream.listen(_onIndex))
      ..add(_player.positionStream.listen(_onPosition))
      ..add(_player.playbackEventStream.listen((_) {}, onError: _onError))
      ..add(AudioEngine.instance.claims.listen(_onClaim));
  }

  static final RecitationPlayer instance = RecitationPlayer._();

  AudioPlayer get _player => AudioEngine.instance.player;
  bool get _mine => identical(AudioEngine.instance.owner, this);

  final _subscriptions = <StreamSubscription<Object?>>[];

  Reciter? _reciter;
  Recitation? _recitation;
  List<int> _queue = const [];
  var _index = 0;

  var _active = false;
  var _playing = false;
  var _loading = false;
  var _failed = false;

  var _speed = 1.0;
  var _repeat = RecitationRepeat.off;

  DateTime? _sleepAt;
  var _sleepAtEnd = false;
  Timer? _sleepTimer;

  List<AyahTiming>? _timings;
  var _timingLoading = false;
  int? _ayah;

  AyahRange? _range;
  var _rangePass = 0;

  var _languageCode = 'ar';
  var _lastSaved = DateTime.fromMillisecondsSinceEpoch(0);

  // ── what the screens read ──

  /// Whether a recitation session exists — what the mini player is shown for.
  bool get isActive => _active;
  bool get isPlaying => _playing;
  bool get isLoading => _loading;

  /// The last attempt could not play: no network, a folder that moved.
  bool get failed => _failed;

  Reciter? get reciter => _reciter;
  Recitation? get recitation => _recitation;
  List<int> get queue => _queue;
  int? get surah => _active && _index < _queue.length ? _queue[_index] : null;

  double get speed => _speed;
  RecitationRepeat get repeat => _repeat;

  DateTime? get sleepAt => _sleepAt;
  bool get sleepsAtEndOfSurah => _sleepAtEnd;
  bool get hasSleepTimer => _sleepAt != null || _sleepAtEnd;

  /// Null when the recording has no timing, or it has not arrived yet.
  List<AyahTiming>? get timings => _timings;
  bool get timingLoading => _timingLoading;
  bool get hasTiming => _recitation?.hasTiming ?? false;

  /// The ayah being recited now, when there is timing to say so.
  int? get ayah => _ayah;

  AyahRange? get range => _range;

  Stream<Duration> get positionStream => _player.positionStream;
  Stream<Duration?> get durationStream => _player.durationStream;
  Duration get position => _player.position;
  Duration? get duration => _player.duration;

  bool get hasNext => _index + 1 < _queue.length || _repeat == RecitationRepeat.all;
  bool get hasPrevious => _index > 0;

  SurahRef? get ref => switch ((_reciter, _recitation, surah)) {
        (final reciter?, final recitation?, final surah?) =>
          SurahRef(reciterKey: reciter.key, recitationId: recitation.id, surah: surah),
        _ => null,
      };

  bool isCurrent(Recitation recitation, int surah) =>
      _active && _recitation?.id == recitation.id && this.surah == surah;

  // ── controls ──

  /// Plays [surah] of [recitation], with [queue] after it — by default the rest
  /// of the recording, which is what «تشغيل الكل» means and what a reader who
  /// taps one surah and walks away would expect to keep hearing.
  Future<void> play(
    Reciter reciter,
    Recitation recitation,
    int surah, {
    List<int>? queue,
    Duration? startAt,
    String languageCode = 'ar',
  }) async {
    var list = [
      for (final number in queue ?? recitation.surahs)
        if (recitation.surahs.contains(number)) number,
    ];
    if (!list.contains(surah)) list = [surah];

    AudioEngine.instance.claim(this);

    _reciter = reciter;
    _recitation = recitation;
    _queue = list;
    _index = list.indexOf(surah);
    _languageCode = languageCode;
    _active = true;
    _failed = false;
    _loading = true;
    _playing = false;
    _clearSurahState();
    notifyListeners();

    try {
      await _player.stop();

      final sources = <AudioSource>[
        for (final number in list) await _sourceFor(reciter, recitation, number),
      ];

      await _player.setAudioSources(sources, initialIndex: _index, initialPosition: startAt);
      await _player.setSpeed(_speed);
      await _player.setLoopMode(_loopMode);

      unawaited(_loadTiming());
      unawaited(_player.play().catchError((Object error) => _onError(error)));
    } catch (error) {
      _onError(error);
    }
  }

  Future<void> toggle() async {
    if (!_active) return;

    if (_playing) {
      await pause();
      return;
    }

    // No "is the player still mine" case here: the radio taking the player
    // ends this session (see `_onClaim`), so an active session always owns it.
    if (_player.processingState == ProcessingState.completed) {
      await _player.seek(Duration.zero, index: _index);
    }

    _failed = false;
    unawaited(_player.play().catchError((Object error) => _onError(error)));
  }

  Future<void> pause() async {
    if (!_mine) return;
    await _player.pause();
    await _saveResume(force: true);
  }

  Future<void> seek(Duration position) async {
    if (!_mine) return;
    await _player.seek(position);
  }

  Future<void> seekBy(Duration delta) async {
    final length = duration ?? Duration.zero;
    final target = position + delta;
    await seek(target < Duration.zero
        ? Duration.zero
        : (length > Duration.zero && target > length ? length : target));
  }

  Future<void> next() async {
    if (!_mine) return;
    if (_index + 1 < _queue.length) {
      await _player.seek(Duration.zero, index: _index + 1);
    } else if (_repeat == RecitationRepeat.all && _queue.isNotEmpty) {
      await _player.seek(Duration.zero, index: 0);
    }
  }

  /// Back to the start of this surah, or — pressed in its first seconds — to
  /// the one before, which is how every player a reader has used behaves.
  Future<void> previous() async {
    if (!_mine) return;
    if (position > const Duration(seconds: 3) || _index == 0) {
      await _player.seek(Duration.zero);
    } else {
      await _player.seek(Duration.zero, index: _index - 1);
    }
  }

  /// Jumps to [surah] within the current queue, if it is in it.
  Future<void> jumpTo(int surah) async {
    final target = _queue.indexOf(surah);
    if (!_mine || target < 0) return;
    await _player.seek(Duration.zero, index: target);
    if (!_playing) unawaited(_player.play().catchError((Object error) => _onError(error)));
  }

  /// Ends the session — the mini player's ✕.
  Future<void> stop() async {
    await _saveResume(force: true);
    cancelSleepTimer();
    _active = false;
    _playing = false;
    _loading = false;
    _clearSurahState();
    notifyListeners();
    if (_mine) await _player.stop();
  }

  static const speeds = [0.75, 1.0, 1.25, 1.5, 1.75, 2.0];

  Future<void> setSpeed(double speed) async {
    _speed = speed;
    notifyListeners();
    if (_mine) await _player.setSpeed(speed);
  }

  Future<void> cycleRepeat() async {
    _repeat = RecitationRepeat.values[(_repeat.index + 1) % RecitationRepeat.values.length];
    notifyListeners();
    if (_mine) await _player.setLoopMode(_loopMode);
  }

  LoopMode get _loopMode => switch (_repeat) {
        RecitationRepeat.off => LoopMode.off,
        RecitationRepeat.surah => LoopMode.one,
        RecitationRepeat.all => LoopMode.all,
      };

  // ── repeating a stretch of ayat ──

  /// Repeats ayat [from]–[to] of the current surah. Needs timing; without it
  /// there is no way to know where an ayah is, and the control is not offered.
  Future<void> setRange(int from, int to, {int times = 0}) async {
    final timings = _timings;
    if (timings == null) return;

    final low = math.min(from, to), high = math.max(from, to);
    final start = AyahTimings.of(timings, low);
    if (start == null || AyahTimings.of(timings, high) == null) return;

    _range = AyahRange(from: low, to: high, times: times);
    _rangePass = 0;
    notifyListeners();

    await seek(start.start);
    if (!_playing) unawaited(_player.play().catchError((Object error) => _onError(error)));
  }

  void clearRange() {
    _range = null;
    _rangePass = 0;
    notifyListeners();
  }

  // ── the sleep timer ──

  void sleepAfter(Duration duration) {
    _sleepAtEnd = false;
    _sleepAt = DateTime.now().add(duration);
    _armSleep();
  }

  void sleepAtClock(TimeOfDay time) {
    _sleepAtEnd = false;
    _sleepAt = nextOccurrence(DateTime.now(), time.hour, time.minute);
    _armSleep();
  }

  void sleepAtEndOfSurah() {
    _sleepTimer?.cancel();
    _sleepAt = null;
    _sleepAtEnd = true;
    notifyListeners();
  }

  void cancelSleepTimer() {
    _sleepTimer?.cancel();
    _sleepTimer = null;
    _sleepAt = null;
    _sleepAtEnd = false;
    notifyListeners();
  }

  /// The next time the clock reads [hour]:[minute] — today if that is still
  /// ahead, tomorrow otherwise. «توقف عند ١١:٣٠» set at 23:40 means tomorrow
  /// night, not a timer that has already fired.
  static DateTime nextOccurrence(DateTime now, int hour, int minute) {
    var at = DateTime(now.year, now.month, now.day, hour, minute);
    if (!at.isAfter(now)) at = at.add(const Duration(days: 1));
    return at;
  }

  void _armSleep() {
    _sleepTimer?.cancel();
    final at = _sleepAt;
    if (at == null) return;

    final wait = at.difference(DateTime.now());
    _sleepTimer = Timer(wait.isNegative ? Duration.zero : wait, () {
      unawaited(pause());
      cancelSleepTimer();
    });
    notifyListeners();
  }

  // ── plumbing ──

  Future<AudioSource> _sourceFor(Reciter reciter, Recitation recitation, int surah) async {
    final saved = await RecitationDownloads.instance.fileOf(recitation.id, surah);
    final uri = saved != null ? Uri.file(saved.path) : Uri.parse(recitation.urlOf(surah));

    return AudioSource.uri(
      uri,
      tag: AudioEngine.needsMediaItems
          ? MediaItem(
              id: 'recitation:${recitation.id}:$surah',
              title: titleOf(surah, _languageCode),
              artist: reciter.name,
              // The publisher is named on the lock screen too: it is their audio.
              album: '${recitation.name} · ${recitation.sourceName}',
              artUri: reciter.imageUrl == null ? null : Uri.tryParse(reciter.imageUrl!),
            )
          : null,
    );
  }

  static String titleOf(int surah, String languageCode) {
    final name = surahName(surah);
    if (name == null) return '$surah';
    return languageCode == 'ar' ? 'سورة ${name.arabic}' : 'Surah ${name.english}';
  }

  Future<void> _loadTiming() async {
    final recitation = _recitation;
    final number = surah;
    if (recitation == null || number == null || !recitation.hasTiming) return;

    _timingLoading = true;
    notifyListeners();

    final timings = await AyahTimingCache.instance.load(recitation, number);

    // The reader may have moved on while it was fetched.
    if (_recitation?.id != recitation.id || surah != number) return;

    _timings = timings;
    _timingLoading = false;
    _ayah = timings == null ? null : AyahTimings.ayahAt(timings, position);
    notifyListeners();
  }

  void _clearSurahState() {
    _timings = null;
    _timingLoading = false;
    _ayah = null;
    _range = null;
    _rangePass = 0;
  }

  void _onState(PlayerState state) {
    if (!_mine || !_active) return;

    final completed = state.processingState == ProcessingState.completed;
    final playing = state.playing && !completed;
    final loading = state.playing &&
        (state.processingState == ProcessingState.loading ||
            state.processingState == ProcessingState.buffering);

    if (completed) {
      // The end of the queue. Where the reader stopped is the start of the
      // last surah, not its final second, or "continue" would play silence.
      unawaited(_saveResume(force: true, position: Duration.zero));
      if (_sleepAtEnd) cancelSleepTimer();
    }

    if (playing != _playing || loading != _loading) {
      _playing = playing;
      _loading = loading;
      if (playing) _failed = false;
      notifyListeners();
    }
  }

  void _onIndex(int? index) {
    if (!_mine || !_active || index == null || index == _index) return;

    if (_sleepAtEnd) {
      // The surah the reader asked to stop after has ended.
      unawaited(_player.pause());
      unawaited(_player.seek(Duration.zero, index: index));
      cancelSleepTimer();
    }

    _index = index;
    _clearSurahState();
    notifyListeners();
    unawaited(_loadTiming());
    unawaited(_saveResume(force: true));
  }

  void _onPosition(Duration position) {
    if (!_mine || !_active) return;

    final timings = _timings;

    final range = _range;
    if (range != null && timings != null) {
      final end = AyahTimings.of(timings, range.to)?.end;
      final start = AyahTimings.of(timings, range.from)?.start;
      if (end != null && start != null && position >= end) {
        _rangePass++;
        if (range.times == 0 || _rangePass < range.times) {
          unawaited(_player.seek(start));
          return;
        }
        _range = null;
        notifyListeners();
      }
    }

    if (_sleepAtEnd && _repeat == RecitationRepeat.surah) {
      // Repeating one surah never changes the index, so "stop at the end of
      // this surah" has to watch the clock instead.
      final length = duration;
      if (length != null && length > Duration.zero && position >= length - const Duration(milliseconds: 600)) {
        unawaited(pause());
        unawaited(_player.seek(Duration.zero));
        cancelSleepTimer();
      }
    }

    if (timings != null) {
      final ayah = AyahTimings.ayahAt(timings, position);
      if (ayah != _ayah) {
        _ayah = ayah;
        notifyListeners();
      }
    }

    unawaited(_saveResume());
  }

  void _onError(Object error) {
    if (!_active) return;
    assert(() {
      debugPrint('[recitations] playback failed: $error');
      return true;
    }());
    _failed = true;
    _loading = false;
    _playing = false;
    notifyListeners();
  }

  void _onClaim(Object owner) {
    if (identical(owner, this) || !_active) return;
    // The radio has the speakers. The session is gone with the queue it
    // loaded; the mini player goes, and "continue" on the listening tab is
    // how the reader comes back.
    unawaited(_saveResume(force: true));
    cancelSleepTimer();
    _active = false;
    _playing = false;
    _loading = false;
    _clearSurahState();
    notifyListeners();
  }

  Future<void> _saveResume({bool force = false, Duration? position}) async {
    final current = ref;
    if (current == null) return;

    final now = DateTime.now();
    if (!force && now.difference(_lastSaved) < const Duration(seconds: 5)) return;
    _lastSaved = now;

    await RecitationLibrary.instance.saveResume(current, position ?? this.position);
  }

  @override
  void dispose() {
    for (final subscription in _subscriptions) {
      subscription.cancel();
    }
    _sleepTimer?.cancel();
    super.dispose();
  }
}
