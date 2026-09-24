import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';

import '../models/models.dart';

/// Where one ayah starts and ends inside a surah's recording.
@immutable
class AyahTiming {
  const AyahTiming({required this.ayah, required this.start, required this.end});

  final int ayah;
  final Duration start;
  final Duration end;
}

/// Reading and using a recording's ayah timing.
///
/// The publisher's timing file is a list of `{ayah, start_time, end_time}` in
/// milliseconds. Entry zero, when present, is the isti'adha or basmala before
/// the first ayah — audio, but not an ayah — so it is dropped: the tracker
/// highlights ayat, and «الآية ٠» is not one.
class AyahTimings {
  const AyahTimings._();

  /// Null when [body] is not a timing file at all, which the caller treats as
  /// "no timing for this surah" rather than as an error the reader must see.
  static List<AyahTiming>? parse(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is! List) return null;

      final timings = <AyahTiming>[
        for (final entry in decoded)
          if (entry is Map)
            if ((entry['ayah'] as num?)?.toInt() case final ayah? when ayah >= 1)
              if ((entry['start_time'] as num?)?.toInt() case final start? when start >= 0)
                if ((entry['end_time'] as num?)?.toInt() case final end? when end > start)
                  AyahTiming(
                    ayah: ayah,
                    start: Duration(milliseconds: start),
                    end: Duration(milliseconds: end),
                  ),
      ]..sort((a, b) => a.start.compareTo(b.start));

      return timings.isEmpty ? null : timings;
    } on FormatException {
      return null;
    }
  }

  /// The ayah being recited at [position], or null before the first one.
  ///
  /// A binary search: this runs on every position tick, and al-Baqarah has 286
  /// entries.
  static int? ayahAt(List<AyahTiming> timings, Duration position) {
    var low = 0;
    var high = timings.length - 1;
    int? found;

    while (low <= high) {
      final middle = (low + high) >> 1;
      if (timings[middle].start <= position) {
        found = middle;
        low = middle + 1;
      } else {
        high = middle - 1;
      }
    }

    // Past this ayah's end and short of the next one's start is a pause the
    // reciter took; the ayah just finished is still the one to show.
    return found == null ? null : timings[found].ayah;
  }

  static AyahTiming? of(List<AyahTiming> timings, int ayah) {
    for (final timing in timings) {
      if (timing.ayah == ayah) return timing;
    }
    return null;
  }
}

/// Fetches a surah's timing from the publisher once and keeps it on the device.
///
/// Kept because the file never changes for a given recording, and because a
/// reader who downloaded a surah to listen offline should keep the tracker
/// offline too.
class AyahTimingCache {
  AyahTimingCache._();

  static final AyahTimingCache instance = AyahTimingCache._();

  final _memory = <String, List<AyahTiming>?>{};

  Future<List<AyahTiming>?> load(Recitation recitation, int surah) async {
    final url = recitation.timingUrlOf(surah);
    if (url == null) return null;

    final key = '${recitation.id}:$surah';
    if (_memory.containsKey(key)) return _memory[key];

    final file = await _file(recitation.id, surah);

    if (file != null && await file.exists()) {
      final cached = AyahTimings.parse(await file.readAsString());
      if (cached != null) return _memory[key] = cached;
    }

    try {
      final response = await http.get(Uri.parse(url)).timeout(const Duration(seconds: 15));
      if (response.statusCode != 200) return null;

      final body = utf8.decode(response.bodyBytes);
      final timings = AyahTimings.parse(body);

      if (timings != null && file != null) {
        await file.parent.create(recursive: true);
        await file.writeAsString(body);
      }

      return _memory[key] = timings;
    } catch (_) {
      // No network, or the publisher is down. Not cached as "none": the next
      // attempt may well succeed, and the tracker simply stays hidden until it does.
      return null;
    }
  }

  Future<File?> _file(int recitationId, int surah) async {
    if (kIsWeb) return null;
    final directory = await getApplicationDocumentsDirectory();
    return File('${directory.path}/recitations/timing/$recitationId/$surah.json');
  }
}
