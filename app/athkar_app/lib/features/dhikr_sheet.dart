import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../core/arabic_text.dart';
import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';
import 'feedback_screen.dart';
import 'home_screen.dart';
import 'share_sheet.dart';

/// The takhrij sheet: where a dhikr came from, what it means, and what is
/// established about its virtue.
///
/// A bottom sheet rather than a screen because it is a footnote — the reader is
/// in the middle of something and wants to check a reference, not navigate. It
/// is also the one place the project's central claim is spelled out in full:
/// book, number, grading, and who issued it.
class DhikrSheet {
  const DhikrSheet._();

  static Future<void> show(BuildContext context, Dhikr dhikr) => showModalBottomSheet(
        context: context,
        isScrollControlled: true,
        builder: (context) => _DhikrSheetBody(dhikr: dhikr),
      );
}

class _DhikrSheetBody extends StatelessWidget {
  const _DhikrSheetBody({required this.dhikr});

  final Dhikr dhikr;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.62,
      maxChildSize: 0.92,
      builder: (context, controller) => ListView(
        controller: controller,
        padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 14, AthkarSpacing.page, 34),
        children: [
          Center(
            child: Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(
                color: tokens.border,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),
          const SizedBox(height: 20),

          Text(
            settings.showTashkeel
                ? dhikr.arabicText
                : ArabicText.stripDiacritics(dhikr.arabicText),
            style: AthkarType.amiri(
              size: 23 * settings.fontScale,
              color: tokens.ink,
              height: 2.05,
            ),
          ),

          if (dhikr.translation != null) ...[
            const AthkarRule(margin: EdgeInsets.symmetric(vertical: 18)),
            _Field(label: context.tr('dhikr.translation'), value: dhikr.translation!),
          ],

          if (dhikr.transliteration != null) ...[
            const SizedBox(height: 14),
            _Field(label: context.tr('dhikr.transliteration'), value: dhikr.transliteration!),
          ],

          // Shown only when a sound narration states it. An app that invents
          // encouragement for a dhikr has stopped being trustworthy about the
          // dhikr itself.
          if (dhikr.virtue != null) ...[
            const SizedBox(height: 14),
            _Field(label: context.tr('dhikr.virtue'), value: dhikr.virtue!),
          ],

          if (dhikr.hasSource) ...[
            const AthkarRule(margin: EdgeInsets.symmetric(vertical: 18)),
            _Field(
              label: context.tr('dhikr.source'),
              value: '${dhikr.sourceBook} · ${dhikr.sourceReference}'
                  '${dhikr.grade == null ? '' : ' · ${gradeLabel(context, dhikr.grade)}'}',
            ),
            if (dhikr.gradedBy case final grader?) ...[
              const SizedBox(height: 6),
              Text(
                context.tr('dhikr.gradedBy', {'name': grader}),
                style: AthkarType.sans(size: 11.5, color: tokens.faint),
              ),
            ],
          ],

          const SizedBox(height: 22),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () async {
                    await Clipboard.setData(
                        ClipboardData(text: _subject(context, dhikr).plainText));
                    if (context.mounted) {
                      AthkarAlerts.toast(context, context.tr('dhikr.copied'));
                    }
                  },
                  icon: const Icon(Icons.copy_outlined, size: 16),
                  label: Text(context.tr('dhikr.copy')),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => ShareSheet.show(context, _subject(context, dhikr)),
                  icon: const Icon(Icons.ios_share, size: 16),
                  label: Text(context.tr('dhikr.share')),
                ),
              ),
              const SizedBox(width: 8),
              IconButton(
                onPressed: () => settings.toggleFavourite(dhikr.id),
                icon: Icon(
                  settings.isFavourite(dhikr.id) ? Icons.bookmark : Icons.bookmark_outline,
                  size: 20,
                  color: settings.isFavourite(dhikr.id) ? tokens.brand : tokens.muted,
                ),
                tooltip: context.tr('dhikr.favourite'),
              ),
            ],
          ),

          const SizedBox(height: 10),
          // The correction path, one tap from the text it is about. A reader
          // who spots a wrong number is the most valuable correspondent this
          // project has, and making them go and find a form loses most of them.
          Center(
            child: TextButton(
              onPressed: () {
                Navigator.of(context).pop();
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => FeedbackScreen(
                      dhikrId: dhikr.id,
                      initialKind: FeedbackKind.correction,
                    ),
                  ),
                );
              },
              child: Text(context.tr('dhikr.reportIssue')),
            ),
          ),
        ],
      ),
    );
  }

  /// What the copy button and the share sheet both work from.
  ///
  /// One definition, because the copied text and the shared text are the same
  /// text: the dhikr and its reference, and nothing else — no app name, no
  /// link, no invitation to download. A shared dhikr should be shareable. The
  /// wordmark the *image* carries is a separate decision, taken in
  /// `share_sheet.dart`.
  static ShareSubject _subject(BuildContext context, Dhikr dhikr) =>
      ShareSubject.dhikr(
        dhikr,
        grade: dhikr.grade == null ? null : gradeLabel(context, dhikr.grade),
      );
}

class _Field extends StatelessWidget {
  const _Field({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: AthkarType.sans(size: 11.5, color: tokens.brand, weight: FontWeight.w500),
        ),
        const SizedBox(height: 6),
        Text(value, style: AthkarType.sans(size: 13.5, color: tokens.ink, height: 1.8)),
      ],
    );
  }
}
