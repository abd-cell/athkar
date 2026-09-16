import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'widget_designs.dart';
import 'widget_skin.dart';

/// «ويدجت اختياري» — the reader pins their own text.
///
/// This is the one place in the app where words reach a screen carrying no
/// takhrij, and the whole design of the screen is about keeping that honest.
/// The text is the reader's own, typed on their own phone; it is never
/// uploaded, never shown to anyone else, and the widget prints the attribution
/// line under it as *their* note rather than letting the words sit where a
/// source would. Nothing here can be mistaken for catalogue content, because
/// nothing here ever leaves this device.
///
/// The admin can withdraw the whole feature — see
/// `WidgetSettings.allowCustomWidget` — and sets how long the text may be. That
/// length is a legibility limit before it is a storage one: a widget is a few
/// square centimetres, and an ayah that does not fit is an ayah cut in half.
class CustomWidgetScreen extends StatefulWidget {
  const CustomWidgetScreen({super.key});

  @override
  State<CustomWidgetScreen> createState() => _CustomWidgetScreenState();
}

class _CustomWidgetScreenState extends State<CustomWidgetScreen> {
  late final TextEditingController _text;
  late final TextEditingController _attribution;

  @override
  void initState() {
    super.initState();

    final settings = SettingsScope.read(context);
    _text = TextEditingController(text: settings.widgetCustomText);
    _attribution = TextEditingController(text: settings.widgetCustomAttribution);
  }

  @override
  void dispose() {
    _text.dispose();
    _attribution.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final rules = AppStateScope.of(context).widgetSettings;

    if (!rules.allowCustomWidget) {
      return Scaffold(
        appBar: AppBar(title: Text(context.tr('widgets.custom.title'))),
        body: AthkarEmptyState(
          title: context.tr('widgets.custom.disabled'),
          body: context.tr('widget.disabledHint'),
          icon: Icons.push_pin_outlined,
        ),
      );
    }

    final skin = WidgetSkin.of(context, rules: rules);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.custom.title'))),
      body: ListView(
        padding:
            const EdgeInsets.fromLTRB(AthkarSpacing.page, 16, AthkarSpacing.page, 32),
        children: [
          Text(
            context.tr('widgets.custom.intro'),
            style: AthkarType.sans(size: 12.5, color: tokens.muted, height: 1.9),
          ),
          const SizedBox(height: 18),

          // Live, off the controller rather than off what is saved: the reader
          // is composing for a shape, and a preview that only updates when they
          // press save makes them press save to find out.
          WidgetPanel(
            skin: skin,
            height: WidgetDesigns.heightFor('custom_pinned'),
            child: _Preview(
              text: _text.text,
              attribution: _attribution.text,
              skin: skin,
            ),
          ),
          const SizedBox(height: 22),

          TextField(
            controller: _text,
            maxLines: 5,
            maxLength: rules.customWidgetMaxLength,
            textAlignVertical: TextAlignVertical.top,
            onChanged: (_) => setState(() {}),
            style: AthkarType.amiri(size: 18, color: tokens.ink, height: 1.9),
            decoration: InputDecoration(
              hintText: context.tr('widgets.custom.placeholder'),
            ),
          ),
          const SizedBox(height: 6),

          TextField(
            controller: _attribution,
            maxLength: 60,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              hintText: context.tr('widgets.custom.attributionHint'),
            ),
          ),
          const SizedBox(height: 10),

          FilledButton(
            onPressed: _save,
            child: Text(context.tr('common.save')),
          ),
          const SizedBox(height: 10),

          Text(
            context.tr('widgets.custom.privacy'),
            style: AthkarType.sans(size: 11.5, color: tokens.faint, height: 1.8),
          ),
        ],
      ),
    );
  }

  Future<void> _save() async {
    final settings = SettingsScope.read(context);
    final state = AppStateScope.read(context);

    await settings.setWidgetCustom(_text.text, _attribution.text);

    // The launcher holds a copy of whatever was last handed to it, so a save
    // that did not push would leave the old words on the home screen.
    await state.pushWidget();

    if (mounted) AthkarAlerts.toast(context, context.tr('common.saved'));
  }
}

class _Preview extends StatelessWidget {
  const _Preview({
    required this.text,
    required this.attribution,
    required this.skin,
  });

  final String text;
  final String attribution;
  final WidgetSkin skin;

  @override
  Widget build(BuildContext context) {
    if (text.trim().isEmpty) {
      return Center(
        child: Text(
          context.tr('widgets.custom.empty'),
          textAlign: TextAlign.center,
          style: AthkarType.sans(size: 11.5, color: skin.muted),
        ),
      );
    }

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Flexible(
          child: Text(
            text,
            textAlign: TextAlign.center,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
            style: AthkarType.amiri(size: 19, color: skin.ink, height: 1.7),
          ),
        ),
        if (attribution.trim().isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(
            attribution,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AthkarType.sans(size: 10, color: skin.muted),
          ),
        ],
      ],
    );
  }
}
