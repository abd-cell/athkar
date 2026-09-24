import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';

import '../models/models.dart';

/// One surah of one recording, saved on the device.
@immutable
class SavedSurah {
  const SavedSurah({required this.recitationId, required this.surah, required this.bytes});

  final int recitationId;
  final int surah;
  final int bytes;
}

/// Surahs downloaded for listening offline.
///
/// Downloading is always the reader's own tap, one surah at a time — a
/// recording of the whole mushaf is a gigabyte or more, and an app that
/// fetched it unasked would be spending somebody's data plan for them. A
/// downloaded surah is also the private way to listen: once it is here, the
/// publisher is not asked for it again.
///
/// Files are written to `…/recitations/<recitation>/<surah>.mp3` through a
/// `.part` file, and an interrupted download resumes from where it stopped
/// with an HTTP range request — the publisher's servers honour them, and a
/// 60 MB surah restarted from zero on every dropped connection would never
/// finish on a train.
class RecitationDownloads extends ChangeNotifier {
  RecitationDownloads._();

  static final RecitationDownloads instance = RecitationDownloads._();

  final _saved = <String, SavedSurah>{};

  /// Fraction received, or a negative number while the server has not said
  /// how large the file is.
  final _progress = <String, double>{};
  final _cancelled = <String>{};

  var _loaded = false;

  /// Web builds have no file system to download into.
  bool get isSupported => !kIsWeb;

  static String _key(int recitationId, int surah) => '$recitationId:$surah';

  bool isSaved(int recitationId, int surah) => _saved.containsKey(_key(recitationId, surah));

  bool isDownloading(int recitationId, int surah) =>
      _progress.containsKey(_key(recitationId, surah));

  /// Between 0 and 1, negative while the size is unknown, null when idle.
  double? progressOf(int recitationId, int surah) => _progress[_key(recitationId, surah)];

  List<SavedSurah> get saved => _saved.values.toList()
    ..sort((a, b) => a.recitationId != b.recitationId
        ? a.recitationId.compareTo(b.recitationId)
        : a.surah.compareTo(b.surah));

  int get totalBytes => _saved.values.fold(0, (sum, entry) => sum + entry.bytes);

  /// Reads what is already on disk. Called once at launch; cheap after that.
  Future<void> load() async {
    if (_loaded || !isSupported) return;
    _loaded = true;

    final root = await _root();
    if (!await root.exists()) return;

    await for (final entity in root.list(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.mp3')) continue;

      final parts = entity.uri.pathSegments;
      if (parts.length < 2) continue;

      final recitationId = int.tryParse(parts[parts.length - 2]);
      final surah = int.tryParse(parts.last.replaceAll('.mp3', ''));
      if (recitationId == null || surah == null) continue;

      _saved[_key(recitationId, surah)] =
          SavedSurah(recitationId: recitationId, surah: surah, bytes: await entity.length());
    }

    notifyListeners();
  }

  /// The saved file, or null when this surah is not on the device.
  Future<File?> fileOf(int recitationId, int surah) async {
    if (!isSaved(recitationId, surah)) return null;
    final file = await _file(recitationId, surah);
    return await file.exists() ? file : null;
  }

  /// Downloads [surah] of [recitation]. Returns whether it is now saved.
  Future<bool> download(Recitation recitation, int surah) async {
    if (!isSupported) return false;

    final key = _key(recitation.id, surah);
    if (_saved.containsKey(key) || _progress.containsKey(key)) return _saved.containsKey(key);

    _cancelled.remove(key);
    _progress[key] = -1;
    notifyListeners();

    final target = await _file(recitation.id, surah);
    final partial = File('${target.path}.part');
    final client = http.Client();

    try {
      await target.parent.create(recursive: true);

      final already = await partial.exists() ? await partial.length() : 0;
      final request = http.Request('GET', Uri.parse(recitation.urlOf(surah)));
      if (already > 0) request.headers['Range'] = 'bytes=$already-';

      final response = await client.send(request).timeout(const Duration(seconds: 30));

      // 206 continues the partial file; 200 means the server ignored the range
      // and is sending the whole thing, so the partial file starts over.
      final resuming = response.statusCode == 206 && already > 0;
      if (response.statusCode != 200 && !resuming) {
        throw HttpException('HTTP ${response.statusCode}');
      }

      final expected = (response.contentLength ?? -1) + (resuming ? already : 0);
      final sink = partial.openWrite(mode: resuming ? FileMode.append : FileMode.write);
      var received = resuming ? already : 0;

      try {
        await for (final chunk in response.stream) {
          if (_cancelled.contains(key)) break;
          sink.add(chunk);
          received += chunk.length;
          _progress[key] = expected > 0 ? received / expected : -1;
          notifyListeners();
        }
      } finally {
        await sink.close();
      }

      if (_cancelled.contains(key)) {
        // Kept, so tapping download again resumes rather than restarts.
        return false;
      }

      // A short file is a dropped connection that happened to close cleanly.
      if (expected > 0 && received < expected) return false;

      if (await target.exists()) await target.delete();
      await partial.rename(target.path);

      _saved[key] = SavedSurah(recitationId: recitation.id, surah: surah, bytes: received);
      return true;
    } catch (error) {
      assert(() {
        debugPrint('[recitations] download of $key failed: $error');
        return true;
      }());
      return false;
    } finally {
      client.close();
      _progress.remove(key);
      _cancelled.remove(key);
      notifyListeners();
    }
  }

  void cancel(int recitationId, int surah) {
    final key = _key(recitationId, surah);
    if (_progress.containsKey(key)) _cancelled.add(key);
  }

  Future<void> delete(int recitationId, int surah) async {
    if (!isSupported) return;

    final file = await _file(recitationId, surah);
    if (await file.exists()) await file.delete();

    final partial = File('${file.path}.part');
    if (await partial.exists()) await partial.delete();

    _saved.remove(_key(recitationId, surah));
    notifyListeners();
  }

  /// Everything saved, and every half-finished download.
  Future<void> deleteAll() async {
    if (!isSupported) return;

    final root = await _root();
    if (await root.exists()) {
      for (final entity in root.listSync()) {
        // The timing cache lives beside the audio and is a few kilobytes; it is
        // what keeps the verse tracker working offline, so it stays.
        if (entity is Directory && !entity.path.endsWith('timing')) {
          await entity.delete(recursive: true);
        }
      }
    }

    _saved.clear();
    notifyListeners();
  }

  Future<Directory> _root() async {
    final directory = await getApplicationDocumentsDirectory();
    return Directory('${directory.path}/recitations');
  }

  Future<File> _file(int recitationId, int surah) async {
    final root = await _root();
    return File('${root.path}/$recitationId/${surah.toString().padLeft(3, '0')}.mp3');
  }
}
