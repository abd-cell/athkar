import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import 'category_screen.dart';
import 'search_screen.dart';

/// Every chapter of adhkar, grouped by kind.
///
/// «Kind» is not a field the API carries, so it is derived here, and the two
/// halves of the derivation are not equally sound:
///
/// - **Time-bound** is read straight off the data: a chapter with a
///   [PrayerAnchor] other than [PrayerAnchor.none] is one the reader has at a
///   moment of the day, which is exactly what an anchor means. Nothing is
///   hardcoded and a new anchored chapter joins the group by itself.
/// - **Occasion** and **unbounded** rest on the key lists below, because
///   nothing in the catalogue distinguishes «أذكار السفر» from «الاستغفار».
///   That is a constant holding a content decision, which this project puts in
///   the CMS rather than in the app. Until `AthkarCategory` carries a type of
///   its own, a chapter whose key is in neither list falls into the closing
///   «عامّة» group, so nothing can be published into invisibility.
///
/// Grouping on the anchor alone was tried first and is wrong: «أذكار المساء»
/// anchors to Asr, which is right for scheduling and puts it under a «النهار»
/// heading, and the ten chapters with no anchor all collapse into one bucket.
class AdhkarScreen extends StatelessWidget {
  const AdhkarScreen({super.key});

  /// Chapters tied to a circumstance rather than to a clock.
  static const _occasionKeys = {
    'after-prayer', 'food', 'clothing', 'khala', 'mosque', 'travel', 'distress',
  };

  /// Chapters with neither a time nor an occasion — said whenever.
  static const _unboundedKeys = {'istighfar', 'salat-nabi'};

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);

    final categories = state.content.categories;

    bool unanchored(AthkarCategory c) => c.anchor == PrayerAnchor.none;

    final timed = [for (final c in categories) if (!unanchored(c)) c];
    final occasion = [
      for (final c in categories)
        if (unanchored(c) && _occasionKeys.contains(c.key)) c,
    ];
    final unbounded = [
      for (final c in categories)
        if (unanchored(c) && _unboundedKeys.contains(c.key)) c,
    ];
    final rest = [
      for (final c in categories)
        if (unanchored(c) &&
            !_occasionKeys.contains(c.key) &&
            !_unboundedKeys.contains(c.key))
          c,
    ];

    // An empty heading says the app is broken when it only means the CMS has
    // published nothing there yet, so a group with no chapters is not drawn.
    final grouped = <(String, List<AthkarCategory>)>[
      for (final (key, members) in [
        ('adhkar.group.timed', timed),
        ('adhkar.group.occasion', occasion),
        ('adhkar.group.unbounded', unbounded),
        ('adhkar.group.general', rest),
      ])
        if (members.isNotEmpty) (key, members),
    ];

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        bottom: false,
        child: categories.isEmpty
            ? AthkarEmptyState(
                title: context.tr('categories.empty'),
                body: context.tr('categories.emptyHint'),
                icon: Icons.menu_book_outlined,
              )
            : ListView(
                padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 14, AthkarSpacing.page, 30),
                children: [
                  Row(
                    children: [
                      Text(
                        context.tr('nav.athkar'),
                        style: AthkarType.amiri(
                          size: 22, color: tokens.ink, weight: FontWeight.w700),
                      ),
                      const Spacer(),
                      IconButton(
                        onPressed: () => Navigator.of(context).push(
                          MaterialPageRoute(builder: (_) => const SearchScreen()),
                        ),
                        icon: const Icon(Icons.search, size: 20),
                      ),
                    ],
                  ),
                  for (final (key, members) in grouped) ...[
                    const SizedBox(height: 6),
                    AthkarSectionHeader(title: context.tr(key)),
                    const SizedBox(height: 10),
                    for (final category in members) ...[
                      _CategoryRow(category: category),
                      const SizedBox(height: 10),
                    ],
                  ],
                ],
              ),
      ),
    );
  }
}

/// One chapter, as the index draws it: name, count, and a quiet tick when the
/// reader has finished it today.
class _CategoryRow extends StatelessWidget {
  const _CategoryRow({required this.category});

  final AthkarCategory category;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final isDone = state.progress.completedToday.contains(category.id);

    return AthkarCard(
      radius: AthkarSpacing.smallCardRadius,
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => CategoryScreen(categoryId: category.id)),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  category.name,
                  style: AthkarType.amiri(
                    size: 18, color: tokens.ink, weight: FontWeight.w700),
                ),
                const SizedBox(height: 3),
                Text(
                  context.tr('categories.count', {
                    'count': Numerals.format(
                      category.adhkar.length,
                      arabicIndic: settings.arabicNumerals,
                    ),
                  }),
                  style: AthkarType.sans(size: 11.5, color: tokens.muted),
                ),
              ],
            ),
          ),
          if (isDone) Icon(Icons.check_rounded, size: 18, color: tokens.brand),
        ],
      ),
    );
  }
}
