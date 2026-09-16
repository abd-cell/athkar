import 'dart:async';

import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../core/widget_bridge.dart';
import '../main.dart';
import '../widgets/athkar_ui.dart';
import 'widgets/widget_designs.dart';
import 'widgets/widget_gallery_screen.dart';
import 'widgets/widget_preview_data.dart';
import 'widgets/widget_skin.dart';

/// «الويدجت الموضوعة» — what is on the reader's home screen right now.
///
/// One question, answered honestly, and it is not the question this screen used
/// to answer. It used to be a picker over two `WidgetKind` values; the gallery
/// took that job, and what was left was a screen that could disagree with it.
/// Now it shows the *resolved* selection — the reader's choice corrected to
/// something the admin still offers and this build can still draw — so the two
/// screens cannot drift apart.
///
/// Three states, because the reader cannot tell them apart from inside the app
/// and each needs different words: widgets switched off, available but not yet
/// placed, and placed.
class WidgetScreen extends StatefulWidget {
  const WidgetScreen({super.key});

  @override
  State<WidgetScreen> createState() => _WidgetScreenState();
}

class _WidgetScreenState extends State<WidgetScreen> {
  var _placed = false;
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _check();

    // The preview carries a live countdown, like the gallery's.
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  Future<void> _check() async {
    final placed = await WidgetBridge.instance.isPlaced();
    if (mounted) setState(() => _placed = placed);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final rules = state.widgetSettings;

    if (!rules.isEnabled) {
      return Scaffold(
        appBar: AppBar(title: Text(context.tr('widgets.hub.placed'))),
        body: AthkarEmptyState(
          title: context.tr('widget.disabled'),
          body: context.tr('widget.disabledHint'),
          icon: Icons.widgets_outlined,
        ),
      );
    }

    final selection = state.selectedWidget;

    // Null means the gallery holds nothing this build understands — a real
    // state on an install several releases old, and one the reader deserves a
    // sentence about rather than an empty rectangle.
    if (selection == null) {
      return Scaffold(
        appBar: AppBar(title: Text(context.tr('widgets.hub.placed'))),
        body: AthkarEmptyState(
          title: context.tr('widgets.gallery.empty'),
          body: context.tr('widgets.gallery.emptyHint'),
          icon: Icons.widgets_outlined,
        ),
      );
    }

    final settings = SettingsScope.of(context);
    final available = selection.designsAvailable(WidgetDesigns.designsFor(selection.key));
    final design =
        settings.widgetDesign(selection.key, fallback: selection.defaultDesign) % available;

    final skin = WidgetSkin.of(context, rules: rules);
    final data = WidgetPreviewData.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.hub.placed'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
            AthkarSpacing.page, 16, AthkarSpacing.page, 30),
        children: [
          WidgetPanel(
            skin: skin,
            height: WidgetDesigns.heightFor(selection.key),
            child: WidgetDesigns.build(context, selection.key, design, data, skin) ??
                const SizedBox.shrink(),
          ),
          const SizedBox(height: 12),

          Row(
            children: [
              Expanded(
                child: Text(
                  selection.title,
                  style:
                      AthkarType.sans(size: 13.5, color: tokens.ink, weight: FontWeight.w600),
                ),
              ),
              if (available > 1)
                Text(
                  context.tr('widgets.designCounter', {
                    'index': '${design + 1}',
                    'total': '$available',
                  }),
                  style: AthkarType.sans(size: 11, color: tokens.muted),
                ),
            ],
          ),

          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              color: tokens.brandTint,
              borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
            ),
            child: Text(
              // Two different sentences, because "add it from your launcher" is
              // useless to somebody who already has, and "it is on your home
              // screen" is a lie to somebody who has not.
              _placed ? context.tr('widget.placed') : context.tr('widget.howToAdd'),
              style: AthkarType.sans(size: 12.5, color: tokens.brandInk, height: 1.8),
            ),
          ),

          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const WidgetGalleryScreen()),
            ),
            icon: const Icon(Icons.grid_view_outlined, size: 16),
            label: Text(context.tr('widgets.changeWidget')),
          ),
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: () async {
              await state.pushWidget();
              await _check();
            },
            icon: const Icon(Icons.refresh, size: 16),
            label: Text(context.tr('widget.refresh')),
          ),
        ],
      ),
    );
  }
}
