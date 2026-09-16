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

/// The three sections of the index: الأذكار, الأدعية, الفضائل.
///
/// Which section a chapter belongs to is [AthkarCategory.section] — a field an
/// editor sets. This screen used to derive the grouping from a list of category
/// keys held in this file, and said so itself: a content decision compiled into
/// the app, where a chapter published on a Tuesday could not be grouped until
/// the next release. Nothing here decides any more; it draws what it is told.
///
/// A chapter nobody has filed is drawn too, in a closing section of its own.
/// Dropping it would make a published chapter invisible, and an editor who
/// forgot the field would have no way to notice.
class AdhkarScreen extends StatelessWidget {
  const AdhkarScreen({super.key});

  /// The sections in the order the reader meets them. [CategorySection.none]
  /// is deliberately last: it is the tray of what has not been filed, not a
  /// section of the book.
  static const _order = [
    (CategorySection.adhkar, 'adhkar.section.adhkar', 'adhkar.section.adhkar.hint'),
    (CategorySection.duas, 'adhkar.section.duas', 'adhkar.section.duas.hint'),
    (CategorySection.virtues, 'adhkar.section.virtues', 'adhkar.section.virtues.hint'),
    (CategorySection.none, 'adhkar.section.none', 'adhkar.section.none.hint'),
  ];

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);

    final categories = state.content.categories;

    // An empty section says the app is broken when it only means nothing has
    // been published there yet, so a section with no chapters is not drawn.
    final sections = [
      for (final (section, title, hint) in _order)
        if ([for (final c in categories) if (c.section == section) c] case final members
            when members.isNotEmpty)
          (section, title, hint, members),
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
                  const SizedBox(height: 6),
                  for (final (section, title, hint, members) in sections) ...[
                    _SectionCard(
                      title: context.tr(title),
                      subtitle: context.tr(hint),
                      count: members.length,
                      onTap: () => Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => SectionScreen(
                            section: section,
                            title: context.tr(title),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 10),
                  ],
                ],
              ),
      ),
    );
  }
}

/// The chapters of one section.
///
/// It reads the section out of the state rather than being handed a list, so a
/// sync that arrives while the reader is looking at it redraws with the new
/// chapters instead of showing the ones that existed when it opened.
class SectionScreen extends StatelessWidget {
  const SectionScreen({super.key, required this.section, required this.title});

  final CategorySection section;
  final String title;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);

    final members = [
      for (final category in state.content.categories)
        if (category.section == section) category,
    ];

    return Scaffold(
      backgroundColor: tokens.paper,
      appBar: AppBar(backgroundColor: tokens.paper, title: Text(title)),
      body: SafeArea(
        top: false,
        child: members.isEmpty
            ? AthkarEmptyState(
                title: context.tr('categories.empty'),
                body: context.tr('categories.emptyHint'),
                icon: Icons.menu_book_outlined,
              )
            : ListView.separated(
                padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 14, AthkarSpacing.page, 30),
                itemCount: members.length,
                separatorBuilder: (_, __) => const SizedBox(height: 10),
                itemBuilder: (context, index) => _CategoryRow(category: members[index]),
              ),
      ),
    );
  }
}

/// One section, as the index opens on it.
class _SectionCard extends StatelessWidget {
  const _SectionCard({
    required this.title,
    required this.subtitle,
    required this.count,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final int count;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return AthkarCard(
      onTap: onTap,
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 18),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: AthkarType.amiri(
                    size: 19, color: tokens.ink, weight: FontWeight.w700),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: AthkarType.sans(size: 12, color: tokens.muted, height: 1.6),
                ),
                const SizedBox(height: 6),
                Text(
                  context.tr('adhkar.section.count', {
                    'count': Numerals.format(count, arabicIndic: settings.arabicNumerals),
                  }),
                  style: AthkarType.sans(size: 11.5, color: tokens.brandInk),
                ),
              ],
            ),
          ),
          Icon(Icons.chevron_left, size: 20, color: tokens.faint),
        ],
      ),
    );
  }
}

/// One chapter, as a section draws it: name, count, and a quiet tick when the
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
