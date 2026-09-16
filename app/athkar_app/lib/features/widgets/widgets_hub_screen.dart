import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../widgets/athkar_ui.dart';
import '../widget_screen.dart';
import 'custom_widget_screen.dart';
import 'widget_appearance_screen.dart';
import 'widget_gallery_screen.dart';
import 'widget_help_screen.dart';

/// «الويدجت (الأدوات)» — the way in to everything a widget can be.
///
/// A hub rather than one long screen because the four things behind it answer
/// different questions and are visited at different times: *which widget is on
/// my home screen* is settled once, *what does it look like* is fiddled with,
/// *what does the app even offer* is browsed, and *how do I add one* is asked
/// exactly once and never again.
///
/// Rows disappear when the admin has withdrawn what they lead to, rather than
/// greying out. A disabled row invites a reader to press it and find out why,
/// and there is no answer they would accept — the reason is a decision taken
/// somewhere they cannot see.
class WidgetsHubScreen extends StatelessWidget {
  const WidgetsHubScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final rules = state.widgetSettings;

    if (!rules.isEnabled) {
      return Scaffold(
        appBar: AppBar(title: Text(context.tr('widgets.hub.title'))),
        body: AthkarEmptyState(
          title: context.tr('widget.disabled'),
          body: context.tr('widget.disabledHint'),
          icon: Icons.widgets_outlined,
        ),
      );
    }

    final canCustomise = rules.allowBackgroundColor ||
        rules.allowTransparency ||
        rules.allowBackgroundImage;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.hub.title'))),
      body: ListView(
        padding:
            const EdgeInsets.fromLTRB(AthkarSpacing.page, 16, AthkarSpacing.page, 32),
        children: [
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('widgets.hub.placed'),
                  icon: Icons.smartphone_outlined,
                  onTap: () => _open(context, const WidgetScreen()),
                ),
                if (rules.allowCustomWidget) ...[
                  const AthkarRule(margin: EdgeInsets.zero),
                  AthkarListRow(
                    label: context.tr('widgets.custom.title'),
                    icon: Icons.push_pin_outlined,
                    onTap: () => _open(context, const CustomWidgetScreen()),
                  ),
                ],
              ],
            ),
          ),
          _hint(context, tokens, 'widgets.hub.placedHint'),

          if (canCustomise) ...[
            const SizedBox(height: 20),
            AthkarSectionHeader(title: context.tr('widgets.hub.customisation')),
            const SizedBox(height: 10),
            AthkarCard(
              padding: EdgeInsets.zero,
              child: AthkarListRow(
                label: context.tr('widgets.appearance.title'),
                icon: Icons.palette_outlined,
                onTap: () => _open(context, const WidgetAppearanceScreen()),
              ),
            ),
            _hint(context, tokens, 'widgets.hub.customisationHint'),
          ],

          const SizedBox(height: 20),
          AthkarSectionHeader(title: context.tr('widgets.hub.help')),
          const SizedBox(height: 10),
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('widgets.gallery.title'),
                  icon: Icons.grid_view_outlined,
                  onTap: () => _open(context, const WidgetGalleryScreen()),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('widgets.help.title'),
                  icon: Icons.help_outline,
                  onTap: () => _open(context, const WidgetHelpScreen()),
                ),
              ],
            ),
          ),
          _hint(context, tokens, 'widgets.hub.helpHint'),
        ],
      ),
    );
  }

  Widget _hint(BuildContext context, AthkarTokens tokens, String key) => Padding(
        padding: const EdgeInsets.fromLTRB(4, 8, 4, 0),
        child: Text(
          context.tr(key),
          style: AthkarType.sans(size: 11.5, color: tokens.faint, height: 1.8),
        ),
      );

  void _open(BuildContext context, Widget screen) {
    Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
  }
}
