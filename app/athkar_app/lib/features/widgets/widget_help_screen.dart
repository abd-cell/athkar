import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../../widgets/athkar_ui.dart';

/// «كيف أضيف الويدجت؟»
///
/// Platform-specific because the gesture genuinely differs, and a screen that
/// hedged — "press and hold somewhere, then look for widgets" — would be
/// useless to the reader who is standing there holding the phone. The Android
/// and iOS paths are printed separately, and the reader's own platform first.
///
/// The lock-screen section carries a **caveat rather than a promise**, and that
/// is the whole reason it was rewritten. Android had no third-party lock-screen
/// widgets at all between Lollipop and Android 16, and the panel that brought
/// them back is rolling out by device; iOS has had them since iOS 16 but this
/// app ships no WidgetKit extension yet. Telling every reader to press and hold
/// their lock screen sends most of them looking for a list that is not there,
/// and what they conclude is that the app is broken.
///
/// The last section is the one this app has to say and most do not: a widget
/// that has stopped updating is almost always a battery optimiser, not a bug,
/// and the reader can fix it in ninety seconds if somebody tells them where to
/// look.
class WidgetHelpScreen extends StatelessWidget {
  const WidgetHelpScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final isAndroidFirst = kIsWeb || Platform.isAndroid;

    final sections = <_Section>[
      if (isAndroidFirst) _android(context) else _ios(context),
      if (isAndroidFirst) _ios(context) else _android(context),
      _lock(context, androidFirst: isAndroidFirst),
      _stale(context),
    ];

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.help.title'))),
      body: ListView(
        padding:
            const EdgeInsets.fromLTRB(AthkarSpacing.page, 16, AthkarSpacing.page, 34),
        children: [
          for (final section in sections) ...[
            AthkarSectionHeader(title: section.title),
            const SizedBox(height: 10),
            _Steps(steps: section.steps),
            if (section.note case final note?) ...[
              const SizedBox(height: 8),
              _Note(text: note),
            ],
            const SizedBox(height: 22),
          ],
        ],
      ),
    );
  }

  _Section _android(BuildContext context) => _Section(
        title: context.tr('widgets.help.android'),
        steps: [
          context.tr('widgets.help.android.1'),
          context.tr('widgets.help.android.2'),
          context.tr('widgets.help.android.3'),
        ],
      );

  _Section _ios(BuildContext context) => _Section(
        title: context.tr('widgets.help.ios'),
        steps: [
          context.tr('widgets.help.ios.1'),
          context.tr('widgets.help.ios.2'),
          context.tr('widgets.help.ios.3'),
        ],
      );

  /// The lock screen, told the way it actually is on the reader's platform.
  _Section _lock(BuildContext context, {required bool androidFirst}) => _Section(
        title: context.tr('widgets.help.lock'),
        steps: androidFirst
            ? [
                context.tr('widgets.help.lock.android.1'),
                context.tr('widgets.help.lock.android.2'),
              ]
            : [
                context.tr('widgets.help.lock.ios.1'),
                context.tr('widgets.help.lock.ios.2'),
              ],
        note: context.tr(
          androidFirst ? 'widgets.help.lock.androidNote' : 'widgets.help.lock.iosNote',
        ),
      );

  _Section _stale(BuildContext context) => _Section(
        title: context.tr('widgets.help.stale'),
        steps: [
          context.tr('widgets.help.stale.1'),
          context.tr('widgets.help.stale.2'),
        ],
      );
}

class _Section {
  const _Section({required this.title, required this.steps, this.note});

  final String title;
  final List<String> steps;

  /// The sentence that keeps a section from over-promising. Only the lock
  /// screen needs one today.
  final String? note;
}

/// A caveat under a set of steps, in the quiet weight — it is a qualification,
/// not a warning, and styling it as an alarm would be its own kind of lie.
class _Note extends StatelessWidget {
  const _Note({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Text(
        text,
        style: AthkarType.sans(size: 11.5, color: tokens.faint, height: 1.9),
      ),
    );
  }
}

class _Steps extends StatelessWidget {
  const _Steps({required this.steps});

  final List<String> steps;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return AthkarCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (var index = 0; index < steps.length; index++) ...[
            if (index > 0) const AthkarRule(),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 22,
                  height: 22,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: tokens.brandTint,
                    shape: BoxShape.circle,
                  ),
                  child: Text(
                    '${index + 1}',
                    style: AthkarType.sans(
                      size: 11,
                      color: tokens.brandInk,
                      weight: FontWeight.w600,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    steps[index],
                    style: AthkarType.sans(size: 12.5, color: tokens.ink, height: 1.9),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
