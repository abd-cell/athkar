/// Folds Arabic to a form that search can match on.
///
/// The exact mirror of `Shareds/Text/ArabicText.cs` on the server. The two must
/// stay in step: the server stores the folded form of every dhikr, and a rule
/// added on one side only makes the app search for something the server never
/// wrote down.
///
/// Why it exists at all: Arabic is written with marks a reader supplies from
/// memory and a typist mostly omits. Somebody looking for «اذكار الصباح» types
/// it with no diacritics, a bare alif for the hamza, and quite possibly a final
/// ه where the text has ة.
class ArabicText {
  const ArabicText._();

  /// Harakat, Qur'anic annotation marks, the dagger alif and the tatweel.
  /// Removed entirely: they carry pronunciation, never identity.
  static bool _isDiacritic(int c) =>
      (c >= 0x064B && c <= 0x065F) || // fathatan … wavy hamza below
      c == 0x0640 || // tatweel, a stretching glyph rather than a letter
      c == 0x0670 || // superscript (dagger) alif
      (c >= 0x06D6 && c <= 0x06ED); // annotation, sajdah and waqf marks

  /// The folded form: no diacritics, one shape per letter family, no
  /// punctuation, single spaces, lower-cased so a Latin query folds too.
  static String normalize(String? raw) {
    if (raw == null || raw.trim().isEmpty) return '';

    final buffer = StringBuffer();
    var lastWasSpace = false;

    for (final rune in raw.runes) {
      if (_isDiacritic(rune)) continue;

      final folded = _fold(rune);

      if (folded == ' ') {
        if (!lastWasSpace && buffer.isNotEmpty) buffer.write(' ');
        lastWasSpace = true;
        continue;
      }

      if (folded == null) continue;

      buffer.write(folded);
      lastWasSpace = false;
    }

    return buffer.toString().trimRight();
  }

  /// One character's folded form: `' '` for anything that separates words,
  /// `null` for anything to drop outright.
  static String? _fold(int c) {
    switch (c) {
      // Alif family → bare alif. The hamza's seat is orthographic and is the
      // single most common thing a typist gets "wrong".
      case 0x0622: // آ
      case 0x0623: // أ
      case 0x0625: // إ
      case 0x0671: // ٱ
        return 'ا';

      // Alif maqsura → ya: the two are interchanged freely, and different
      // keyboards default to different ones.
      case 0x0649:
        return 'ي';

      // Ta marbuta → ha, which is how it is typed as often as not.
      case 0x0629:
        return 'ه';

      case 0x0624: // ؤ
        return 'و';
      case 0x0626: // ئ
        return 'ي';
      case 0x0621: // ء standing alone
        return null;
    }

    // Arabic-Indic and extended digits → ASCII, so «٣٣» and «33» are one search.
    if (c >= 0x0660 && c <= 0x0669) return String.fromCharCode(0x30 + (c - 0x0660));
    if (c >= 0x06F0 && c <= 0x06F9) return String.fromCharCode(0x30 + (c - 0x06F0));

    final char = String.fromCharCode(c);
    if (char.trim().isEmpty) return ' ';
    if (_punctuation.hasMatch(char)) return ' ';
    return char.toLowerCase();
  }

  static final _punctuation = RegExp(r'[\p{P}\p{S}]', unicode: true);

  /// True when [haystack]'s folded text contains the folded [needle].
  static bool contains(String? haystack, String? needle) =>
      normalize(haystack).contains(normalize(needle));

  /// [raw] with harakat and Qur'anic annotation marks removed, for *display*
  /// — the reader's «إظهار التشكيل» toggle.
  ///
  /// Deliberately not [normalize]: that also folds letter shapes, strips
  /// punctuation and lower-cases for search, none of which belong in text
  /// shown on screen. This keeps every other character exactly as written.
  static String stripDiacritics(String raw) =>
      String.fromCharCodes(raw.runes.where((c) => !_isDiacritic(c)));
}
