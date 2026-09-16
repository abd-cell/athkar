import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../core/theme.dart';

/// The mark: a «ذ» held inside an open tasbih ring, the bead at the ring's head.
///
/// Drawn rather than shipped as an asset so it takes the theme's own colours
/// and stays crisp at any size — and so it can be *drawn on*: [reveal] runs the
/// whole mark from nothing (0) to finished (1). The bead arrives first, the
/// ring winds on behind it, the letter settles last. That ordering is the point
/// of the splash animation; a fade of the finished mark would say nothing.
class AthkarLogo extends StatelessWidget {
  const AthkarLogo({
    super.key,
    this.size = 88,
    this.color,
    this.reveal = 1,
  });

  final double size;

  /// Defaults to the theme's brand green.
  final Color? color;

  /// 0 = nothing drawn, 1 = the finished mark.
  final double reveal;

  /// The phase windows, shared with the splash so its wordmark can start as the
  /// letter lands rather than at an unrelated moment.
  static const beadPhase = Interval(0, 0.16, curve: Curves.easeOutBack);
  static const ringPhase = Interval(0.10, 0.66, curve: Curves.easeOutCubic);
  static const glyphPhase = Interval(0.60, 0.92, curve: Curves.easeOutCubic);

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final ink = color ?? tokens.brand;
    final t = reveal.clamp(0.0, 1.0);
    final glyph = glyphPhase.transform(t);

    return SizedBox.square(
      dimension: size,
      child: CustomPaint(
        painter: _MarkPainter(
          color: ink,
          bead: beadPhase.transform(t),
          ring: ringPhase.transform(t),
        ),
        child: Center(
          child: Opacity(
            opacity: glyph.clamp(0.0, 1.0),
            child: Transform.scale(
              scale: 0.82 + 0.18 * glyph,
              child: Text(
                'ذ',
                textAlign: TextAlign.center,
                style: AthkarType.amiri(
                  size: size * 0.56,
                  color: ink,
                  weight: FontWeight.w400,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The mark on a solid brand tile — the app-icon lockup, for the places that
/// want the thumbnail rather than the bare mark.
class AthkarLogoTile extends StatelessWidget {
  const AthkarLogoTile({super.key, this.size = 88});

  final double size;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: tokens.brand,
        borderRadius: BorderRadius.circular(size * 0.24),
      ),
      alignment: Alignment.center,
      child: AthkarLogo(size: size * 0.74, color: tokens.onBrand),
    );
  }
}

class _MarkPainter extends CustomPainter {
  const _MarkPainter({
    required this.color,
    required this.bead,
    required this.ring,
  });

  final Color color;

  /// Scale of the head bead, 0..1 (may overshoot — the curve is a back-ease).
  final double bead;

  /// Fraction of the ring's sweep that has been laid down, 0..1.
  final double ring;

  /// Where the ring opens, and where its head sits: just right of twelve, so
  /// the gap reads as a knot rather than as a broken circle.
  static const _head = -68 * math.pi / 180;
  static const _sweep = 316 * math.pi / 180;

  @override
  void paint(Canvas canvas, Size size) {
    final centre = Offset(size.width / 2, size.height / 2);
    final stroke = size.width * 0.085;
    final beadRadius = stroke * 0.98;
    // Inset so the bead — fatter than the stroke — still lands inside the box.
    final radius = size.width / 2 - beadRadius;

    if (ring > 0) {
      canvas.drawArc(
        Rect.fromCircle(center: centre, radius: radius),
        _head,
        _sweep * ring,
        false,
        Paint()
          ..color = color
          ..style = PaintingStyle.stroke
          ..strokeWidth = stroke
          ..strokeCap = StrokeCap.round
          ..isAntiAlias = true,
      );
    }

    if (bead > 0) {
      canvas.drawCircle(
        centre + Offset(math.cos(_head), math.sin(_head)) * radius,
        beadRadius * bead,
        Paint()
          ..color = color
          ..isAntiAlias = true,
      );
    }
  }

  @override
  bool shouldRepaint(_MarkPainter old) =>
      old.color != color || old.bead != bead || old.ring != ring;
}
