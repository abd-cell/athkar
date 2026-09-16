import 'dart:io';

import 'package:athkar_app/l10n/strings_ar.dart';
import 'package:flutter_test/flutter_test.dart';

/// Every key the code asks for must exist.
///
/// The parity test next door checks that Arabic and English agree with each
/// other. It cannot catch the failure this one is for: a key that is in
/// **neither** map, because a screen was written against keys that were added
/// to the CMS's i18n files instead of the app's.
///
/// That failure is quiet by design. `tr` falls back to Arabic, then to the raw
/// key, and prints to the debug console — so the screen renders, nothing
/// throws, and a release build shows `widget.surface.home` to a reader in a
/// heading where a sentence should be. It happened once during this feature and
/// was caught by looking at the running app, which is not a method that scales.
void main() {
  test('every tr(…) key in lib/ exists in the Arabic map', () {
    // Matches the literal forms only — `tr('a.b')` and `tr("a.b")`, with or
    // without arguments. A key built at runtime cannot be checked here and is
    // deliberately out of scope; there are none today.
    final call = RegExp(r'''\btr\(\s*(['"])([a-zA-Z0-9_.]+)\1''');

    final missing = <String, Set<String>>{};

    for (final entity in Directory('lib').listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;

      // The maps themselves are where the keys are defined, not used.
      if (entity.path.contains('l10n')) continue;

      for (final match in call.allMatches(entity.readAsStringSync())) {
        final key = match.group(2)!;
        if (arabicStrings.containsKey(key)) continue;

        missing.putIfAbsent(entity.path, () => {}).add(key);
      }
    }

    expect(
      missing,
      isEmpty,
      reason: 'these keys are asked for but defined nowhere:\n'
          '${missing.entries.map((e) => '  ${e.key}: ${e.value.join(', ')}').join('\n')}',
    );
  });
}
