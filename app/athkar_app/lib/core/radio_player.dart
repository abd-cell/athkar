import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:just_audio/just_audio.dart';
import 'package:just_audio_background/just_audio_background.dart';

import '../models/models.dart';
import 'audio_engine.dart';

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

/// The live radio.
///
/// A singleton because there is one pair of speakers: starting a second station
/// must stop the first, and that is only expressible if one object knows about
/// both. Screens listen to it and read [station] and [state]. It plays through
/// the app's one shared player — see [AudioEngine] — and steps back to idle
/// the moment a recitation takes that player over.
///
/// A live stream is *stopped*, never paused. Pausing keeps a position in a
/// broadcast that has moved on, so resuming would play the past and then jump —
/// so play always re-opens the URL and lands on what is being broadcast now.
class RadioPlayer extends ChangeNotifier {
  RadioPlayer._() {
    _subscription = _player.playerStateStream.listen(_onPlayerState);
    _claims = AudioEngine.instance.claims.listen((owner) {
      // Somebody else has the speakers now. Not a stop — the player is theirs
      // to drive — only this card going back to its resting state.
      if (!identical(owner, this)) {
        _failed = false;
        _set(RadioPlaybackState.idle);
      }
    });
  }

  static final RadioPlayer instance = RadioPlayer._();

  AudioPlayer get _player => AudioEngine.instance.player;
  bool get _mine => identical(AudioEngine.instance.owner, this);

  StreamSubscription<PlayerState>? _subscription;
  StreamSubscription<Object>? _claims;

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
    AudioEngine.instance.claim(this);
    _set(RadioPlaybackState.connecting);

    try {
      // Stopped rather than left running while the next one loads: two streams
      // decoding at once is two streams coming out of the speaker.
      await _player.stop();
      final uri = Uri.parse(station.streamUrl);
      await _player.setAudioSource(
        AudioSource.uri(
          uri,
          // The lock screen and the notification shade name what is playing.
          tag: AudioEngine.needsMediaItems
              ? MediaItem(
                  id: 'radio:${station.key}',
                  title: station.name,
                  artist: station.provider,
                  artUri: station.logoUrl == null ? null : Uri.tryParse(station.logoUrl!),
                )
              : null,
        ),
      );
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

    // A recitation that has taken the player is not this card's to stop.
    if (!_mine) return;

    try {
      await _player.stop();
    } catch (_) {
      // Stopping a player that is already stopped is not an error worth having.
    }
  }

  void _onPlayerState(PlayerState playerState) {
    if (!_mine) return;

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
    _claims?.cancel();
    // The player is shared; the engine outlives this.
    super.dispose();
  }
}
