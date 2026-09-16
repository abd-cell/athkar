import 'package:athkar_app/core/bidi_text.dart';
import 'package:flutter_test/flutter_test.dart';

/// Direction, for the one surface the app does not render itself.
///
/// A notification is laid out by the OS, in a shade whose base direction comes
/// from the *device's* locale. So the cases that matter here are the ones where
/// the phone's setting and the text disagree — an Arabic reminder on a phone set
/// to English, and an English reminder carrying an Arabic narration. Both are
/// ordinary, and both look broken without a mark.
///
/// The failure is never "the Arabic came out backwards" — the bidi algorithm
/// gets letters right. It is that the *neutrals* move: full stops jump to the
/// far edge, and a number beside an Arabic book name swaps sides.
void main() {
  const rlm = '\u200F';
  const lrm = '\u200E';
  const fsi = '\u2068';
  const pdi = '\u2069';

  group('direction detection', () {
    test('reads the first strong character, not the first character', () {
      // The takhrij line often opens with a digit or a quotation mark. Neither
      // is strong, and neither should decide which way the line runs.
      expect(BidiText.isRtl('«صحيح البخاري»'), isTrue);
      expect(BidiText.isRtl('6405 صحيح البخاري'), isTrue);
      expect(BidiText.isRtl('"Sahih al-Bukhari"'), isFalse);
    });

    test('text with no strong character at all is not called right-to-left', () {
      expect(BidiText.isRtl('6405'), isFalse);
      expect(BidiText.isRtl('   '), isFalse);
      expect(BidiText.isRtl(''), isFalse);
    });

    test('Arabic-Indic digits are a number, not a direction', () {
      // They sit inside the Arabic block, so a range check written by hand
      // swallows them — and a takhrij reference standing alone would be laid
      // out as an Arabic line. This is the case the two implementations are
      // most likely to drift on, so it is asserted identically on both sides.
      expect(BidiText.isRtl('٦٤٠٥'), isFalse);
      expect(BidiText.isRtl('صحيح البخاري ٦٤٠٥'), isTrue);
    });
  });

  group('isolating an inserted value', () {
    test('a Latin city inside an Arabic sentence cannot drag its punctuation', () {
      final filled = 'حسب التوقيت المحلي لمدينة ${BidiText.isolate('Amman')}.';

      expect(filled, 'حسب التوقيت المحلي لمدينة ${fsi}Amman$pdi.');

      // The isolate closes before the full stop, so the stop stays with the
      // Arabic sentence rather than joining the Latin run.
      expect(filled.endsWith('$pdi.'), isTrue);
    });

    test('an Arabic city is isolated too, because direction is not knowable up front', () {
      // Whether a city name runs one way or the other is decided by the
      // reader's settings, long after the sentence was written in the CMS.
      expect(BidiText.isolate('عمّان'), '$fsiعمّان$pdi');
    });

    test('nothing in, nothing out', () {
      // Two invisible characters where a value should have been would be worse
      // than the gap: they are unsearchable and unexplainable.
      expect(BidiText.isolate(''), '');
    });
  });

  group('marking a notification', () {
    test('an Arabic line is marked right-to-left', () {
      expect(
        BidiText.forNotification('حان الآن موعد صلاة الفجر'),
        '$rlmحان الآن موعد صلاة الفجر',
      );
    });

    test('an English line is marked left-to-right', () {
      expect(
        BidiText.forNotification('It is now time for Fajr'),
        '${lrm}It is now time for Fajr',
      );
    });

    test('each line is marked on its own, because a reminder carries both', () {
      // The English reader's case: an English announcement with an Arabic
      // narration beneath it. One mark for the whole body would have to be
      // wrong about one of the two.
      const body = 'It is now time for Fajr\n\n'
          'سُبْحَانَ اللَّهِ وَبِحَمْدِهِ\n'
          'صحيح البخاري 6405';

      final marked = BidiText.forNotification(body).split('\n');

      expect(marked[0], startsWith(lrm));
      expect(marked[1], '');
      expect(marked[2], startsWith(rlm));

      // The line that reverses without help: an Arabic book name and a Latin
      // number. Marked right-to-left, «صحيح البخاري 6405» keeps its order.
      expect(marked[3], startsWith(rlm));
    });

    test('the blank line between announcement and narration stays blank', () {
      // A mark on it makes it a line with content as far as some shades are
      // concerned, and the paragraph break becomes a stray empty row.
      expect(BidiText.forNotification('أ\n\nب').split('\n')[1], '');
    });

    test('empty text is left exactly as it is', () {
      expect(BidiText.forNotification(''), '');
    });

    test('marking twice adds one mark, not two', () {
      // Not hypothetical: the server marks a push before sending it, and the
      // app redraws that same push through here when it arrives in the
      // foreground. Caught on a real phone, where the title came through
      // carrying two right-to-left marks.
      final once = BidiText.forNotification('حان الآن موعد صلاة الفجر');
      final twice = BidiText.forNotification(once);

      expect(once, '$rlmحان الآن موعد صلاة الفجر');
      expect(twice, once);
      expect(rlm.allMatches(twice).length, 1);
    });

    test('a line already marked the other way is left alone', () {
      // Whoever marked it first knew something this pass does not — a
      // transliterated line, say. Re-deciding would overrule them.
      const marked = '${lrm}Sahih al-Bukhari';
      expect(BidiText.forNotification(marked), marked);
    });
  });
}
