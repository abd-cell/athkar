library;

import 'package:http/http.dart' as http;

import '../core/api_client.dart';
import '../core/app_response.dart';
import '../models/models.dart';

/// One class per area of the API, all in one file.
///
/// Each is a thin naming layer over [ApiClient]: it knows the path, the query
/// shape and how to parse the payload, and nothing else. No caching, no
/// retries, no business rules — those belong to the stores in `core/` and to
/// the screens that use them.

class ConfigurationService {
  const ConfigurationService();

  Future<AppResponse<AppConfig>> get() => ApiClient.instance.get(
        'configuration',
        parse: (json) => AppConfig.fromJson(json as Map<String, dynamic>),
      );
}

class DeviceService {
  const DeviceService();

  /// Announces this install.
  ///
  /// The complete list of what the server learns about a reader is the
  /// argument list of this method: a key the app generated, a platform, a push
  /// token, a language, a timezone and a two-letter region. No name, no
  /// contact, no coordinates.
  Future<AppResponse<DeviceState>> register({
    required String deviceKey,
    required int platform,
    required String languageCode,
    required String timeZoneId,
    required bool notificationsEnabled,
    String? pushToken,
    String? appVersion,
    String? countryCode,
  }) =>
      ApiClient.instance.post(
        'devices/register',
        body: {
          'deviceKey': deviceKey,
          'platform': platform,
          'languageCode': languageCode,
          'timeZoneId': timeZoneId,
          'notificationsEnabled': notificationsEnabled,
          'pushToken': pushToken,
          'appVersion': appVersion,
          'countryCode': countryCode,
        },
        parse: (json) => DeviceState.fromJson(json as Map<String, dynamic>),
      );

  /// Publishes a rotated push token on its own.
  ///
  /// Separate from [register] because a rotation knows only the token: sending
  /// a full registration here would write back whatever language and timezone
  /// this object happened to be holding, undoing a change the reader may have
  /// just made.
  Future<AppResponse<void>> updatePushToken({
    required String deviceKey,
    required String? pushToken,
    required bool notificationsEnabled,
  }) =>
      ApiClient.instance.post(
        'devices/push-token',
        body: {
          'deviceKey': deviceKey,
          'pushToken': pushToken,
          'notificationsEnabled': notificationsEnabled,
        },
      );

  Future<AppResponse<List<NotificationItem>>> notifications({int page = 1}) =>
      ApiClient.instance.get(
        'devices/notifications',
        query: {'pageNumber': page, 'pageSize': 30},
        parse: (json) => [
          for (final entry in ((json as Map<String, dynamic>)['data'] as List<dynamic>? ?? []))
            NotificationItem.fromJson(entry as Map<String, dynamic>),
        ],
      );

  Future<AppResponse<void>> markRead(int id) =>
      ApiClient.instance.post('devices/notifications/$id/read');

  Future<AppResponse<void>> markAllRead() =>
      ApiClient.instance.post('devices/notifications/read-all');

  /// Removes this install from the server. See `settings.forgetDevice`.
  Future<AppResponse<void>> forget() => ApiClient.instance.delete('devices');
}

class ContentService {
  const ContentService();

  /// The catalogue. Pass the version already on the device; the usual answer is
  /// `isUpToDate` and an empty payload.
  Future<AppResponse<Catalog>> catalog({required String language, int? knownVersion}) =>
      ApiClient.instance.get(
        'content/catalog',
        query: {'language': language, if (knownVersion != null) 'version': knownVersion},
        parse: (json) => Catalog.fromJson(json as Map<String, dynamic>),
      );

  /// Server-side search.
  ///
  /// Used only when the reader is online *and* the local search found nothing —
  /// the device already holds the whole corpus, so searching it locally is both
  /// instant and correct. See `search_screen.dart`.
  Future<AppResponse<List<Dhikr>>> search(String term, {required String language}) =>
      ApiClient.instance.get(
        'content/search',
        query: {'language': language, 'search': term, 'pageSize': 50},
        parse: (json) => [
          for (final entry in ((json as Map<String, dynamic>)['data'] as List<dynamic>? ?? []))
            Dhikr.fromJson(entry as Map<String, dynamic>),
        ],
      );
}

class LanguageService {
  const LanguageService();

  Future<AppResponse<List<AppLanguage>>> list() => ApiClient.instance.get(
        'languages',
        parse: (json) => [
          for (final entry in (json as List<dynamic>))
            AppLanguage.fromJson(entry as Map<String, dynamic>),
        ],
      );

  /// The CMS's interface-copy overlay. Merged over the strings compiled into
  /// this build — see `core/l10n.dart`.
  Future<AppResponse<Map<String, String>>> strings(String code) => ApiClient.instance.get(
        'languages/$code/strings',
        parse: (json) {
          final strings = (json as Map<String, dynamic>)['strings'] as Map<String, dynamic>? ?? {};
          return {for (final entry in strings.entries) entry.key: '${entry.value}'};
        },
      );
}

class ReminderService {
  const ReminderService();

  /// The campaigns this device schedules itself.
  Future<AppResponse<List<DeviceReminder>>> forDevice(String language) =>
      ApiClient.instance.get(
        'reminders',
        query: {'language': language},
        parse: (json) => [
          for (final entry in (json as List<dynamic>))
            DeviceReminder.fromJson(entry as Map<String, dynamic>),
        ],
      );
}

class QuranService {
  const QuranService();

  /// Whether there is anything newer than what this device holds.
  ///
  /// [edition] omitted means "whichever mushaf is the default", which is what a
  /// device that has never chosen asks — and what an install upgraded from the
  /// single-mushaf build asks on its first launch.
  Future<AppResponse<QuranVersion>> checkVersion({int? knownVersion, String? edition}) =>
      ApiClient.instance.get(
        'quran/check-version',
        query: {
          if (knownVersion != null) 'version': knownVersion,
          if (edition != null) 'edition': edition,
        },
        parse: (json) => QuranVersion.fromJson(json as Map<String, dynamic>),
      );

  /// The mushafs on offer. Anonymous, like everything else the reader's app
  /// asks for — which mushaf somebody reads is not the server's business.
  Future<AppResponse<List<QuranEdition>>> editions() => ApiClient.instance.get(
        'quran/editions',
        parse: (json) => [
          for (final entry in (json as List<dynamic>))
            QuranEdition.fromJson(entry as Map<String, dynamic>),
        ],
      );

  /// Opens the package as a byte stream. The caller writes it to disk and
  /// verifies the checksum — see `core/quran_store.dart`.
  Future<AppResponse<http.StreamedResponse>> download({String? edition}) =>
      ApiClient.instance.download(
        'quran/download',
        query: {if (edition != null) 'edition': edition},
      );
}

class WidgetService {
  const WidgetService();

  /// The admin's widget rules. Anonymous, and cached by the caller — the widget
  /// must keep drawing when the phone is offline.
  Future<AppResponse<WidgetSettings>> get() => ApiClient.instance.get(
        'widget',
        parse: (json) => WidgetSettings.fromJson(json as Map<String, dynamic>),
      );

  /// The gallery the reader browses, with the rules it is browsed under.
  ///
  /// One call rather than two: the app fetches both on every sync and neither
  /// half is any use without the other.
  Future<AppResponse<WidgetCatalog>> catalog(String language) => ApiClient.instance.get(
        'widget/catalog',
        query: {'language': language},
        parse: (json) => WidgetCatalog.fromJson(json as Map<String, dynamic>),
      );
}

class SupportService {
  const SupportService();

  Future<AppResponse<List<FaqItem>>> faq(String language) => ApiClient.instance.get(
        'support/faq',
        query: {'language': language},
        parse: (json) => [
          for (final entry in (json as List<dynamic>))
            FaqItem.fromJson(entry as Map<String, dynamic>),
        ],
      );

  Future<AppResponse<void>> sendFeedback({
    required int kind,
    required String message,
    int? dhikrId,
    String? contactEmail,
    String? appVersion,
  }) =>
      ApiClient.instance.post(
        'support/feedback',
        body: {
          'kind': kind,
          'message': message,
          'dhikrId': dhikrId,
          'contactEmail': contactEmail,
          'appVersion': appVersion,
        },
      );
}

/// One instance of each, so screens do not construct services.
class Api {
  const Api._();

  static const configuration = ConfigurationService();
  static const devices = DeviceService();
  static const content = ContentService();
  static const languages = LanguageService();
  static const reminders = ReminderService();
  static const quran = QuranService();
  static const support = SupportService();
  static const widget = WidgetService();
}
