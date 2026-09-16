import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter/foundation.dart';
import 'package:path_provider/path_provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sqflite/sqflite.dart';

import '../models/models.dart';
import '../services/services.dart';
import 'app_response.dart';

/// The mushafs, once the reader has asked for one.
///
/// The server stores each prepared SQLite file and hands it over whole; this
/// reads it locally from then on. Nothing about the Qur'an is ever fetched per
/// screen — a reader on a train with no signal opens the same surah as a reader
/// at home. See `docs/BUSINESS_LOGIC.md` §7 for the file's expected schema,
/// including the waqf tables.
///
/// Everything here is keyed by **edition** — the slug of a particular mushaf.
/// A reader may keep more than one, since Hafs and Warsh are different texts
/// rather than different versions of one, and each is a couple of hundred
/// megabytes a reader chose to spend.
class QuranStore {
  QuranStore._(this._prefs);

  final SharedPreferences _prefs;

  /// The version saved for one edition: `quran.version.<edition>`.
  static const _kVersionPrefix = 'quran.version.';

  /// Which mushaf the reader is reading.
  static const _kEdition = 'quran.edition';

  /// Where the reader had reached, per edition: `quran.lastPage.<edition>`.
  ///
  /// Per edition and not one number for the app, because page 300 of Hafs and
  /// page 300 of Warsh are not the same words — carrying a position between two
  /// mushafs would put the reader somewhere they have never been.
  static const _kLastPagePrefix = 'quran.lastPage.';

  /// What the single-mushaf build used, both of them. Read only to migrate.
  static const _kLegacyVersion = 'quran.version';
  static const _legacyFileName = 'quran.db';

  Database? _database;

  /// The edition the open handle belongs to, so switching mushafs closes the
  /// file rather than serving Warsh's pages out of Hafs.
  String? _openEdition;

  static Future<QuranStore> load() async =>
      QuranStore._(await SharedPreferences.getInstance());

  /// The mushaf being read, or null when the reader has not downloaded one —
  /// which is the state most installs are in and is not a problem to be solved.
  String? get selectedEdition => _prefs.getString(_kEdition);

  /// Every mushaf on this device, by slug.
  List<String> get savedEditions => [
        for (final key in _prefs.getKeys())
          if (key.startsWith(_kVersionPrefix)) key.substring(_kVersionPrefix.length),
      ];

  /// Whether a single-mushaf build's file is still sitting there unclaimed.
  /// Cheap, and checked on every launch, so it reads a key rather than the disk.
  bool get hasLegacy => _prefs.containsKey(_kLegacyVersion);

  int? versionOf(String edition) => _prefs.getInt('$_kVersionPrefix$edition');

  /// The page the reader last had open in this mushaf, or null before they have
  /// opened one. Null is not a failure: it is a reader who has not started.
  int? lastPageOf(String edition) => _prefs.getInt('$_kLastPagePrefix$edition');

  /// Where the reader had reached in the mushaf being read.
  int? get lastPage {
    final edition = selectedEdition;
    return edition == null ? null : lastPageOf(edition);
  }

  /// Remembers the page being read. Called as the reader turns pages, so it
  /// writes only when the number actually changes — a preference write per
  /// frame of a swipe would be a lot of disk for one number.
  Future<void> setLastPage(int page) async {
    final edition = selectedEdition;
    if (edition == null || page < 1) return;
    if (lastPageOf(edition) == page) return;

    await _prefs.setInt('$_kLastPagePrefix$edition', page);
  }

  bool has(String edition) => versionOf(edition) != null;

  /// The version of the mushaf currently being read.
  int? get savedVersion {
    final edition = selectedEdition;
    return edition == null ? null : versionOf(edition);
  }

  bool get isSaved => savedVersion != null;

  /// Switches which mushaf the app reads. The other stays on disk.
  Future<void> select(String edition) async {
    if (selectedEdition == edition) return;

    // The handle points at the previous file; leaving it open would answer
    // every query from the mushaf the reader just switched away from.
    await close();
    await _prefs.setString(_kEdition, edition);
  }

  /// Adopts the file a single-mushaf build left behind.
  ///
  /// That build stored one `quran.db` under one version number and had no idea
  /// which mushaf it was — the server only started saying so when editions
  /// arrived. So the first version check after the upgrade names the default
  /// edition, and the file already on disk is filed under that name instead of
  /// being thrown away: it is the right bytes, and re-downloading it would cost
  /// a reader hundreds of megabytes to end up exactly where they were.
  ///
  /// Does nothing when there is no legacy file, which is every install after
  /// the first launch.
  Future<void> adoptLegacy(String edition) async {
    final version = _prefs.getInt(_kLegacyVersion);
    if (version == null) return;

    final directory = await getApplicationDocumentsDirectory();
    final legacy = File('${directory.path}/$_legacyFileName');

    // The key goes either way: a legacy row with no file behind it is a state
    // to clear, not one to keep re-examining on every launch.
    if (await legacy.exists() && !has(edition)) {
      await close();
      await legacy.rename('${directory.path}/${_fileNameOf(edition)}');
      await _prefs.setInt('$_kVersionPrefix$edition', version);
      await _prefs.setString(_kEdition, edition);
    } else if (await legacy.exists()) {
      await legacy.delete();
    }

    await _prefs.remove(_kLegacyVersion);
  }

  static String _fileNameOf(String edition) => 'quran_$edition.db';

  Future<File> _file(String edition) async {
    final directory = await getApplicationDocumentsDirectory();
    return File('${directory.path}/${_fileNameOf(edition)}');
  }

  /// Downloads and verifies a published mushaf.
  ///
  /// [onProgress] receives a fraction between 0 and 1, or null while the
  /// server has not declared a length.
  ///
  /// Written to a temporary file and moved into place only after the checksum
  /// matches: a mushaf truncated by a dropped connection must never become the
  /// file the app opens, because the failure would show up as missing surahs
  /// weeks later rather than as an error now.
  Future<AppResponse<void>> download(
    QuranEdition edition, {
    void Function(double? progress)? onProgress,
  }) async {
    final opened = await Api.quran.download(edition: edition.edition);
    if (!opened.success) return opened.cast<void>();

    final response = opened.data!;
    final total = response.contentLength ?? edition.sizeBytes;

    final target = await _file(edition.edition);
    final temporary = File('${target.path}.part');

    try {
      final sink = temporary.openWrite();
      var received = 0;

      await for (final chunk in response.stream) {
        sink.add(chunk);
        received += chunk.length;
        onProgress?.call(total > 0 ? received / total : null);
      }

      await sink.close();

      if (edition.sha256.isNotEmpty) {
        final actual = await _sha256(temporary);
        if (actual != edition.sha256.toLowerCase()) {
          await temporary.delete();
          return AppResponse.failure(
            AppErrorCodes.parse,
            'الملف الذي وصل غير مطابق. أعد المحاولة.',
          );
        }
      }

      // The old database must be closed before its file is replaced, or the
      // handle keeps pointing at bytes that are no longer there.
      await close();
      if (await target.exists()) await target.delete();
      await temporary.rename(target.path);

      await _prefs.setInt('$_kVersionPrefix${edition.edition}', edition.version);
      await _prefs.setString(_kEdition, edition.edition);
      return AppResponse.ok(null);
    } catch (error) {
      assert(() {
        debugPrint('[quran] download failed: $error');
        return true;
      }());

      if (await temporary.exists()) await temporary.delete();
      return AppResponse.failure(AppErrorCodes.network, 'تعذّر تحميل المصحف.');
    }
  }

  /// Opens the selected mushaf, or null when there is none.
  Future<Database?> open() async {
    final edition = selectedEdition;
    if (edition == null) return null;

    if (_database != null && _openEdition == edition) return _database;
    if (_database != null) await close();

    final file = await _file(edition);
    if (!await file.exists()) return null;

    // Read-only: the app queries this file and never writes to it, and opening
    // it writable would let a stray migration corrupt a verified corpus.
    _database = await openDatabase(file.path, readOnly: true);
    _openEdition = edition;
    return _database;
  }

  Future<void> close() async {
    await _database?.close();
    _database = null;
    _openEdition = null;
  }

  /// Removes one saved mushaf. Offered in settings, because a couple of hundred
  /// megabytes is a real amount of a phone's storage.
  ///
  /// Deleting the one being read moves the reader to another they still have,
  /// rather than leaving a selection pointing at a file that is gone.
  Future<void> delete(String edition) async {
    if (_openEdition == edition) await close();

    final file = await _file(edition);
    if (await file.exists()) await file.delete();

    await _prefs.remove('$_kVersionPrefix$edition');

    // The reading position goes with the file. Keeping it would offer to
    // continue on page 412 of a mushaf that is no longer on the device, and
    // re-downloading later would resume at a page the reader had forgotten.
    await _prefs.remove('$_kLastPagePrefix$edition');

    if (selectedEdition == edition) {
      final remaining = savedEditions;
      remaining.isEmpty
          ? await _prefs.remove(_kEdition)
          : await _prefs.setString(_kEdition, remaining.first);
    }
  }

  /// How much space a saved mushaf takes, for settings to show.
  Future<int> sizeOnDisk([String? edition]) async {
    final which = edition ?? selectedEdition;
    if (which == null) return 0;

    final file = await _file(which);
    return await file.exists() ? file.length() : 0;
  }

  /// Hashes in chunks rather than reading the file into memory — this is a
  /// file measured in hundreds of megabytes on a device measured in gigabytes.
  static Future<String> _sha256(File file) async {
    final digest = await sha256.bind(file.openRead()).first;
    return digest.toString();
  }
}
