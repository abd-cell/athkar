import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:just_audio/just_audio.dart';

import '../models/models.dart';

/// What the radio card is showing right now.
enum RadioPlaybackState {
  /// Nothing is playing. The ordinary state, and the one a failure returns to.
  idle,

  /// Asked to play and waiting for the first audio. A live stream over a poor
  /// connection sits here for several seconds, which is why it is a state of
  /// its own rather than an optimistic "playing".
  connecting,

  playing,
}

/// The one audio player in the app.
///
/// A singleton because there is one pair of speakers: starting a second station
/// must stop the first, and that is only expressible if one object knows about
/// both. Screens listen to it and read [station] and [state]; nothing else
/// touches [AudioPlayer].
///
/// A live stream is *stopped*, never paused. Pausing keeps a position in a
/// broadcast that has moved on, so resuming would play the past and then jump —
/// so play always re-opens the URL and lands on what is being broadcast now.
class RadioPlayer extends ChangeNotifier {
  RadioPlayer._() {
    _subscription = _player.playerStateStream.listen(_onPlayerState);
  }

  static final RadioPlayer instance = RadioPlayer._();

  final AudioPlayer _player = AudioPlayer();
  StreamSubscription<PlayerState>? _subscription;

  RadioStation? _station;
  RadioPlaybackState _state = RadioPlaybackState.idle;
  bool _failed = false;

  /// The station being played, or the last one tried. Null before anything has
  /// been pressed.
  RadioStation? get station => _station;

  RadioPlaybackState get state => _state;

  /// True when the last attempt could not be played — no network, a stream that
  /// has moved, a server that answered with something that is not audio. Reset
  /// the moment anything is pressed again.
  bool get failed => _failed;

  bool isBusy(RadioStation candidate) =>
      _station?.id == candidate.id && _state != RadioPlaybackState.idle;

  bool isPlaying(RadioStation candidate) =>
      _station?.id == candidate.id && _state == RadioPlaybackState.playing;

  /// Starts [station], or stops it if it is the one already on.
  Future<void> toggle(RadioStation station) async {
    if (isBusy(station)) {
      await stop();
      return;
    }

    _station = station;
    _failed = false;
    _set(RadioPlaybackState.connecting);

    try {
      // Stopped rather than left running while the next one loads: two streams
      // decoding at once is two streams coming out of the speaker.
      await _player.stop();
      await _player.setUrl(station.streamUrl);
      await _player.play();
    } catch (_) {
      // Every failure here is the same failure to the reader — it did not
      // play — and the exceptions just_audio raises are platform detail. The
      // card says so and offers the button again.
      _failed = true;
      _set(RadioPlaybackState.idle);
    }
  }

  Future<void> stop() async {
    _failed = false;
    _set(RadioPlaybackState.idle);

    try {
      await _player.stop();
    } catch (_) {
      // Stopping a player that is already stopped is not an error worth having.
    }
  }

  void _onPlayerState(PlayerState playerState) {
    // A stream that ends on its own — the broadcaster dropped, or the network
    // did — comes back as `completed` rather than as an error. To the reader it
    // is the same thing: the sound stopped.
    if (playerState.processingState == ProcessingState.completed) {
      _set(RadioPlaybackState.idle);
      return;
    }

    if (!playerState.playing) return;

    _set(switch (playerState.processingState) {
      ProcessingState.ready => RadioPlaybackState.playing,
      ProcessingState.idle => RadioPlaybackState.idle,
      _ => RadioPlaybackState.connecting,
    });
  }

  void _set(RadioPlaybackState next) {
    if (_state == next) return;
    _state = next;
    notifyListeners();
  }

  @override
  void dispose() {
    _subscription?.cancel();
    _player.dispose();
    super.dispose();
  }
}
