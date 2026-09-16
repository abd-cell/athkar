import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:athkar_app/core/settings.dart';

/// How the mushaf opens.
///
/// A package that carries the printed-page layer *is* the mushaf as the press
/// set it — 604 pages broken where the King Fahd Complex broke them — and that
/// is what a reader knows by sight and navigates by memory. So pages are the
/// default, and the reader's choice outlives the screen that made it.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('an install that has never chosen opens the mushaf as pages', () async {
    SharedPreferences.setMockInitialValues({});

    final settings = await Settings.load();

    expect(settings.quranAsPages, isTrue);
  });

  test('a reader who chose flowing verses keeps that choice', () async {
    SharedPreferences.setMockInitialValues({});

    final settings = await Settings.load();
    await settings.setQuranAsPages(false);

    expect(settings.quranAsPages, isFalse);

    // And it survives the next launch, which is the whole point of storing it:
    // the screen's own field is rebuilt every time the tab is opened.
    final relaunched = await Settings.load();
    expect(relaunched.quranAsPages, isFalse);
  });

  test('choosing pages again is remembered too', () async {
    SharedPreferences.setMockInitialValues({'quran.asPages': false});

    final settings = await Settings.load();
    expect(settings.quranAsPages, isFalse);

    await settings.setQuranAsPages(true);
    expect((await Settings.load()).quranAsPages, isTrue);
  });
}
