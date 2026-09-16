import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_compass/flutter_compass.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/prayer_times.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../core/true_heading.dart';
import '../widgets/athkar_ui.dart';
import 'location_screen.dart';

/// The compass.
///
/// Three things make this correct rather than approximately correct, and all
/// three are invisible when they work:
///
/// - **The reading is corrected to true north.** A magnetometer points at
///   magnetic north; the qibla is computed from true north. See
///   [TrueHeading] for where the correction comes from and why it is not
///   computed here.
/// - **An uncorrected reading says so.** When the platform cannot supply a
///   declination the arrow still turns, but the screen tells the reader it is
///   pointing at magnetic north — rather than quietly being a few degrees out.
/// - **A device with no magnetometer still gets an answer.** Many cheap Android
///   phones have none, and a compass that never moves is worse than the bearing
///   written as a number.
class QiblaScreen extends StatefulWidget {
  const QiblaScreen({super.key});

  @override
  State<QiblaScreen> createState() => _QiblaScreenState();
}

class _QiblaScreenState extends State<QiblaScreen> {
  /// Null until the platform answers, and possibly for good — see [TrueHeading].
  double? _declination;
  var _asked = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _loadDeclination();
  }

  Future<void> _loadDeclination() async {
    final settings = SettingsScope.of(context);
    final latitude = settings.latitude;
    final longitude = settings.longitude;

    if (latitude == null || longitude == null || _asked) return;
    _asked = true;

    final value = await TrueHeading.instance.declination(latitude, longitude);
    if (mounted) setState(() => _declination = value);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final qibla = PrayerCalculator.qiblaDirection(settings);
    final distance = PrayerCalculator.distanceToMakkah(settings);

    if (qibla == null) {
      return Scaffold(
        backgroundColor: tokens.paper,
        body: AthkarEmptyState(
          title: context.tr('prayer.noLocation'),
          body: context.tr('prayer.noLocationHint'),
          icon: Icons.explore_outlined,
          action: FilledButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const LocationScreen()),
            ),
            child: Text(context.tr('prayer.chooseCity')),
          ),
        ),
      );
    }

    final digits = settings.arabicNumerals;

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        child: StreamBuilder<CompassEvent?>(
          stream: FlutterCompass.events,
          builder: (context, snapshot) {
            final magnetic = snapshot.data?.heading;

            // A null heading once the stream has produced an event means this
            // device has no usable magnetometer.
            final hasSensor = snapshot.hasData && magnetic != null;

            // Everything below this line is a true bearing. On iOS the reading
            // already is one and the declination is zero by construction.
            final heading = hasSensor ? TrueHeading.apply(magnetic, _declination) : null;
            final difference = heading == null ? null : TrueHeading.difference(qibla, heading);

            final isCorrected = TrueHeading.instance.isAlreadyTrue || _declination != null;

            return ListView(
              padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 14, AthkarSpacing.page, 30),
              children: [
                Center(
                  child: Column(
                    children: [
                      Text(
                        context.tr('qibla.title'),
                        style: AthkarType.amiri(
                            size: 22, color: tokens.ink, weight: FontWeight.w700),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        context.tr('qibla.distance', {
                          'city': settings.cityName ?? '',
                          'distance': Numerals.format(
                            distance?.round() ?? 0,
                            arabicIndic: digits,
                          ),
                        }),
                        style: AthkarType.sans(size: 11.5, color: tokens.muted),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 14),
                Center(
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.baseline,
                    textBaseline: TextBaseline.alphabetic,
                    children: [
                      Text(
                        '${Numerals.format(qibla.round(), arabicIndic: digits)}°',
                        style: AthkarType.sans(
                          size: 46,
                          color: tokens.ink,
                          weight: FontWeight.w300,
                          height: 1.1,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        _compassPoint(context, qibla),
                        style: AthkarType.sans(size: 12.5, color: tokens.muted),
                      ),
                    ],
                  ),
                ),

                const SizedBox(height: 20),
                Center(child: _CompassDial(qibla: qibla, heading: heading)),

                const SizedBox(height: 22),
                Text(
                  _instruction(context, difference, hasSensor, qibla, digits),
                  textAlign: TextAlign.center,
                  style: AthkarType.sans(
                    size: 13.5,
                    color: tokens.brandInk,
                    weight: FontWeight.w500,
                    height: 1.6,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  context.tr('qibla.hint'),
                  textAlign: TextAlign.center,
                  style: AthkarType.sans(size: 11.5, color: tokens.muted),
                ),

                const SizedBox(height: 16),
                if (hasSensor)
                  _AccuracyCard(
                    accuracy: snapshot.data?.accuracy,
                    isCorrected: isCorrected,
                  ),
              ],
            );
          },
        ),
      ),
    );
  }

  /// What to tell the reader to do.
  ///
  /// Within five degrees counts as facing it: nobody holds a bearing tighter
  /// than that on a prayer mat, and a message flickering between "turn left"
  /// and "turn right" is worse than none.
  String _instruction(
    BuildContext context,
    double? difference,
    bool hasSensor,
    double qibla,
    bool digits,
  ) {
    if (!hasSensor) {
      return '${context.tr('qibla.noSensor')}\n'
          '${context.tr('qibla.noSensorHint', {
                'degrees': Numerals.format(qibla.round(), arabicIndic: digits),
              })}';
    }

    if (difference == null) return '';
    if (difference.abs() <= 5) return context.tr('qibla.aligned');

    return context.tr('qibla.turn', {
      'degrees': Numerals.format(difference.abs().round(), arabicIndic: digits),
      'direction': difference > 0 ? context.tr('qibla.right') : context.tr('qibla.left'),
    });
  }

  String _compassPoint(BuildContext context, double bearing) {
    final north = context.tr('qibla.north');
    final south = context.tr('qibla.south');
    final east = context.tr('qibla.east');
    final west = context.tr('qibla.west');

    return switch (bearing) {
      < 22.5 || >= 337.5 => north,
      < 67.5 => '$north $east',
      < 112.5 => east,
      < 157.5 => '$south $east',
      < 202.5 => south,
      < 247.5 => '$south $west',
      < 292.5 => west,
      _ => '$north $west',
    };
  }
}

/// The dial: a ring with the cardinal letters, a dot on it for Makkah, and a
/// needle.
class _CompassDial extends StatelessWidget {
  const _CompassDial({required this.qibla, required this.heading});

  final double qibla;
  final double? heading;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    // The dial rotates against the device so north stays north. With no sensor
    // it sits at zero and the needle simply shows the bearing from north.
    final rotation = -(heading ?? 0) * math.pi / 180;
    final qiblaAngle = qibla * math.pi / 180;

    return SizedBox(
      width: 288,
      height: 288,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Container(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: tokens.surface,
              border: Border.all(color: tokens.border),
            ),
          ),
          Transform.rotate(
            angle: rotation,
            child: Stack(
              alignment: Alignment.center,
              children: [
                _Cardinal(context.tr('qibla.north'), Alignment.topCenter),
                _Cardinal(context.tr('qibla.south'), Alignment.bottomCenter),
                _Cardinal(context.tr('qibla.east'), Alignment.centerRight),
                _Cardinal(context.tr('qibla.west'), Alignment.centerLeft),

                // The inner ring, and the dot that marks Makkah on it.
                Padding(
                  padding: const EdgeInsets.all(26),
                  child: Container(
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      border: Border.all(color: tokens.hairline),
                    ),
                    child: Transform.rotate(
                      angle: qiblaAngle,
                      child: Align(
                        alignment: Alignment.topCenter,
                        child: Transform.translate(
                          offset: const Offset(0, -7),
                          child: Container(
                            width: 14,
                            height: 14,
                            decoration: BoxDecoration(
                              shape: BoxShape.circle,
                              color: tokens.brand,
                              border: Border.all(color: tokens.surface, width: 2),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),

                // The needle, pointing at the qibla.
                Transform.rotate(
                  angle: qiblaAngle,
                  child: CustomPaint(
                    size: const Size(206, 206),
                    painter: _NeedlePainter(
                      color: tokens.brand,
                      tail: tokens.border,
                      hub: tokens.ink,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Cardinal extends StatelessWidget {
  const _Cardinal(this.label, this.alignment);

  final String label;
  final Alignment alignment;

  @override
  Widget build(BuildContext context) => Align(
        alignment: alignment,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Text(
            label,
            style: AthkarType.sans(
              size: 12,
              color: AthkarTokens.of(context).muted,
              weight: FontWeight.w600,
            ),
          ),
        ),
      );
}

class _NeedlePainter extends CustomPainter {
  const _NeedlePainter({required this.color, required this.tail, required this.hub});

  final Color color;
  final Color tail;
  final Color hub;

  @override
  void paint(Canvas canvas, Size size) {
    final centre = Offset(size.width / 2, size.height / 2);

    final head = Paint()
      ..color = color
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;

    final back = Paint()
      ..color = tail
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round;

    canvas.drawLine(centre, centre.translate(0, -92), head);
    canvas.drawLine(centre, centre.translate(0, 56), back);
    canvas.drawCircle(centre, 7, Paint()..color = hub);
  }

  @override
  bool shouldRepaint(_NeedlePainter old) =>
      old.color != color || old.tail != tail || old.hub != hub;
}

/// Sensor accuracy, the figure-of-eight instruction when it is poor, and
/// whether the reading is corrected to true north at all.
///
/// The last of those is here rather than hidden: a badly calibrated
/// magnetometer, or an uncorrected one, gives a confidently wrong answer, and
/// the reader has no way to tell without being told.
class _AccuracyCard extends StatelessWidget {
  const _AccuracyCard({required this.accuracy, required this.isCorrected});

  final double? accuracy;
  final bool isCorrected;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    // The plugin reports accuracy as degrees of expected error, and negative
    // when it has no estimate at all.
    final isGood = accuracy != null && accuracy! >= 0 && accuracy! <= 15;

    return AthkarCard(
      radius: AthkarSpacing.smallCardRadius,
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      context.tr('qibla.accuracy'),
                      style: AthkarType.sans(
                          size: 13.5, color: tokens.ink, weight: FontWeight.w500),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      isGood
                          ? context.tr('qibla.accuracy.good')
                          : context.tr('qibla.accuracy.poor'),
                      style: AthkarType.sans(size: 11.5, color: tokens.muted),
                    ),
                  ],
                ),
              ),
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: isGood ? tokens.brand : const Color(0xFFB07A2E),
                ),
              ),
            ],
          ),
          if (!isCorrected) ...[
            const AthkarRule(margin: EdgeInsets.symmetric(vertical: 12)),
            Text(
              context.tr('qibla.magneticOnly'),
              style: AthkarType.sans(size: 11.5, color: tokens.muted, height: 1.7),
            ),
          ],
        ],
      ),
    );
  }
}
