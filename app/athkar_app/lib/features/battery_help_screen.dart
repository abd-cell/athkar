import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import '../widgets/athkar_ui.dart';

/// «لماذا لا تصلني التنبيهات؟»
///
/// The single most asked question an app with scheduled reminders receives, and
/// almost never the app's fault: Chinese-market Android skins in particular kill
/// background apps aggressively, and an exact alarm from a "stopped" app never
/// fires.
///
/// Written as per-manufacturer steps rather than a generic apology, because the
/// menus differ enough that generic advice does not get anybody to the right
/// screen. The app cannot fix this itself — only the reader can, once.
class BatteryHelpScreen extends StatelessWidget {
  const BatteryHelpScreen({super.key});

  static const _steps = [
    'battery.step.xiaomi',
    'battery.step.oppo',
    'battery.step.huawei',
    'battery.step.samsung',
    'battery.step.generic',
  ];

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('battery.title'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 16, AthkarSpacing.page, 30),
        children: [
          Text(
            context.tr('battery.intro'),
            style: AthkarType.sans(size: 13, color: tokens.muted, height: 1.8),
          ),
          const SizedBox(height: 18),
          for (final key in _steps) ...[
            AthkarCard(
              radius: AthkarSpacing.smallCardRadius,
              child: Text(
                context.tr(key),
                style: AthkarType.sans(size: 13, color: tokens.ink, height: 1.8),
              ),
            ),
            const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }
}
