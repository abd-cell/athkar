import '../l10n/strings_ar.dart';
import 'app_response.dart';

/// Turns a numeric error code into a sentence.
///
/// The server sends codes and never prose — see `Shareds/Models/ErrorCode.cs`.
/// Each client owns its own wording, which is what lets the app say something
/// specific to a reader holding a phone while the CMS says something specific
/// to an editor.
///
/// The map is deliberately partial. Most codes describe things a reader cannot
/// act on, so anything unlisted falls back to one honest generic sentence
/// rather than leaking a term from the domain model.
class ErrorMessages {
  const ErrorMessages._();

  /// The key in the string maps for a given code, or null to use the generic.
  static String? _keyFor(int code) => switch (code) {
        // Local failures, raised by ApiClient itself.
        AppErrorCodes.network => 'error.network',
        AppErrorCodes.timeout => 'error.timeout',
        AppErrorCodes.offlineNoCache => 'error.offlineNoCache',

        // The handful of server codes a reader can do something about.
        3 => 'error.notFound',
        7 => 'error.fileTooLarge',
        200 => 'error.deviceNotFound',
        601 => 'error.noQuranPackage',
        602 => 'error.checksumMismatch',
        702 => 'error.tooMuchFeedback',
        _ => null,
      };

  /// The message for [code] in [languageCode]. Never returns null: an
  /// unrecognised code still has to say something to the person waiting.
  static String resolve(int code, String languageCode, Map<String, String> strings) {
    final key = _keyFor(code) ?? 'error.generic';
    return strings[key] ?? arabicStrings[key] ?? 'حدث خطأ غير متوقع.';
  }
}
