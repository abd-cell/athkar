import 'package:athkar_app/l10n/strings_ar.dart';
import 'package:athkar_app/l10n/strings_en.dart';
import 'package:flutter_test/flutter_test.dart';

/// The interface copy, checked for the things a reader would notice.
///
/// Both files ask in their headers that every key exist in the other, and
/// nothing was holding them to it: a key added to one only shows up as raw
/// «home.hadithOfDay» on somebody's screen in the other language.
void main() {
  group('parity', () {
    test('every Arabic key has an English one', () {
      expect(arabicStrings.keys.toSet().difference(englishStrings.keys.toSet()), isEmpty);
    });

    test('every English key has an Arabic one', () {
      expect(englishStrings.keys.toSet().difference(arabicStrings.keys.toSet()), isEmpty);
    });

    test('nothing is left blank', () {
      for (final entry in {...arabicStrings, ...englishStrings}.entries) {
        expect(entry.value.trim(), isNotEmpty, reason: entry.key);
      }
    });
  });

  group('placeholders', () {
    /// `{name}` in one language and not the other means the value silently
    /// vanishes for half the readers.
    Set<String> placeholders(String value) =>
        RegExp(r'\{(\w+)\}').allMatches(value).map((m) => m.group(1)!).toSet();

    test('match across languages', () {
      for (final key in arabicStrings.keys) {
        final english = englishStrings[key];
        if (english == null) continue;

        expect(
          placeholders(english),
          placeholders(arabicStrings[key]!),
          reason: key,
        );
      }
    });
  });

  group('reminder wording', () {
    // The anchor is a noun the template puts a preposition in front of. When
    // the nouns carried their own, the screen read «بعد بعد العصر بـ٣٠ دقيقة»
    // and English «30 min before after Asr».
    const anchors = [
      'reminders.anchor.fajr',
      'reminders.anchor.sunrise',
      'reminders.anchor.dhuhr',
      'reminders.anchor.asr',
      'reminders.anchor.maghrib',
      'reminders.anchor.isha',
      'reminders.anchor.bedtime',
    ];

    test('anchor names are bare nouns', () {
      for (final key in anchors) {
        expect(arabicStrings[key], isNotNull, reason: key);
        expect(arabicStrings[key]!.startsWith('بعد '), isFalse, reason: key);
        expect(arabicStrings[key]!.startsWith('قبل '), isFalse, reason: key);

        final english = englishStrings[key]!.toLowerCase();
        expect(english.startsWith('after '), isFalse, reason: key);
        expect(english.startsWith('before '), isFalse, reason: key);
      }
    });

    test('the templates carry the preposition instead', () {
      expect(arabicStrings['reminders.offsetAfter'], startsWith('بعد '));
      expect(arabicStrings['reminders.offsetBefore'], startsWith('قبل '));
      expect(englishStrings['reminders.offsetAfter'], contains('after '));
      expect(englishStrings['reminders.offsetBefore'], contains('before '));
    });

    test('a zero offset gets its own sentence', () {
      // Otherwise a reminder that fires *at* the prayer reads «بعد الفجر بـ٠
      // دقيقة», which looks like a bug to the reader deciding whether to trust
      // the alert.
      expect(arabicStrings['reminders.atAnchor'], 'عند {anchor}');
      expect(englishStrings['reminders.atAnchor'], contains('{anchor}'));

      for (final key in ['reminders.atAnchor']) {
        expect(arabicStrings[key], isNot(contains('{minutes}')), reason: key);
        expect(englishStrings[key], isNot(contains('{minutes}')), reason: key);
      }
    });

    test('the bedtime row keeps a label of its own', () {
      // It is a settings row, not an anchor in a sentence, so it reads as a
      // phrase rather than the bare noun the template needs.
      expect(arabicStrings['reminders.bedtime'], 'قبل النوم');
      expect(englishStrings['reminders.bedtime'], isNotNull);
    });
  });
}
