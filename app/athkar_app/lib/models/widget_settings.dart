/// The admin's rules for the home-screen widget.
///
/// Mirrors `Areas/Domain/Widgets/WidgetSettings.cs`. Cached on the device like
/// everything else the app needs at launch — the widget has to keep drawing
/// with the radio off.
library;

/// Mirrors `Shareds/Enums/WidgetKind.cs`.
enum WidgetKind {
  nextPrayer(1),
  dhikr(2),
  combined(3);

  const WidgetKind(this.value);
  final int value;

  static WidgetKind fromValue(int? value) => switch (value) {
        2 => WidgetKind.dhikr,
        3 => WidgetKind.combined,
        _ => WidgetKind.nextPrayer,
      };

  static WidgetKind fromName(String? name) {
    for (final kind in WidgetKind.values) {
      if (kind.name == name) return kind;
    }
    return WidgetKind.nextPrayer;
  }
}

/// Mirrors `Shareds/Enums/WidgetTheme.cs`.
enum WidgetTheme {
  system(0),
  light(1),
  dark(2),
  transparent(3);

  const WidgetTheme(this.value);
  final int value;

  static WidgetTheme fromValue(int? value) => switch (value) {
        1 => WidgetTheme.light,
        2 => WidgetTheme.dark,
        3 => WidgetTheme.transparent,
        _ => WidgetTheme.system,
      };
}

class WidgetSettings {
  const WidgetSettings({
    required this.isEnabled,
    required this.defaultKind,
    required this.allowPrayerWidget,
    required this.allowDhikrWidget,
    required this.theme,
    required this.refreshMinutes,
    required this.showHijriDate,
    required this.showCountdown,
    required this.allowBackgroundColor,
    required this.allowTransparency,
    required this.allowBackgroundImage,
    required this.allowCustomWidget,
    required this.customWidgetMaxLength,
    required this.version,
    this.defaultWidgetKey,
    this.dhikrCategoryId,
  });

  final bool isEnabled;
  final WidgetKind defaultKind;
  final bool allowPrayerWidget;
  final bool allowDhikrWidget;
  final WidgetTheme theme;
  final int refreshMinutes;
  final bool showHijriDate;
  final bool showCountdown;

  // What the reader is allowed to change for themselves.
  //
  // Permissions, not preferences: the reader's own choice lives here on the
  // phone and never reaches the server. They exist because a customisation can
  // break the one promise a widget makes — a transparent panel over a
  // photographic wallpaper can render a dhikr unreadable, and an unreadable
  // dhikr on a home screen is worse than no widget — so the project keeps a way
  // to withdraw one without shipping an app update.
  final bool allowBackgroundColor;
  final bool allowTransparency;
  final bool allowBackgroundImage;

  /// Whether the reader may pin their own text — an ayah, a du'a, a line.
  ///
  /// The one place in this app where a widget shows words that carry no
  /// takhrij. It stays honest because the text is the reader's own, typed on
  /// their own phone, never uploaded and never shown to anyone else, and the
  /// app labels it as theirs rather than dressing it as catalogue content.
  final bool allowCustomWidget;

  final int customWidgetMaxLength;

  /// The gallery entry a reader gets before they have chosen one.
  ///
  /// A key, matched against this build's renderer registry. Null — and an
  /// unknown key — both mean the same thing here: fall back to the first entry
  /// this build can actually draw. The server may be a release ahead of the app,
  /// so an unrecognised default has to degrade rather than leave a home screen
  /// blank.
  final String? defaultWidgetKey;

  /// Null means the widget follows the time of day, as the home screen does.
  final int? dhikrCategoryId;

  final int version;

  /// What a first launch uses before the server has answered. Matches the
  /// server's own defaults, so nothing changes shape when it does.
  static const fallback = WidgetSettings(
    isEnabled: true,
    defaultKind: WidgetKind.nextPrayer,
    allowPrayerWidget: true,
    allowDhikrWidget: true,
    theme: WidgetTheme.system,
    refreshMinutes: 30,
    showHijriDate: true,
    showCountdown: true,
    allowBackgroundColor: true,
    allowTransparency: true,
    allowBackgroundImage: true,
    allowCustomWidget: true,
    customWidgetMaxLength: 280,
    version: 0,
  );

  /// The kinds the reader may actually pick, in a stable order.
  List<WidgetKind> get allowedKinds => [
        if (allowPrayerWidget) WidgetKind.nextPrayer,
        if (allowDhikrWidget) WidgetKind.dhikr,
      ];

  /// The reader's choice, corrected to one the admin still allows.
  ///
  /// This is what makes withdrawing a kind actually reach a home screen: a
  /// reader who picked the dhikr widget last month falls back to the prayer one
  /// rather than keeping something the admin has taken away.
  WidgetKind resolveKind(WidgetKind preferred) {
    final allowed = allowedKinds;
    if (allowed.isEmpty) return defaultKind;
    if (allowed.contains(preferred)) return preferred;

    return allowed.contains(defaultKind) ? defaultKind : allowed.first;
  }

  factory WidgetSettings.fromJson(Map<String, dynamic> json) => WidgetSettings(
        isEnabled: json['isEnabled'] as bool? ?? true,
        defaultKind: WidgetKind.fromValue(json['defaultKind'] as int?),
        allowPrayerWidget: json['allowPrayerWidget'] as bool? ?? true,
        allowDhikrWidget: json['allowDhikrWidget'] as bool? ?? true,
        theme: WidgetTheme.fromValue(json['theme'] as int?),
        refreshMinutes: json['refreshMinutes'] as int? ?? 30,
        showHijriDate: json['showHijriDate'] as bool? ?? true,
        showCountdown: json['showCountdown'] as bool? ?? true,
        allowBackgroundColor: json['allowBackgroundColor'] as bool? ?? true,
        allowTransparency: json['allowTransparency'] as bool? ?? true,
        allowBackgroundImage: json['allowBackgroundImage'] as bool? ?? true,
        allowCustomWidget: json['allowCustomWidget'] as bool? ?? true,
        customWidgetMaxLength: json['customWidgetMaxLength'] as int? ?? 280,
        defaultWidgetKey: json['defaultWidgetKey'] as String?,
        dhikrCategoryId: json['dhikrCategoryId'] as int?,
        version: json['version'] as int? ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'isEnabled': isEnabled,
        'defaultKind': defaultKind.value,
        'allowPrayerWidget': allowPrayerWidget,
        'allowDhikrWidget': allowDhikrWidget,
        'theme': theme.value,
        'refreshMinutes': refreshMinutes,
        'showHijriDate': showHijriDate,
        'showCountdown': showCountdown,
        'allowBackgroundColor': allowBackgroundColor,
        'allowTransparency': allowTransparency,
        'allowBackgroundImage': allowBackgroundImage,
        'allowCustomWidget': allowCustomWidget,
        'customWidgetMaxLength': customWidgetMaxLength,
        'defaultWidgetKey': defaultWidgetKey,
        'dhikrCategoryId': dhikrCategoryId,
        'version': version,
      };
}
