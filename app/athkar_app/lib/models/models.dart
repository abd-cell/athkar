/// Every DTO the app exchanges with the server, in one file.
///
/// One file on purpose: they are read together, they change together, and each
/// is a dozen lines. The enums mirror `Shareds/Enums` on the server and the
/// CMS's `core/api/models.ts` — the numbers are the contract, so reordering a
/// member here silently re-labels data in all three.
library;

export 'widget_catalog.dart';
export 'widget_settings.dart';

// ─────────────────────────────── enums ───────────────────────────────

/// Mirrors `Shareds/Enums/HadithGrade.cs`.
enum HadithGrade {
  sahih(1),
  hasan(2),
  sahihLighayrihi(3),
  hasanLighayrihi(4),
  quranVerse(5),
  muttafaqAlayh(6);

  const HadithGrade(this.value);
  final int value;

  static HadithGrade? fromValue(int? value) {
    if (value == null) return null;
    for (final grade in HadithGrade.values) {
      if (grade.value == value) return grade;
    }
    return null;
  }
}

/// Mirrors `Shareds/Enums/CategoryRhythm.cs`.
enum CategoryRhythm {
  none(0),
  daily(1),
  monthly(2);

  const CategoryRhythm(this.value);
  final int value;

  static CategoryRhythm fromValue(int? value) => switch (value) {
        1 => CategoryRhythm.daily,
        2 => CategoryRhythm.monthly,
        _ => CategoryRhythm.none,
      };
}

/// Mirrors `Shareds/Enums/PrayerAnchor.cs`.
enum PrayerAnchor {
  none(0),
  fajr(1),
  sunrise(2),
  dhuhr(3),
  asr(4),
  maghrib(5),
  isha(6),
  bedtime(7),
  islamicMidnight(8),
  lastThirdOfNight(9);

  const PrayerAnchor(this.value);
  final int value;

  static PrayerAnchor fromValue(int? value) {
    for (final anchor in PrayerAnchor.values) {
      if (anchor.value == value) return anchor;
    }
    return PrayerAnchor.none;
  }
}

/// Mirrors `Shareds/Enums/ReminderKind.cs`.
enum ReminderKind {
  fixedTime(1),
  prayerAnchored(2);

  const ReminderKind(this.value);
  final int value;

  static ReminderKind fromValue(int? value) =>
      value == 2 ? ReminderKind.prayerAnchored : ReminderKind.fixedTime;
}

/// Mirrors `Shareds/Enums/NotificationKind.cs`.
enum NotificationKind {
  reminder(1),
  broadcast(2),
  system(3);

  const NotificationKind(this.value);
  final int value;

  static NotificationKind fromValue(int? value) => switch (value) {
        2 => NotificationKind.broadcast,
        3 => NotificationKind.system,
        _ => NotificationKind.reminder,
      };
}

/// Mirrors `Shareds/Enums/CalculationMethod.cs`. Mapped onto the `adhan`
/// package's own parameters in `core/prayer_times.dart`.
enum CalculationMethod {
  ummAlQura(1),
  muslimWorldLeague(2),
  egyptian(3),
  karachi(4),
  kuwait(5),
  qatar(6),
  dubai(7),
  turkey(8),
  northAmerica(9),
  singapore(10),
  tehran(11),
  moonsightingCommittee(12),

  /// دائرة الإفتاء العام الأردنية — Fajr 18°, Isha 18°.
  ///
  /// Appended at 13: these numbers are the cross-stack contract, so a new
  /// convention goes on the end and never between existing members.
  jordan(13);

  const CalculationMethod(this.value);
  final int value;

  static CalculationMethod fromValue(int? value) {
    for (final method in CalculationMethod.values) {
      if (method.value == value) return method;
    }
    return CalculationMethod.ummAlQura;
  }
}

/// Mirrors `Shareds/Enums/Madhab.cs`.
enum Madhab {
  standard(1),
  hanafi(2);

  const Madhab(this.value);
  final int value;

  static Madhab fromValue(int? value) => value == 2 ? Madhab.hanafi : Madhab.standard;
}

/// Which days a reminder repeats on, as the server's bit set.
class WeekDays {
  const WeekDays._();

  static const none = 0;
  static const all = 127;

  /// True when [mask] selects [weekday], using Dart's `DateTime.weekday`
  /// (Monday = 1 … Sunday = 7). The server's bit 0 is Sunday, which is the one
  /// place these two calendars disagree.
  static bool includes(int mask, int weekday) {
    final bit = weekday == DateTime.sunday ? 0 : weekday;
    return (mask & (1 << bit)) != 0;
  }
}

// ─────────────────────────────── models ───────────────────────────────

/// `/configuration` — fetched before the first frame, cached forever after.
class AppConfig {
  const AppConfig({
    required this.primaryColor,
    required this.defaultCalculationMethod,
    required this.defaultMadhab,
    required this.morningOffsetMinutes,
    required this.eveningOffsetMinutes,
    required this.contentVersion,
    this.reviewingScholar,
    this.reviewingScholarCredential,
    this.supportEmail,
    this.supportWebsite,
    this.privacyPolicyUrl,
    this.androidPackageName,
    this.iosAppStoreId,
  });

  final String primaryColor;
  final CalculationMethod defaultCalculationMethod;
  final Madhab defaultMadhab;
  final int morningOffsetMinutes;
  final int eveningOffsetMinutes;
  final int contentVersion;
  final String? reviewingScholar;
  final String? reviewingScholarCredential;
  final String? supportEmail;
  final String? supportWebsite;
  final String? privacyPolicyUrl;
  final String? androidPackageName;
  final String? iosAppStoreId;

  /// What a first launch with no network shows. Every value matches the
  /// server's own defaults, so the app is never blank while it waits.
  static const fallback = AppConfig(
    primaryColor: '#2B6B4A',
    defaultCalculationMethod: CalculationMethod.ummAlQura,
    defaultMadhab: Madhab.standard,
    morningOffsetMinutes: 30,
    eveningOffsetMinutes: 30,
    contentVersion: 0,
  );

  factory AppConfig.fromJson(Map<String, dynamic> json) => AppConfig(
        primaryColor: json['primaryColor'] as String? ?? '#2B6B4A',
        defaultCalculationMethod:
            CalculationMethod.fromValue(json['defaultCalculationMethod'] as int?),
        defaultMadhab: Madhab.fromValue(json['defaultMadhab'] as int?),
        morningOffsetMinutes: json['morningOffsetMinutes'] as int? ?? 30,
        eveningOffsetMinutes: json['eveningOffsetMinutes'] as int? ?? 30,
        contentVersion: json['contentVersion'] as int? ?? 0,
        reviewingScholar: json['reviewingScholar'] as String?,
        reviewingScholarCredential: json['reviewingScholarCredential'] as String?,
        supportEmail: json['supportEmail'] as String?,
        supportWebsite: json['supportWebsite'] as String?,
        privacyPolicyUrl: json['privacyPolicyUrl'] as String?,
        androidPackageName: json['androidPackageName'] as String?,
        iosAppStoreId: json['iosAppStoreId'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'primaryColor': primaryColor,
        'defaultCalculationMethod': defaultCalculationMethod.value,
        'defaultMadhab': defaultMadhab.value,
        'morningOffsetMinutes': morningOffsetMinutes,
        'eveningOffsetMinutes': eveningOffsetMinutes,
        'contentVersion': contentVersion,
        'reviewingScholar': reviewingScholar,
        'reviewingScholarCredential': reviewingScholarCredential,
        'supportEmail': supportEmail,
        'supportWebsite': supportWebsite,
        'privacyPolicyUrl': privacyPolicyUrl,
        'androidPackageName': androidPackageName,
        'iosAppStoreId': iosAppStoreId,
      };
}

class AppLanguage {
  const AppLanguage({
    required this.code,
    required this.nativeName,
    required this.englishName,
    required this.isRtl,
  });

  final String code;
  final String nativeName;
  final String englishName;
  final bool isRtl;

  factory AppLanguage.fromJson(Map<String, dynamic> json) => AppLanguage(
        code: json['code'] as String? ?? 'ar',
        nativeName: json['nativeName'] as String? ?? '',
        englishName: json['englishName'] as String? ?? '',
        isRtl: json['isRtl'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
        'code': code,
        'nativeName': nativeName,
        'englishName': englishName,
        'isRtl': isRtl,
      };
}

/// One remembrance, with the attribution that made it publishable.
class Dhikr {
  const Dhikr({
    required this.id,
    required this.categoryId,
    required this.sortOrder,
    required this.arabicText,
    required this.repeatCount,
    this.translation,
    this.transliteration,
    this.virtue,
    this.sourceBook,
    this.sourceReference,
    this.grade,
    this.gradedBy,
  });

  final int id;
  final int categoryId;
  final int sortOrder;

  /// The vocalised Arabic. Present in every language's payload — it is the text
  /// itself, not a translation of one.
  final String arabicText;

  final int repeatCount;
  final String? translation;
  final String? transliteration;

  /// The narrated virtue, where one is established. Null far more often than not.
  final String? virtue;

  final String? sourceBook;
  final String? sourceReference;
  final HadithGrade? grade;
  final String? gradedBy;

  /// True when this row can show a takhrij line. Every published dhikr can —
  /// the server refuses to publish one that cannot — but a cache written by an
  /// older build might hold one that predates the rule.
  bool get hasSource => (sourceBook ?? '').isNotEmpty && (sourceReference ?? '').isNotEmpty;

  factory Dhikr.fromJson(Map<String, dynamic> json) => Dhikr(
        id: json['id'] as int? ?? 0,
        categoryId: json['categoryId'] as int? ?? 0,
        sortOrder: json['sortOrder'] as int? ?? 0,
        arabicText: json['arabicText'] as String? ?? '',
        repeatCount: json['repeatCount'] as int? ?? 1,
        translation: json['translation'] as String?,
        transliteration: json['transliteration'] as String?,
        virtue: json['virtue'] as String?,
        sourceBook: json['sourceBook'] as String?,
        sourceReference: json['sourceReference'] as String?,
        grade: HadithGrade.fromValue(json['grade'] as int?),
        gradedBy: json['gradedBy'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'categoryId': categoryId,
        'sortOrder': sortOrder,
        'arabicText': arabicText,
        'repeatCount': repeatCount,
        'translation': translation,
        'transliteration': transliteration,
        'virtue': virtue,
        'sourceBook': sourceBook,
        'sourceReference': sourceReference,
        'grade': grade?.value,
        'gradedBy': gradedBy,
      };
}

/// A chapter of adhkar, with its contents.
class AthkarCategory {
  const AthkarCategory({
    required this.id,
    required this.key,
    required this.name,
    required this.sortOrder,
    required this.rhythm,
    required this.anchor,
    required this.adhkar,
    this.description,
    this.icon,
  });

  final int id;
  final String key;
  final String name;
  final String? description;
  final String? icon;
  final int sortOrder;

  /// Whether a session's progress survives the night. See [CategoryRhythm].
  final CategoryRhythm rhythm;

  /// The moment of the day this chapter belongs to, which is how the home
  /// screen decides what to put in front of the reader without being told.
  final PrayerAnchor anchor;

  final List<Dhikr> adhkar;

  /// How many repetitions a full run of this chapter is — the denominator the
  /// session's progress bar counts towards.
  int get totalRepeats => adhkar.fold(0, (sum, dhikr) => sum + dhikr.repeatCount);

  factory AthkarCategory.fromJson(Map<String, dynamic> json) => AthkarCategory(
        id: json['id'] as int? ?? 0,
        key: json['key'] as String? ?? '',
        name: json['name'] as String? ?? '',
        description: json['description'] as String?,
        icon: json['icon'] as String?,
        sortOrder: json['sortOrder'] as int? ?? 0,
        rhythm: CategoryRhythm.fromValue(json['rhythm'] as int?),
        anchor: PrayerAnchor.fromValue(json['anchor'] as int?),
        adhkar: [
          for (final entry in (json['adhkar'] as List<dynamic>? ?? []))
            Dhikr.fromJson(entry as Map<String, dynamic>),
        ],
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'key': key,
        'name': name,
        'description': description,
        'icon': icon,
        'sortOrder': sortOrder,
        'rhythm': rhythm.value,
        'anchor': anchor.value,
        'adhkar': [for (final dhikr in adhkar) dhikr.toJson()],
      };
}

/// `/content/catalog` — the whole published corpus in one language.
class Catalog {
  const Catalog({
    required this.version,
    required this.languageCode,
    required this.isUpToDate,
    required this.categories,
  });

  final int version;
  final String languageCode;

  /// True when the caller's version already matched, in which case
  /// [categories] is empty and there is nothing to apply.
  final bool isUpToDate;

  final List<AthkarCategory> categories;

  factory Catalog.fromJson(Map<String, dynamic> json) => Catalog(
        version: json['version'] as int? ?? 0,
        languageCode: json['languageCode'] as String? ?? 'ar',
        isUpToDate: json['isUpToDate'] as bool? ?? false,
        categories: [
          for (final entry in (json['categories'] as List<dynamic>? ?? []))
            AthkarCategory.fromJson(entry as Map<String, dynamic>),
        ],
      );

  Map<String, dynamic> toJson() => {
        'version': version,
        'languageCode': languageCode,
        'isUpToDate': isUpToDate,
        'categories': [for (final category in categories) category.toJson()],
      };
}

/// A reminder this device schedules itself. See `core/reminder_scheduler.dart`.
class DeviceReminder {
  const DeviceReminder({
    required this.id,
    required this.key,
    required this.kind,
    required this.anchor,
    required this.offsetMinutes,
    required this.days,
    required this.isUserAdjustable,
    required this.androidChannelId,
    required this.version,
    required this.title,
    required this.body,
    this.categoryId,
    this.localTime,
  });

  final int id;
  final String key;
  final int? categoryId;
  final ReminderKind kind;

  /// "HH:mm" for a fixed-time reminder; null for an anchored one.
  final String? localTime;

  final PrayerAnchor anchor;
  final int offsetMinutes;

  /// The server's weekday bit set — read through [WeekDays.includes].
  final int days;

  /// Whether the reader may change or silence this one from settings.
  final bool isUserAdjustable;

  final String androidChannelId;

  /// Compare against what was last scheduled; re-schedule only when it moves.
  final int version;

  final String title;
  final String body;

  factory DeviceReminder.fromJson(Map<String, dynamic> json) => DeviceReminder(
        id: json['id'] as int? ?? 0,
        key: json['key'] as String? ?? '',
        categoryId: json['categoryId'] as int?,
        kind: ReminderKind.fromValue(json['kind'] as int?),
        localTime: json['localTime'] as String?,
        anchor: PrayerAnchor.fromValue(json['anchor'] as int?),
        offsetMinutes: json['offsetMinutes'] as int? ?? 0,
        days: json['days'] as int? ?? WeekDays.all,
        isUserAdjustable: json['isUserAdjustable'] as bool? ?? true,
        androidChannelId: json['androidChannelId'] as String? ?? 'athkar.reminders.v1',
        version: json['version'] as int? ?? 1,
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'key': key,
        'categoryId': categoryId,
        'kind': kind.value,
        'localTime': localTime,
        'anchor': anchor.value,
        'offsetMinutes': offsetMinutes,
        'days': days,
        'isUserAdjustable': isUserAdjustable,
        'androidChannelId': androidChannelId,
        'version': version,
        'title': title,
        'body': body,
      };
}

class NotificationItem {
  const NotificationItem({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    required this.createdAt,
    required this.isRead,
    this.route,
  });

  final int id;
  final NotificationKind kind;
  final String title;
  final String body;
  final String? route;
  final DateTime createdAt;
  final bool isRead;

  factory NotificationItem.fromJson(Map<String, dynamic> json) => NotificationItem(
        id: json['id'] as int? ?? 0,
        kind: NotificationKind.fromValue(json['kind'] as int?),
        title: json['title'] as String? ?? '',
        body: json['body'] as String? ?? '',
        route: json['route'] as String?,
        createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '')?.toLocal() ??
            DateTime.now(),
        isRead: json['isRead'] as bool? ?? false,
      );
}

class FaqItem {
  const FaqItem({
    required this.id,
    required this.category,
    required this.question,
    required this.answer,
  });

  final int id;
  final int category;
  final String question;
  final String answer;

  factory FaqItem.fromJson(Map<String, dynamic> json) => FaqItem(
        id: json['id'] as int? ?? 0,
        category: json['category'] as int? ?? 0,
        question: json['question'] as String? ?? '',
        answer: json['answer'] as String? ?? '',
      );
}

/// `/quran/check-version` — everything needed to decide whether to download, and
/// to verify what arrives.
class QuranVersion {
  const QuranVersion({
    required this.version,
    required this.updateAvailable,
    required this.sizeBytes,
    required this.hasWaqfAnnotations,
    this.edition,
    this.name,
    this.sha256,
    this.releaseNotes,
  });

  /// Which mushaf this answer is about.
  ///
  /// Echoed back even when the device did not name one, which is how an install
  /// upgraded from the single-mushaf build learns what it has been reading all
  /// along — see `QuranStore.adoptLegacy`.
  final String? edition;

  /// The reader-facing name of that edition.
  final String? name;

  /// Null when nothing has been published — a normal state, not an error.
  final int? version;

  final bool updateAvailable;
  final int sizeBytes;
  final bool hasWaqfAnnotations;
  final String? sha256;
  final String? releaseNotes;

  bool get isAvailable => version != null;

  factory QuranVersion.fromJson(Map<String, dynamic> json) => QuranVersion(
        edition: json['edition'] as String?,
        name: json['name'] as String?,
        version: json['version'] as int?,
        updateAvailable: json['updateAvailable'] as bool? ?? false,
        sizeBytes: json['sizeBytes'] as int? ?? 0,
        hasWaqfAnnotations: json['hasWaqfAnnotations'] as bool? ?? false,
        sha256: json['sha256'] as String?,
        releaseNotes: json['releaseNotes'] as String?,
      );
}

/// One mushaf the reader may choose, as the server offers it.
///
/// Distinct from [QuranVersion], which answers "is there anything newer than
/// what I have" about a single mushaf. This is the shelf.
class QuranEdition {
  const QuranEdition({
    required this.edition,
    required this.name,
    required this.version,
    required this.sizeBytes,
    required this.sha256,
    required this.hasWaqfAnnotations,
    required this.isDefault,
    this.releaseNotes,
  });

  /// The slug the device stores its copy under. Stable across versions.
  final String edition;

  final String name;
  final int version;
  final int sizeBytes;
  final String sha256;
  final bool hasWaqfAnnotations;

  /// Which one a device is given when it names none — the one a fresh install
  /// is offered before the reader has chosen.
  final bool isDefault;

  final String? releaseNotes;

  factory QuranEdition.fromJson(Map<String, dynamic> json) => QuranEdition(
        edition: json['edition'] as String? ?? '',
        name: json['name'] as String? ?? '',
        version: json['version'] as int? ?? 0,
        sizeBytes: json['sizeBytes'] as int? ?? 0,
        sha256: json['sha256'] as String? ?? '',
        hasWaqfAnnotations: json['hasWaqfAnnotations'] as bool? ?? false,
        isDefault: json['isDefault'] as bool? ?? false,
        releaseNotes: json['releaseNotes'] as String?,
      );
}

/// What `/devices/register` answers with: the row, plus the two versions that
/// decide whether the app has anything to fetch.
class DeviceState {
  const DeviceState({
    required this.contentVersion,
    required this.unreadNotifications,
    this.quranPackageVersion,
  });

  final int contentVersion;
  final int? quranPackageVersion;
  final int unreadNotifications;

  factory DeviceState.fromJson(Map<String, dynamic> json) => DeviceState(
        contentVersion: json['contentVersion'] as int? ?? 0,
        quranPackageVersion: json['quranPackageVersion'] as int?,
        unreadNotifications: json['unreadNotifications'] as int? ?? 0,
      );
}
