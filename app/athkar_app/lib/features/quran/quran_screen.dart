import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/arabic_text.dart';
import '../../core/mushaf.dart';
import '../../core/numerals.dart';
import '../../core/quran_library.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../services/services.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'mushaf_screen.dart';
import 'surah_screen.dart';

/// The Qur'an tab: either an invitation to save the mushaf, or the index of it.
///
/// The download is an explicit, single decision — «حفظ» — and not something the
/// app does on the reader's behalf. It is a couple of hundred megabytes, often
/// on metered data, and a reader who never opens this tab should never pay for
/// it. Once saved it is theirs, offline, permanently.
class QuranScreen extends StatefulWidget {
  const QuranScreen({super.key});

  @override
  State<QuranScreen> createState() => _QuranScreenState();
}

class _QuranScreenState extends State<QuranScreen> {
  /// The mushafs on offer. Empty until the first answer arrives, which is not
  /// an error — a reader with no signal and a saved mushaf reads it anyway.
  List<QuranEdition> _editions = const [];

  /// Whether the mushaf being read has a newer version published.
  QuranVersion? _available;

  /// Which edition is mid-download, so one row shows a bar and the others do
  /// not. Null when nothing is downloading.
  String? _downloadingEdition;

  List<Surah>? _surahs;

  /// Whether this package carries the printed-page layer. A package without it
  /// is not broken — it is the verse-by-verse mushaf, and the toggle simply
  /// does not appear.
  var _hasPages = false;

  /// Which view the reader last chose, remembered across launches.
  ///
  /// Null until the settings are read in `didChangeDependencies` — an
  /// `InheritedNotifier` cannot be reached from `initState`. It falls back to
  /// the setting on every read, so a reader who has never touched the toggle
  /// gets pages, which is what a mushaf is.
  bool? _asPages;

  /// How the index is listed. Not the same question as `_asPages`, which is
  /// about what *opens* when a surah is tapped; this is about what the reader
  /// is looking at a list of.
  var _view = QuranIndexView.surahs;

  /// Where each surah opens, so a row can say so without a query per row.
  Map<int, int> _startPages = const {};

  List<MushafPageEntry> _pages = const [];
  List<JuzEntry> _juz = const [];

  double? _progress;
  var _query = '';

  /// Verses matching the query, as opposed to surah names. Searched a beat
  /// after the reader stops typing rather than on every keystroke: the corpus
  /// is 6,236 verses and the first keystroke of «الحمد» would match most of it.
  List<AyahHit> _verseHits = const [];
  Timer? _searchDebounce;

  /// Where the reader had reached, so the tab can offer to go back to it.
  int? _lastPage;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final state = AppStateScope.read(context);

    if (state.quran.isSaved) {
      final surahs = await QuranLibrary.surahs(state.quran);
      final hasPages = await Mushaf.isAvailable(state.quran);

      // Only worth reading when there are pages to index. A verse-only mushaf
      // answers all three of these with nothing.
      final startPages = hasPages ? await Mushaf.surahStartPages(state.quran) : const <int, int>{};
      final pages = hasPages ? await Mushaf.pageIndex(state.quran) : const <MushafPageEntry>[];
      final juz = hasPages ? await Mushaf.juzIndex(state.quran) : const <JuzEntry>[];

      if (mounted) {
        setState(() {
          _surahs = surahs;
          _hasPages = hasPages;
          _lastPage = state.quran.lastPage;
          _startPages = startPages;
          _pages = pages;
          _juz = juz;
          if (!hasPages) _view = QuranIndexView.surahs;
        });
      }
    } else if (mounted) {
      // Cleared rather than left standing: an index of a mushaf that is no
      // longer selected is worse than none, because every row opens nothing.
      setState(() {
        _surahs = null;
        _hasPages = false;
        _lastPage = null;
        _startPages = const {};
        _pages = const [];
        _juz = const [];
        _view = QuranIndexView.surahs;
      });
    }

    final shelf = await Api.quran.editions();
    if (mounted) setState(() => _editions = shelf.data ?? const []);

    // Asked about the mushaf being read, not about whichever was published
    // last: "there is an update" has to mean an update to *this* text.
    final response = await Api.quran.checkVersion(
      knownVersion: state.quran.savedVersion,
      edition: state.quran.selectedEdition,
    );

    if (mounted) setState(() => _available = response.data);
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final surahs = _surahs;

    if (!state.quran.isSaved || surahs == null || surahs.isEmpty) {
      return Scaffold(
        backgroundColor: tokens.paper,
        body: _Shelf(
          editions: _editions,
          saved: state.quran.savedEditions,
          downloading: _downloadingEdition,
          progress: _progress,
          onDownload: _download,
          onOpen: _switchTo,
          onDelete: _delete,
        ),
      );
    }

    final matches = _query.isEmpty
        ? surahs
        : [
            for (final surah in surahs)
              if (surah.nameAr.contains(_query) ||
                  surah.nameEn.toLowerCase().contains(_query.toLowerCase()))
                surah,
          ];

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AthkarSpacing.page, 14, AthkarSpacing.page, 10),
              child: Row(
                children: [
                  Text(
                    context.tr('quran.title'),
                    style: AthkarType.amiri(
                      size: 22, color: tokens.ink, weight: FontWeight.w700),
                  ),
                  const Spacer(),
                  // The shelf stays reachable once a mushaf is saved: it is
                  // where a second one is downloaded, where the reader switches
                  // between them, and where one is removed from the device.
                  // Shown for a single mushaf too — otherwise a reader with one
                  // has no way back to it, and deleting it is the reason they
                  // would look.
                  if (_editions.isNotEmpty) ...[
                    AthkarChip(label: context.tr('quran.mushafs'), onTap: _showShelf),
                    const SizedBox(width: 8),
                  ],
                  if (_available?.updateAvailable ?? false)
                    AthkarChip(
                      label: context.tr('quran.update'),
                      selected: true,
                      onTap: _updateCurrent,
                    ),
                ],
              ),
            ),
            // How the mushaf is indexed: by surah, by juz, or page by page.
            // Only offered when the package carries the printed pages — in a
            // verse-only mushaf there is nothing for the other two to point at.
            if (_hasPages)
              Padding(
                padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 0, AthkarSpacing.page, 10),
                child: Row(
                  children: [
                    Text(
                      context.tr('quran.view.label'),
                      style: AthkarType.sans(size: 12.5, color: tokens.muted),
                    ),
                    const SizedBox(width: 10),
                    for (final view in QuranIndexView.values) ...[
                      AthkarChip(
                        label: context.tr(view.labelKey),
                        selected: _view == view,
                        onTap: () => _chooseView(view),
                      ),
                      const SizedBox(width: 8),
                    ],
                  ],
                ),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
              child: TextField(
                onChanged: _onQueryChanged,
                decoration: InputDecoration(
                  hintText: context.tr('quran.search'),
                  prefixIcon: Icon(Icons.search, size: 18, color: tokens.muted),
                ),
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(
                  AthkarSpacing.page, 12, AthkarSpacing.page, 24),
                children: [
                  // Where the reader stopped. Above the index and only while
                  // nothing is being searched: someone looking for a verse is
                  // not looking for where they were.
                  if (_hasPages && _query.isEmpty)
                    if (_lastPage case final page?)
                      AthkarListRow(
                        label: context.tr('quran.continue'),
                        value: '${context.tr('quran.page')} '
                            '${Numerals.format(page, arabicIndic: settings.arabicNumerals)}',
                        onTap: () => _openPage(page),
                      ),

                  // The index itself, in whichever way the reader is listing
                  // it. A search narrows the surahs and leaves the other two
                  // alone: looking for a word is not looking for page 300.
                  if (_query.isNotEmpty || _view == QuranIndexView.surahs)
                    for (final surah in matches)
                      _SurahCard(
                        surah: surah,
                        startPage: _startPages[surah.id],
                        arabicNumerals: settings.arabicNumerals,
                        onTap: () => _open(surah),
                      )
                  else if (_view == QuranIndexView.juz)
                    for (final juz in _juz)
                      AthkarListRow(
                        label: '${context.tr('quran.juz')} '
                            '${Numerals.format(juz.number, arabicIndic: settings.arabicNumerals)}',
                        value: [
                          _nameOf(juz.surahId),
                          '${context.tr('quran.verse')} '
                              '${Numerals.format(juz.ayah, arabicIndic: settings.arabicNumerals)}',
                          '${context.tr('quran.page')} '
                              '${Numerals.format(juz.page, arabicIndic: settings.arabicNumerals)}',
                        ].where((part) => part.isNotEmpty).join(' · '),
                        onTap: () => _openPage(juz.page),
                      )
                  // The pages are the one list long enough to be worth
                  // building lazily: 604 rows eagerly is 604 widgets nobody has
                  // scrolled to yet.
                  else
                    _PageList(
                      pages: _pages,
                      nameOf: _nameOf,
                      arabicNumerals: settings.arabicNumerals,
                      onOpen: _openPage,
                    ),

                  if (_verseHits.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    AthkarSectionHeader(title: context.tr('quran.searchVerses')),
                    for (final hit in _verseHits)
                      _VerseHitRow(
                        hit: hit,
                        surahName: _nameOf(hit.surahId),
                        arabicNumerals: settings.arabicNumerals,
                        onTap: () => _openHit(hit),
                      ),
                  ],

                  if (_query.isNotEmpty && matches.isEmpty && _verseHits.isEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 24),
                      child: Text(
                        context.tr('quran.searchEmpty'),
                        textAlign: TextAlign.center,
                        style: AthkarType.sans(size: 13, color: tokens.muted),
                      ),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// Searches surah names as the reader types, and the text of the mushaf a
  /// beat later. The two are separate on purpose: matching a name is instant
  /// and matching 6,236 verses is not worth doing per keystroke.
  void _onQueryChanged(String value) {
    setState(() => _query = value);

    _searchDebounce?.cancel();

    if (ArabicText.normalize(value).length < 2) {
      setState(() => _verseHits = const []);
      return;
    }

    _searchDebounce = Timer(const Duration(milliseconds: 250), () async {
      final store = AppStateScope.read(context).quran;
      final hits = await QuranLibrary.search(store, value);
      if (mounted && value == _query) setState(() => _verseHits = hits);
    });
  }

  String _nameOf(int surahId) =>
      _surahs?.firstWhere(
        (surah) => surah.id == surahId,
        orElse: () => const Surah(
            id: 0, nameAr: '', nameEn: '', ayahCount: 0, revelationPlace: ''),
      ).nameAr ??
      '';

  /// Opens a found verse where the reader can see it in context: on its printed
  /// page when the mushaf has one, and otherwise in its surah.
  Future<void> _openHit(AyahHit hit) async {
    if (hit.page case final page? when _hasPages) {
      await _openPage(page);
      return;
    }

    final surah = _surahs?.where((s) => s.id == hit.surahId).firstOrNull;
    if (surah == null || !mounted) return;

    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => SurahScreen(surah: surah)),
    );
  }

  Future<void> _openPage(int page) async {
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => MushafScreen(initialPage: page)),
    );

    // The reader may have turned pages in there; the offer to continue should
    // say where they are now, not where they were when the tab last loaded.
    if (mounted) {
      setState(() => _lastPage = AppStateScope.read(context).quran.lastPage);
    }
  }

  /// Remembers the choice as well as making it. A reader who prefers flowing
  /// text should not have to say so again every time the tab is reopened.
  ///
  /// Listing by juz or by page also says how a surah should open: somebody
  /// reading the mushaf by its pages wants the page when they tap a surah too.
  void _chooseView(QuranIndexView view) {
    setState(() => _view = view);

    final asPages = view != QuranIndexView.surahs;
    if ((_asPages ?? SettingsScope.read(context).quranAsPages) == asPages) return;

    setState(() => _asPages = asPages);
    SettingsScope.read(context).setQuranAsPages(asPages);
  }

  /// Opens the surah the way the reader is currently reading: as continuous
  /// text, or at the page the press opens it on.
  Future<void> _open(Surah surah) async {
    if (_asPages ?? SettingsScope.read(context).quranAsPages) {
      final state = AppStateScope.read(context);
      final page = await Mushaf.pageOfSurah(state.quran, surah.id);
      if (!mounted) return;
      if (page != null) {
        await _openPage(page);
        return;
      }
    }

    if (!mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => SurahScreen(surah: surah)),
    );
  }

  /// The shelf, as a sheet, once there is already a mushaf on screen behind it.
  Future<void> _showShelf() => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (sheetContext) => FractionallySizedBox(
          heightFactor: 0.8,
          child: _Shelf(
            editions: _editions,
            saved: AppStateScope.of(sheetContext).quran.savedEditions,
            downloading: _downloadingEdition,
            progress: _progress,
            onDownload: (edition) {
              Navigator.of(sheetContext).pop();
              _download(edition);
            },
            onOpen: (edition) {
              Navigator.of(sheetContext).pop();
              _switchTo(edition);
            },
            onDelete: (edition) {
              Navigator.of(sheetContext).pop();
              _delete(edition);
            },
          ),
        ),
      );

  /// Removes a mushaf from the device, after asking. A couple of hundred
  /// megabytes is a real amount of a phone, and this is the only way to get it
  /// back — so it is a question, and the answer has to be the deliberate one.
  Future<void> _delete(QuranEdition edition) async {
    final confirmed = await AthkarAlerts.confirm(
      context,
      title: context.tr('quran.delete.confirm'),
      body: context.tr('quran.delete.confirmBody', {'name': edition.name}),
      confirmLabel: context.tr('common.delete'),
      destructive: true,
    );
    if (!confirmed || !mounted) return;

    final state = AppStateScope.read(context);
    await state.quran.delete(edition.edition);

    // The folded search index belongs to the mushaf that is now gone.
    QuranLibrary.forgetCorpus();

    if (!mounted) return;
    setState(() {
      _surahs = null;
      _verseHits = const [];
      _lastPage = null;
      _available = null;
    });

    await _load();
    if (mounted) AthkarAlerts.toast(context, context.tr('quran.delete.done'));
  }

  /// Reads a mushaf the device already has. No network and no download — which
  /// is the whole point of having paid for it once.
  Future<void> _switchTo(QuranEdition edition) async {
    await AppStateScope.read(context).quran.select(edition.edition);

    // Built from the previous mushaf, and its page numbers are that mushaf's.
    QuranLibrary.forgetCorpus();

    if (!mounted) return;

    setState(() {
      _surahs = null;
      _available = null;
      _verseHits = const [];
    });

    await _load();
  }

  /// Re-downloads the mushaf being read, when a newer version is published.
  Future<void> _updateCurrent() async {
    final current = AppStateScope.read(context).quran.selectedEdition;
    if (current == null) return;

    for (final edition in _editions) {
      if (edition.edition == current) {
        await _download(edition);
        return;
      }
    }
  }

  Future<void> _download(QuranEdition edition) async {
    if (_downloadingEdition != null) return;

    setState(() {
      _downloadingEdition = edition.edition;
      _progress = 0;
    });

    final state = AppStateScope.read(context);

    final response = await state.quran.download(
      edition,
      onProgress: (value) {
        if (mounted) setState(() => _progress = value);
      },
    );

    if (!mounted) return;
    setState(() {
      _downloadingEdition = null;
      _progress = null;
    });

    if (response.success) {
      await _load();
      if (mounted) AthkarAlerts.toast(context, context.tr('quran.saved'));
    } else {
      AthkarAlerts.error(context, response.errorMessage ?? context.tr('error.generic'));
    }
  }
}

/// The mushafs on offer, and which of them this device has.
///
/// This is the state before any mushaf is on the device — where most installs
/// stay — and so it is a designed screen rather than a placeholder. It doubles
/// as the switcher afterwards: the same list, reached from a chip.
///
/// The download stays an explicit, single decision per mushaf. Each is a couple
/// of hundred megabytes, often on metered data, and nothing here is fetched on
/// the reader's behalf.
class _Shelf extends StatelessWidget {
  const _Shelf({
    required this.editions,
    required this.saved,
    required this.downloading,
    required this.progress,
    required this.onDownload,
    required this.onOpen,
    this.onDelete,
  });

  final List<QuranEdition> editions;

  /// The slugs this device already holds.
  final List<String> saved;

  /// The slug currently downloading, if any.
  final String? downloading;

  final double? progress;
  final void Function(QuranEdition edition) onDownload;
  final void Function(QuranEdition edition) onOpen;

  /// Null on the first-run shelf, where nothing is saved yet and a delete
  /// action would be an answer to a question nobody has.
  final void Function(QuranEdition edition)? onDelete;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    if (editions.isEmpty) {
      return AthkarEmptyState(
        title: context.tr('quran.notAvailable'),
        body: context.tr('quran.notAvailableHint'),
        icon: Icons.menu_book_outlined,
      );
    }

    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 18, AthkarSpacing.page, 30),
        children: [
          Text(
            context.tr('quran.mushafs'),
            style: AthkarType.amiri(size: 20, color: tokens.ink, weight: FontWeight.w700),
          ),
          const SizedBox(height: 4),
          Text(
            context.tr('quran.mushafsHint'),
            style: AthkarType.sans(size: 12, color: tokens.muted, height: 1.7),
          ),
          const SizedBox(height: 16),
          for (final edition in editions)
            _EditionCard(
              edition: edition,
              isSaved: saved.contains(edition.edition),
              isDownloading: downloading == edition.edition,
              progress: progress,
              arabicNumerals: settings.arabicNumerals,
              onDownload: () => onDownload(edition),
              onOpen: () => onOpen(edition),
              onDelete: onDelete == null ? null : () => onDelete!(edition),
            ),
        ],
      ),
    );
  }
}

class _EditionCard extends StatelessWidget {
  const _EditionCard({
    required this.edition,
    required this.isSaved,
    required this.isDownloading,
    required this.progress,
    required this.arabicNumerals,
    required this.onDownload,
    required this.onOpen,
    this.onDelete,
  });

  final QuranEdition edition;
  final bool isSaved;
  final bool isDownloading;
  final double? progress;
  final bool arabicNumerals;
  final VoidCallback onDownload;
  final VoidCallback onOpen;
  final VoidCallback? onDelete;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final megabytes = (edition.sizeBytes / (1024 * 1024)).round();

    return AthkarCard(
      margin: const EdgeInsets.only(bottom: 12),
      // Tapping a mushaf already on the device opens it. One that is not stays
      // behind its own button: a download this size is never something a reader
      // should be able to start by brushing a card.
      onTap: isSaved ? onOpen : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  edition.name,
                  style: AthkarType.amiri(
                    size: 18, color: tokens.ink, weight: FontWeight.w700),
                ),
              ),
              if (isSaved)
                Icon(Icons.offline_pin_outlined, size: 18, color: tokens.brand),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            isSaved
                ? context.tr('quran.savedOnDevice')
                : context.tr('quran.downloadHint', {
                    'size':
                        '${Numerals.format(megabytes, arabicIndic: arabicNumerals)} MB',
                  }),
            style: AthkarType.sans(size: 12, color: tokens.muted, height: 1.7),
          ),
          if (edition.releaseNotes case final notes? when notes.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              notes,
              style: AthkarType.sans(size: 11.5, color: tokens.faint, height: 1.7),
            ),
          ],
          const SizedBox(height: 12),
          if (isDownloading)
            Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(3),
                  child: LinearProgressIndicator(
                    value: progress,
                    minHeight: 4,
                    backgroundColor: tokens.border,
                    color: tokens.brand,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  context.tr('quran.downloading', {
                    'percent': Numerals.format(
                      ((progress ?? 0) * 100).round(),
                      arabicIndic: arabicNumerals,
                    ),
                  }),
                  style: AthkarType.sans(size: 12, color: tokens.muted),
                ),
              ],
            )
          else if (isSaved)
            Row(
              children: [
                OutlinedButton(onPressed: onOpen, child: Text(context.tr('quran.open'))),
                if (onDelete != null) ...[
                  const Spacer(),
                  TextButton(
                    onPressed: onDelete,
                    child: Text(
                      context.tr('quran.delete'),
                      style: AthkarType.sans(size: 12.5, color: tokens.muted),
                    ),
                  ),
                ],
              ],
            )
          else
            FilledButton(onPressed: onDownload, child: Text(context.tr('quran.save'))),
        ],
      ),
    );
  }
}

/// One verse the search found: the text as the mushaf prints it, and where to
/// find it. The text is clipped rather than wrapped without limit — a search
/// result is a signpost, and al-Baqarah 282 would otherwise fill the screen.
class _VerseHitRow extends StatelessWidget {
  const _VerseHitRow({
    required this.hit,
    required this.surahName,
    required this.arabicNumerals,
    required this.onTap,
  });

  final AyahHit hit;
  final String surahName;
  final bool arabicNumerals;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final where = [
      if (surahName.isNotEmpty) surahName,
      '${context.tr('quran.verse')} '
          '${Numerals.format(hit.ayah, arabicIndic: arabicNumerals)}',
      if (hit.page case final page?)
        '${context.tr('quran.page')} '
            '${Numerals.format(page, arabicIndic: arabicNumerals)}',
    ].join(' · ');

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              hit.text,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.right,
              style: AthkarType.amiri(size: 18, color: tokens.ink, height: 1.9),
            ),
            const SizedBox(height: 4),
            Text(where, style: AthkarType.sans(size: 11.5, color: tokens.muted)),
          ],
        ),
      ),
    );
  }
}

/// How the mushaf is indexed on this screen.
enum QuranIndexView {
  surahs('quran.view.surahs'),
  juz('quran.view.juz'),
  pages('quran.view.pages');

  const QuranIndexView(this.labelKey);

  final String labelKey;
}

/// One surah in the index: its name set as the mushaf sets it, where it opens,
/// and how long it is.
///
/// A card rather than a row because the three facts belong together — a reader
/// choosing where to read wants «مكية · الصفحة ٥٨٦ · ٧ آيات» in one glance, and
/// a list of bare names makes them tap to find out.
class _SurahCard extends StatelessWidget {
  const _SurahCard({
    required this.surah,
    required this.startPage,
    required this.arabicNumerals,
    required this.onTap,
  });

  final Surah surah;

  /// Null in a mushaf with no page layer; the row then simply does not say.
  final int? startPage;

  final bool arabicNumerals;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final where = [
      context.tr(surah.revelationPlace == 'makkah' ? 'quran.makki' : 'quran.madani'),
      if (startPage case final page?)
        '${context.tr('quran.page')} '
            '${Numerals.format(page, arabicIndic: arabicNumerals)}',
    ].join(' · ');

    return AthkarCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
      onTap: onTap,
      child: Row(
        children: [
          // The number in the ornament the mushaf marks its surahs with.
          _Rosette(number: surah.id, arabicNumerals: arabicNumerals),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  surah.nameAr,
                  style: AthkarType.amiri(
                    size: 19, color: tokens.ink, weight: FontWeight.w700),
                ),
                const SizedBox(height: 3),
                Text(where, style: AthkarType.sans(size: 11.5, color: tokens.muted)),
              ],
            ),
          ),
          Text(
            '${Numerals.format(surah.ayahCount, arabicIndic: arabicNumerals)} '
            '${context.tr('quran.verse')}',
            style: AthkarType.sans(size: 11.5, color: tokens.brandInk),
          ),
        ],
      ),
    );
  }
}

/// The eight-pointed frame a mushaf puts a surah's number in.
class _Rosette extends StatelessWidget {
  const _Rosette({required this.number, required this.arabicNumerals});

  final int number;
  final bool arabicNumerals;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return SizedBox(
      width: 34,
      height: 34,
      child: Stack(
        alignment: Alignment.center,
        children: [
          // Two squares at 45° to each other: the outline of the eight-pointed
          // star the printed mushaf uses, without an asset to ship.
          Transform.rotate(
            angle: 0.785398,
            child: Container(
              width: 24,
              height: 24,
              decoration: BoxDecoration(
                border: Border.all(color: tokens.brand.withValues(alpha: 0.45)),
                borderRadius: BorderRadius.circular(3),
              ),
            ),
          ),
          Container(
            width: 24,
            height: 24,
            decoration: BoxDecoration(
              border: Border.all(color: tokens.brand.withValues(alpha: 0.45)),
              borderRadius: BorderRadius.circular(3),
            ),
          ),
          Text(
            Numerals.format(number, arabicIndic: arabicNumerals),
            style: AthkarType.sans(size: 11, color: tokens.brandInk, weight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

/// The 604 pages, built as they are scrolled to.
///
/// Inside the surrounding list rather than instead of it, so the search field
/// and the view selector stay put while this scrolls — which is why it is
/// `shrinkWrap`ped with its own scrolling turned off.
class _PageList extends StatelessWidget {
  const _PageList({
    required this.pages,
    required this.nameOf,
    required this.arabicNumerals,
    required this.onOpen,
  });

  final List<MushafPageEntry> pages;
  final String Function(int surahId) nameOf;
  final bool arabicNumerals;
  final void Function(int page) onOpen;

  @override
  Widget build(BuildContext context) => ListView.builder(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        padding: EdgeInsets.zero,
        itemCount: pages.length,
        itemBuilder: (context, index) {
          final entry = pages[index];

          return AthkarListRow(
            label: '${context.tr('quran.page')} '
                '${Numerals.format(entry.page, arabicIndic: arabicNumerals)}',
            value: nameOf(entry.surahId),
            onTap: () => onOpen(entry.page),
          );
        },
      );
}
