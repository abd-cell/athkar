import 'package:flutter/material.dart';

import '../core/arabic_text.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../models/models.dart';
import 'athkar_ui.dart';

/// One dhikr as a card: the text, the translation when asked for, the takhrij
/// line and the repeat count.
///
/// Shared rather than copied, because the chapter list and the favourites list
/// show the same thing and a second copy would drift the moment the takhrij
/// line changes — and that line is the project's central claim.
class DhikrCard extends StatelessWidget {
  const DhikrCard({super.key, required this.dhikr, this.onTap});

  final Dhikr dhikr;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return AthkarCard(
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            settings.showTashkeel
                ? dhikr.arabicText
                : ArabicText.stripDiacritics(dhikr.arabicText),
            style: AthkarType.amiri(
              size: 21 * settings.fontScale,
              color: tokens.ink,
              height: 1.95,
            ),
          ),
          if (settings.showTranslation && dhikr.translation != null) ...[
            const SizedBox(height: 10),
            Text(
              dhikr.translation!,
              style: AthkarType.sans(size: 13, color: tokens.muted, height: 1.7),
            ),
          ],
          const AthkarRule(margin: EdgeInsets.only(top: 14, bottom: 12)),
          Row(
            children: [
              Expanded(
                child: Text(
                  dhikr.hasSource ? '${dhikr.sourceBook} ${dhikr.sourceReference}' : '',
                  style: AthkarType.sans(size: 11.5, color: tokens.muted),
                ),
              ),
              Text(
                dhikr.repeatCount == 1
                    ? context.tr('reading.once')
                    : context.tr('reading.times', {
                        'count': Numerals.format(
                          dhikr.repeatCount,
                          arabicIndic: settings.arabicNumerals,
                        ),
                      }),
                style: AthkarType.sans(size: 11.5, color: tokens.brand),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
