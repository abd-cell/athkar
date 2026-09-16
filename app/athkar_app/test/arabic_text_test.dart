import 'package:athkar_app/core/arabic_text.dart';
import 'package:flutter_test/flutter_test.dart';

/// The folding that makes search work, on the app's side.
///
/// **These cases are deliberately identical to `ArabicTextTests.cs` on the
/// server.** The two implementations have to agree: the server stores the
/// folded form of every dhikr, and a rule that exists on one side only makes
/// the app search for something the server never wrote down. If you add a case
/// here, add it there.
void main() {
  group('ArabicText.normalize', () {
    const cases = {
      // Diacritics are dropped entirely.
      'سُبْحَانَ اللهِ': 'سبحان الله',
      'أَذْكَارُ الصَّبَاحِ': 'اذكار الصباح',
      // Every alif shape folds to the bare one.
      'آمَنَ': 'امن',
      'إِيمَان': 'ايمان',
      'ٱلْحَمْدُ': 'الحمد',
      // Alif maqsura and ya are one letter for searching.
      'مُوسَى': 'موسي',
      // Ta marbuta folds to ha.
      'الصَّلَاة': 'الصلاه',
      // Hamza carriers fold to the letter underneath.
      'مُؤْمِن': 'مومن',
      'سَائِل': 'سايل',
      // The tatweel is a stretching glyph, not a letter.
      'الحــــمد': 'الحمد',
      // Arabic-Indic digits become ASCII.
      '٣٣': '33',
      '١٠٠': '100',
    };

    cases.forEach((input, expected) {
      test('folds "$input"', () => expect(ArabicText.normalize(input), expected));
    });

    test('collapses whitespace and punctuation', () {
      expect(ArabicText.normalize('  لا   إلهَ، إلَّا  اللهُ!  '), 'لا اله الا الله');
    });

    test('handles nothing gracefully', () {
      expect(ArabicText.normalize(null), '');
      expect(ArabicText.normalize(''), '');
      expect(ArabicText.normalize('   '), '');
    });
  });

  test('a bare query matches vocalised text', () {
    const corpus = 'سُبْحَانَ اللهِ وَبِحَمْدِهِ، سُبْحَانَ اللهِ العَظِيمِ';

    expect(ArabicText.contains(corpus, 'سبحان الله'), isTrue);
    expect(ArabicText.contains(corpus, 'وبحمده'), isTrue);
    expect(ArabicText.contains(corpus, 'استغفر'), isFalse);
  });
}
