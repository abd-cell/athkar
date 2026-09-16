import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../models/models.dart';

/// The panel a widget is drawn on, and the four colours drawn onto it.
///
/// A widget does not live in the app. It sits on a launcher over a wallpaper
/// nobody here chose, which is why the reader gets to tint it, thin it and put
/// a photograph behind it at all — and why every one of those has to be
/// resolved *once*, in front of the ink, rather than guessed at by each of the
/// thirty renderers.
///
/// The rule the resolution enforces: **ink is chosen from the panel, never from
/// the app's own theme.** A reader running the app dark with a parchment tint on
/// their widget would otherwise get parchment text on parchment, which looks
/// exactly like a widget that failed to load.
@immutable
class WidgetSkin {
  const WidgetSkin({
    required this.panel,
    required this.ink,
    required this.muted,
    required this.accent,
    required this.hairline,
    required this.opacity,
    required this.imagePath,
  });

  /// The panel's own colour, before [opacity] is applied.
  final Color panel;

  /// Primary text — the times, the dhikr, the day.
  final Color ink;

  /// Labels and attributions.
  final Color muted;

  /// The next prayer, the day being counted to: one thing per widget, no more.
  final Color accent;

  final Color hairline;

  /// 1 is a solid panel; [WidgetAppearance.minOpacity] is the design's «شفاف».
  final double opacity;

  /// A photograph the reader put behind the widget, if any.
  final String? imagePath;

  bool get hasImage => imagePath != null && imagePath!.isNotEmpty && !kIsWeb;

  /// Resolves the reader's choices against the admin's permissions.
  ///
  /// Permissions first, always: a customisation the admin has withdrawn must
  /// stop applying to a widget already on a home screen, not merely disappear
  /// from the screen that offered it.
  static WidgetSkin of(BuildContext context, {WidgetSettings? rules}) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final permissions = rules ?? settings.widgetSettings;

    final tint = permissions.allowBackgroundColor ? settings.widgetBackgroundColor : null;
    final panel = tint == null ? tokens.surface : Color(tint);

    // Luminance rather than the app's brightness: the panel is what the text
    // has to survive, and the reader may well have tinted it the other way.
    final dark = ThemeData.estimateBrightnessForColor(panel) == Brightness.dark;

    final ink = dark ? const Color(0xFFF3ECE0) : const Color(0xFF241D16);

    return WidgetSkin(
      panel: panel,
      ink: ink,
      muted: ink.withValues(alpha: 0.62),
      // The brand green loses against a dark tint, so the dark palette's
      // lighter green is used there — the same swap the app itself makes.
      accent: dark ? AthkarColors.brandDarkInk : AthkarColors.brand,
      hairline: ink.withValues(alpha: 0.14),
      opacity: permissions.allowTransparency ? settings.widgetOpacity : 1,
      imagePath: permissions.allowBackgroundImage ? settings.widgetImagePath : null,
    );
  }
}

/// One widget-sized panel, painted the way the launcher will paint it.
///
/// Fixed [height] rather than an aspect ratio: a launcher grid gives a widget a
/// height in cells, and a preview that grows with the phone's width would show
/// the reader a shape they will never get.
class WidgetPanel extends StatelessWidget {
  const WidgetPanel({
    super.key,
    required this.skin,
    required this.child,
    this.height = 118,
    this.padding = const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
    this.radius = 20,
  });

  final WidgetSkin skin;
  final Widget child;
  final double height;
  final EdgeInsets padding;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final shape = BorderRadius.circular(radius);

    return SizedBox(
      height: height,
      width: double.infinity,
      child: ClipRRect(
        borderRadius: shape,
        child: Stack(
          fit: StackFit.expand,
          children: [
            // The photograph sits under the panel, not instead of it: the tint
            // at the reader's chosen opacity is what keeps the text legible
            // over an image nobody here has seen.
            if (skin.hasImage)
              Image.file(
                File(skin.imagePath!),
                fit: BoxFit.cover,
                // A file the reader has since deleted must not take the widget
                // down with it.
                errorBuilder: (_, __, ___) => const SizedBox.shrink(),
              ),
            Container(color: skin.panel.withValues(alpha: skin.opacity)),
            Padding(padding: padding, child: child),
          ],
        ),
      ),
    );
  }
}
