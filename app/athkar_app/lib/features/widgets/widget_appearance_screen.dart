import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:path_provider/path_provider.dart';

import '../../core/l10n.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'widget_designs.dart';
import 'widget_preview_data.dart';
import 'widget_skin.dart';

/// «التخصيص» — the colour, the transparency and the photograph.
///
/// Three things a reader may do to a widget, each of which the admin can
/// withdraw, and each of which is shown against a live preview rather than a
/// swatch. That is not a flourish: the reader is choosing a colour for a panel
/// that will sit over a wallpaper this screen cannot see, and a swatch tells
/// them nothing about whether the times will still be readable on it.
///
/// The floor on transparency is [WidgetAppearance.minOpacity] rather than zero,
/// and that limit is enforced when the value is *read* rather than when it is
/// set. A fully transparent panel over a photograph is how a dhikr becomes
/// unreadable on a home screen, and the reader cannot tell that anything is
/// wrong — they simply stop reading it.
class WidgetAppearanceScreen extends StatelessWidget {
  const WidgetAppearanceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final rules = AppStateScope.of(context).widgetSettings;

    final skin = WidgetSkin.of(context, rules: rules);
    final data = WidgetPreviewData.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.appearance.title'))),
      body: ListView(
        padding:
            const EdgeInsets.fromLTRB(AthkarSpacing.page, 16, AthkarSpacing.page, 32),
        children: [
          // The preview first, because every control below it is a question
          // about this picture.
          WidgetPanel(
            skin: skin,
            height: WidgetDesigns.heightFor('today_prayers'),
            child: WidgetDesigns.build(context, 'today_prayers', 0, data, skin) ??
                const SizedBox.shrink(),
          ),
          const SizedBox(height: 24),

          if (rules.allowBackgroundColor) ...[
            AthkarSectionHeader(title: context.tr('widgets.appearance.colour')),
            const SizedBox(height: 12),
            _Tints(selected: settings.widgetBackgroundColor),
            const SizedBox(height: 24),
          ],

          if (rules.allowTransparency) ...[
            AthkarSectionHeader(
              title: context.tr('widgets.appearance.transparency'),
              note: context.tr('widgets.appearance.transparencyHint'),
            ),
            Slider(
              value: settings.widgetOpacity,
              min: WidgetAppearance.minOpacity,
              max: 1,
              onChanged: (value) => settings.setWidgetOpacity(value),
            ),
            const SizedBox(height: 16),
          ],

          if (rules.allowBackgroundImage) ...[
            AthkarSectionHeader(
              title: context.tr('widgets.appearance.image'),
              note: context.tr('widgets.appearance.imageHint'),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: kIsWeb ? null : () => _pick(context),
                    icon: const Icon(Icons.image_outlined, size: 16),
                    label: Text(context.tr('widgets.appearance.chooseImage')),
                  ),
                ),
                if (settings.widgetImagePath != null) ...[
                  const SizedBox(width: 10),
                  OutlinedButton(
                    onPressed: () => settings.setWidgetImagePath(null),
                    child: Text(context.tr('common.remove')),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 20),
          ],

          if (!rules.allowBackgroundColor &&
              !rules.allowTransparency &&
              !rules.allowBackgroundImage)
            AthkarEmptyState(
              title: context.tr('widgets.appearance.none'),
              body: context.tr('widgets.appearance.noneHint'),
              icon: Icons.palette_outlined,
            ),

          const SizedBox(height: 8),
          Text(
            context.tr('widgets.appearance.footnote'),
            style: AthkarType.sans(size: 11.5, color: tokens.faint, height: 1.8),
          ),
        ],
      ),
    );
  }

  /// Copies the chosen photograph into the app's own directory.
  ///
  /// The picker hands back a path in a cache the system is free to empty, so a
  /// widget pointed at it would lose its background at an unpredictable moment
  /// weeks later — the kind of fault nobody can reproduce. One copy, one known
  /// name, overwritten each time.
  Future<void> _pick(BuildContext context) async {
    final settings = SettingsScope.read(context);

    try {
      final picked = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        maxWidth: 1600,
        imageQuality: 88,
      );

      if (picked == null) return;

      final directory = await getApplicationDocumentsDirectory();
      final destination = File('${directory.path}/widget_background.jpg');
      await File(picked.path).copy(destination.path);

      await settings.setWidgetImagePath(destination.path);
    } catch (_) {
      // A refused permission, a picker the platform does not offer, a file the
      // app cannot read: none of them is worth more than a sentence.
      if (context.mounted) {
        AthkarAlerts.error(context, context.tr('widgets.appearance.imageFailed'));
      }
    }
  }
}

/// The tints on offer.
///
/// A short list rather than a colour wheel: each of these was checked against
/// both the light and the dark ink, and a free picker guarantees that somebody
/// lands on a green that swallows the text.
class _Tints extends StatelessWidget {
  const _Tints({required this.selected});

  final int? selected;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return Wrap(
      spacing: 10,
      runSpacing: 10,
      children: [
        _Swatch(
          colour: tokens.surface,
          label: context.tr('widgets.appearance.default'),
          selected: selected == null,
          onTap: () => settings.setWidgetBackgroundColor(null),
        ),
        for (final value in WidgetAppearance.tints)
          _Swatch(
            colour: Color(value),
            selected: selected == value,
            onTap: () => settings.setWidgetBackgroundColor(value),
          ),
      ],
    );
  }
}

class _Swatch extends StatelessWidget {
  const _Swatch({
    required this.colour,
    required this.selected,
    required this.onTap,
    this.label,
  });

  final Color colour;
  final bool selected;
  final VoidCallback onTap;
  final String? label;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        width: label == null ? 46 : null,
        height: 46,
        padding: label == null ? null : const EdgeInsets.symmetric(horizontal: 14),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: colour,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: selected ? tokens.brand : tokens.border,
            width: selected ? 2 : 1,
          ),
        ),
        child: label == null
            ? (selected
                ? Icon(
                    Icons.check,
                    size: 18,
                    color: ThemeData.estimateBrightnessForColor(colour) == Brightness.dark
                        ? Colors.white
                        : Colors.black87,
                  )
                : null)
            : Text(
                label!,
                style: AthkarType.sans(size: 11.5, color: tokens.ink),
              ),
      ),
    );
  }
}
