import 'dart:async';

import 'package:audio_session/audio_session.dart';
import 'package:flutter/foundation.dart';
import 'package:just_audio/just_audio.dart';
import 'package:just_audio_background/just_audio_background.dart';

import 'theme.dart';

/// The app's one audio player, shared by the radio and the recitations.
///
/// One because there is one pair of speakers, and because the background
/// service that keeps a recitation playing with the screen off
/// (`just_audio_background`) serves exactly one player — a second would be
/// refused outright. So both [RadioPlayer] and [RecitationPlayer] play through
/// [player], and whichever starts [claim]s it; the other hears about it on
/// [claims] and goes back to idle, so the radio card never shows "playing"
/// while a surah is coming out of the speaker.
class AudioEngine {
  AudioEngine._();

  static final AudioEngine instance = AudioEngine._();

  AudioPlayer? _player;
  Object? _owner;
  final _claims = StreamController<Object>.broadcast();

  /// Whether [initialize] ran. Tests and the web build may never call it; the
  /// player is then created bare, without the lock-screen integration.
  static var _backgroundReady = false;

  AudioPlayer get player => _player ??= AudioPlayer();

  /// Who is using [player] right now.
  Object? get owner => _owner;

  /// Fires with the new owner every time the player changes hands.
  Stream<Object> get claims => _claims.stream;

  /// Takes the player for [owner]. The previous owner is told through [claims].
  void claim(Object owner) {
    if (identical(_owner, owner)) return;
    _owner = owner;
    _claims.add(owner);
  }

  /// Whether sources must carry a `MediaItem` tag — the background service
  /// refuses a source without one.
  static bool get needsMediaItems => _backgroundReady;

  /// Called once in `main`, before anything can touch [player].
  ///
  /// The notification channel id is versioned like every other channel here
  /// (see `LocalNotifications`): Android fixes a channel's settings the first
  /// time it is created, so changing how playback notifies means a new id,
  /// never an edited one.
  static Future<void> initialize() async {
    if (kIsWeb) return;

    try {
      await JustAudioBackground.init(
        androidNotificationChannelId: 'athkari.playback.v1',
        androidNotificationChannelName: 'الاستماع',
        androidNotificationChannelDescription: 'التحكم في التلاوة أثناء تشغيلها',
        androidNotificationOngoing: true,
        androidStopForegroundOnPause: true,
        notificationColor: AthkarColors.brand,
        // The status-bar silhouette, not the launcher icon: Android keeps only
        // an icon's alpha there, and the full-bleed launcher tile is a square.
        androidNotificationIcon: 'drawable/ic_notification',
        fastForwardInterval: const Duration(seconds: 10),
        rewindInterval: const Duration(seconds: 10),
      );
      _backgroundReady = true;

      // Spoken word, not music: a phone call or a navigation prompt pauses
      // or ducks it instead of talking over the Qur'an, and unplugging a
      // headset pauses rather than moving the recitation to the loudspeaker.
      final session = await AudioSession.instance;
      await session.configure(const AudioSessionConfiguration.speech());
    } catch (error) {
      // A phone where the service cannot start still plays in the foreground;
      // losing the lock-screen controls is not a reason to lose the audio.
      assert(() {
        debugPrint('[audio] background playback unavailable: $error');
        return true;
      }());
    }
  }
}
