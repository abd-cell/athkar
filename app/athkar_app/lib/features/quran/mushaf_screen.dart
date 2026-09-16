import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/mushaf.dart';
import '../../core/numerals.dart';
import '../../core/quran_library.dart';
import '../../core/quran_store.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../widgets/athkar_ui.dart';

/// The mushaf as it is printed: a page at a time, lines broken where the press
/// broke them.
///
/// The package carries the grid — which words sit on which line of which page,
/// and whether that line is centred or justified — so nothing here decides
/// where a line ends. What this screen owns is fitting that grid onto a phone:
/// a line is drawn at one size for the whole page, and the space between its
/// words is stretched to the margins, which is how a justified line is built
/// out of words the font draws separately.
class MushafScreen extends StatefulWidget {
  const MushafScreen({super.key, required this.initialPage});

  final int initialPage;

  @override
  State<MushafScreen> createState() => _MushafScreenState();
}

class _MushafScreenState extends State<MushafScreen> {
  late final PageController _controller;
  late int _current;

  final _surahNames = <int, String>{};

  @override
  void initState() {
    super.initState();
    _current = widget.initialPage.clamp(1, Mushaf.totalPages);
    // Right to left: page 1 is the rightmost, as in the book.
    _controller = PageController(initialPage: Mushaf.totalPages - _current);
    _loadNames();

    // The page it opens on counts: a reader who taps «متابعة القراءة» and reads
    // that one page without turning has still been there.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) AppStateScope.read(context).quran.setLastPage(_current);
    });
  }

  Future<void> _loadNames() async {
    final store = AppStateScope.read(context).quran;
    final surahs = await QuranLibrary.surahs(store);
    if (!mounted) return;
    setState(() {
      for (final surah in surahs) {
        _surahNames[surah.id] = surah.nameAr;
      }
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  /// Remembers where the reader is, so the tab can offer to come back here.
  /// Written per page turn and not per frame — `setLastPage` drops a write that
  /// would store the number already stored.
  void _onPageChanged(int index) {
    final page = Mushaf.totalPages - index;
    setState(() => _current = page);
    AppStateScope.read(context).quran.setLastPage(page);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final store = AppStateScope.of(context).quran;

    return Scaffold(
      backgroundColor: tokens.readingPaper,
      appBar: AppBar(
        backgroundColor: tokens.readingPaper,
        title: Text(
          '${context.tr('quran.page')} '
          '${Numerals.format(_current, arabicIndic: settings.arabicNumerals)}',
        ),
      ),
      body: SafeArea(
        child: PageView.builder(
          controller: _controller,
          itemCount: Mushaf.totalPages,
          onPageChanged: _onPageChanged,
          itemBuilder: (context, index) => _Page(
            store: store,
            number: Mushaf.totalPages - index,
            surahNames: _surahNames,
          ),
        ),
      ),
    );
  }
}

class _Page extends StatefulWidget {
  const _Page({required this.store, required this.number, required this.surahNames});

  final QuranStore store;
  final int number;
  final Map<int, String> surahNames;

  @override
  State<_Page> createState() => _PageState();
}

class _PageState extends State<_Page> {
  MushafPage? _page;
  String? _family;
  var _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final page = await Mushaf.page(widget.store, widget.number);
    final family = await MushafFonts.familyFor(widget.store, widget.number);

    if (!mounted) return;
    setState(() {
      _page = page;
      _family = family;
      _loading = false;
    });
  }

  /// The rows of the grid this page leaves empty.
  static int _blanks(MushafPage page) =>
      page.lines.where((line) => line.kind == MushafLineKind.blank).length;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    if (_loading) return const Center(child: AthkarSpinner());

    final page = _page;
    if (page == null) {
      return Center(
        child: Text(context.tr('quran.pageMissing'),
            style: AthkarType.sans(size: 13, color: tokens.muted)),
      );
    }

    return Padding(
      padding: const EdgeInsets.fromLTRB(18, 12, 18, 8),
      child: Column(
        children: [
          Expanded(
            child: Column(
              children: [
                // The first two pages hold eight lines of text on the same
                // fifteen-line grid as every other page, and the print centres
                // them. Where a package puts its empty rows in that grid varies
                // — leading in one build, trailing in another — so they are
                // taken out and split evenly rather than drawn where they sit.
                for (var index = 0; index < _blanks(page) ~/ 2; index++) const Spacer(),
                for (final line in page.lines)
                  if (line.kind != MushafLineKind.blank)
                    Expanded(
                      child: _Line(
                        line: line,
                        family: _family,
                        surahName: widget.surahNames[line.surahId ?? 0],
                      ),
                    ),
                for (var index = _blanks(page) ~/ 2; index < _blanks(page); index++)
                  const Spacer(),
              ],
            ),
          ),
          const SizedBox(height: 6),
          Text(
            Numerals.format(page.number, arabicIndic: settings.arabicNumerals),
            style: AthkarType.sans(size: 12, color: tokens.muted),
          ),
        ],
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.line, required this.family, this.surahName});

  final MushafLine line;
  final String? family;
  final String? surahName;

  /// The size a line is drawn at before it is fitted to the page. Lines are
  /// measured against the width available and scaled down together with their
  /// own spacing, so a dense line stays on one line rather than wrapping —
  /// wrapping would move a word to a line the press never put it on.
  static const _baseSize = 26.0;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    if (line.kind == MushafLineKind.blank) return const SizedBox.shrink();

    if (line.kind == MushafLineKind.surahName) {
      return _SurahHeader(name: surahName);
    }

    final style = _style(tokens.ink);

    if (line.kind == MushafLineKind.basmallah) {
      return Center(
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text('بِسْمِ ٱللَّهِ ٱلرَّحْمَٰنِ ٱلرَّحِيمِ', style: style),
        ),
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        final scaler = MediaQuery.textScalerOf(context);
        final natural = _naturalWidth(style, scaler);
        // Only ever shrink: a justified line that is short has its gaps
        // stretched by `spaceBetween` instead. The measurement has to include
        // the same gaps the row will draw, or a line that is wider than it
        // measured runs off the page.
        // Shrunk by a further hair so that a line which had to be scaled still
        // has slack for `spaceBetween` to distribute. Fitted exactly, its words
        // sit a single space apart and the line reads as a wall — the print
        // stretches the letters instead, and this is the nearest we get with a
        // font we are not stretching.
        final scale = natural > constraints.maxWidth && natural > 0
            ? constraints.maxWidth / natural * _breathing
            : 1.0;
        final scaled = _style(tokens.ink, size: _baseSize * scale);

        final words = [
          for (final word in line.words)
            Text(word.text, style: scaled, textDirection: TextDirection.rtl),
        ];

        return Row(
          textDirection: TextDirection.rtl,
          mainAxisAlignment:
              line.isCentered ? MainAxisAlignment.center : MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            for (var index = 0; index < words.length; index++) ...[
              words[index],
              // A centred line keeps ordinary word spacing; a justified one
              // takes its spacing from the row itself.
              if (line.isCentered && index != words.length - 1)
                SizedBox(width: scaled.fontSize! * _gapRatio),
            ],
          ],
        );
      },
    );
  }

  TextStyle _style(Color color, {double? size}) => TextStyle(
        fontFamily: family,
        fontSize: size ?? _baseSize,
        color: color,
        height: 1.0,
      );

  /// The gap a centred line puts between its words, as a fraction of the size.
  static const _gapRatio = 0.28;

  /// How much of the width a scaled line gives back, so its words are spaced
  /// rather than merely fitted.
  static const _breathing = 0.96;

  /// What the line measures as this widget will actually lay it out: the words
  /// themselves, plus the gaps the row puts between them.
  ///
  /// Measuring the joined string instead understates a centred line — its gaps
  /// are wider than a space — and the line then overflows the page rather than
  /// being scaled to fit.
  double _naturalWidth(TextStyle style, TextScaler scaler) {
    var width = 0.0;
    for (final word in line.words) {
      final painter = TextPainter(
        text: TextSpan(text: word.text, style: style),
        textDirection: TextDirection.rtl,
        textScaler: scaler,
      )..layout();
      width += painter.width;
    }

    final gaps = line.words.length - 1;
    if (gaps <= 0) return width;

    if (line.isCentered) return width + gaps * style.fontSize! * _gapRatio;

    // A justified line needs at least a space between words; below that the
    // words would touch.
    final space = TextPainter(
      text: TextSpan(text: ' ', style: style),
      textDirection: TextDirection.rtl,
      textScaler: scaler,
    )..layout();
    return width + gaps * space.width;
  }
}

/// The frame a surah opens with.
///
/// Drawn by the app rather than by the font: the package carries the mushaf's
/// text face, not the Complex's ornamental header face, and an app that
/// invented one would be claiming a fidelity it does not have.
class _SurahHeader extends StatelessWidget {
  const _SurahHeader({this.name});

  final String? name;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Center(
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 2),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 2),
        decoration: BoxDecoration(
          color: tokens.brandTint,
          border: Border.all(color: tokens.brand.withValues(alpha: 0.35)),
          borderRadius: BorderRadius.circular(6),
        ),
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            'سُورَةُ ${name ?? ''}'.trim(),
            style: AthkarType.amiri(size: 17, color: tokens.brand, weight: FontWeight.w700),
          ),
        ),
      ),
    );
  }
}
