import 'package:flutter/material.dart';

import '../core/bootstrap.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../services/services.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';

/// What a reader writes to the project.
///
/// Mirrors `Shareds/Enums/FeedbackKind.cs`. [correction] is the one that
/// matters: a reader telling us a grading is wrong is the most valuable message
/// this project receives, and the desk sorts it to the top.
enum FeedbackKind {
  suggestion(1),
  complaint(2),
  correction(3),
  praise(4);

  const FeedbackKind(this.value);
  final int value;
}

class FeedbackScreen extends StatefulWidget {
  const FeedbackScreen({super.key, this.dhikrId, this.initialKind = FeedbackKind.suggestion});

  /// The dhikr this is about, when the reader came from one. A correction that
  /// arrives attached to its row is worth several that arrive as prose.
  final int? dhikrId;

  final FeedbackKind initialKind;

  @override
  State<FeedbackScreen> createState() => _FeedbackScreenState();
}

class _FeedbackScreenState extends State<FeedbackScreen> {
  final _message = TextEditingController();
  final _email = TextEditingController();

  late var _kind = widget.initialKind;
  var _sending = false;

  @override
  void dispose() {
    _message.dispose();
    _email.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final digits = SettingsScope.of(context).arabicNumerals;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('feedback.title'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 16, AthkarSpacing.page, 30),
        children: [
          Text(
            context.tr('feedback.kind'),
            style: AthkarType.sans(size: 12, color: tokens.muted),
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final kind in FeedbackKind.values)
                AthkarChip(
                  label: context.tr('feedback.kind.${kind.name}'),
                  selected: _kind == kind,
                  onTap: () => setState(() => _kind = kind),
                ),
            ],
          ),

          const SizedBox(height: 20),
          TextField(
            controller: _message,
            maxLines: 7,
            maxLength: 4000,
            // Flutter's own counter is always «0/4000» in Latin digits. Every
            // other figure on the screen follows the reader's choice, and a
            // lone Latin pair under an Arabic field is exactly the sort of
            // seam the design asks us not to leave.
            buildCounter: (_, {required currentLength, required isFocused, maxLength}) => Text(
              '${Numerals.format(currentLength, arabicIndic: digits)}'
              '/${Numerals.format(maxLength ?? 0, arabicIndic: digits)}',
              style: AthkarType.sans(size: 11, color: tokens.muted),
            ),
            decoration: InputDecoration(
              labelText: context.tr('feedback.message'),
              hintText: context.tr('feedback.messageHint'),
              alignLabelWithHint: true,
            ),
          ),

          const SizedBox(height: 12),
          TextField(
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            decoration: InputDecoration(
              labelText: context.tr('feedback.email'),
              hintText: context.tr('feedback.emailHint'),
            ),
          ),
          const SizedBox(height: 6),
          // The one place a reader may volunteer contact details, and it says so.
          Text(
            context.tr('privacy.body'),
            style: AthkarType.sans(size: 11, color: tokens.faint, height: 1.6),
          ),

          const SizedBox(height: 22),
          FilledButton(
            onPressed: _sending ? null : _send,
            child: _sending
                ? AthkarSpinner.mono(tokens.onBrand)
                : Text(context.tr('feedback.send')),
          ),
        ],
      ),
    );
  }

  Future<void> _send() async {
    final message = _message.text.trim();
    if (message.length < 5) return;

    setState(() => _sending = true);

    final response = await Api.support.sendFeedback(
      kind: _kind.value,
      message: message,
      dhikrId: widget.dhikrId,
      contactEmail: _email.text.trim().isEmpty ? null : _email.text.trim(),
      appVersion: AppState.appVersion,
    );

    if (!mounted) return;
    setState(() => _sending = false);

    if (response.success) {
      AthkarAlerts.toast(context, context.tr('feedback.sent'));
      Navigator.of(context).pop();
    } else {
      AthkarAlerts.error(context, response.errorMessage ?? context.tr('error.generic'));
    }
  }
}
