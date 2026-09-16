import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import '../widgets/dhikr_card.dart';
import 'dhikr_sheet.dart';
import 'reading_screen.dart';
import 'search_screen.dart';
import 'session_screen.dart';

/// The index of chapters, or one chapter's contents.
///
/// Two jobs in one screen because they are the same list at two depths, and a
/// separate file for each would duplicate the row, the progress badge and the
/// empty state.
class CategoryScreen extends StatelessWidget {
  const CategoryScreen({super.key, this.categoryId, this.highlightDhikrId});

  /// Opens one chapter. Null shows the index.
  final int? categoryId;

  /// Opens the chapter containing this dhikr and scrolls to it — how a tapped
  /// notification for a single dhikr resolves.
  final int? highlightDhikrId;

  @override
  Widget build(BuildContext context) {
    final state = AppStateScope.of(context);

    var id = categoryId;
    if (id == null && highlightDhikrId != null) {
      id = state.content.dhikrById(highlightDhikrId!)?.categoryId;
    }

    final category = id == null ? null : state.content.byId(id);

    return category == null
        ? const _CategoryIndex()
        : _CategoryDetail(category: category, highlightDhikrId: highlightDhikrId);
  }
}

class _CategoryIndex extends StatelessWidget {
  const _CategoryIndex();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final categories = state.content.categories;
    final completed = state.progress.completedToday;

    return Scaffold(
      appBar: AppBar(
        title: Text(context.tr('categories.title')),
        actions: [
          IconButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const SearchScreen()),
            ),
            icon: const Icon(Icons.search, size: 20),
          ),
        ],
      ),
      body: categories.isEmpty
          ? AthkarEmptyState(
              title: context.tr('categories.empty'),
              body: context.tr('categories.emptyHint'),
              icon: Icons.menu_book_outlined,
            )
          : ListView.separated(
              padding: const EdgeInsets.fromLTRB(
                AthkarSpacing.page, 12, AthkarSpacing.page, 32),
              itemCount: categories.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final category = categories[index];
                final isDone = completed.contains(category.id);

                return AthkarCard(
                  radius: AthkarSpacing.smallCardRadius,
                  padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => CategoryScreen(categoryId: category.id),
                    ),
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
                                size: 18,
                                color: tokens.ink,
                                weight: FontWeight.w700,
                              ),
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
                      // A quiet tick, not a badge: the design encourages, it
                      // does not score.
                      if (isDone)
                        Icon(Icons.check_rounded, size: 18, color: tokens.brand),
                    ],
                  ),
                );
              },
            ),
    );
  }
}

class _CategoryDetail extends StatelessWidget {
  const _CategoryDetail({required this.category, this.highlightDhikrId});

  final AthkarCategory category;
  final int? highlightDhikrId;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text(category.name),
        actions: [
          IconButton(
            tooltip: context.tr('reading.title'),
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => ReadingScreen(category: category)),
            ),
            icon: const Icon(Icons.notes_outlined, size: 20),
          ),
        ],
      ),
      body: ListView.separated(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 12, AthkarSpacing.page, 110),
        itemCount: category.adhkar.length,
        separatorBuilder: (_, __) => const SizedBox(height: 10),
        itemBuilder: (context, index) {
          final dhikr = category.adhkar[index];

          return DhikrCard(
            dhikr: dhikr,
            onTap: () => DhikrSheet.show(context, dhikr),
          );
        },
      ),
      // The one action this screen exists to offer, kept where a thumb is.
      bottomSheet: Container(
        color: tokens.scrimTop,
        padding: EdgeInsets.fromLTRB(
          AthkarSpacing.page, 12, AthkarSpacing.page,
          20 + MediaQuery.of(context).padding.bottom,
        ),
        child: FilledButton(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => SessionScreen(category: category)),
          ),
          child: Text(context.tr('home.readAndCount')),
        ),
      ),
    );
  }
}
