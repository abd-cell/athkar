import 'package:flutter/material.dart';

import '../core/arabic_text.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import 'session_screen.dart';

/// The same chapter as one continuous passage, for somebody who would rather
/// read than tap.
///
/// Its own screen rather than a toggle on the session, because the two are
/// different postures: the session is counted and interrupts itself, this one
/// is uninterrupted and never counts. The design gives it a slightly darker
/// ground, a centred ornament between passages, and a bar of the three controls
/// that matter while reading.
class ReadingScreen extends StatefulWidget {
  const ReadingScreen({super.key, required this.category});

  final AthkarCategory category;

  @override
  State<ReadingScreen> createState() => _ReadingScreenState();
}

class _ReadingScreenState extends State<ReadingScreen> {
  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return Scaffold(
      backgroundColor: tokens.readingPaper,
      appBar: AppBar(
        backgroundColor: tokens.readingPaper,
        title: Text(widget.category.name),
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(30, 4, 30, 0),
              child: Column(
                children: [
                  Text(
                    '${widget.category.name} · ${context.tr('reading.continuous')}',
                    style: AthkarType.sans(
                      size: 11.5,
                      color: tokens.muted,
                      letterSpacing: 0.08,
                    ),
                  ),
                  const SizedBox(height: 14),
                  const AthkarOrnament(),
                ],
              ),
            ),
            Expanded(
              child: ListView.separated(
                padding: const EdgeInsets.fromLTRB(30, 22, 30, 30),
                itemCount: widget.category.adhkar.length,
                separatorBuilder: (_, __) => const AthkarRule(
                  margin: EdgeInsets.symmetric(vertical: 22),
                ),
                itemBuilder: (context, index) {
                  final dhikr = widget.category.adhkar[index];

                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        settings.showTashkeel
                            ? dhikr.arabicText
                            : ArabicText.stripDiacritics(dhikr.arabicText),
                        // Justified, which is how the design sets it and how a
                        // page of Arabic has always been set.
                        textAlign: TextAlign.justify,
                        style: AthkarType.amiri(
                          size: 22 * settings.fontScale,
                          color: tokens.ink,
                          height: 2.3,
                        ),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        dhikr.repeatCount == 1
                            ? context.tr('reading.once')
                            : context.tr('reading.times', {
                                'count': Numerals.format(
                                  dhikr.repeatCount,
                                  arabicIndic: settings.arabicNumerals,
                                ),
                              }),
                        style: AthkarType.sans(size: 11.5, color: tokens.faint),
                      ),
                      if (settings.showTranslation && dhikr.translation != null) ...[
                        const SizedBox(height: 10),
                        Text(
                          dhikr.translation!,
                          style: AthkarType.sans(size: 13, color: tokens.muted, height: 1.8),
                        ),
                      ],
                    ],
                  );
                },
              ),
            ),
            _ReadingBar(category: widget.category),
          ],
        ),
      ),
    );
  }
}

/// Text size, diacritics, theme, and the way back to counting — the four things
/// a reader reaches for mid-passage, and nothing else.
class _ReadingBar extends StatelessWidget {
  const _ReadingBar({required this.category});

  final AthkarCategory category;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return Container(
      decoration: BoxDecoration(
        color: tokens.scrimTop,
        border: Border(top: BorderSide(color: tokens.hairline)),
      ),
      padding: EdgeInsets.fromLTRB(
        AthkarSpacing.page, 12, AthkarSpacing.page,
        16 + MediaQuery.of(context).padding.bottom,
      ),
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton(
              onPressed: () => _cycleFontSize(context, settings),
              child: Text(context.tr('reading.fontSize')),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton(
              onPressed: () => settings.setShowTashkeel(!settings.showTashkeel),
              style: settings.showTashkeel
                  ? OutlinedButton.styleFrom(backgroundColor: tokens.brandTint)
                  : null,
              child: Text(context.tr('reading.tashkeel')),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton(
              onPressed: () => settings.setThemeMode(
                Theme.of(context).brightness == Brightness.dark
                    ? ThemeMode.light
                    : ThemeMode.dark,
              ),
              child: Text(
                Theme.of(context).brightness == Brightness.dark
                    ? context.tr('reading.light')
                    : context.tr('reading.dark'),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: FilledButton(
              onPressed: () => Navigator.of(context).pushReplacement(
                MaterialPageRoute(builder: (_) => SessionScreen(category: category)),
              ),
              child: Text(context.tr('reading.count')),
            ),
          ),
        ],
      ),
    );
  }

  /// Steps through the sizes rather than opening a slider: mid-passage, one tap
  /// that visibly changes the text is worth more than a precise control.
  void _cycleFontSize(BuildContext context, Settings settings) {
    const steps = [1.0, 1.15, 1.3, 1.5, 0.9];
    final current = steps.indexWhere((step) => (step - settings.fontScale).abs() < 0.01);
    settings.setFontScale(steps[(current + 1) % steps.length]);
  }
}
