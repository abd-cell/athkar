import 'package:flutter/material.dart';

import '../core/bootstrap.dart';
import '../core/l10n.dart';
import '../core/mushaf.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';

/// Everything the reader can change.
///
/// Grouped the way the design's settings screen groups it: how it looks, how it
/// reads, and what the app keeps. Prayer-time settings are deliberately *not*
/// here — they live on the prayer screen, next to the times they change.
class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('settings.title'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 14, AthkarSpacing.page, 30),
        children: [
          _SectionLabel(context.tr('settings.appearance')),
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('settings.theme'),
                  value: switch (settings.themeMode) {
                    ThemeMode.light => context.tr('settings.theme.light'),
                    ThemeMode.dark => context.tr('settings.theme.dark'),
                    ThemeMode.system => context.tr('settings.theme.system'),
                  },
                  onTap: () => _pickTheme(context, settings),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('settings.language'),
                  value: settings.languageCode == 'ar' ? 'العربية' : 'English',
                  onTap: () => _pickLanguage(context, state),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('settings.numerals'),
                  value: settings.arabicNumerals
                      ? context.tr('settings.numerals.arabic')
                      : context.tr('settings.numerals.western'),
                  trailing: Switch(
                    value: settings.arabicNumerals,
                    onChanged: settings.setArabicNumerals,
                  ),
                ),
              ],
            ),
          ),

          const SizedBox(height: 16),
          _SectionLabel(context.tr('settings.fontSize')),
          AthkarCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // A live preview, because a number on a slider means nothing —
                // the reader is choosing how comfortable the text is to read.
                Text(
                  context.tr('settings.fontSizePreview'),
                  textAlign: TextAlign.center,
                  style: AthkarType.amiri(
                    size: 22 * settings.fontScale,
                    color: tokens.ink,
                    height: 2,
                  ),
                ),
                Slider(
                  value: settings.fontScale,
                  min: 0.8,
                  max: 1.8,
                  divisions: 10,
                  onChanged: settings.setFontScale,
                ),
                AthkarListRow(
                  label: context.tr('settings.showTashkeel'),
                  trailing: Switch(
                    value: settings.showTashkeel,
                    onChanged: settings.setShowTashkeel,
                  ),
                ),
                AthkarListRow(
                  label: context.tr('settings.showTranslation'),
                  trailing: Switch(
                    value: settings.showTranslation,
                    onChanged: settings.setShowTranslation,
                  ),
                ),
                AthkarListRow(
                  label: context.tr('settings.haptics'),
                  trailing: Switch(value: settings.haptics, onChanged: settings.setHaptics),
                ),
              ],
            ),
          ),

          const SizedBox(height: 16),
          const _QuranSection(),

          _SectionLabel(context.tr('settings.data')),
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('settings.sync'),
                  value: context.tr('settings.syncedVersion', {
                    'version': Numerals.format(
                      state.content.version ?? 0,
                      arabicIndic: settings.arabicNumerals,
                    ),
                  }),
                  trailing: state.isSyncing ? const AthkarSpinner(size: 18) : null,
                  onTap: state.isSyncing ? null : () => state.sync(),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('settings.forgetDevice'),
                  onTap: () => _forget(context),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text(
            context.tr('settings.forgetDeviceHint'),
            style: AthkarType.sans(size: 11, color: tokens.faint, height: 1.6),
          ),
        ],
      ),
    );
  }

  Future<void> _pickTheme(BuildContext context, Settings settings) async {
    final chosen = await showModalBottomSheet<ThemeMode>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final mode in ThemeMode.values)
              AthkarListRow(
                label: context.tr('settings.theme.${mode.name}'),
                onTap: () => Navigator.of(context).pop(mode),
              ),
            const SizedBox(height: 12),
          ],
        ),
      ),
    );

    if (chosen != null) await settings.setThemeMode(chosen);
  }

  /// Switching language re-syncs the catalogue and the reminder wording, which
  /// is why it goes through [AppState] rather than straight to the store.
  Future<void> _pickLanguage(BuildContext context, AppState state) async {
    final response = await Api.languages.list();
    final languages = response.data ?? const [];

    if (!context.mounted) return;

    final chosen = await showModalBottomSheet<String>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final language in languages)
              AthkarListRow(
                label: language.nativeName,
                value: language.englishName,
                onTap: () => Navigator.of(context).pop(language.code),
              ),
            // A failed call must not leave the reader unable to switch back to
            // a language the app ships with.
            if (languages.isEmpty) ...[
              AthkarListRow(label: 'العربية', onTap: () => Navigator.of(context).pop('ar')),
              AthkarListRow(label: 'English', onTap: () => Navigator.of(context).pop('en')),
            ],
            const SizedBox(height: 12),
          ],
        ),
      ),
    );

    if (chosen != null) await state.changeLanguage(chosen);
  }

  Future<void> _forget(BuildContext context) async {
    final confirmed = await AthkarAlerts.confirm(
      context,
      title: context.tr('settings.forgetConfirm'),
      body: context.tr('settings.forgetDeviceHint'),
      confirmLabel: context.tr('common.delete'),
      cancelLabel: context.tr('common.cancel'),
      destructive: true,
    );

    if (!confirmed || !context.mounted) return;

    final response = await Api.devices.forget();

    if (!context.mounted) return;
    response.success
        ? AthkarAlerts.toast(context, context.tr('common.ok'))
        : AthkarAlerts.error(context, response.errorMessage ?? context.tr('error.generic'));
  }
}

/// The mushaf, where the reader would look for it.
///
/// The form the Qur'an is read in — the printed page or flowing verses — was
/// only ever offered as a pair of chips at the top of the Qur'an tab, which is
/// a place a reader passes through rather than a place they go to change
/// something. It is the same setting, in both places.
///
/// The section is absent entirely when nothing has been downloaded: an empty
/// "0 MB · delete" row explains less than showing nothing does.
class _QuranSection extends StatefulWidget {
  const _QuranSection();

  @override
  State<_QuranSection> createState() => _QuranSectionState();
}

class _QuranSectionState extends State<_QuranSection> {
  var _hasPages = false;
  int? _size;

  /// The reader-facing name of the mushaf being read, once the shelf has been
  /// fetched. Null offline on a first run, where the slug stands in for it.
  String? _name;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _measure();
  }

  /// Both answers come off the file itself, so they are read once per visit
  /// rather than held in a store that would have to be kept true.
  Future<void> _measure() async {
    final state = AppStateScope.of(context);
    if (!state.quran.isSaved) {
      if (mounted && (_size != null || _hasPages)) {
        setState(() {
          _size = null;
          _hasPages = false;
        });
      }
      return;
    }

    final size = await state.quran.sizeOnDisk();
    final hasPages = await Mushaf.isAvailable(state.quran);

    if (mounted) {
      setState(() {
        _size = size;
        _hasPages = hasPages;
      });
    }

    // The name is worth a network call only because this row is about to offer
    // deleting the thing it names. It is allowed to fail: the slug is a worse
    // label, not a broken one.
    final shelf = await Api.quran.editions();
    final selected = state.quran.selectedEdition;

    for (final edition in shelf.data ?? const <QuranEdition>[]) {
      if (edition.edition == selected && mounted) {
        setState(() => _name = edition.name);
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    if (!state.quran.isSaved) return const SizedBox.shrink();

    final size = _size;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _SectionLabel(context.tr('settings.quran')),
        AthkarCard(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              // Offered only when the file carries the page layer. A package
              // without it has one way to be read, and a toggle with one
              // working position is a promise the file cannot keep.
              if (_hasPages) ...[
                AthkarListRow(
                  label: context.tr('settings.quran.view'),
                  value: settings.quranAsPages
                      ? context.tr('quran.view.pages')
                      : context.tr('quran.view.surahs'),
                  onTap: () => _pickView(context, settings),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
              ],
              // Which mushaf, by name. The section used to describe "the
              // mushaf" because there was only one; now the size and the delete
              // below it belong to a particular text, and saying which is the
              // difference between a clear row and a dangerous one.
              AthkarListRow(
                label: context.tr('settings.quran.mushaf'),
                value: _name ?? state.quran.selectedEdition ?? '—',
              ),
              const AthkarRule(margin: EdgeInsets.zero),
              AthkarListRow(
                label: context.tr('settings.quran.size'),
                value: size == null
                    ? '—'
                    : context.tr('settings.quran.megabytes', {
                        'size': Numerals.format(
                          (size / (1024 * 1024)).round(),
                          arabicIndic: settings.arabicNumerals,
                        ),
                      }),
              ),
              const AthkarRule(margin: EdgeInsets.zero),
              AthkarListRow(
                label: context.tr('settings.quran.delete'),
                onTap: () => _delete(context, state),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
      ],
    );
  }

  Future<void> _pickView(BuildContext context, Settings settings) async {
    final chosen = await showModalBottomSheet<bool>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            AthkarListRow(
              label: context.tr('quran.view.pages'),
              value: context.tr('settings.quran.pagesHint'),
              onTap: () => Navigator.of(context).pop(true),
            ),
            AthkarListRow(
              label: context.tr('quran.view.surahs'),
              value: context.tr('settings.quran.surahsHint'),
              onTap: () => Navigator.of(context).pop(false),
            ),
            const SizedBox(height: 12),
          ],
        ),
      ),
    );

    if (chosen != null) await settings.setQuranAsPages(chosen);
  }

  /// Confirmed, and worth confirming: this is a download measured in hundreds
  /// of megabytes that has to be made again over whatever connection the reader
  /// is on.
  Future<void> _delete(BuildContext context, AppState state) async {
    final confirmed = await AthkarAlerts.confirm(
      context,
      title: context.tr('settings.quran.deleteConfirm'),
      body: context.tr('settings.quran.deleteConfirmBody'),
      confirmLabel: context.tr('common.delete'),
      cancelLabel: context.tr('common.cancel'),
      destructive: true,
    );

    if (!confirmed) return;

    final edition = state.quran.selectedEdition;
    if (edition == null) return;

    await state.forgetQuran(edition);
    if (mounted) setState(() => _size = null);
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 8, right: 2, left: 2),
        child: Text(
          text,
          style: AthkarType.sans(size: 11.5, color: AthkarTokens.of(context).muted),
        ),
      );
}
