import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import '../widgets/dhikr_card.dart';
import 'dhikr_sheet.dart';

/// What the reader has bookmarked, and nothing else.
///
/// The favourites are a set of ids in preferences, so the list is resolved
/// against the catalogue on every build: a dhikr the CMS has since withdrawn
/// simply stops appearing rather than rendering as a blank card.
class FavouritesScreen extends StatelessWidget {
  const FavouritesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final adhkar = <Dhikr>[
      for (final id in settings.favourites)
        if (state.content.dhikrById(id) case final dhikr?) dhikr,
    ];

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('profile.favourites'))),
      body: adhkar.isEmpty
          ? AthkarEmptyState(
              title: context.tr('favourites.empty'),
              body: context.tr('favourites.emptyHint'),
              icon: Icons.bookmark_outline,
            )
          : ListView.separated(
              padding: const EdgeInsets.fromLTRB(
                AthkarSpacing.page, 12, AthkarSpacing.page, 32),
              itemCount: adhkar.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) => DhikrCard(
                dhikr: adhkar[index],
                onTap: () => DhikrSheet.show(context, adhkar[index]),
              ),
            ),
    );
  }
}
