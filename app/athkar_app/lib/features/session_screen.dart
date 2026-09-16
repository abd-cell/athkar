import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import 'dhikr_sheet.dart';

/// The screen the app is really for.
///
/// Three decisions carry it, and each is a deliberate rejection of the obvious
/// alternative:
///
/// - **The whole screen is the button.** Not a counter widget at the bottom with
///   a tap target the size of a thumbnail. Somebody saying a dhikr a hundred
///   times is not looking at the screen, and should not have to aim.
/// - **It advances by itself.** Finishing a count moves to the next dhikr, so
///   the reader's attention stays on the words rather than on the interface.
/// - **The screen stays awake.** A device that sleeps at thirty-three is the
///   single most irritating way this screen can fail.
class SessionScreen extends StatefulWidget {
  const SessionScreen({super.key, required this.category});

  final AthkarCategory category;

  @override
  State<SessionScreen> createState() => _SessionScreenState();
}

class _SessionScreenState extends State<SessionScreen> {
  late final PageController _pages;

  var _index = 0;
  var _count = 0;
  var _finished = false;

  @override
  void initState() {
    super.initState();

    final state = AppStateScope.read(context);
    final resumed = state.progress.sessionState(
      widget.category.id,
      daily: widget.category.rhythm == CategoryRhythm.daily,
    );

    _index = (resumed?.$1 ?? 0).clamp(0, widget.category.adhkar.length - 1);
    _count = resumed?.$2 ?? 0;

    _pages = PageController(initialPage: _index);

    // Kept awake for the session only, and released in dispose — a wakelock
    // left on is a battery complaint in a store review.
    WakelockPlus.enable();
  }

  @override
  void dispose() {
    WakelockPlus.disable();
    _pages.dispose();
    super.dispose();
  }

  Dhikr get _current => widget.category.adhkar[_index];

  /// One tap.
  ///
  /// The haptic is not decoration: with the phone at your side and your eyes
  /// closed, it is the only confirmation that the tap registered.
  Future<void> _tap() async {
    if (_finished) return;

    final settings = SettingsScope.read(context);
    final state = AppStateScope.read(context);

    final next = _count + 1;

    if (next < _current.repeatCount) {
      if (settings.haptics) HapticFeedback.selectionClick();

      setState(() => _count = next);
      await state.progress.saveSessionState(widget.category.id, _index, next);
      return;
    }

    // The count is complete. A heavier haptic marks it, which is what lets
    // somebody keep their eyes shut through a chapter.
    if (settings.haptics) HapticFeedback.mediumImpact();

    if (_index + 1 >= widget.category.adhkar.length) {
      setState(() => _finished = true);

      await state.progress.markCompleted(widget.category.id, widget.category.totalRepeats);
      await state.progress.clearSessionState(widget.category.id);
      return;
    }

    setState(() {
      _index++;
      _count = 0;
    });

    await state.progress.saveSessionState(widget.category.id, _index, 0);

    await _pages.animateToPage(
      _index,
      duration: const Duration(milliseconds: 260),
      curve: Curves.easeOutCubic,
    );
  }

  /// Manual navigation, for a reader who wants to go back over one.
  void _jumpTo(int index) {
    setState(() {
      _index = index;
      _count = 0;
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    if (_finished) return _CompletedView(category: widget.category);

    final digits = settings.arabicNumerals;

    return Scaffold(
      backgroundColor: tokens.paper,
      appBar: AppBar(
        title: Text(widget.category.name),
        actions: [
          IconButton(
            tooltip: context.tr('dhikr.source'),
            onPressed: () => DhikrSheet.show(context, _current),
            icon: const Icon(Icons.info_outline, size: 20),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            // Progress across the whole chapter, not the current dhikr: the
            // reader wants to know how far through they are, not how far
            // through the one in front of them.
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
              child: Row(
                children: [
                  Expanded(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(3),
                      child: LinearProgressIndicator(
                        value: (_index + 1) / widget.category.adhkar.length,
                        minHeight: 4,
                        backgroundColor: tokens.border,
                        color: tokens.brand,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Text(
                    context.tr('session.progress', {
                      'current': Numerals.format(_index + 1, arabicIndic: digits),
                      'total': Numerals.format(
                        widget.category.adhkar.length,
                        arabicIndic: digits,
                      ),
                    }),
                    style: AthkarType.sans(size: 11.5, color: tokens.muted),
                  ),
                ],
              ),
            ),

            // The tap target: everything below the progress bar.
            Expanded(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: _tap,
                child: PageView.builder(
                  controller: _pages,
                  itemCount: widget.category.adhkar.length,
                  onPageChanged: _jumpTo,
                  itemBuilder: (context, index) => _DhikrPage(
                    dhikr: widget.category.adhkar[index],
                    count: index == _index ? _count : 0,
                  ),
                ),
              ),
            ),

            Padding(
              padding: EdgeInsets.fromLTRB(
                AthkarSpacing.page, 8, AthkarSpacing.page,
                16 + MediaQuery.of(context).padding.bottom,
              ),
              child: Text(
                context.tr('session.tapAnywhere'),
                textAlign: TextAlign.center,
                style: AthkarType.sans(size: 11.5, color: tokens.faint),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One dhikr, set large, with the remaining count under it.
class _DhikrPage extends StatelessWidget {
  const _DhikrPage({required this.dhikr, required this.count});

  final Dhikr dhikr;
  final int count;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final remaining = dhikr.repeatCount - count;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 30),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Flexible(
            child: SingleChildScrollView(
              child: Column(
                children: [
                  Text(
                    dhikr.arabicText,
                    textAlign: TextAlign.center,
                    style: AthkarType.amiri(
                      // Larger than anywhere else in the app: this is the one
                      // screen where the text is the entire interface.
                      size: 26 * settings.fontScale,
                      color: tokens.ink,
                      height: 2.1,
                    ),
                  ),
                  if (settings.showTranslation && dhikr.translation != null) ...[
                    const SizedBox(height: 18),
                    Text(
                      dhikr.translation!,
                      textAlign: TextAlign.center,
                      style: AthkarType.sans(size: 13.5, color: tokens.muted, height: 1.8),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 28),
          Text(
            Numerals.format(remaining, arabicIndic: settings.arabicNumerals),
            style: AthkarType.sans(
              size: 64,
              color: tokens.brand,
              weight: FontWeight.w300,
              height: 1,
            ),
          ),
          if (dhikr.repeatCount > 1) ...[
            const SizedBox(height: 6),
            Text(
              context.tr('session.remaining', {
                'count': Numerals.format(remaining, arabicIndic: settings.arabicNumerals),
              }),
              style: AthkarType.sans(size: 12, color: tokens.muted),
            ),
          ],
        ],
      ),
    );
  }
}

/// The end of a chapter.
///
/// It says «تقبّل الله منك» and offers to start again. No score, no streak
/// fanfare, nothing that turns remembrance into a game — see the note on
/// [ProgressStore] about why the streak is never shown as a thing to lose.
class _CompletedView extends StatelessWidget {
  const _CompletedView({required this.category});

  final AthkarCategory category;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Scaffold(
      backgroundColor: tokens.paper,
      appBar: AppBar(title: Text(category.name)),
      body: AthkarEmptyState(
        title: context.tr('session.completed'),
        body: context.tr('session.completedBody'),
        icon: Icons.check_circle_outline,
        action: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            OutlinedButton(
              onPressed: () => Navigator.of(context).pushReplacement(
                MaterialPageRoute(builder: (_) => SessionScreen(category: category)),
              ),
              child: Text(context.tr('session.restart')),
            ),
            const SizedBox(width: 10),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(),
              child: Text(context.tr('session.done')),
            ),
          ],
        ),
      ),
    );
  }
}
