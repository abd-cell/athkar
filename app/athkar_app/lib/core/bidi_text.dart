/// Bidirectional text, for the one place the app cannot control the rendering.
///
/// Inside the app a `Directionality` widget settles which way a line runs. A
/// **notification does not have one**: it is drawn by Android or iOS, in a shade
/// whose base direction comes from the *device's* locale, not from the text. So
/// an Arabic reminder on a phone set to English is laid out left-to-right, and
/// the damage is not that the Arabic reads backwards — the bidi algorithm gets
/// the letters right — but that everything neutral around it moves:
///
/// - «حان الآن موعد صلاة الفجر.» loses its full stop to the left edge,
/// - «صحيح البخاري 6405» renders as «6405 صحيح البخاري»,
/// - «لمدينة Amman» drags the following space and punctuation into the Latin run.
///
/// Two different jobs, and they are not interchangeable:
///
/// **[isolate]** wraps a value *inserted into* a sentence — a city name, a
/// number — so it cannot disturb its neighbours whichever way it runs.
/// **[forNotification]** states the base direction of each line, so the shade
/// lays the line out by what it says rather than by where the phone is set.
///
/// The server applies the identical rules in `Shareds/Text/BidiText.cs` to the
/// pushes it sends, because a push that arrives while the app is closed is drawn
/// without the app ever seeing it. `test/bidi_text_test.dart` and
/// `BidiTextTests` hold the same cases so a rule cannot drift on one side.
class BidiText {
  const BidiText._();

  /// First-strong isolate: "work out this run's direction on its own".
  static const _fsi = '\u2068';

  /// Pop directional isolate — closes [_fsi].
  static const _pdi = '\u2069';

  /// Right-to-left mark. Invisible, strong, and enough to set a line's base
  /// direction when it leads.
  static const _rlm = '\u200F';

  /// Left-to-right mark.
  static const _lrm = '\u200E';

  /// Whether a run reads right-to-left, by the first strong character in it.
  ///
  /// Written out rather than delegated to `intl`'s `detectRtlDirectionality`,
  /// which answers a different question: it is a *heuristic* that weighs how
  /// much of the text is RTL, so it calls «٦٤٠٥» right-to-left. Unicode classes
  /// Arabic-Indic digits as a number, not as direction, and the question here is
  /// the first-strong one — which way should this line be laid out. Delegating
  /// would have put the app and the server into disagreement on exactly the
  /// string a takhrij reference is made of.
  ///
  /// Neutrals are skipped rather than counted: a line opening with a digit or a
  /// guillemet is still an Arabic line.
  static bool isRtl(String text) {
    for (final unit in text.runes) {
      if (_isStrongRtl(unit)) return true;
      if (_isLetter(unit)) return false;
    }

    return false;
  }

  /// Wraps a value so it cannot change the direction of the sentence it lands in.
  ///
  /// For anything substituted at runtime: a city from the reader's settings, a
  /// hadith number, a name. Whether it matches the sentence's direction is not
  /// knowable where the sentence was written, so it is isolated either way.
  /// Empty in, empty out — isolating nothing would leave two invisible
  /// characters where a value should have been.
  static String isolate(String value) => value.isEmpty ? value : '$_fsi$value$_pdi';

  /// Marks every line of a notification with its own base direction.
  ///
  /// Per line, not per message, and that is the point: a reminder for an English
  /// reader is an English announcement with an Arabic narration beneath it, and
  /// the two want opposite directions in the same notification. One mark for the
  /// whole body would have to be wrong about one of them.
  ///
  /// Blank lines are left alone — the paragraph break between the announcement
  /// and the narration is deliberate, and a mark on it would make it a line with
  /// content as far as some shades are concerned.
  /// A line that is already marked is left as it is, which makes this safe to
  /// apply twice. It genuinely happens: the server marks a push before sending
  /// it, and when that push arrives with the app in the foreground the app
  /// redraws it through here. Without the check the text collects a second mark
  /// every time — harmless to read, but it accumulates anywhere the string is
  /// stored, and it makes the two stacks disagree about what they sent.
  static String forNotification(String text) {
    if (text.isEmpty) return text;

    return text.split('\n').map((line) {
      if (line.trim().isEmpty || line.startsWith(_rlm) || line.startsWith(_lrm)) return line;
      return '${isRtl(line) ? _rlm : _lrm}$line';
    }).join('\n');
  }

  /// The strong right-to-left blocks: Hebrew, Arabic and its supplements,
  /// Syriac, Thaana, NKo, and the presentation forms — minus the digits that
  /// live inside them.
  static bool _isStrongRtl(int c) {
    // «٦٤٠٥» is a number, not a direction. These sit inside the Arabic block, so
    // the ranges below would otherwise swallow them and lay a bare takhrij
    // reference out as an Arabic line.
    final isArabicNumeral = (c >= 0x0660 && c <= 0x0669) ||
        c == 0x066B ||
        c == 0x066C ||
        (c >= 0x06F0 && c <= 0x06F9);

    if (isArabicNumeral) return false;

    return (c >= 0x0590 && c <= 0x05FF) || //  Hebrew
        (c >= 0x0600 && c <= 0x06FF) || //     Arabic
        (c >= 0x0700 && c <= 0x074F) || //     Syriac
        (c >= 0x0750 && c <= 0x077F) || //     Arabic Supplement
        (c >= 0x0780 && c <= 0x07BF) || //     Thaana
        (c >= 0x07C0 && c <= 0x07FF) || //     NKo
        (c >= 0x0860 && c <= 0x08FF) || //     Syriac Supplement, Arabic Extended-A
        (c >= 0xFB1D && c <= 0xFDFF) || //     Hebrew and Arabic presentation forms
        (c >= 0xFE70 && c <= 0xFEFF);
  }

  /// A letter of any script — what makes a character *strong* once the
  /// right-to-left blocks above have been ruled out.
  static bool _isLetter(int c) =>
      (c >= 0x41 && c <= 0x5A) || //   A–Z
      (c >= 0x61 && c <= 0x7A) || //   a–z
      (c >= 0x00C0 && c <= 0x024F) || // Latin supplements and extensions
      (c >= 0x0370 && c <= 0x03FF) || // Greek
      (c >= 0x0400 && c <= 0x04FF) || // Cyrillic
      (c >= 0x0900 && c <= 0x0DFF) || // Indic scripts
      (c >= 0x0E00 && c <= 0x0E7F) || // Thai
      (c >= 0x1E00 && c <= 0x1EFF) || // Latin Extended Additional
      (c >= 0x3040 && c <= 0x30FF) || // Kana
      (c >= 0x4E00 && c <= 0x9FFF); //  CJK
}
