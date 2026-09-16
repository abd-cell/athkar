import 'package:flutter/foundation.dart';

import 'arabic_text.dart';
import 'quran_store.dart';

/// Reads the downloaded mushaf.
///
/// The schema below is the contract with whoever prepares the file for upload,
/// and it is documented in `docs/BUSINESS_LOGIC.md` §7. Everything here is
/// defensive: a package that lacks the optional waqf tables simply produces no
/// annotations, and a package that lacks the required ones produces an empty
/// index rather than an exception. A reader whose download is somehow wrong
/// should see "nothing here", never a crash.
class QuranLibrary {
  const QuranLibrary._();

  /// The index of surahs.
  static Future<List<Surah>> surahs(QuranStore store) async {
    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.query('surahs', orderBy: 'id ASC');

      return [
        for (final row in rows)
          Surah(
            id: row['id'] as int? ?? 0,
            nameAr: row['name_ar'] as String? ?? '',
            nameEn: row['name_en'] as String? ?? '',
            ayahCount: row['ayah_count'] as int? ?? 0,
            revelationPlace: row['revelation_place'] as String? ?? '',
          ),
      ];
    } catch (error) {
      assert(() {
        debugPrint('[quran] could not read the surah index: $error');
        return true;
      }());
      return const [];
    }
  }

  /// The verses of one surah, in order.
  static Future<List<Ayah>> ayahs(QuranStore store, int surahId) async {
    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.query(
        'ayahs',
        where: 'surah_id = ?',
        whereArgs: [surahId],
        orderBy: 'ayah_number ASC',
      );

      return [
        for (final row in rows)
          Ayah(
            surahId: surahId,
            number: row['ayah_number'] as int? ?? 0,
            text: row['text_uthmani'] as String? ?? row['text'] as String? ?? '',
            page: row['page'] as int?,
            juz: row['juz'] as int?,
          ),
      ];
    } catch (error) {
      assert(() {
        debugPrint('[quran] could not read surah $surahId: $error');
        return true;
      }());
      return const [];
    }
  }

  /// Every verse whose text contains the query, folded on both sides.
  ///
  /// The folding is the point: a reader types «الحمد لله» without a single
  /// fatha, and the mushaf is fully vocalised. Both sides go through the same
  /// rules the server folds its own search text with — see `ArabicText` — so
  /// what is typed and what is printed meet somewhere in the middle.
  ///
  /// The folded corpus is built once and kept, because the alternative is
  /// folding 6,236 verses on every keystroke. It is a few hundred kilobytes of
  /// text, and it is dropped the moment the mushaf being read changes.
  static Future<List<AyahHit>> search(
    QuranStore store,
    String query, {
    int limit = 60,
  }) async {
    final needle = foldForSearch(query);
    if (needle.length < 2) return const [];

    final corpus = await _corpus(store);

    final hits = <AyahHit>[];
    for (final entry in corpus) {
      if (!entry.folded.contains(needle)) continue;
      hits.add(entry.hit);
      if (hits.length >= limit) break;
    }
    return hits;
  }

  /// Folds a query and a verse to a form in which they can meet.
  ///
  /// `ArabicText.normalize` alone is not enough here, and the reason is the
  /// Uthmani hand. It writes «ٱلْعَـٰلَمِينَ» with a dagger alif where a reader
  /// types «العالمين» with a written one, and «ٱلصَّلَوٰةَ» with a waw where a
  /// reader types «الصلاة» without. Fold those away and a reader searching for
  /// the opening of al-Fatiha is told the Qur'an does not contain it.
  ///
  /// So three rules run before the shared folding, and one after:
  ///
  /// - a waw carrying a dagger alif is the alif of «صلاة» — «ٱلصَّلَوٰةَ» → «الصلاة»;
  /// - any other dagger alif is a written alif — «ذَٰلِكَ» → «ذالك»;
  /// - then the ordinary fold strips the rest of the marks;
  /// - and the bare alif goes last, on **both** sides, because after all that it
  ///   is still the letter the two spellings disagree about most: «ذالك» and
  ///   «ذلك» are one word, and only dropping it says so.
  ///
  /// The cost is a looser match — «قال» and «قل» fold together — which is the
  /// right trade for a search that shows the verse and lets the reader choose.
  /// `ArabicText` itself is left alone: it is mirrored on the server, and its
  /// rules are about adhkar, whose text is not written in this hand.
  static String foldForSearch(String? raw) {
    if (raw == null || raw.isEmpty) return '';

    final buffer = StringBuffer();
    for (var index = 0; index < raw.length; index++) {
      final character = raw[index];

      if (character == _tatweel) continue;

      // The Uthmani hand writes some letters as small marks above the line —
      // «إِبْرَٰهِـۧمَ» carries its ya that way, and the ordinary fold strips
      // marks. Put them back as the letters they stand for.
      if (character == _smallHighYeh) {
        buffer.write('ي');
        continue;
      }

      if (character == _smallHighWaw) {
        buffer.write('و');
        continue;
      }

      if (character == _daggerAlif) {
        // «صلوٰة»: the waw is not read, the dagger is the alif.
        if (buffer.isNotEmpty && buffer.toString().endsWith('و')) {
          final kept = buffer.toString();
          buffer.clear();
          buffer.write(kept.substring(0, kept.length - 1));
        }
        buffer.write('ا');
        continue;
      }

      buffer.write(character);
    }

    return ArabicText.normalize(buffer.toString()).replaceAll('ا', '');
  }

  static const _daggerAlif = 'ٰ';
  static const _tatweel = 'ـ';
  static const _smallHighYeh = 'ۧ';
  static const _smallHighWaw = 'ۥ';

  /// Forgets the folded corpus. Called when the mushaf changes — a search
  /// answering out of the previous edition would point at its page numbers.
  static void forgetCorpus() {
    _folded = null;
    _foldedEdition = null;
  }

  static List<_FoldedAyah>? _folded;
  static String? _foldedEdition;

  static Future<List<_FoldedAyah>> _corpus(QuranStore store) async {
    final edition = store.selectedEdition;
    if (_folded != null && _foldedEdition == edition) return _folded!;

    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.query(
        'ayahs',
        columns: ['surah_id', 'ayah_number', 'text_uthmani', 'page'],
        orderBy: 'id ASC',
      );

      final corpus = [
        for (final row in rows)
          _FoldedAyah(
            folded: foldForSearch(row['text_uthmani'] as String? ?? ''),
            hit: AyahHit(
              surahId: row['surah_id'] as int? ?? 0,
              ayah: row['ayah_number'] as int? ?? 0,
              text: row['text_uthmani'] as String? ?? '',
              page: row['page'] as int?,
            ),
          ),
      ];

      _folded = corpus;
      _foldedEdition = edition;
      return corpus;
    } catch (error) {
      assert(() {
        debugPrint('[quran] could not build the search index: $error');
        return true;
      }());
      return const [];
    }
  }

  /// The waqf annotations for one verse, when the package carries them.
  ///
  /// This is the "advanced" option of the two described in the business-logic
  /// doc: the glyphs themselves are part of the verse text and render without
  /// any of this, and these rows are what make them *tappable* — a sheet saying
  /// what a particular mark means at that particular place.
  static Future<List<WaqfMark>> waqfMarks(QuranStore store, int surahId, int ayah) async {
    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.rawQuery(
        '''
        SELECT m.word_index, m.symbol, t.name_ar, t.ruling_ar
        FROM waqf_marks m
        LEFT JOIN waqf_types t ON t.symbol = m.symbol
        WHERE m.surah_id = ? AND m.ayah_number = ?
        ORDER BY m.word_index ASC
        ''',
        [surahId, ayah],
      );

      return [
        for (final row in rows)
          WaqfMark(
            wordIndex: row['word_index'] as int? ?? 0,
            symbol: row['symbol'] as String? ?? '',
            name: row['name_ar'] as String? ?? '',
            ruling: row['ruling_ar'] as String? ?? '',
          ),
      ];
    } catch (_) {
      // The tables are optional by design — see QuranPackage.hasWaqfAnnotations.
      return const [];
    }
  }
}

/// One verse the search found, with where it sits.
class AyahHit {
  const AyahHit({
    required this.surahId,
    required this.ayah,
    required this.text,
    this.page,
  });

  final int surahId;
  final int ayah;
  final String text;

  /// Null in a package with no page layer — the hit then opens the surah.
  final int? page;
}

class Surah {
  const Surah({
    required this.id,
    required this.nameAr,
    required this.nameEn,
    required this.ayahCount,
    required this.revelationPlace,
  });

  final int id;
  final String nameAr;
  final String nameEn;
  final int ayahCount;

  /// "makkah" or "madinah", as the package spells it.
  final String revelationPlace;
}

class Ayah {
  const Ayah({
    required this.surahId,
    required this.number,
    required this.text,
    this.page,
    this.juz,
  });

  final int surahId;
  final int number;

  /// The Uthmani text, waqf glyphs included — they are characters in the string,
  /// not decoration added by the app.
  final String text;

  final int? page;
  final int? juz;
}

class WaqfMark {
  const WaqfMark({
    required this.wordIndex,
    required this.symbol,
    required this.name,
    required this.ruling,
  });

  /// Which word of the verse the mark follows.
  final int wordIndex;

  /// The glyph itself — ۘ, ۚ, ۖ and the rest.
  final String symbol;

  /// Its name: «الوقف اللازم», «الوقف الجائز».
  final String name;

  /// What it means for a reciter, in a sentence.
  final String ruling;
}

class _FoldedAyah {
  const _FoldedAyah({required this.folded, required this.hit});

  final String folded;
  final AyahHit hit;
}
