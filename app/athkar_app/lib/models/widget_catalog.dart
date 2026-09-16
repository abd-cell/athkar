/// The widget gallery, as the server hands it over.
///
/// Mirrors `Areas/Services/Widgets/Models/WidgetCatalogModels.cs`.
///
/// The division of labour this file encodes is the whole design of the feature:
/// **the server names the widgets, the app draws them.** A widget gets a
/// fraction of a second of CPU and no network, so what it can draw is decided
/// at build time; nothing an admin types can become a new layout on a phone
/// that already shipped.
///
/// So [WidgetCatalogItem.key] is a contract, not a label. The app holds one
/// renderer per key in `features/widgets/widget_designs.dart` and **silently
/// skips a key it has never heard of** — which is what lets the console add a
/// widget for next year's release without breaking last year's install.
library;

import 'widget_settings.dart';

/// Mirrors `Shareds/Enums/WidgetSurface.cs`.
enum WidgetSurface {
  home(1),
  lock(2);

  const WidgetSurface(this.value);
  final int value;

  static WidgetSurface fromValue(int? value) =>
      value == 2 ? WidgetSurface.lock : WidgetSurface.home;
}

/// Mirrors `Shareds/Enums/WidgetFamily.cs` — a grouping label, not a layout.
enum WidgetFamily {
  prayer(1),
  date(2),
  prayerAndDate(3),
  dhikr(4),
  quran(5),
  countdown(6),
  moon(7),
  tracker(8);

  const WidgetFamily(this.value);
  final int value;

  static WidgetFamily fromValue(int? value) {
    for (final family in WidgetFamily.values) {
      if (family.value == value) return family;
    }
    return WidgetFamily.prayer;
  }
}

class WidgetCatalogItem {
  const WidgetCatalogItem({
    required this.id,
    required this.key,
    required this.surface,
    required this.family,
    required this.title,
    required this.designCount,
    required this.defaultDesign,
    required this.isExclusive,
    required this.isNew,
    required this.sortOrder,
    this.subtitle,
  });

  final int id;

  /// Names a renderer the app already holds. An unknown key is skipped.
  final String key;

  final WidgetSurface surface;
  final WidgetFamily family;

  /// Already resolved to the reader's language by the server.
  final String title;
  final String? subtitle;

  /// The admin's **cap**, not a promise. See [designsAvailable].
  final int designCount;

  final int defaultDesign;

  final bool isExclusive;
  final bool isNew;
  final int sortOrder;

  /// How many designs this install can actually offer.
  ///
  /// The smaller of what the admin allows and what this build can draw, and
  /// never less than one. Both directions matter: a server ahead of the app
  /// must not advertise a design that would render blank, and a server behind
  /// it must be able to withdraw one without an app update.
  int designsAvailable(int implemented) {
    final allowed = designCount < implemented ? designCount : implemented;
    return allowed < 1 ? 1 : allowed;
  }

  factory WidgetCatalogItem.fromJson(Map<String, dynamic> json) => WidgetCatalogItem(
        id: json['id'] as int? ?? 0,
        key: (json['key'] as String? ?? '').trim(),
        surface: WidgetSurface.fromValue(json['surface'] as int?),
        family: WidgetFamily.fromValue(json['family'] as int?),
        title: json['title'] as String? ?? '',
        subtitle: json['subtitle'] as String?,
        designCount: json['designCount'] as int? ?? 1,
        defaultDesign: json['defaultDesign'] as int? ?? 0,
        isExclusive: json['isExclusive'] as bool? ?? false,
        isNew: json['isNew'] as bool? ?? false,
        sortOrder: json['sortOrder'] as int? ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'key': key,
        'surface': surface.value,
        'family': family.value,
        'title': title,
        'subtitle': subtitle,
        'designCount': designCount,
        'defaultDesign': defaultDesign,
        'isExclusive': isExclusive,
        'isNew': isNew,
        'sortOrder': sortOrder,
      };
}

/// The gallery and the rules it is browsed under, as one cached object.
///
/// Fetched together because neither half is any use alone — a gallery the
/// reader may not customise, or a set of permissions with nothing to apply them
/// to — and cached together so the screen opens offline with both.
class WidgetCatalog {
  const WidgetCatalog({required this.settings, required this.items});

  final WidgetSettings settings;
  final List<WidgetCatalogItem> items;

  /// What a first launch browses before the server has answered.
  ///
  /// Deliberately empty rather than a hard-coded copy of the seed: an empty
  /// gallery says "nothing has arrived yet" and the screen has a sentence for
  /// that, whereas a built-in list would show widgets this deployment may have
  /// withdrawn and be indistinguishable from the real thing.
  static const empty = WidgetCatalog(settings: WidgetSettings.fallback, items: []);

  bool get isEmpty => items.isEmpty;

  List<WidgetCatalogItem> forSurface(WidgetSurface surface) =>
      items.where((item) => item.surface == surface).toList();

  /// Which home-screen entry this reader actually gets, in one place.
  ///
  /// Three fallbacks, in order, and every one of them is load-bearing:
  ///
  /// 1. **The reader's own choice**, if it is still offered and this build can
  ///    draw it. An entry the admin has since withdrawn must stop being theirs,
  ///    the same way a withdrawn dhikr leaves the home screen.
  /// 2. **The admin's default**, under the same two tests. The server may be a
  ///    release ahead of this app, so a default naming a renderer this build
  ///    lacks has to degrade rather than leave the launcher blank.
  /// 3. **The first entry this build can draw.** Not the first entry outright —
  ///    the gallery is the admin's order, and the first of it may be something
  ///    only a later release knows.
  ///
  /// Null only when the gallery holds nothing this build understands at all,
  /// which is a real state on a very old install and the caller has to say so
  /// rather than draw an empty box.
  WidgetCatalogItem? resolveSelection({
    required String? chosen,
    required bool Function(String key) canDraw,
  }) {
    final home = [
      for (final item in items)
        if (item.surface == WidgetSurface.home && canDraw(item.key)) item,
    ];

    if (home.isEmpty) return null;

    for (final key in [chosen, settings.defaultWidgetKey]) {
      if (key == null || key.isEmpty) continue;

      for (final item in home) {
        if (item.key == key) return item;
      }
    }

    return home.first;
  }

  /// Tolerant by necessity: launch reads this out of a cache that an older
  /// build may have written in another shape, and a cast failure there is an
  /// exception in the splash screen rather than a gallery with a sentence in it.
  factory WidgetCatalog.fromJson(Map<String, dynamic> json) {
    final settings = json['settings'];
    final items = json['items'];

    return WidgetCatalog(
      settings: settings is Map<String, dynamic>
          ? WidgetSettings.fromJson(settings)
          : WidgetSettings.fallback,
      items: items is! List
          ? const []
          : [
              for (final raw in items)
                if (raw is Map<String, dynamic>) WidgetCatalogItem.fromJson(raw),
            ],
    );
  }

  Map<String, dynamic> toJson() => {
        'settings': settings.toJson(),
        'items': [for (final item in items) item.toJson()],
      };
}

/// The limits on what the reader may do to a widget's appearance.
///
/// Here rather than in the screen that offers them, because [Settings] clamps
/// on *read*: a value stored by an older build — or by one where the admin
/// allowed a range this one does not — must not be able to produce an
/// unreadable widget on a home screen nobody is looking at.
class WidgetAppearance {
  const WidgetAppearance._();

  /// The floor on the panel's opacity.
  ///
  /// Not zero. A fully transparent panel over a photographic wallpaper is how a
  /// dhikr becomes unreadable, and an unreadable dhikr on a home screen is
  /// worse than no widget — the reader cannot see that anything is wrong, they
  /// simply stop reading it. The design's own «شفاف» sits at this floor.
  static const minOpacity = 0.25;

  /// The tints offered, as ARGB values.
  ///
  /// A short list rather than a colour wheel: every one of these was checked
  /// against both the light and the dark ink, and a free picker guarantees that
  /// somebody lands on a green that swallows the text.
  static const tints = <int>[
    0xFF2F5838, // the brand
    0xFF244D2E,
    0xFF1B1814, // the dark paper
    0xFF23201C,
    0xFF3B3229,
    0xFF6F6153,
    0xFF8C5A3C,
    0xFF7A2E2E,
    0xFF2B4A6F,
    0xFF3E3A66,
    0xFFF4ECE0, // the light paper
    0xFFFBF6EE,
  ];
}
