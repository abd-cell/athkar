import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import 'quran_store.dart';

/// The printed mushaf: 604 pages of 15 lines, broken where the press broke them.
///
/// This is a *second* way to read the same package. `QuranLibrary` reads verses
/// and lets the phone reflow them; this reads the page grid, which only exists
/// when the package carries the page layer (see `docs/BUSINESS_LOGIC.md` §7.4).
/// Everything here is defensive in the same way `QuranLibrary` is: a package
/// without the layer produces null, never an exception, and the reader is
/// offered the verse view instead.
class Mushaf {
  const Mushaf._();

  static const totalPages = 604;

  /// Whether this package can be shown as pages at all.
  static Future<bool> isAvailable(QuranStore store) async {
    final database = await store.open();
    if (database == null) return false;

    try {
      final rows = await database.rawQuery(
        "SELECT name FROM sqlite_master WHERE type = 'table' "
        "AND name IN ('words', 'lines', 'fonts')",
      );
      return rows.length == 3;
    } catch (_) {
      return false;
    }
  }

  /// One page, or null when the package has no page layer.
  static Future<MushafPage?> page(QuranStore store, int number) async {
    final database = await store.open();
    if (database == null) return null;

    try {
      final lines = await database.query(
        'lines',
        where: 'page = ?',
        whereArgs: [number],
        orderBy: 'line_number ASC',
      );
      if (lines.isEmpty) return null;

      final words = await database.query(
        'words',
        where: 'page = ?',
        whereArgs: [number],
        orderBy: 'id ASC',
      );

      final byId = {for (final row in words) row['id'] as int: row};

      return MushafPage(
        number: number,
        lines: [
          for (final line in lines)
            MushafLine(
              number: line['line_number'] as int? ?? 0,
              kind: MushafLineKind.parse(line['kind'] as String?),
              isCentered: (line['is_centered'] as int? ?? 0) == 1,
              surahId: line['surah_id'] as int?,
              words: [
                for (var id = line['first_word_id'] as int? ?? 1;
                    id <= (line['last_word_id'] as int? ?? 0);
                    id++)
                  if (byId[id] case final row?)
                    MushafWord(
                      id: id,
                      surahId: row['surah_id'] as int? ?? 0,
                      ayah: row['ayah_number'] as int? ?? 0,
                      text: row['text'] as String? ?? '',
                    ),
              ],
            ),
        ],
      );
    } catch (error) {
      assert(() {
        debugPrint('[mushaf] could not read page $number: $error');
        return true;
      }());
      return null;
    }
  }

  /// Where every surah opens, in one query.
  ///
  /// The index needs all 114 at once — asking per row would be 114 round trips
  /// to build one screen.
  static Future<Map<int, int>> surahStartPages(QuranStore store) async {
    final database = await store.open();
    if (database == null) return const {};

    try {
      final rows = await database.rawQuery(
        'SELECT surah_id, MIN(page) AS page FROM words GROUP BY surah_id',
      );

      return {
        for (final row in rows)
          if (row['surah_id'] case final int id)
            if (row['page'] case final int page) id: page,
      };
    } catch (_) {
      return const {};
    }
  }

  /// The pages, each with the surah its first line belongs to — which is what
  /// makes a list of 604 numbers navigable rather than a wall of them.
  static Future<List<MushafPageEntry>> pageIndex(QuranStore store) async {
    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.rawQuery(
        'SELECT page, surah_id, MIN(id) AS first FROM words GROUP BY page ORDER BY page ASC',
      );

      return [
        for (final row in rows)
          MushafPageEntry(
            page: row['page'] as int? ?? 0,
            surahId: row['surah_id'] as int? ?? 0,
          ),
      ];
    } catch (_) {
      return const [];
    }
  }

  /// The thirty ajzaa and where each begins. Read from the verse table rather
  /// than from a table of its own: the juz of a verse is a property the package
  /// already carries, and a second list of boundaries could disagree with it.
  static Future<List<JuzEntry>> juzIndex(QuranStore store) async {
    final database = await store.open();
    if (database == null) return const [];

    try {
      final rows = await database.rawQuery(
        'SELECT juz, MIN(page) AS page, MIN(id) AS first FROM ayahs '
        'WHERE juz IS NOT NULL GROUP BY juz ORDER BY juz ASC',
      );

      final entries = <JuzEntry>[];
      for (final row in rows) {
        final first = row['first'] as int?;
        final opening = first == null
            ? null
            : (await database.query('ayahs',
                    columns: ['surah_id', 'ayah_number'],
                    where: 'id = ?',
                    whereArgs: [first],
                    limit: 1))
                .firstOrNull;

        entries.add(JuzEntry(
          number: row['juz'] as int? ?? 0,
          page: row['page'] as int? ?? 0,
          surahId: opening?['surah_id'] as int? ?? 0,
          ayah: opening?['ayah_number'] as int? ?? 0,
        ));
      }
      return entries;
    } catch (error) {
      assert(() {
        debugPrint('[mushaf] could not read the juz index: $error');
        return true;
      }());
      return const [];
    }
  }

  /// The page a surah opens on, for the index to jump to.
  static Future<int?> pageOfSurah(QuranStore store, int surahId) async {
    final database = await store.open();
    if (database == null) return null;

    try {
      final rows = await database.rawQuery(
        'SELECT MIN(page) AS page FROM words WHERE surah_id = ?',
        [surahId],
      );
      return rows.isEmpty ? null : rows.first['page'] as int?;
    } catch (_) {
      return null;
    }
  }
}

/// Loads the package's own font into the engine, once per family.
///
/// The font travels inside the package rather than inside the app: which script
/// a mushaf is drawn in is a property of that mushaf, and an app that shipped
/// one face would have to ship them all. A package may carry a single font for
/// the whole book or one per page — `page IS NULL` is the former — so this asks
/// for the page's own font first and falls back to the shared one.
class MushafFonts {
  const MushafFonts._();

  static final Map<int, String?> _familyOfPage = {};
  static final Set<String> _loaded = {};

  /// The family to draw [page] in, loading it if this is the first time.
  /// Null when the package carries no font, in which case the caller should
  /// fall back to the app's own face rather than draw nothing.
  static Future<String?> familyFor(QuranStore store, int page) async {
    if (_familyOfPage.containsKey(page)) return _familyOfPage[page];

    final database = await store.open();
    if (database == null) return null;

    try {
      final rows = await database.rawQuery(
        'SELECT family, data FROM fonts WHERE page = ? OR page IS NULL '
        'ORDER BY page IS NULL ASC LIMIT 1',
        [page],
      );
      if (rows.isEmpty) return _familyOfPage[page] = null;

      final family = rows.first['family'] as String?;
      final data = rows.first['data'];
      if (family == null || family.isEmpty || data is! Uint8List) {
        return _familyOfPage[page] = null;
      }

      if (_loaded.add(family)) {
        final loader = FontLoader(family)
          ..addFont(Future.value(ByteData.view(data.buffer, data.offsetInBytes, data.length)));
        await loader.load();
      }

      return _familyOfPage[page] = family;
    } catch (error) {
      assert(() {
        debugPrint('[mushaf] could not load the font for page $page: $error');
        return true;
      }());
      return _familyOfPage[page] = null;
    }
  }

  /// Forgotten when the package is replaced — a new mushaf may be drawn in a
  /// different face, and the engine would otherwise keep serving the old one.
  static void reset() {
    _familyOfPage.clear();
    _loaded.clear();
  }
}

enum MushafLineKind {
  ayah,
  surahName,
  basmallah,

  /// A line of the grid the print leaves empty. The first two pages hold eight
  /// lines of text on the same fifteen-line grid as every other page, and
  /// keeping the empty rows is what sits al-Fatiha where the press sits it
  /// rather than stretching it over the whole page.
  blank;

  static MushafLineKind parse(String? value) => switch (value) {
        'surah_name' => MushafLineKind.surahName,
        'basmallah' => MushafLineKind.basmallah,
        'blank' => MushafLineKind.blank,
        _ => MushafLineKind.ayah,
      };
}

class MushafPage {
  const MushafPage({required this.number, required this.lines});

  final int number;
  final List<MushafLine> lines;
}

class MushafLine {
  const MushafLine({
    required this.number,
    required this.kind,
    required this.isCentered,
    required this.words,
    this.surahId,
  });

  final int number;
  final MushafLineKind kind;

  /// Centred rather than justified. A fact of the print, not of the words: two
  /// lines that both end a surah can differ.
  final bool isCentered;

  final List<MushafWord> words;

  /// For a header line, the surah it announces.
  final int? surahId;
}

class MushafWord {
  const MushafWord({
    required this.id,
    required this.surahId,
    required this.ayah,
    required this.text,
  });

  final int id;
  final int surahId;
  final int ayah;

  /// What to draw — a word, or the verse marker with its number inside it.
  final String text;
}

/// One page of the mushaf, as the index lists it.
class MushafPageEntry {
  const MushafPageEntry({required this.page, required this.surahId});

  final int page;

  /// The surah the page's first line belongs to.
  final int surahId;
}

/// One juz, and where it opens.
class JuzEntry {
  const JuzEntry({
    required this.number,
    required this.page,
    required this.surahId,
    required this.ayah,
  });

  final int number;
  final int page;
  final int surahId;
  final int ayah;
}
