import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../core/arabic_text.dart';
import '../core/bootstrap.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';

/// The counter, for a dhikr that is not part of a chapter.
///
/// The big circle is the whole control. Targets are the four that actually get
/// used — 33, 99, 100 and free — because a number picker for something a reader
/// sets once is a worse answer than four taps.
class TasbihScreen extends StatefulWidget {
  const TasbihScreen({super.key});

  @override
  State<TasbihScreen> createState() => _TasbihScreenState();
}

class _TasbihScreenState extends State<TasbihScreen> {
  static const _targets = [33, 99, 100];

  /// The name the built-in dhikr's counter is stored by.
  ///
  /// A chosen dhikr is stored under `dhikr.<id>` instead. The store has always
  /// been keyed by name for exactly this — a second counter costs no migration
  /// — so an install that has been counting «سبحان الله وبحمده» keeps its
  /// number under the name it already has.
  static const _defaultCounter = 'subhanallah';

  /// What the counter shows before the catalogue has ever synced. Compiled in
  /// only because an install with no content at all still has to show
  /// something; everything else on this screen comes from published content.
  static const _defaultText = 'سُبْحَانَ اللهِ وَبِحَمْدِهِ';

  var _count = 0;
  int? _target = 100;
  var _restored = false;

  /// The counter the screen is currently on, under the name it is stored by.
  String get _counterName {
    final id = SettingsScope.of(context).tasbihDhikrId;
    if (id == null) return _defaultCounter;

    // An id the catalogue no longer carries falls back with the text, so the
    // number on screen and the name it is saved under never disagree.
    return AppStateScope.of(context).content.dhikrById(id) == null
        ? _defaultCounter
        : 'dhikr.$id';
  }

  /// The dhikr being counted, or null for the built-in one.
  Dhikr? get _dhikr {
    final id = SettingsScope.of(context).tasbihDhikrId;
    return id == null ? null : AppStateScope.of(context).content.dhikrById(id);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();

    // Restored once, on first build: a counter that forgets itself when the
    // reader glances at another tab is not a counter.
    if (_restored) return;
    _restored = true;

    final stored = AppStateScope.of(context).progress.tasbihCounters[_counterName];
    if (stored != null && stored != _count) setState(() => _count = stored);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final digits = settings.arabicNumerals;
    final dhikr = _dhikr;

    final progress = _target == null ? 0.0 : (_count % _target!) / _target!;

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            AthkarSpacing.page, 14, AthkarSpacing.page, 30),
          children: [
            Row(
              children: [
                Text(
                  context.tr('tasbih.title'),
                  style: AthkarType.amiri(size: 22, color: tokens.ink, weight: FontWeight.w700),
                ),
                const Spacer(),
                AthkarChip(label: context.tr('tasbih.history'), onTap: _showHistory),
              ],
            ),

            const SizedBox(height: 14),
            // The whole card changes the dhikr. It is the one thing on this
            // screen a reader would reach for that is not the circle, and a
            // separate button beside it would only compete with the counter.
            AthkarCard(
              padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
              onTap: _chooseDhikr,
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        context.tr('tasbih.current'),
                        style: AthkarType.sans(size: 11.5, color: tokens.muted),
                      ),
                      const SizedBox(width: 6),
                      Icon(Icons.expand_more, size: 15, color: tokens.muted),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text(
                    dhikr?.arabicText ?? _defaultText,
                    textAlign: TextAlign.center,
                    style: AthkarType.amiri(
                      size: 21 * settings.fontScale,
                      color: tokens.ink,
                      height: 1.9,
                    ),
                  ),
                  // The takhrij travels with the dhikr. A counter is still a
                  // place the text is read, and the rule does not bend for it.
                  if (dhikr != null && dhikr.hasSource) ...[
                    const SizedBox(height: 8),
                    AthkarSourceLine(
                      text: '${dhikr.sourceBook} · ${dhikr.sourceReference}',
                    ),
                  ],
                ],
              ),
            ),

            const SizedBox(height: 16),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                for (final target in _targets) ...[
                  AthkarChip(
                    label: Numerals.format(target, arabicIndic: digits),
                    selected: _target == target,
                    onTap: () => setState(() {
                      _target = target;
                      _count = 0;
                    }),
                  ),
                  const SizedBox(width: 8),
                ],
                AthkarChip(
                  label: context.tr('tasbih.free'),
                  selected: _target == null,
                  onTap: () => setState(() {
                    _target = null;
                    _count = 0;
                  }),
                ),
              ],
            ),

            const SizedBox(height: 26),
            Center(
              child: GestureDetector(
                onTap: _tap,
                child: Container(
                  width: 250,
                  height: 250,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: tokens.surface,
                    border: Border.all(color: tokens.border),
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        Numerals.format(_count, arabicIndic: digits),
                        style: AthkarType.sans(
                          size: 84,
                          color: tokens.brand,
                          weight: FontWeight.w300,
                          height: 1,
                        ),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        _target == null
                            ? context.tr('tasbih.freeHint')
                            : context.tr('tasbih.ofTarget', {
                                'target': Numerals.format(_target!, arabicIndic: digits),
                              }),
                        style: AthkarType.sans(size: 12.5, color: tokens.muted),
                      ),
                      const SizedBox(height: 18),
                      // A short bar rather than a ring around the circle: the
                      // design keeps the circle itself unbroken so the numeral
                      // stays the only thing in it.
                      SizedBox(
                        width: 120,
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(3),
                          child: LinearProgressIndicator(
                            value: progress,
                            minHeight: 4,
                            backgroundColor: tokens.border,
                            color: tokens.brand,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),

            const SizedBox(height: 24),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                OutlinedButton(
                  onPressed: _reset,
                  child: Text(context.tr('tasbih.reset')),
                ),
                const SizedBox(width: 10),
                OutlinedButton(
                  onPressed: () => settings.setHaptics(!settings.haptics),
                  child: Text(
                    '${context.tr('tasbih.vibrate')} · '
                    '${settings.haptics ? context.tr('common.on') : context.tr('common.off')}',
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _showHistory() => showModalBottomSheet<void>(
        context: context,
        builder: (context) => const _HistorySheet(),
      );

  /// Changes which dhikr is being counted.
  ///
  /// The counter follows the dhikr rather than the screen: each one keeps its
  /// own number, so a reader who switches to a different dhikr and back finds
  /// the count they left. That is why the stored value is read here instead of
  /// resetting to zero.
  Future<void> _chooseDhikr() async {
    final settings = SettingsScope.read(context);

    final chosen = await showModalBottomSheet<_DhikrChoice>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _DhikrPicker(selectedId: settings.tasbihDhikrId),
    );

    if (chosen == null || !mounted) return;

    await settings.setTasbihDhikr(chosen.id);
    if (!mounted) return;

    final name = chosen.id == null ? _defaultCounter : 'dhikr.${chosen.id}';
    final stored = AppStateScope.read(context).progress.tasbihCounters[name] ?? 0;
    setState(() => _count = stored);
  }

  void _tap() {
    final settings = SettingsScope.read(context);
    final state = AppStateScope.read(context);
    final next = _count + 1;

    // The heavier haptic at the target is what lets somebody count with the
    // phone in their pocket.
    final reachedTarget = _target != null && next % _target! == 0;

    if (settings.haptics) {
      reachedTarget ? HapticFeedback.mediumImpact() : HapticFeedback.selectionClick();
    }

    setState(() => _count = next);
    state.progress.setTasbihCounter(_counterName, next);
  }

  /// Confirmed, because a mistaken reset at ninety-eight is the one thing this
  /// screen can do that cannot be undone.
  Future<void> _reset() async {
    if (_count == 0) return;

    final confirmed = await AthkarAlerts.confirm(
      context,
      title: context.tr('tasbih.resetConfirm'),
      body: context.tr('tasbih.resetConfirmBody'),
      confirmLabel: context.tr('tasbih.reset'),
      cancelLabel: context.tr('common.cancel'),
      destructive: true,
    );

    if (!confirmed || !mounted) return;

    setState(() => _count = 0);
    await AppStateScope.read(context).progress.setTasbihCounter(_counterName, 0);
  }
}

/// What came back from the picker.
///
/// A class rather than a bare `int?` because the sheet has two different
/// nothings: dismissed without choosing, and chose the built-in dhikr — and
/// `null` can only mean one of them.
class _DhikrChoice {
  const _DhikrChoice(this.id);

  /// Null is the built-in «سبحان الله وبحمده».
  final int? id;
}

/// Which dhikr the counter counts.
///
/// Every row is a published dhikr out of the reader's own catalogue — the same
/// corpus the chapters are read from, with the same takhrij behind it. Nothing
/// here is compiled in, so an editor who adds a dhikr in the control panel adds
/// it to the counter too, without a release.
class _DhikrPicker extends StatefulWidget {
  const _DhikrPicker({required this.selectedId});

  final int? selectedId;

  @override
  State<_DhikrPicker> createState() => _DhikrPickerState();
}

class _DhikrPickerState extends State<_DhikrPicker> {
  var _query = '';

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final categories = AppStateScope.of(context).content.categories;

    // Short adhkar only. A counter is for something said tens of times, and a
    // twelve-line supplication in the circle would be unreadable at any font
    // size — the picker declining to offer it is kinder than the reader
    // discovering that after choosing it.
    const longest = 160;

    final matches = <(AthkarCategory, Dhikr)>[
      for (final category in categories)
        for (final dhikr in category.adhkar)
          if (dhikr.arabicText.length <= longest &&
              (_query.isEmpty ||
                  ArabicText.contains(dhikr.arabicText, _query) ||
                  ArabicText.contains(category.name, _query)))
            (category, dhikr),
    ];

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
        ),
        child: FractionallySizedBox(
          heightFactor: 0.85,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 10),
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
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
                child: Text(
                  context.tr('tasbih.choose'),
                  style: AthkarType.amiri(
                    size: 19, color: tokens.ink, weight: FontWeight.w700),
                ),
              ),
              const SizedBox(height: 12),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
                child: TextField(
                  onChanged: (value) => setState(() => _query = value),
                  decoration: InputDecoration(
                    hintText: context.tr('tasbih.searchDhikr'),
                    prefixIcon: Icon(Icons.search, size: 18, color: tokens.muted),
                  ),
                ),
              ),
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(
                    AthkarSpacing.page, 12, AthkarSpacing.page, 24),
                  children: [
                    if (_query.isEmpty)
                      _PickerRow(
                        text: _TasbihScreenState._defaultText,
                        note: context.tr('tasbih.default'),
                        selected: widget.selectedId == null,
                        fontScale: settings.fontScale,
                        onTap: () =>
                            Navigator.of(context).pop(const _DhikrChoice(null)),
                      ),
                    if (matches.isEmpty && _query.isNotEmpty)
                      AthkarEmptyState(
                        title: context.tr('tasbih.noMatch'),
                        body: context.tr('tasbih.noMatchHint'),
                        icon: Icons.search_off,
                      )
                    else
                      for (final (category, dhikr) in matches)
                        _PickerRow(
                          text: dhikr.arabicText,
                          note: category.name,
                          selected: widget.selectedId == dhikr.id,
                          fontScale: settings.fontScale,
                          onTap: () =>
                              Navigator.of(context).pop(_DhikrChoice(dhikr.id)),
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

class _PickerRow extends StatelessWidget {
  const _PickerRow({
    required this.text,
    required this.note,
    required this.selected,
    required this.fontScale,
    required this.onTap,
  });

  final String text;
  final String note;
  final bool selected;
  final double fontScale;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return AthkarCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  text,
                  style: AthkarType.amiri(
                    size: 17 * fontScale, color: tokens.ink, height: 1.8),
                ),
                const SizedBox(height: 6),
                Text(note, style: AthkarType.sans(size: 11.5, color: tokens.muted)),
              ],
            ),
          ),
          if (selected) ...[
            const SizedBox(width: 10),
            Icon(Icons.check_circle, size: 19, color: tokens.brand),
          ],
        ],
      ),
    );
  }
}

/// What the counters stand at, by name.
///
/// A sheet rather than a screen for the same reason the takhrij is one: the
/// reader is mid-dhikr and wants to glance, not navigate.
class _HistorySheet extends StatelessWidget {
  const _HistorySheet();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final state = AppStateScope.of(context);
    final counters = state.progress.tasbihCounters;

    final entries = counters.entries.where((entry) => entry.value > 0).toList()
      ..sort((a, b) => b.value.compareTo(a.value));

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 14, AthkarSpacing.page, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
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
            const SizedBox(height: 18),
            Text(
              context.tr('tasbih.history'),
              style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
            ),
            const SizedBox(height: 14),
            if (entries.isEmpty)
              AthkarEmptyState(
                title: context.tr('tasbih.history.empty'),
                body: context.tr('tasbih.history.emptyHint'),
                icon: Icons.history,
              )
            else
              for (final entry in entries)
                AthkarListRow(
                  label: _label(context, state, entry.key),
                  trailing: Text(
                    Numerals.format(entry.value, arabicIndic: settings.arabicNumerals),
                    style: AthkarType.sans(
                      size: 17, color: tokens.brand, weight: FontWeight.w600),
                  ),
                ),
          ],
        ),
      ),
    );
  }

  /// Names a stored counter.
  ///
  /// Three cases, in order: a dhikr the catalogue still carries is named by its
  /// own text; the built-in one by its translation key; and a dhikr an editor
  /// has since retired by a line that says so. The last is the one that matters
  /// — the reader's count is still real, and showing `dhikr.41` beside it would
  /// look like a bug rather than like a dhikr that was withdrawn.
  static String _label(BuildContext context, AppState state, String key) {
    if (key == _TasbihScreenState._defaultCounter) {
      return context.tr('tasbih.counter.$key');
    }

    if (key.startsWith('dhikr.')) {
      final id = int.tryParse(key.substring('dhikr.'.length));
      final dhikr = id == null ? null : state.content.dhikrById(id);
      if (dhikr != null) return dhikr.arabicText;
    }

    return context.tr('tasbih.counter.retired');
  }
}
