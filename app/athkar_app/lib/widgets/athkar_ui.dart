library;

import 'package:flutter/material.dart';

import '../core/theme.dart';

/// The UI kit: the handful of shapes the design repeats on every screen.
///
/// Nothing here is generic. Each widget exists because the prototype draws that
/// exact thing more than once — an outlined parchment card, a pill, a hairline
/// rule with an ornament in the middle. A screen that reaches for a raw
/// `Container` with a `BoxDecoration` is usually re-deriving one of these.

/// The outlined parchment card that nearly everything sits in.
class AthkarCard extends StatelessWidget {
  const AthkarCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.fromLTRB(20, 18, 20, 18),
    this.margin,
    this.onTap,
    this.radius = AthkarSpacing.cardRadius,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry? margin;
  final VoidCallback? onTap;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final card = Container(
      margin: margin,
      decoration: BoxDecoration(
        color: tokens.surface,
        border: Border.all(color: tokens.border),
        borderRadius: BorderRadius.circular(radius),
      ),
      child: Padding(padding: padding, child: child),
    );

    if (onTap == null) return card;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(radius),
      child: card,
    );
  }
}

/// A card filled with the brand green — the "right now" card on the home screen
/// and nothing else, so it stays the one thing on the page that draws the eye.
class AthkarBrandCard extends StatelessWidget {
  const AthkarBrandCard({super.key, required this.child, this.margin, this.onTap});

  final Widget child;
  final EdgeInsetsGeometry? margin;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
      child: Container(
        margin: margin,
        decoration: BoxDecoration(
          color: tokens.brand,
          borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
        ),
        padding: const EdgeInsets.all(20),
        child: child,
      ),
    );
  }
}

/// The rounded outline chip: a city, a repeat target, a filter.
class AthkarChip extends StatelessWidget {
  const AthkarChip({
    super.key,
    required this.label,
    this.selected = false,
    this.onTap,
    this.leading,
    this.trailing,
  });

  final String label;
  final bool selected;
  final VoidCallback? onTap;
  final Widget? leading;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        constraints: const BoxConstraints(minHeight: 40),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        decoration: BoxDecoration(
          color: selected ? tokens.brand : Colors.transparent,
          border: Border.all(color: selected ? tokens.brand : tokens.border),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (leading != null) ...[leading!, const SizedBox(width: 7)],
            Text(
              label,
              style: AthkarType.sans(
                size: 12.5,
                color: selected ? tokens.onBrand : tokens.ink,
                weight: selected ? FontWeight.w600 : FontWeight.w500,
              ),
            ),
            if (trailing != null) ...[const SizedBox(width: 7), trailing!],
          ],
        ),
      ),
    );
  }
}

/// A section heading with an optional quiet note on the other side — the
/// «اقرأ اليوم · ذكر · حديث · ورد قرآني» pattern.
class AthkarSectionHeader extends StatelessWidget {
  const AthkarSectionHeader({super.key, required this.title, this.note});

  final String title;
  final String? note;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.baseline,
      textBaseline: TextBaseline.alphabetic,
      children: [
        Text(title, style: AthkarType.amiri(size: 17, color: tokens.ink, weight: FontWeight.w700)),
        const Spacer(),
        if (note != null)
          Text(note!, style: AthkarType.sans(size: 11.5, color: tokens.muted)),
      ],
    );
  }
}

/// The hairline rule that separates rows inside a card.
class AthkarRule extends StatelessWidget {
  const AthkarRule({super.key, this.margin = const EdgeInsets.symmetric(vertical: 12)});

  final EdgeInsetsGeometry margin;

  @override
  Widget build(BuildContext context) => Container(
        height: 1,
        margin: margin,
        color: AthkarTokens.of(context).hairline,
      );
}

/// The centred ornamental rule the design uses above a passage of reading — a
/// short line, not a full-width divider, because it is punctuation rather than
/// structure.
class AthkarOrnament extends StatelessWidget {
  const AthkarOrnament({super.key, this.width = 64});

  final double width;

  @override
  Widget build(BuildContext context) => Center(
        child: Container(
          width: width,
          height: 1,
          color: AthkarTokens.of(context).border,
        ),
      );
}

/// The app's loading indicator.
///
/// Never use `CircularProgressIndicator` — its Material sweep is from a
/// different design language and looks wrong on parchment. This is a thin ring
/// in the brand colour, at the weight the rest of the page is drawn in.
class AthkarSpinner extends StatelessWidget {
  const AthkarSpinner({super.key, this.size = 22, this.color});

  /// A spinner inside a filled button, where the brand colour would vanish.
  const AthkarSpinner.mono(Color this.color, {super.key, this.size = 18});

  final double size;
  final Color? color;

  @override
  Widget build(BuildContext context) => SizedBox(
        width: size,
        height: size,
        child: CircularProgressIndicator(
          strokeWidth: 2,
          color: color ?? AthkarTokens.of(context).brand,
        ),
      );
}

/// A full-page state: nothing here, nothing found, nothing to sync.
///
/// One widget for all of them because they differ only in wording, and because
/// an empty screen is where an app most often stops explaining itself.
class AthkarEmptyState extends StatelessWidget {
  const AthkarEmptyState({
    super.key,
    required this.title,
    this.body,
    this.action,
    this.icon,
  });

  final String title;
  final String? body;
  final Widget? action;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(40),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 32, color: tokens.faint),
              const SizedBox(height: 16),
            ],
            Text(
              title,
              textAlign: TextAlign.center,
              style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
            ),
            if (body != null) ...[
              const SizedBox(height: 8),
              Text(
                body!,
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 13, color: tokens.muted, height: 1.7),
              ),
            ],
            if (action != null) ...[const SizedBox(height: 20), action!],
          ],
        ),
      ),
    );
  }
}

/// A row in a settings or profile list: a label, an optional value, a chevron.
class AthkarListRow extends StatelessWidget {
  const AthkarListRow({
    super.key,
    required this.label,
    this.value,
    this.onTap,
    this.trailing,
    this.icon,
  });

  final String label;
  final String? value;
  final VoidCallback? onTap;
  final Widget? trailing;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      onTap: onTap,
      child: Container(
        constraints: const BoxConstraints(minHeight: AthkarSpacing.tapTarget + 8),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
        child: Row(
          children: [
            if (icon != null) ...[
              Icon(icon, size: 18, color: tokens.muted),
              const SizedBox(width: 12),
            ],
            Expanded(
              child: Text(
                label,
                style: AthkarType.sans(size: 13.5, color: tokens.ink, weight: FontWeight.w500),
              ),
            ),
            if (value != null)
              Text(value!, style: AthkarType.sans(size: 12.5, color: tokens.muted)),
            if (trailing != null) trailing!,
            if (onTap != null && trailing == null) ...[
              const SizedBox(width: 6),
              // Not conditional on the text direction: Flutter declares the
              // Material chevrons with `matchTextDirection`, so the glyph is
              // already mirrored in RTL. Choosing the other icon by hand flips
              // it a second time and points it back the wrong way.
              Icon(Icons.chevron_right, size: 18, color: tokens.faint),
            ],
          ],
        ),
      ),
    );
  }
}

/// The takhrij line: book, number and grading, in the quiet weight the design
/// gives it. It is the project's distinguishing claim, so it appears under
/// every dhikr — but as a footnote, never as a headline.
class AthkarSourceLine extends StatelessWidget {
  const AthkarSourceLine({super.key, required this.text, this.trailing});

  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Row(
      children: [
        Expanded(
          child: Text(text, style: AthkarType.sans(size: 11.5, color: tokens.muted)),
        ),
        if (trailing != null) trailing!,
      ],
    );
  }
}
