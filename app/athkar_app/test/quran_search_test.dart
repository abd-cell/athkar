import 'package:flutter_test/flutter_test.dart';

import 'package:athkar_app/core/quran_library.dart';

/// Folding a search of the mushaf.
///
/// The rule this file exists for: a reader types the spelling they were taught
/// to write, and the mushaf is printed in the Uthmani hand, which writes the
/// same words differently. Every case below is one where the two disagree, and
/// every one of them is a verse somebody would actually go looking for — the
/// opening of al-Fatiha among them, which the shared adhkar folding alone does
/// not find.
void main() {
  /// Whether a query would match a verse — the substring test `search` runs,
  /// with the same folding on both sides.
  bool matches(String verse, String query) =>
      QuranLibrary.foldForSearch(verse).contains(QuranLibrary.foldForSearch(query));

  group('the Uthmani hand and the hand a reader writes in', () {
    test('a dagger alif is the alif the reader types', () {
      expect(matches('ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ', 'الحمد لله رب العالمين'), isTrue);
      expect(matches('ذَٰلِكَ ٱلْكِتَـٰبُ لَا رَيْبَ ۛ فِيهِ', 'ذلك الكتاب لا ريب فيه'), isTrue);
    });

    test('a dagger alif is also the alif the reader does NOT type', () {
      // «ٱلرَّحْمَٰنِ» is read with the alif and written without it, and a
      // reader types it the way it is read *and* the way it is written.
      expect(matches('ٱلرَّحْمَـٰنِ ٱلرَّحِيمِ', 'الرحمن الرحيم'), isTrue);
      expect(matches('ٱلرَّحْمَـٰنِ ٱلرَّحِيمِ', 'الرحمان الرحيم'), isTrue);
    });

    test('a waw carrying a dagger alif is that alif, and is not read', () {
      expect(matches('وَأَقِيمُوا۟ ٱلصَّلَوٰةَ وَءَاتُوا۟ ٱلزَّكَوٰةَ', 'الصلاة'), isTrue);
      expect(matches('وَأَقِيمُوا۟ ٱلصَّلَوٰةَ وَءَاتُوا۟ ٱلزَّكَوٰةَ', 'الزكاة'), isTrue);
    });

    test('the hamza seats and the small marks do not have to be typed', () {
      expect(matches('إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ', 'اياك نعبد'), isTrue);
      expect(matches('وَإِذْ يَرْفَعُ إِبْرَٰهِـۧمُ ٱلْقَوَاعِدَ', 'ابراهيم'), isTrue);
      expect(matches('قُلْ هُوَ ٱللَّهُ أَحَدٌ', 'قل هو الله احد'), isTrue);
    });

    test('a verse the query is not in stays unmatched', () {
      expect(matches('قُلْ هُوَ ٱللَّهُ أَحَدٌ', 'الحمد لله'), isFalse);
      expect(matches('ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ', 'الناس'), isFalse);
    });
  });

  test('an empty or one-letter query folds to nothing worth searching', () {
    expect(QuranLibrary.foldForSearch(''), isEmpty);
    expect(QuranLibrary.foldForSearch(null), isEmpty);

    // A bare alif is dropped, so «ا» is not a search — which is the point: it
    // would otherwise match every verse in the book.
    expect(QuranLibrary.foldForSearch('ا'), isEmpty);
  });
}
