import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';

/// What the share sheet is asked to render.
///
/// Deliberately not a [Dhikr]: an ayah, a hadith of the day and a dhikr are all
/// «a piece of narrated text with a line saying where it came from», and the
/// sheet has no business knowing which of the three it is holding.
class ShareSubject {
  const ShareSubject({
    required this.body,
    required this.plainText,
    this.heading,
    this.attribution,
  });

  /// The heading above the text — a surah name, a chapter name. Optional: a
  /// dhikr the reader opened from a search result belongs to no chapter they
  /// are looking at, and an invented heading would be worse than none.
  final String? heading;

  /// The narrated Arabic itself. Always Amiri, always the largest thing on the
  /// card.
  final String body;

  /// The takhrij line: book, number, grading. What makes the image quotable
  /// rather than merely decorative — an image of a dhikr with no reference on
  /// it is exactly the thing this project exists to stop circulating.
  final String? attribution;

  /// Exactly what a text share sends.
  ///
  /// Kept separate from [body] + [attribution] because the two shares have
  /// different rules: the image carries a small wordmark (that is what a shared
  /// image is *for*), the text carries none.
  final String plainText;

  /// The subject for a dhikr, built from the fields the takhrij sheet shows.
  factory ShareSubject.dhikr(Dhikr dhikr, {String? heading, String? grade}) {
    final buffer = StringBuffer(dhikr.arabicText);
    if (dhikr.hasSource) {
      buffer.write('\n\n${dhikr.sourceBook} ${dhikr.sourceReference}');
    }

    return ShareSubject(
      heading: heading,
      body: dhikr.arabicText,
      attribution: dhikr.hasSource
          ? '${dhikr.sourceBook} · ${dhikr.sourceReference}'
              '${grade == null ? '' : ' · $grade'}'
          : null,
      plainText: buffer.toString(),
    );
  }
}

/// One of the card designs the reader picks between.
///
/// A design is five colours and a switch, not a widget: the card's *layout* is
/// fixed, so whichever one is chosen the text sits in the same place at the
/// same size, and a long dhikr does not reflow into a different shape depending
/// on which ground it was put on.
@immutable
class ShareDesign {
  const ShareDesign({
    required this.ground,
    required this.groundEnd,
    required this.ink,
    required this.accent,
    required this.faint,
    this.framed = false,
  });

  final Color ground;

  /// The second stop of the ground gradient. Equal to [ground] for a flat card.
  final Color groundEnd;

  final Color ink;

  /// Heading, ornament, frame and the mark.
  final Color accent;

  /// The takhrij line and the wordmark caption.
  final Color faint;

  /// Draws the inner rule border of the manuscript design.
  final bool framed;

  /// The four, in the order the carousel shows them.
  ///
  /// Parchment first because it is the app's own ground and the one a reader
  /// recognises; the solid green last because it is the loudest.
  static const all = [
    // ورق — the app's own paper.
    ShareDesign(
      ground: Color(0xFFFBF6EE),
      groundEnd: Color(0xFFF1E7D6),
      ink: Color(0xFF241D16),
      accent: AthkarColors.brand,
      faint: Color(0xFF8C8171),
    ),
    // مخطوطة — parchment inside a drawn frame.
    ShareDesign(
      ground: Color(0xFFF7F1E6),
      groundEnd: Color(0xFFF7F1E6),
      ink: Color(0xFF241D16),
      accent: AthkarColors.brand,
      faint: Color(0xFF8C8171),
      framed: true,
    ),
    // ليل — near-black, for readers whose phones are dark.
    ShareDesign(
      ground: Color(0xFF1B1814),
      groundEnd: Color(0xFF15130F),
      ink: Color(0xFFECE4D6),
      accent: AthkarColors.brandDarkInk,
      faint: Color(0xFF8C8171),
    ),
    // غصن — the brand green, solid.
    ShareDesign(
      ground: AthkarColors.brand,
      groundEnd: AthkarColors.brandInk,
      ink: Colors.white,
      accent: Color(0xFFCFE3D4),
      faint: Color(0xB3FFFFFF),
    ),
  ];
}

/// Pick a design, then pick a format.
///
/// The carousel exists because the one thing a reader wants to do with a dhikr
/// is send it to somebody, and a screenshot of the reading screen carries the
/// app's chrome, the status bar and the scroll position along with it. Four
/// designs is enough to feel chosen and few enough to swipe through without
/// having to decide.
class ShareSheet {
  const ShareSheet._();

  static Future<void> show(BuildContext context, ShareSubject subject) =>
      showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (context) => _ShareSheetBody(subject: subject),
      );
}

class _ShareSheetBody extends StatefulWidget {
  const _ShareSheetBody({required this.subject});

  final ShareSubject subject;

  @override
  State<_ShareSheetBody> createState() => _ShareSheetBodyState();
}

class _ShareSheetBodyState extends State<_ShareSheetBody> {
  /// The exported PNG's width, in pixels, on every device. See [_shareImage].
  static const _exportWidth = 1080.0;

  final _controller = PageController();

  /// One boundary per design. The capture reads the *current* page's boundary,
  /// so the image is what the reader is looking at rather than whatever the
  /// carousel happened to build first.
  final _boundaries = <int, GlobalKey>{
    for (var i = 0; i < ShareDesign.all.length; i++) i: GlobalKey(),
  };

  int _index = 0;
  bool _busy = false;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return SafeArea(
      top: false,
      // Scrollable because the sheet is a fixed stack of a square card and two
      // buttons: on a short screen — a small phone in landscape, a split view —
      // that stack is taller than the sheet, and an overflowing Column drops
      // the buttons off the bottom with no way to reach them.
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(
            AthkarSpacing.page, 14, AthkarSpacing.page, 22),
        child: Column(
          mainAxisSize: MainAxisSize.min,
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
            const SizedBox(height: 16),

            // Title optically centred with the close button pinned to the far
            // edge, so the title stays centred whatever its length.
            Stack(
              alignment: Alignment.center,
              children: [
                Text(
                  context.tr('share.title'),
                  style: AthkarType.amiri(
                      size: 17, color: tokens.ink, weight: FontWeight.w700),
                ),
                Align(
                  alignment: AlignmentDirectional.centerEnd,
                  child: _CloseButton(onTap: () => Navigator.of(context).pop()),
                ),
              ],
            ),

            const SizedBox(height: 20),
            Text(
              context.tr('share.chooseDesign'),
              style: AthkarType.sans(
                  size: 13.5, color: tokens.ink, weight: FontWeight.w600),
            ),
            const SizedBox(height: 16),

            AspectRatio(
              aspectRatio: 1,
              child: PageView.builder(
                controller: _controller,
                itemCount: ShareDesign.all.length,
                onPageChanged: (index) => setState(() => _index = index),
                itemBuilder: (context, index) => Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: RepaintBoundary(
                    key: _boundaries[index],
                    child: ShareCard(
                      subject: widget.subject,
                      design: ShareDesign.all[index],
                      wordmark: context.tr('share.wordmark'),
                    ),
                  ),
                ),
              ),
            ),

            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                for (var i = 0; i < ShareDesign.all.length; i++)
                  // Tappable as well as swipeable: four designs is few enough
                  // that a reader who saw the one they wanted go past should be
                  // able to point at it rather than swipe back round.
                  GestureDetector(
                    onTap: () => _controller.animateToPage(
                      i,
                      duration: const Duration(milliseconds: 240),
                      curve: Curves.easeOut,
                    ),
                    behavior: HitTestBehavior.opaque,
                    child: Padding(
                      // Padding, not margin: it makes the strip a 24pt-tall
                      // target without making the mark itself any thicker.
                      padding: const EdgeInsets.symmetric(
                          horizontal: 3, vertical: 10),
                      child: AnimatedContainer(
                        duration: const Duration(milliseconds: 180),
                        width: i == _index ? 22 : 14,
                        height: 3,
                        decoration: BoxDecoration(
                          color: i == _index ? tokens.brand : tokens.border,
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ),
                    ),
                  ),
              ],
            ),

            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: _busy ? null : _shareImage,
                child: _busy
                    ? AthkarSpinner.mono(tokens.onBrand)
                    : Text(context.tr('share.asImage')),
              ),
            ),
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: tokens.brand.withValues(alpha: 0.55),
                  foregroundColor: tokens.onBrand,
                ),
                onPressed: _busy ? null : _shareText,
                child: Text(context.tr('share.asText')),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _shareText() async {
    await SharePlus.instance.share(ShareParams(text: widget.subject.plainText));
    if (mounted) Navigator.of(context).pop();
  }

  /// Rasterises the card the reader is looking at and hands the file to the
  /// system sheet.
  ///
  /// The pixel ratio is derived from the card's own width rather than fixed, so
  /// the exported image is [_exportWidth] wide on every phone. Captured at the
  /// device ratio instead, the same card is 830px on one handset and 1400px on
  /// another, and the difference is visible the moment two people compare what
  /// they received.
  Future<void> _shareImage() async {
    setState(() => _busy = true);

    try {
      final boundaryContext = _boundaries[_index]?.currentContext;
      final boundary =
          boundaryContext?.findRenderObject() as RenderRepaintBoundary?;
      if (boundary == null) throw StateError('no boundary for design $_index');

      final image = await boundary.toImage(
        pixelRatio: _exportWidth / boundary.size.width,
      );
      final data = await image.toByteData(format: ui.ImageByteFormat.png);
      image.dispose();
      if (data == null) throw StateError('the card produced no bytes');

      final directory = await getTemporaryDirectory();
      final file = File('${directory.path}/athkari-share.png');
      await file.writeAsBytes(data.buffer.asUint8List(), flush: true);

      await SharePlus.instance.share(ShareParams(files: [XFile(file.path)]));
      if (mounted) Navigator.of(context).pop();
    } catch (_) {
      // A failed export is a missing image, not a crash: the reader still has
      // the text button sitting underneath.
      if (!mounted) return;
      setState(() => _busy = false);
      AthkarAlerts.toast(context, context.tr('share.imageFailed'));
    }
  }
}

class _CloseButton extends StatelessWidget {
  const _CloseButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Semantics(
      button: true,
      label: context.tr('share.close'),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: Container(
          width: 34,
          height: 34,
          decoration:
              BoxDecoration(color: tokens.brandTint, shape: BoxShape.circle),
          child: Icon(Icons.close, size: 17, color: tokens.muted),
        ),
      ),
    );
  }
}

/// The card itself — the only thing that ends up in the exported PNG.
///
/// Public because it is also what the carousel previews: previewing a *copy* of
/// the export is how the two drift apart.
class ShareCard extends StatelessWidget {
  const ShareCard({
    super.key,
    required this.subject,
    required this.design,
    required this.wordmark,
  });

  final ShareSubject subject;
  final ShareDesign design;
  final String wordmark;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        // Every measurement on the card scales off its own width, so the
        // exported 1080px image is the preview enlarged rather than a second,
        // subtly different composition.
        final unit = constraints.maxWidth / 340;

        return DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [design.ground, design.groundEnd],
            ),
            borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
          ),
          child: Padding(
            padding: EdgeInsets.all(design.framed ? 12 * unit : 0),
            child: Container(
              decoration: design.framed
                  ? BoxDecoration(
                      border: Border.all(
                        color: design.accent.withValues(alpha: 0.45),
                        width: 1.2 * unit,
                      ),
                      borderRadius: BorderRadius.circular(12 * unit),
                    )
                  : null,
              padding: EdgeInsets.fromLTRB(
                  24 * unit, 26 * unit, 24 * unit, 20 * unit),
              child: Column(
                children: [
                  if (subject.heading case final heading?) ...[
                    Text(
                      heading,
                      textAlign: TextAlign.center,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AthkarType.amiri(
                        size: 15 * unit,
                        color: design.accent,
                        weight: FontWeight.w700,
                      ),
                    ),
                    SizedBox(height: 7 * unit),
                  ],
                  // The ornament stays even when there is no heading: without
                  // it a dhikr card — most of them carry no chapter name — opens
                  // on a band of empty parchment and reads as a cropped photo
                  // rather than a composed card.
                  _Ornament(colour: design.accent, unit: unit),

                  // The text takes whatever room is left and shrinks — never
                  // clips — so a four-line dhikr and a forty-line one both come
                  // out as a complete card.
                  Expanded(
                    child: Center(
                      child: FittedBox(
                        fit: BoxFit.scaleDown,
                        child: SizedBox(
                          width: 292 * unit,
                          child: Text(
                            subject.body,
                            textAlign: TextAlign.center,
                            style: AthkarType.amiri(
                              size: 21 * unit,
                              color: design.ink,
                              height: 2.0,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),

                  if (subject.attribution case final attribution?) ...[
                    SizedBox(height: 10 * unit),
                    Text(
                      attribution,
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: AthkarType.sans(
                          size: 10 * unit, color: design.faint, height: 1.6),
                    ),
                  ],

                  SizedBox(height: 14 * unit),
                  _Wordmark(design: design, unit: unit, text: wordmark),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _Ornament extends StatelessWidget {
  const _Ornament({required this.colour, required this.unit});

  final Color colour;
  final double unit;

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          _line(),
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 6 * unit),
            child: Text(
              '۞',
              style: TextStyle(fontSize: 11 * unit, color: colour),
            ),
          ),
          _line(),
        ],
      );

  Widget _line() => Container(
        width: 26 * unit,
        height: 1,
        color: colour.withValues(alpha: 0.35),
      );
}

/// The one place the app names itself.
///
/// A shared *text* carries no wordmark — a dhikr passed on should read as the
/// dhikr, not as an advertisement. A shared *image* is a different object: it
/// is a picture somebody made with this app, and the mark is how whoever
/// receives it finds out where it came from.
class _Wordmark extends StatelessWidget {
  const _Wordmark({
    required this.design,
    required this.unit,
    required this.text,
  });

  final ShareDesign design;
  final double unit;
  final String text;

  @override
  Widget build(BuildContext context) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 17 * unit,
            height: 19 * unit,
            decoration: BoxDecoration(
              color: design.accent.withValues(alpha: 0.9),
              borderRadius: BorderRadius.circular(4 * unit),
            ),
            alignment: Alignment.center,
            child: Text(
              'ذ',
              style: AthkarType.amiri(size: 11 * unit, color: design.ground),
            ),
          ),
          SizedBox(height: 5 * unit),
          Text(
            text,
            style: AthkarType.sans(size: 8.5 * unit, color: design.faint),
          ),
        ],
      );
}
