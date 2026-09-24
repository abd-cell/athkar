import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/quran_library.dart';
import '../../core/recitation_library.dart';
import '../../core/recitation_player.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../widgets/athkar_ui.dart';
import 'listen_widgets.dart';

/// The full player: what is being recited, where in it, and every control.
///
/// When the recording has ayah timing, the ayah being recited is named — and
/// shown in full when the reader has the mushaf on the device, since its text
/// is then already here and fetching it from anywhere else would be both
/// slower and a second source for the same words. When there is no timing the
/// screen says so plainly and hides the controls that would need it, rather
/// than offering a verse tracker that cannot track.
class PlayerScreen extends StatefulWidget {
  const PlayerScreen({super.key});

  @override
  State<PlayerScreen> createState() => _PlayerScreenState();
}

class _PlayerScreenState extends State<PlayerScreen> {
  final _player = RecitationPlayer.instance;

  /// The surah's ayat from the saved mushaf, keyed by the surah they belong to.
  /// Empty when the reader has not downloaded a mushaf.
  var _ayahsOf = 0;
  List<Ayah> _ayahs = const [];

  /// While the thumb is being dragged, the slider shows where it is going
  /// rather than where the audio is.
  double? _dragging;

  Future<void> _loadAyahs(int surah) async {
    if (_ayahsOf == surah) return;
    _ayahsOf = surah;

    final ayahs = await QuranLibrary.ayahs(AppStateScope.read(context).quran, surah);
    if (mounted && _ayahsOf == surah) setState(() => _ayahs = ayahs);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final arabicIndic = SettingsScope.of(context).arabicNumerals;

    return ListenableBuilder(
      listenable: Listenable.merge([_player, RecitationLibrary.instance]),
      builder: (context, _) {
        final reciter = _player.reciter;
        final recitation = _player.recitation;
        final surah = _player.surah;

        if (!_player.isActive || reciter == null || recitation == null || surah == null) {
          return Scaffold(
            backgroundColor: tokens.paper,
            appBar: AppBar(backgroundColor: tokens.paper),
            body: AthkarEmptyState(
              title: context.tr('listen.player.idle'),
              icon: Icons.headphones_outlined,
            ),
          );
        }

        if (_player.hasTiming) _loadAyahs(surah);

        final ref = _player.ref!;
        final bookmarked = RecitationLibrary.instance.isBookmarked(ref);
        final ayahNumber = _player.ayah;
        final ayahText = ayahNumber == null
            ? null
            : _ayahs.where((a) => a.surahId == surah && a.number == ayahNumber).firstOrNull?.text;

        return Scaffold(
          backgroundColor: tokens.paper,
          appBar: AppBar(
            backgroundColor: tokens.paper,
            leading: IconButton(
              tooltip: context.tr('common.close'),
              icon: const Icon(Icons.keyboard_arrow_down_rounded, size: 30),
              onPressed: () => Navigator.of(context).pop(),
            ),
            title: Text(
              _player.hasTiming ? '' : context.tr('listen.timing.none'),
              style: AthkarType.sans(size: 12, color: tokens.muted),
            ),
            centerTitle: true,
            actions: [
              IconButton(
                tooltip: context.tr(bookmarked ? 'listen.bookmark.remove' : 'listen.bookmark.add'),
                onPressed: () => RecitationLibrary.instance.toggleBookmark(ref),
                icon: Icon(
                  bookmarked ? Icons.bookmark_rounded : Icons.bookmark_border_rounded,
                  color: bookmarked ? tokens.brand : tokens.ink,
                ),
              ),
            ],
          ),
          body: SafeArea(
            top: false,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 8, AthkarSpacing.page, 24),
              children: [
                // ── the surah, as a title card ──
                Center(
                  child: Container(
                    width: 200,
                    height: 200,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: tokens.brand,
                      borderRadius: BorderRadius.circular(28),
                    ),
                    padding: const EdgeInsets.all(16),
                    child: Text(
                      RecitationPlayer.titleOf(surah, 'ar'),
                      textAlign: TextAlign.center,
                      style: AthkarType.amiri(size: 30, color: tokens.onBrand, weight: FontWeight.w700, height: 1.6),
                    ),
                  ),
                ),
                const SizedBox(height: 18),

                // ── the ayah being recited ──
                if (_player.hasTiming)
                  _AyahPanel(
                    number: ayahNumber,
                    text: ayahText,
                    loading: _player.timingLoading,
                    available: _player.timings != null,
                    arabicIndic: arabicIndic,
                  ),
                if (_player.hasTiming) const SizedBox(height: 14),

                // ── the tools ──
                Row(
                  children: [
                    Expanded(
                      child: _ToolButton(
                        icon: _player.hasSleepTimer ? Icons.timer_rounded : Icons.timer_outlined,
                        label: context.tr(_player.hasSleepTimer ? 'listen.timer.on' : 'listen.timer'),
                        active: _player.hasSleepTimer,
                        onTap: () => showSleepTimerSheet(context),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _ToolButton(
                        icon: Icons.speed_rounded,
                        label: _player.speed == 1.0
                            ? context.tr('listen.speed')
                            : '${Numerals.format(_player.speed.toString().replaceAll(RegExp(r'\.0$'), ''), arabicIndic: arabicIndic)}×',
                        active: _player.speed != 1.0,
                        onTap: () => showSpeedSheet(context),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    _ToolButton(
                      icon: switch (_player.repeat) {
                        RecitationRepeat.off => Icons.repeat_rounded,
                        RecitationRepeat.surah => Icons.repeat_one_rounded,
                        RecitationRepeat.all => Icons.repeat_on_rounded,
                      },
                      label: context.tr('listen.repeat.${_player.repeat.name}'),
                      active: _player.repeat != RecitationRepeat.off,
                      onTap: _player.cycleRepeat,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: _ToolButton(
                        icon: Icons.format_list_numbered_rtl_rounded,
                        label: _player.range == null
                            ? context.tr('listen.range')
                            : context.tr('listen.range.active', {
                                'from': Numerals.format(_player.range!.from, arabicIndic: arabicIndic),
                                'to': Numerals.format(_player.range!.to, arabicIndic: arabicIndic),
                              }),
                        active: _player.range != null,
                        onTap: _player.timings == null ? null : () => showRangeSheet(context),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 22),

                // ── what, and by whom ──
                Text(
                  surahTitle(context, surah),
                  style: AthkarType.amiri(size: 24, color: tokens.ink, weight: FontWeight.w700),
                ),
                const SizedBox(height: 2),
                Text(reciter.name, style: AthkarType.sans(size: 14, color: tokens.muted)),
                Text(recitation.name, style: AthkarType.sans(size: 12, color: tokens.faint)),
                const SizedBox(height: 14),

                // ── where in it ──
                // Left to right in every language, with the transport below it:
                // a timeline runs the way a media player's always has, and the
                // skip buttons must point the same way the bar fills.
                Directionality(
                  textDirection: TextDirection.ltr,
                  child: StreamBuilder<Duration>(
                  stream: _player.positionStream,
                  builder: (context, snapshot) {
                    final position = snapshot.data ?? _player.position;
                    final length = _player.duration ?? Duration.zero;
                    final max = length.inMilliseconds.toDouble();
                    final value = (_dragging ?? position.inMilliseconds.toDouble()).clamp(0.0, max <= 0 ? 1.0 : max);

                    return Column(
                      children: [
                        SliderTheme(
                          data: SliderTheme.of(context).copyWith(
                            trackHeight: 5,
                            thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 7),
                          ),
                          child: Slider(
                            value: value,
                            max: max <= 0 ? 1.0 : max,
                            activeColor: tokens.brand,
                            inactiveColor: tokens.border,
                            onChanged: max <= 0 ? null : (v) => setState(() => _dragging = v),
                            onChangeEnd: max <= 0
                                ? null
                                : (v) {
                                    setState(() => _dragging = null);
                                    _player.seek(Duration(milliseconds: v.round()));
                                  },
                          ),
                        ),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 8),
                          child: Row(
                            children: [
                              Text(
                                listeningClock(Duration(milliseconds: value.round()), arabicIndic: arabicIndic),
                                style: AthkarType.sans(size: 12, color: tokens.muted),
                              ),
                              const Spacer(),
                              Text(
                                '-${listeningClock(length - Duration(milliseconds: value.round()), arabicIndic: arabicIndic)}',
                                style: AthkarType.sans(size: 12, color: tokens.muted),
                              ),
                            ],
                          ),
                        ),
                      ],
                    );
                  },
                  ),
                ),
                const SizedBox(height: 12),

                // ── the transport ── (left to right, like the timeline above)
                Directionality(
                  textDirection: TextDirection.ltr,
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      IconButton(
                        tooltip: context.tr('listen.previous'),
                        iconSize: 34,
                        onPressed: _player.previous,
                        icon: Icon(Icons.skip_previous_rounded, color: tokens.brand),
                      ),
                      IconButton(
                        tooltip: context.tr('listen.back10'),
                        iconSize: 28,
                        onPressed: () => _player.seekBy(const Duration(seconds: -10)),
                        icon: Icon(Icons.replay_10_rounded, color: tokens.ink),
                      ),
                      SizedBox(
                        width: 72,
                        height: 72,
                        child: _player.isLoading
                            ? const Center(child: AthkarSpinner(size: 30))
                            : IconButton.filled(
                                tooltip: context.tr(_player.isPlaying ? 'listen.pause' : 'listen.play'),
                                style: IconButton.styleFrom(backgroundColor: tokens.brand),
                                iconSize: 40,
                                onPressed: _player.toggle,
                                icon: Icon(
                                  _player.isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded,
                                  color: tokens.onBrand,
                                ),
                              ),
                      ),
                      IconButton(
                        tooltip: context.tr('listen.forward10'),
                        iconSize: 28,
                        onPressed: () => _player.seekBy(const Duration(seconds: 10)),
                        icon: Icon(Icons.forward_10_rounded, color: tokens.ink),
                      ),
                      IconButton(
                        tooltip: context.tr('listen.next'),
                        iconSize: 34,
                        onPressed: _player.hasNext ? _player.next : null,
                        icon: Icon(Icons.skip_next_rounded, color: _player.hasNext ? tokens.brand : tokens.faint),
                      ),
                    ],
                  ),
                ),

                if (_player.failed) ...[
                  const SizedBox(height: 12),
                  Text(
                    context.tr('listen.failed'),
                    textAlign: TextAlign.center,
                    style: AthkarType.sans(size: 13, color: tokens.alert),
                  ),
                  TextButton(onPressed: _player.toggle, child: Text(context.tr('listen.retry'))),
                ],

              ],
            ),
          ),
        );
      },
    );
  }
}

class _AyahPanel extends StatelessWidget {
  const _AyahPanel({
    required this.number,
    required this.text,
    required this.loading,
    required this.available,
    required this.arabicIndic,
  });

  final int? number;
  final String? text;
  final bool loading;
  final bool available;
  final bool arabicIndic;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final String label;
    if (loading) {
      label = context.tr('listen.tracking.loading');
    } else if (!available) {
      label = context.tr('listen.tracking.unavailable');
    } else if (number == null) {
      label = context.tr('listen.tracking.before');
    } else {
      label = context.tr('listen.tracking.ayah', {
        'ayah': Numerals.format(number!, arabicIndic: arabicIndic),
      });
    }

    return AthkarCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(label, style: AthkarType.sans(size: 12, color: tokens.brandInk, weight: FontWeight.w600)),
          if (text != null) ...[
            const SizedBox(height: 8),
            Text(
              text!,
              textDirection: TextDirection.rtl,
              style: AthkarType.amiri(size: 21, color: tokens.ink, height: 1.9),
            ),
          ],
        ],
      ),
    );
  }
}

class _ToolButton extends StatelessWidget {
  const _ToolButton({required this.icon, required this.label, required this.onTap, this.active = false});

  final IconData icon;
  final String label;
  final VoidCallback? onTap;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final enabled = onTap != null;
    final color = !enabled ? tokens.faint : (active ? tokens.brand : tokens.ink);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        constraints: const BoxConstraints(minHeight: 46),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        decoration: BoxDecoration(
          color: active ? tokens.brandTint : tokens.surface,
          border: Border.all(color: active ? tokens.brand : tokens.border),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 19, color: color),
            const SizedBox(width: 8),
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: AthkarType.sans(size: 13, color: color, weight: FontWeight.w600),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
