import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';

/// Every message the app shows the reader, in one place.
///
/// **Never use `SnackBar`,** and never `AlertDialog` directly. Material's own
/// are styled for a different design language — a floating dark slab, a white
/// sheet with its own radius and elevation — and either undoes the parchment in
/// a single frame. The strips and the panel below draw the same information in
/// the app's own materials.
///
/// Two shapes, and the choice between them is about consequence:
///   • [toast] / [error] — a strip that says what happened and leaves.
///   • [alert] / [confirm] — a modal panel, for something the reader must read
///     before the app can go on, or must answer.
class AthkarAlerts {
  const AthkarAlerts._();

  /// A short confirmation: copied, saved, sent.
  static void toast(BuildContext context, String message) {
    final tokens = AthkarTokens.of(context);
    final overlay = Overlay.of(context);

    final entry = OverlayEntry(
      builder: (context) => Positioned(
        left: 24,
        right: 24,
        bottom: MediaQuery.of(context).padding.bottom + 90,
        child: IgnorePointer(
          child: Material(
            color: Colors.transparent,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
              decoration: BoxDecoration(
                color: tokens.ink,
                borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
              ),
              child: Text(
                message,
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 13, color: tokens.paper),
              ),
            ),
          ),
        ),
      ),
    );

    overlay.insert(entry);
    Future.delayed(const Duration(seconds: 2), entry.remove);
  }

  /// A failure the reader can act on. Same shape as [toast] but in the ink of a
  /// warning, and it waits a little longer.
  static void error(BuildContext context, String message) {
    final overlay = Overlay.of(context);

    final entry = OverlayEntry(
      builder: (context) => Positioned(
        left: 24,
        right: 24,
        bottom: MediaQuery.of(context).padding.bottom + 90,
        child: IgnorePointer(
          child: Material(
            color: Colors.transparent,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
              decoration: BoxDecoration(
                color: const Color(0xFF7A2E2E),
                borderRadius: BorderRadius.circular(AthkarSpacing.smallCardRadius),
              ),
              child: Text(
                message,
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 13, color: const Color(0xFFF7E8E4)),
              ),
            ),
          ),
        ),
      ),
    );

    overlay.insert(entry);
    Future.delayed(const Duration(seconds: 3), entry.remove);
  }

  /// A statement the reader has to acknowledge — a download that failed, a
  /// permission the app cannot grant itself, a sync that found nothing.
  ///
  /// Pass [actionLabel] to offer a second button; the future then resolves
  /// `true` when it was tapped. With no action the panel is a single full-width
  /// dismiss and the answer is always `false`, so a caller that only wants to
  /// say something can ignore the result.
  ///
  /// Labels default to the localised «Close» / «Try again», which is what all
  /// but a handful of callers want.
  static Future<bool> alert(
    BuildContext context, {
    required String title,
    String? message,
    AthkarAlertKind kind = AthkarAlertKind.info,
    String? dismissLabel,
    String? actionLabel,
  }) =>
      _showPanel(
        context,
        kind: kind,
        icon: _iconFor(kind),
        title: title,
        body: message,
        // A lone dismiss is the primary act, so it gets the filled button; the
        // moment there is something else to do, dismiss steps back to a ghost.
        primaryLabel: actionLabel ?? dismissLabel ?? context.tr('common.close'),
        primaryValue: actionLabel != null,
        secondaryLabel:
            actionLabel == null ? null : (dismissLabel ?? context.tr('common.close')),
      );

  /// A yes/no question. Returns false when dismissed, so a caller never has to
  /// handle null.
  ///
  /// [destructive] is not decoration: it colours the confirming button in the
  /// alert tone and shows the warning glyph, because the two questions this app
  /// asks — forget this device, reset a counter at ninety-eight — are both
  /// things that cannot be undone.
  static Future<bool> confirm(
    BuildContext context, {
    required String title,
    String? body,
    String? confirmLabel,
    String? cancelLabel,
    bool destructive = false,
  }) =>
      _showPanel(
        context,
        kind: destructive ? AthkarAlertKind.error : AthkarAlertKind.info,
        icon: destructive ? Icons.warning_amber_rounded : Icons.help_outline_rounded,
        title: title,
        body: body,
        primaryLabel: confirmLabel ?? context.tr('common.ok'),
        primaryValue: true,
        secondaryLabel: cancelLabel ?? context.tr('common.cancel'),
      );

  /// The one modal both entry points are drawn from, so a confirm and an alert
  /// are never a pixel apart.
  static Future<bool> _showPanel(
    BuildContext context, {
    required AthkarAlertKind kind,
    required IconData icon,
    required String title,
    required String? body,
    required String primaryLabel,
    required bool primaryValue,
    required String? secondaryLabel,
  }) async {
    final tokens = AthkarTokens.of(context);

    final answer = await showDialog<bool>(
      context: context,
      barrierColor: tokens.ink.withValues(alpha: 0.45),
      builder: (context) => Dialog(
        backgroundColor: Colors.transparent,
        elevation: 0,
        insetPadding: const EdgeInsets.symmetric(horizontal: 26, vertical: 24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 360),
          child: AthkarAlertPanel(
            kind: kind,
            icon: icon,
            title: title,
            message: body,
            primaryLabel: primaryLabel,
            secondaryLabel: secondaryLabel,
            onPrimary: () => Navigator.of(context).pop(primaryValue),
            onSecondary: () => Navigator.of(context).pop(false),
          ),
        ),
      ),
    );

    // A barrier tap and a back gesture both arrive as null, and both mean no.
    return answer ?? false;
  }

  static IconData _iconFor(AthkarAlertKind kind) => switch (kind) {
        AthkarAlertKind.error => Icons.error_outline_rounded,
        AthkarAlertKind.warning => Icons.warning_amber_rounded,
        AthkarAlertKind.success => Icons.check_circle_outline_rounded,
        AthkarAlertKind.info => Icons.info_outline_rounded,
      };
}

/// What voice an alert speaks in. Picks the glyph disc and the filled button's
/// colour out of [AthkarTokens]; nothing here is a literal.
enum AthkarAlertKind { error, warning, success, info }

/// The modal card itself — a tinted disc, an Amiri headline, a line of sans
/// explanation, and one or two buttons.
///
/// Public because a screen occasionally wants it inline rather than over a
/// barrier, but the common path is [AthkarAlerts.alert] and
/// [AthkarAlerts.confirm].
class AthkarAlertPanel extends StatelessWidget {
  const AthkarAlertPanel({
    super.key,
    required this.kind,
    required this.title,
    this.icon,
    this.message,
    required this.primaryLabel,
    this.secondaryLabel,
    this.onPrimary,
    this.onSecondary,
  });

  final AthkarAlertKind kind;
  final IconData? icon;
  final String title;
  final String? message;
  final String primaryLabel;
  final String? secondaryLabel;
  final VoidCallback? onPrimary;
  final VoidCallback? onSecondary;

  /// The accent for this kind: the glyph, and the fill behind the primary
  /// button. Info borrows the brand — an ordinary question is not an incident.
  (Color, Color) _accent(AthkarTokens tokens) => switch (kind) {
        AthkarAlertKind.error => (tokens.alert, tokens.alertTint),
        AthkarAlertKind.warning => (tokens.warning, tokens.warningTint),
        AthkarAlertKind.success => (tokens.success, tokens.successTint),
        AthkarAlertKind.info => (tokens.brand, tokens.brandTint),
      };

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final (accent, tint) = _accent(tokens);

    return Material(
      color: tokens.surface,
      borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 22, 20, 18),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
          border: Border.all(color: tokens.border),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Container(
                width: 52,
                height: 52,
                alignment: Alignment.center,
                decoration: BoxDecoration(color: tint, shape: BoxShape.circle),
                child: Icon(icon, size: 26, color: accent),
              ),
              const SizedBox(height: 14),
            ],
            Text(
              title,
              textAlign: TextAlign.center,
              style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
            ),
            if (message != null && message!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 13, color: tokens.muted, height: 1.7),
              ),
            ],
            const SizedBox(height: 18),
            Row(
              children: [
                if (secondaryLabel != null) ...[
                  Expanded(child: _GhostButton(label: secondaryLabel!, onTap: onSecondary)),
                  const SizedBox(width: 10),
                ],
                Expanded(
                  child: _FilledButton(label: primaryLabel, color: accent, onTap: onPrimary),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// The declining answer: outlined, in the page's own ink.
class _GhostButton extends StatelessWidget {
  const _GhostButton({required this.label, this.onTap});

  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
      child: Container(
        height: AthkarSpacing.tapTarget,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
          border: Border.all(color: tokens.border),
        ),
        child: Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: AthkarType.sans(size: 13, color: tokens.ink, weight: FontWeight.w500),
        ),
      ),
    );
  }
}

/// The answer that acts, filled in the kind's accent.
class _FilledButton extends StatelessWidget {
  const _FilledButton({required this.label, required this.color, this.onTap});

  final String label;
  final Color color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
      child: Container(
        height: AthkarSpacing.tapTarget,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: color,
          borderRadius: BorderRadius.circular(AthkarSpacing.buttonRadius),
        ),
        child: Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: AthkarType.sans(size: 13, color: tokens.onBrand, weight: FontWeight.w600),
        ),
      ),
    );
  }
}
