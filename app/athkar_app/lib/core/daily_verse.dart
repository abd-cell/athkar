import 'quran_library.dart';
import 'quran_store.dart';

/// «آية اليوم» — one verse, chosen by the date and read from the downloaded
/// mushaf.
///
/// Two rules shape this file:
///
/// * **The text is never carried by the app.** Only the *reference* is. The
///   verse itself is read out of the package the reader downloaded, so the app
///   can never show a mushaf it does not have, and can never show a verse that
///   disagrees with the one on the reader's own pages. Before the download
///   there is no verse card at all — [forDay] answers null and the home screen
///   leaves the card out.
/// * **The choice follows the day, not the build.** The same verse is there if
///   the reader closes the app and comes back; it changes at midnight.
///
/// The reference list below is the one piece of content in this app that is not
/// administered from the CMS, because the CMS has no verse-of-the-day feature
/// yet. When it grows one, this list is what it replaces.
class DailyVerse {
  const DailyVerse({required this.surah, required this.ayah});

  final Surah surah;
  final Ayah ayah;

  /// Well-known verses of remembrance, supplication and consolation, as
  /// (surah, ayah) pairs.
  static const references = <(int, int)>[
    (2, 152), (2, 255), (2, 286),
    (3, 8), (3, 26), (3, 139), (3, 159), (3, 173),
    (4, 110), (5, 45), (6, 17), (7, 56), (8, 46), (9, 51), (9, 129),
    (11, 88), (12, 87), (13, 28), (14, 7), (16, 97), (17, 80), (18, 39),
    (20, 114), (21, 87), (23, 118), (25, 74), (29, 69), (33, 56),
    (39, 53), (40, 60), (42, 43), (46, 15), (51, 56), (55, 60),
    (65, 3), (67, 2), (93, 5), (94, 6), (103, 3),
  ];

  /// The verse for [day], or null when the mushaf is not downloaded.
  ///
  /// A package that is missing the day's verse is not an error — the list is
  /// walked forward until one of its references resolves, so a partial package
  /// still shows a card rather than a gap.
  static Future<DailyVerse?> forDay(QuranStore store, DateTime day) async {
    final index = day.difference(DateTime(day.year)).inDays;

    final surahs = await QuranLibrary.surahs(store);
    if (surahs.isEmpty) return null;

    for (var attempt = 0; attempt < references.length; attempt++) {
      final (surahId, ayahNumber) = references[(index + attempt) % references.length];

      Surah? surah;
      for (final candidate in surahs) {
        if (candidate.id == surahId) surah = candidate;
      }
      if (surah == null) continue;

      final verses = await QuranLibrary.ayahs(store, surahId);
      for (final candidate in verses) {
        if (candidate.number == ayahNumber && candidate.text.trim().isNotEmpty) {
          return DailyVerse(surah: surah, ayah: candidate);
        }
      }
    }

    return null;
  }
}
