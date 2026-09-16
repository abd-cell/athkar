import 'package:flutter/material.dart';

import '../core/theme.dart';
import '../widgets/athkar_logo.dart';

/// The first frame.
///
/// Shown only while preferences load — milliseconds — so it exists to open a
/// cold start on the mark rather than on a white rectangle, not as a staging
/// area for work. Nothing is fetched here.
///
/// The mark draws itself: bead, then ring, then the letter, then the wordmark
/// under it. `main.dart` holds the splash for [minimumDuration] so that
/// sequence is never cut in half on a fast device — it is the one moment the
/// app has to introduce itself, and half of it is worse than none.
class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  /// How long the whole introduction takes, and therefore the shortest a cold
  /// start can be. Kept here so the caller cannot drift from the animation.
  static const minimumDuration = Duration(milliseconds: 1750);

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: SplashScreen.minimumDuration,
  )..forward();

  bool _honouredMotionPreference = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();

    // A reader who has asked the OS to still the interface gets the finished
    // mark, immediately, rather than a shortened version of the sequence.
    if (!_honouredMotionPreference &&
        MediaQuery.maybeDisableAnimationsOf(context) == true) {
      _honouredMotionPreference = true;
      _controller.value = 1;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Scaffold(
      backgroundColor: tokens.paper,
      body: Center(
        child: AnimatedBuilder(
          animation: _controller,
          builder: (context, _) {
            final t = _controller.value;
            // The wordmark comes up as the letter lands, not on its own clock.
            final words = const Interval(0.66, 1, curve: Curves.easeOutCubic)
                .transform(t)
                .clamp(0.0, 1.0);

            return Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                AthkarLogo(size: 112, reveal: t),
                const SizedBox(height: 22),
                Opacity(
                  opacity: words,
                  child: Transform.translate(
                    offset: Offset(0, 10 * (1 - words)),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'أذكاري',
                          style: AthkarType.amiri(
                            size: 28,
                            color: tokens.ink,
                            weight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'أذكار ومواقيت صلاة وقبلة',
                          style: AthkarType.sans(size: 12.5, color: tokens.muted),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
