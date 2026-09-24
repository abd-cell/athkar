import 'package:flutter_test/flutter_test.dart';

import 'package:athkar_app/core/ayah_timing.dart';
import 'package:athkar_app/core/recitation_library.dart';
import 'package:athkar_app/core/recitation_player.dart';
import 'package:athkar_app/core/surah_names.dart';
import 'package:athkar_app/models/models.dart';

/// The parts of listening that are about time and text — the kind of test
/// this project keeps. Playback itself is a platform plugin and is exercised on
/// a device; what is here is everything that decides *what* plays and *which
/// ayah* the tracker names.
void main() {
  group('ayah timing', () {
    // The publisher's own shape, including the entry-zero isti'adha.
    const body = '''[
      {"ayah":0,"start_time":0,"end_time":7960},
      {"ayah":2,"start_time":20820,"end_time":31000},
      {"ayah":1,"start_time":7960,"end_time":20820},
      {"ayah":3,"start_time":32500,"end_time":40000}
    ]''';

    test('entry zero is audio but not an ayah, and the rest is put in order', () {
      final timings = AyahTimings.parse(body)!;
      expect([for (final t in timings) t.ayah], [1, 2, 3]);
      expect(timings.first.start, const Duration(milliseconds: 7960));
    });

    test('a file that is not a timing list is no timing, not an error', () {
      expect(AyahTimings.parse('not json'), isNull);
      expect(AyahTimings.parse('{"ayah":1}'), isNull);
      expect(AyahTimings.parse('[]'), isNull);
      expect(AyahTimings.parse('[{"ayah":1,"start_time":5,"end_time":2}]'), isNull);
    });

    test('the tracker names the ayah being recited', () {
      final timings = AyahTimings.parse(body)!;

      expect(AyahTimings.ayahAt(timings, const Duration(seconds: 3)), isNull,
          reason: 'before the first ayah is the isti\'adha');
      expect(AyahTimings.ayahAt(timings, const Duration(milliseconds: 7960)), 1);
      expect(AyahTimings.ayahAt(timings, const Duration(seconds: 25)), 2);
      // The reciter pausing between ayat 2 and 3: still ayah 2 on screen.
      expect(AyahTimings.ayahAt(timings, const Duration(milliseconds: 31800)), 2);
      expect(AyahTimings.ayahAt(timings, const Duration(seconds: 45)), 3);
    });
  });

  group('the sleep timer', () {
    test('a time still ahead today is today', () {
      final now = DateTime(2026, 9, 23, 21, 10);
      expect(RecitationPlayer.nextOccurrence(now, 23, 30), DateTime(2026, 9, 23, 23, 30));
    });

    test('a time already passed means tomorrow, not a timer that has fired', () {
      final now = DateTime(2026, 9, 23, 23, 40);
      expect(RecitationPlayer.nextOccurrence(now, 23, 30), DateTime(2026, 9, 24, 23, 30));
      // Exactly now is not "ahead" either.
      expect(RecitationPlayer.nextOccurrence(DateTime(2026, 9, 23, 6, 0), 6, 0),
          DateTime(2026, 9, 24, 6, 0));
    });
  });

  group('recordings', () {
    const recitation = Recitation(
      id: 1,
      name: 'حفص عن عاصم - مرتل',
      serverUrl: 'https://server12.mp3quran.net/maher',
      surahs: [1, 7, 114],
      sourceName: 'MP3Quran',
      timingUrl: 'https://mp3quran.net/api/v3/ayat_timing?read=133&surah=',
    );

    test('a surah file is the folder plus its zero-padded number', () {
      expect(recitation.urlOf(7), 'https://server12.mp3quran.net/maher/007.mp3');
      expect(recitation.urlOf(114), 'https://server12.mp3quran.net/maher/114.mp3');
    });

    test('timing is asked for one surah at a time, and only when there is timing', () {
      expect(recitation.timingUrlOf(7), 'https://mp3quran.net/api/v3/ayat_timing?read=133&surah=7');

      const untimed = Recitation(id: 2, name: 'x', serverUrl: 'https://a/', surahs: [1], sourceName: 's');
      expect(untimed.hasTiming, isFalse);
      expect(untimed.timingUrlOf(1), isNull);
    });

    test('the catalogue carries reciters, and a surah number outside 1–114 is dropped', () {
      final catalog = Catalog.fromJson({
        'version': 5,
        'categories': <Object>[],
        'reciters': [
          {
            'id': 3,
            'key': 'mp3quran-102',
            'name': 'ماهر المعيقلي',
            'isFeatured': true,
            'recitations': [
              {
                'id': 1,
                'name': 'حفص عن عاصم - مرتل',
                'serverUrl': 'https://server12.mp3quran.net/maher/',
                'surahs': [1, 2, 0, 115, 'x'],
                'sourceName': 'MP3Quran',
              },
            ],
          },
        ],
      });

      final reciter = catalog.reciters.single;
      expect(reciter.isFeatured, isTrue);
      expect(reciter.recitations.single.surahs, [1, 2]);

      // And it survives the cache round trip the app stores it through.
      final again = Catalog.fromJson(catalog.toJson()).reciters.single;
      expect(again.key, 'mp3quran-102');
      expect(again.recitations.single.sourceName, 'MP3Quran');
    });
  });

  test('a bookmark reference survives being stored as a string', () {
    const ref = SurahRef(reciterKey: 'mp3quran-102', recitationId: 1, surah: 7);
    expect(SurahRef.parse(ref.id), ref);
    expect(SurahRef.parse('broken'), isNull);
  });

  test('the surah names are the 114, in order', () {
    expect(surahNames.length, 114);
    expect([for (final s in surahNames) s.number], List.generate(114, (i) => i + 1));
    expect(surahName(1)!.arabic, 'الفاتحة');
    expect(surahName(114)!.arabic, 'الناس');
    expect(surahName(0), isNull);
    expect(surahName(115), isNull);
  });
}
