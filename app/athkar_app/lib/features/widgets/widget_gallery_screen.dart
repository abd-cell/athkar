import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'widget_designs.dart';
import 'widget_preview_data.dart';
import 'widget_skin.dart';

/// «الويدجت المتوفرة» — the gallery.
///
/// Every preview on this screen is drawn from the reader's *own* data: their
/// city, their times, their calendar correction, a dhikr out of the catalogue
/// on their phone. That is the point of the screen and not a detail. A gallery
/// of mock widgets showing 5:00 for Fajr in a city nobody lives in is a
/// catalogue of pictures; this one shows the thing itself, so what the reader
/// is choosing between is what they will get.
///
/// Which entries appear is the admin's decision, arriving as the catalogue.
/// Which *designs* an entry offers is the smaller of the admin's cap and what
/// this build can draw, so a server ahead of the app never advertises a design
/// that would render blank, and a server behind it can withdraw one without an
/// app update.
class WidgetGalleryScreen extends StatefulWidget {
  const WidgetGalleryScreen({super.key});

  @override
  State<WidgetGalleryScreen> createState() => _WidgetGalleryScreenState();
}

class _WidgetGalleryScreenState extends State<WidgetGalleryScreen> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();

    // The previews carry live countdowns, so they tick like the home screen's
    // strip does. One timer for the whole list rather than one per preview.
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = AppStateScope.of(context);
    final catalog = state.widgetCatalog;

    // Unknown keys are dropped here rather than drawn as empty boxes: the
    // console may well be a release ahead of this build.
    final home = catalog
        .forSurface(WidgetSurface.home)
        .where((item) => WidgetDesigns.knows(item.key))
        .toList();

    final lock = catalog
        .forSurface(WidgetSurface.lock)
        .where((item) => WidgetDesigns.knows(item.key))
        .toList();

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('widgets.gallery.title'))),
      body: home.isEmpty && lock.isEmpty
          ? AthkarEmptyState(
              title: context.tr('widgets.gallery.empty'),
              body: context.tr('widgets.gallery.emptyHint'),
              icon: Icons.widgets_outlined,
            )
          : ListView(
              padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 14, AthkarSpacing.page, 34),
              children: [
                const _GalleryIntro(),
                if (home.isNotEmpty) ...[
                  const SizedBox(height: 22),
                  _SurfaceHeader(
                    icon: Icons.smartphone_outlined,
                    title: context.tr('widget.surface.home'),
                  ),
                  const SizedBox(height: 12),
                  for (final item in home) _HomeEntry(item: item),
                ],
                if (lock.isNotEmpty) ...[
                  const SizedBox(height: 14),
                  _SurfaceHeader(
                    icon: Icons.lock_outline,
                    title: context.tr('widget.surface.lock'),
                  ),
                  const SizedBox(height: 12),
                  _LockGrid(items: lock),
                  const SizedBox(height: 12),
                  Text(
                    _lockHowTo(context),
                    textAlign: TextAlign.center,
                    style: AthkarType.sans(
                      size: 11.5,
                      color: AthkarTokens.of(context).faint,
                      height: 1.8,
                    ),
                  ),
                ],
              ],
            ),
    );
  }
}

/// What to say under the lock-screen list, on this platform.
///
/// Android and iOS are not two spellings of the same sentence here. On iOS a
/// lock-screen widget is its own thing the reader picks from a list; on Android
/// there is no such list on most phones, and where there is one it shows the
/// *same* widget as the home screen rather than any of these individually.
/// Printing the iOS instruction to an Android reader sends them hunting for
/// something that is not there, and what they conclude is that the app is
/// broken.
String _lockHowTo(BuildContext context) => context.tr(
      kIsWeb || Platform.isAndroid
          ? 'widgets.gallery.lockHowTo.android'
          : 'widgets.gallery.lockHowTo.ios',
    );

/// The card at the top, which does the one job the gallery cannot do for
/// itself: say that these are previews, not buttons.
class _GalleryIntro extends StatelessWidget {
  const _GalleryIntro();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return AthkarBrandCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            context.tr('widgets.gallery.headline'),
            style: AthkarType.amiri(size: 19, color: tokens.brandInk, weight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(
            context.tr('widgets.gallery.body'),
            style: AthkarType.sans(size: 12.5, color: tokens.brandInk, height: 1.9),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Icon(Icons.add_circle_outline, size: 16, color: tokens.brandInk),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  context.tr('widgets.gallery.addHint'),
                  style: AthkarType.sans(size: 11.5, color: tokens.brandInk, height: 1.7),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _SurfaceHeader extends StatelessWidget {
  const _SurfaceHeader({required this.icon, required this.title});

  final IconData icon;
  final String title;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Row(
      children: [
        Icon(icon, size: 18, color: tokens.muted),
        const SizedBox(width: 8),
        Text(
          title,
          style: AthkarType.amiri(size: 17, color: tokens.ink, weight: FontWeight.w700),
        ),
      ],
    );
  }
}

/// One home-screen entry: the live preview, its name, and the two things a
/// reader can do with it.
///
/// Tapping the preview cycles the design rather than opening a picker. There
/// are up to four of them, they differ only in arrangement, and a reader
/// deciding between arrangements wants to *see* the next one, not read a list
/// of names for things they have not seen.
///
/// The second action — «ضعها على شاشتي» — is the one the gallery is for. Until
/// it existed a reader could browse thirty widgets, pick one, and find
/// something else entirely on their home screen: the choice never left this
/// screen. Pressing it records the entry and pushes it to the launcher at once,
/// so a widget already placed changes under their hand rather than at some
/// later sync.
class _HomeEntry extends StatelessWidget {
  const _HomeEntry({required this.item});

  final WidgetCatalogItem item;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final available = item.designsAvailable(WidgetDesigns.designsFor(item.key));

    // Clamped rather than trusted: the stored choice may be a design the admin
    // has since withdrawn, or one an older build offered and this one does not.
    final design = settings.widgetDesign(item.key, fallback: item.defaultDesign) % available;

    final state = AppStateScope.of(context);
    final data = WidgetPreviewData.of(context);
    final skin = WidgetSkin.of(context, rules: state.widgetSettings);

    final isPlaced = state.selectedWidget?.key == item.key;

    return Padding(
      padding: const EdgeInsets.only(bottom: 22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          GestureDetector(
            onTap: available > 1
                ? () => settings.setWidgetDesign(item.key, (design + 1) % available)
                : null,
            child: WidgetPanel(
              skin: skin,
              height: WidgetDesigns.heightFor(item.key),
              child: WidgetDesigns.build(context, item.key, design, data, skin) ??
                  const SizedBox.shrink(),
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: Text(
                  item.title,
                  style:
                      AthkarType.sans(size: 13, color: tokens.ink, weight: FontWeight.w600),
                ),
              ),
              if (isPlaced)
                _Badge(label: context.tr('widgets.onYourScreen'), tone: _Tone.exclusive),
              if (item.isNew) _Badge(label: context.tr('widgets.badge.new'), tone: _Tone.fresh),
              if (item.isExclusive)
                _Badge(label: context.tr('widgets.badge.exclusive'), tone: _Tone.exclusive),
            ],
          ),
          const SizedBox(height: 3),
          Row(
            children: [
              if (available > 1) ...[
                Text(
                  context.tr('widgets.designCounter', {
                    'index': '${design + 1}',
                    'total': '$available',
                  }),
                  style: AthkarType.sans(size: 11, color: tokens.muted),
                ),
                const SizedBox(width: 8),
                Text(
                  context.tr('widgets.tapToChange'),
                  style: AthkarType.sans(size: 11, color: tokens.faint),
                ),
              ] else if (item.subtitle case final subtitle?)
                Expanded(
                  child: Text(
                    subtitle,
                    style: AthkarType.sans(size: 11, color: tokens.muted),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 10),
          // Offered even when this entry is already the chosen one, as a
          // "refresh": a reader whose launcher is showing yesterday needs a way
          // to push again, and this is the button they will reach for.
          OutlinedButton.icon(
            onPressed: () => _place(context, available, design),
            icon: Icon(
              isPlaced ? Icons.refresh : Icons.add_to_home_screen_outlined,
              size: 16,
            ),
            label: Text(
              context.tr(isPlaced ? 'widgets.updateOnScreen' : 'widgets.putOnScreen'),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _place(BuildContext context, int available, int design) async {
    final settings = SettingsScope.read(context);
    final state = AppStateScope.read(context);

    await settings.setSelectedWidgetKey(item.key);
    // Written back clamped, so the launcher and the gallery cannot disagree
    // about which design is current after the admin lowers the cap.
    await settings.setWidgetDesign(item.key, design % available);

    await state.pushWidget();

    if (context.mounted) {
      AthkarAlerts.toast(context, context.tr('widgets.placed'));
    }
  }
}

/// The lock-screen entries, as a grid of names.
///
/// Not previews in place: a lock widget is a glyph a centimetre across, and two
/// dozen of them stacked down a page would be a wall of illegible strips. The
/// name is what a reader picks by; the preview opens when they ask for it.
class _LockGrid extends StatelessWidget {
  const _LockGrid({required this.items});

  final List<WidgetCatalogItem> items;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final item in items)
          SizedBox(
            width: (MediaQuery.sizeOf(context).width - AthkarSpacing.page * 2 - 8) / 2,
            child: AthkarChip(
              label: item.title,
              onTap: () => _preview(context, item),
            ),
          ),
      ],
    );
  }

  void _preview(BuildContext context, WidgetCatalogItem item) {
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) {
        final data = WidgetPreviewData.of(sheetContext);
        final skin = WidgetSkin.of(sheetContext,
            rules: AppStateScope.of(sheetContext).widgetSettings);

        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(
                AthkarSpacing.page, 22, AthkarSpacing.page, 26),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  item.title,
                  textAlign: TextAlign.center,
                  style: AthkarType.amiri(
                    size: 18,
                    color: AthkarTokens.of(sheetContext).ink,
                    weight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 16),
                WidgetPanel(
                  skin: skin,
                  height: WidgetDesigns.heightFor(item.key),
                  radius: 14,
                  child: WidgetDesigns.build(sheetContext, item.key, 0, data, skin) ??
                      const SizedBox.shrink(),
                ),
                const SizedBox(height: 16),
                Text(
                  _lockHowTo(sheetContext),
                  textAlign: TextAlign.center,
                  style: AthkarType.sans(
                    size: 11.5,
                    color: AthkarTokens.of(sheetContext).muted,
                    height: 1.8,
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

enum _Tone { fresh, exclusive }

class _Badge extends StatelessWidget {
  const _Badge({required this.label, required this.tone});

  final String label;
  final _Tone tone;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    // The exclusive badge borrows the brand; the new badge is quieter, because
    // «جديد» expires and «حصرية» does not.
    final (background, foreground) = switch (tone) {
      _Tone.exclusive => (tokens.brand, tokens.onBrand),
      _Tone.fresh => (tokens.brandTint, tokens.brandInk),
    };

    return Container(
      margin: const EdgeInsetsDirectional.only(start: 6),
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 3),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AthkarType.sans(size: 10, color: foreground, weight: FontWeight.w600),
      ),
    );
  }
}
