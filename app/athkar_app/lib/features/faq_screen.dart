import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/athkar_ui.dart';

/// The help topics, managed from the CMS and fetched per language.
///
/// Not cached: it is a short list, it is opened rarely, and an answer that is
/// out of date is worse than a moment's wait. The empty state is honest about
/// needing a connection rather than pretending there are no questions.
class FaqScreen extends StatefulWidget {
  const FaqScreen({super.key});

  @override
  State<FaqScreen> createState() => _FaqScreenState();
}

class _FaqScreenState extends State<FaqScreen> {
  List<FaqItem>? _items;
  final _expanded = <int>{};

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final language = SettingsScope.read(context).languageCode;
    final response = await Api.support.faq(language);

    if (mounted) setState(() => _items = response.data ?? const []);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final items = _items;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('faq.title'))),
      body: items == null
          ? const Center(child: AthkarSpinner())
          : items.isEmpty
              ? AthkarEmptyState(
                  title: context.tr('faq.empty'),
                  icon: Icons.help_outline,
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(
                    AthkarSpacing.page, 14, AthkarSpacing.page, 30),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (context, index) {
                    final item = items[index];
                    final isOpen = _expanded.contains(item.id);

                    return AthkarCard(
                      radius: AthkarSpacing.smallCardRadius,
                      onTap: () => setState(() {
                        isOpen ? _expanded.remove(item.id) : _expanded.add(item.id);
                      }),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  item.question,
                                  style: AthkarType.sans(
                                    size: 13.5,
                                    color: tokens.ink,
                                    weight: FontWeight.w500,
                                    height: 1.6,
                                  ),
                                ),
                              ),
                              Icon(
                                isOpen ? Icons.expand_less : Icons.expand_more,
                                size: 18,
                                color: tokens.faint,
                              ),
                            ],
                          ),
                          // Animated so the list does not jump under the finger
                          // that tapped it.
                          AnimatedCrossFade(
                            duration: const Duration(milliseconds: 180),
                            crossFadeState: isOpen
                                ? CrossFadeState.showSecond
                                : CrossFadeState.showFirst,
                            firstChild: const SizedBox(width: double.infinity),
                            secondChild: Padding(
                              padding: const EdgeInsets.only(top: 12),
                              child: Text(
                                item.answer,
                                style: AthkarType.sans(
                                  size: 13, color: tokens.muted, height: 1.9),
                              ),
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                ),
    );
  }
}
