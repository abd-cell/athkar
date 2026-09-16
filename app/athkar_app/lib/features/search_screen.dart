import 'package:flutter/material.dart';

import '../core/arabic_text.dart';
import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import 'dhikr_sheet.dart';

/// Search, run entirely on the device.
///
/// There is a server-side search endpoint and this screen does not use it. The
/// whole published corpus is already cached here, so a local search is instant,
/// works on a train, and cannot disagree with what the reader can see. The
/// endpoint exists for a future in which the corpus outgrows the cache.
///
/// The matching is the interesting part: both the query and the text are folded
/// through [ArabicText], so «اذكار الصباح» typed without a single diacritic
/// finds «أَذْكَار الصَّبَاح».
class SearchScreen extends StatefulWidget {
  const SearchScreen({super.key});

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  final _controller = TextEditingController();
  var _query = '';

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final results = _search(state.content.categories, _query);

    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _controller,
          autofocus: true,
          onChanged: (value) => setState(() => _query = value),
          decoration: InputDecoration(
            filled: false,
            border: InputBorder.none,
            enabledBorder: InputBorder.none,
            focusedBorder: InputBorder.none,
            hintText: context.tr('categories.search'),
          ),
          style: AthkarType.sans(size: 15, color: tokens.ink),
        ),
      ),
      body: _query.trim().isEmpty
          ? const SizedBox.shrink()
          : results.isEmpty
              ? AthkarEmptyState(
                  title: context.tr('common.noResults'),
                  icon: Icons.search_off_outlined,
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(
                    AthkarSpacing.page, 12, AthkarSpacing.page, 30),
                  itemCount: results.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (context, index) {
                    final (category, dhikr) = results[index];

                    return AthkarCard(
                      onTap: () => DhikrSheet.show(context, dhikr),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            category.name,
                            style: AthkarType.sans(
                              size: 11.5, color: tokens.brand, weight: FontWeight.w500),
                          ),
                          const SizedBox(height: 8),
                          Text(
                            dhikr.arabicText,
                            maxLines: 3,
                            overflow: TextOverflow.ellipsis,
                            style: AthkarType.amiri(
                              size: 19 * settings.fontScale,
                              color: tokens.ink,
                              height: 1.95,
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                ),
    );
  }

  /// Folded matching across the Arabic and the translation.
  ///
  /// The translation is searched too, because a reader on the English build
  /// looking for "forgiveness" should find the istighfar without being able to
  /// type Arabic.
  List<(AthkarCategory, Dhikr)> _search(List<AthkarCategory> categories, String query) {
    final term = ArabicText.normalize(query);
    if (term.isEmpty) return const [];

    return [
      for (final category in categories)
        for (final dhikr in category.adhkar)
          if (ArabicText.normalize(dhikr.arabicText).contains(term) ||
              ArabicText.normalize(dhikr.translation).contains(term) ||
              ArabicText.normalize(category.name).contains(term))
            (category, dhikr),
    ];
  }
}
