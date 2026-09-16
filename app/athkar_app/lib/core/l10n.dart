import 'package:flutter/widgets.dart';

import '../l10n/strings_ar.dart';
import '../l10n/strings_en.dart';

/// Interface copy, in three layers.
///
/// 1. The CMS overlay, fetched from `/languages/{code}/strings` and cached.
/// 2. The map compiled into this build for the chosen language.
/// 3. Arabic, which is complete by construction.
///
/// That ordering is the whole point of the localisation design: a typo in a
/// button can be corrected the same day without a store review, and an install
/// that has never reached the network still renders complete sentences.
class AppLocalizations {
  AppLocalizations(this.languageCode, {Map<String, String> overlay = const {}})
      : _overlay = overlay;

  final String languageCode;
  final Map<String, String> _overlay;

  static const supported = ['ar', 'en'];

  static AppLocalizations of(BuildContext context) =>
      Localizations.of<AppLocalizations>(context, AppLocalizations)!;

  Map<String, String> get _built => switch (languageCode) {
        'en' => englishStrings,
        _ => arabicStrings,
      };

  bool get isRtl => languageCode == 'ar';

  /// The text for [key], with `{placeholder}` substitution.
  ///
  /// Never throws and never returns null: a missing key is a bug to fix, not a
  /// reason to show the reader an exception, so it degrades to Arabic, then to
  /// the key itself, and complains in debug builds.
  String tr(String key, [Map<String, Object?> args = const {}]) {
    var text = _overlay[key] ?? _built[key] ?? arabicStrings[key];

    if (text == null) {
      assert(() {
        debugPrint('[l10n] missing key "$key" for "$languageCode"');
        return true;
      }());
      return key;
    }

    for (final entry in args.entries) {
      text = text!.replaceAll('{${entry.key}}', '${entry.value}');
    }

    return text!;
  }
}

/// `context.tr('some.key')`, which is how every screen reads copy.
extension LocalizationsExtension on BuildContext {
  String tr(String key, [Map<String, Object?> args = const {}]) =>
      AppLocalizations.of(this).tr(key, args);
}

class AppLocalizationsDelegate extends LocalizationsDelegate<AppLocalizations> {
  const AppLocalizationsDelegate(this.overlay);

  /// The CMS overlay for the current language, passed in from the settings
  /// store so a language switch rebuilds with the right one.
  final Map<String, String> overlay;

  @override
  bool isSupported(Locale locale) => AppLocalizations.supported.contains(locale.languageCode);

  @override
  Future<AppLocalizations> load(Locale locale) async =>
      AppLocalizations(locale.languageCode, overlay: overlay);

  @override
  bool shouldReload(AppLocalizationsDelegate old) => old.overlay != overlay;
}
