import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/quran_library.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../widgets/athkar_ui.dart';
import '../share_sheet.dart';

/// One surah, set as continuous text.
///
/// The waqf marks are already in the verse text — they are Unicode characters
/// the package carries, not something the app adds. What this screen adds is the
/// ability to *ask* about one: tapping a verse opens what its marks mean, when
/// the package ships the annotation tables. See `QuranLibrary.waqfMarks`.
class SurahScreen extends StatefulWidget {
  const SurahScreen({super.key, required this.surah});

  final Surah surah;

  @override
  State<SurahScreen> createState() => _SurahScreenState();
}

class _SurahScreenState extends State<SurahScreen> {
  List<Ayah>? _ayahs;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final store = AppStateScope.read(context).quran;
    final ayahs = await QuranLibrary.ayahs(store, widget.surah.id);

    if (mounted) setState(() => _ayahs = ayahs);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final ayahs = _ayahs;

    return Scaffold(
      backgroundColor: tokens.readingPaper,
      appBar: AppBar(
        backgroundColor: tokens.readingPaper,
        title: Text(widget.surah.nameAr),
      ),
      body: ayahs == null
          ? const Center(child: AthkarSpinner())
          : ListView(
              padding: const EdgeInsets.fromLTRB(30, 16, 30, 40),
              children: [
                const AthkarOrnament(width: 90),
                const SizedBox(height: 18),
                // The basmala is its own line, centred and a shade quieter —
                // and omitted for at-Tawbah, which does not open with it.
                if (widget.surah.id != 9 && widget.surah.id != 1)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 20),
                    child: Text(
                      'بِسْمِ اللهِ الرَّحْمنِ الرَّحِيم',
                      textAlign: TextAlign.center,
                      style: AthkarType.amiri(
                        size: 20 * settings.fontScale,
                        color: tokens.muted,
                        height: 2,
                      ),
                    ),
                  ),
                for (final ayah in ayahs)
                  InkWell(
                    onTap: () => _explainWaqf(ayah),
                    onLongPress: () => _shareAyah(ayah),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      child: Text.rich(
                        TextSpan(
                          children: [
                            TextSpan(text: '${ayah.text} '),
                            TextSpan(
                              // The ayah number inside its own ornamental
                              // marker, which is how a mushaf sets it.
                              text: '۝'
                                  '${Numerals.format(ayah.number, arabicIndic: true)}',
                              style: AthkarType.amiri(
                                size: 22 * settings.fontScale,
                                color: tokens.brand,
                              ),
                            ),
                          ],
                        ),
                        textAlign: TextAlign.justify,
                        style: AthkarType.amiri(
                          size: 24 * settings.fontScale,
                          color: tokens.ink,
                          height: 2.15,
                        ),
                      ),
                    ),
                  ),
              ],
            ),
    );
  }

  /// Long-pressing a verse offers it as a card.
  ///
  /// Long press rather than tap because tapping already asks about the pause
  /// marks, and that is the question a reader has *while* reading; sharing is
  /// something they decide to do after.
  ///
  /// The verse goes out as the package spells it, waqf glyphs and all. Tidying
  /// them out would make a prettier card out of a text that is no longer the
  /// mushaf's.
  Future<void> _shareAyah(Ayah ayah) async {
    final reference = context.tr('share.ayahSource', {
      'surah': widget.surah.nameAr,
      'number': Numerals.format(ayah.number, arabicIndic: true),
    });

    await ShareSheet.show(
      context,
      ShareSubject(
        heading: widget.surah.nameAr,
        body: ayah.text,
        attribution: reference,
        plainText: '${ayah.text}\n\n$reference',
      ),
    );
  }

  /// Opens the sheet describing this verse's pause marks.
  ///
  /// Silent when the package has no annotations: an empty sheet would be worse
  /// than nothing, and most packages will not carry them.
  Future<void> _explainWaqf(Ayah ayah) async {
    final store = AppStateScope.read(context).quran;
    final marks = await QuranLibrary.waqfMarks(store, ayah.surahId, ayah.number);

    if (!mounted || marks.isEmpty) return;

    final tokens = AthkarTokens.of(context);

    await showModalBottomSheet(
      context: context,
      builder: (context) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(AthkarSpacing.page),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                context.tr('quran.waqf'),
                style: AthkarType.amiri(
                  size: 19, color: tokens.ink, weight: FontWeight.w700),
              ),
              const SizedBox(height: 14),
              for (final mark in marks)
                Padding(
                  padding: const EdgeInsets.only(bottom: 14),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        mark.symbol,
                        style: AthkarType.amiri(size: 26, color: tokens.brand),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              mark.name,
                              style: AthkarType.sans(
                                size: 13.5,
                                color: tokens.ink,
                                weight: FontWeight.w600,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              mark.ruling,
                              style: AthkarType.sans(
                                size: 12.5, color: tokens.muted, height: 1.8),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
