import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/recitation_downloads.dart';
import '../../core/recitation_player.dart';
import '../../core/settings.dart';
import '../../core/surah_names.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'player_screen.dart';

/// «١٩:٢٨», or «١:٠٢:٠٥» past the hour.
String listeningClock(Duration value, {required bool arabicIndic}) {
  final clamped = value.isNegative ? Duration.zero : value;
  final hours = clamped.inHours;
  final minutes = clamped.inMinutes.remainder(60).toString().padLeft(2, '0');
  final seconds = clamped.inSeconds.remainder(60).toString().padLeft(2, '0');
  final text = hours > 0 ? '$hours:$minutes:$seconds' : '$minutes:$seconds';
  return Numerals.format(text, arabicIndic: arabicIndic);
}

/// A surah's name in the reader's language — the only place the listening
/// screens read [surahNames], so the Arabic and English lists stay one list.
String surahTitle(BuildContext context, int surah) {
  final name = surahName(surah);
  if (name == null) return '$surah';
  return SettingsScope.of(context).languageCode == 'ar' ? name.arabic : name.english;
}

/// Starts [surah] and opens the full player. What the publisher can see when
/// a surah streams from its servers is explained in the FAQ (Privacy), not in a
/// dialog in front of every first listen.
Future<void> startListening(
  BuildContext context,
  Reciter reciter,
  Recitation recitation,
  int surah, {
  List<int>? queue,
  Duration? startAt,
  bool openPlayer = true,
}) async {
  await RecitationPlayer.instance.play(
    reciter,
    recitation,
    surah,
    queue: queue,
    startAt: startAt,
    languageCode: SettingsScope.of(context).languageCode,
  );

  if (openPlayer && context.mounted) {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PlayerScreen()));
  }
}

/// A reciter's portrait — drawn only when an editor has set one.
///
/// With no portrait nothing is drawn at all, not a placeholder: a row of
/// identical initials in tinted squares says nothing the name beside it does
/// not, and most reciters have no picture (the publisher supplies none, and an
/// editor adds one only when they have the right to). Use [maybe] where the
/// portrait is optional furniture, so a missing one leaves no gap.
class ReciterAvatar extends StatelessWidget {
  const ReciterAvatar({super.key, required this.reciter, this.size = 56, this.radius = 16});

  final Reciter reciter;
  final double size;
  final double radius;

  static bool hasPortrait(Reciter reciter) => (reciter.imageUrl ?? '').trim().isNotEmpty;

  /// The portrait, or null when there is none — for a `leading:` slot.
  static Widget? maybe(Reciter reciter, {double size = 56, double radius = 16}) =>
      hasPortrait(reciter) ? ReciterAvatar(reciter: reciter, size: size, radius: radius) : null;

  @override
  Widget build(BuildContext context) {
    if (!hasPortrait(reciter)) return const SizedBox.shrink();

    return ClipRRect(
      borderRadius: BorderRadius.circular(radius),
      child: Image.network(
        reciter.imageUrl!.trim(),
        width: size,
        height: size,
        fit: BoxFit.cover,
        // A picture that fails to load is simply not shown.
        errorBuilder: (_, __, ___) => SizedBox(width: size, height: size),
      ),
    );
  }
}

/// The download control on a surah row: an arrow, a ring while it downloads
/// (tap to cancel), a filled mark once saved (tap to delete).
class SurahDownloadButton extends StatelessWidget {
  const SurahDownloadButton({super.key, required this.recitation, required this.surah});

  final Recitation recitation;
  final int surah;

  @override
  Widget build(BuildContext context) {
    final downloads = RecitationDownloads.instance;
    if (!downloads.isSupported) return const SizedBox.shrink();

    return ListenableBuilder(
      listenable: downloads,
      builder: (context, _) {
        final tokens = AthkarTokens.of(context);
        final progress = downloads.progressOf(recitation.id, surah);

        if (progress != null) {
          return IconButton(
            tooltip: context.tr('listen.download.cancel'),
            onPressed: () => downloads.cancel(recitation.id, surah),
            icon: SizedBox(
              width: 22,
              height: 22,
              child: CustomPaint(
                painter: _RingPainter(
                  value: progress < 0 ? null : progress,
                  track: tokens.border,
                  color: tokens.brand,
                ),
                child: Icon(Icons.close, size: 12, color: tokens.brand),
              ),
            ),
          );
        }

        if (downloads.isSaved(recitation.id, surah)) {
          return IconButton(
            tooltip: context.tr('listen.download.delete'),
            onPressed: () async {
              final confirmed = await AthkarAlerts.confirm(
                context,
                title: context.tr('listen.download.deleteTitle'),
                body: context.tr('listen.download.deleteBody', {'surah': surahTitle(context, surah)}),
                confirmLabel: context.tr('listen.download.delete'),
                destructive: true,
              );
              if (confirmed) await downloads.delete(recitation.id, surah);
            },
            icon: Icon(Icons.download_done_rounded, color: tokens.brand),
          );
        }

        return IconButton(
          tooltip: context.tr('listen.download'),
          onPressed: () async {
            final ok = await downloads.download(recitation, surah);
            if (!ok && context.mounted && !downloads.isSaved(recitation.id, surah)) {
              AthkarAlerts.error(context, context.tr('listen.download.failed'));
            }
          },
          icon: Icon(Icons.download_outlined, color: tokens.muted),
        );
      },
    );
  }
}

/// A determinate ring for a download. Drawn rather than a
/// `CircularProgressIndicator`, which this app reserves for [AthkarSpinner].
class _RingPainter extends CustomPainter {
  _RingPainter({required this.value, required this.track, required this.color});

  final double? value;
  final Color track;
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    final stroke = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.2
      ..strokeCap = StrokeCap.round;

    canvas.drawArc(rect.deflate(1.5), 0, 6.283, false, stroke..color = track);

    // Unknown size: a quarter arc, so it reads as "working" without claiming
    // a percentage it does not have.
    final sweep = value == null ? 1.57 : 6.283 * value!.clamp(0.0, 1.0);
    canvas.drawArc(rect.deflate(1.5), -1.5708, sweep, false, stroke..color = color);
  }

  @override
  bool shouldRepaint(_RingPainter old) => old.value != value || old.color != color;
}

/// The bar that follows the reader around while a recitation is on: what is
/// playing, play/pause, and ✕ to end it. Tapping it opens the full player.
class MiniPlayer extends StatelessWidget {
  const MiniPlayer({super.key});

  @override
  Widget build(BuildContext context) {
    final player = RecitationPlayer.instance;

    return ListenableBuilder(
      listenable: player,
      builder: (context, _) {
        final reciter = player.reciter;
        final surah = player.surah;
        if (!player.isActive || reciter == null || surah == null) return const SizedBox.shrink();

        final tokens = AthkarTokens.of(context);

        return Material(
          color: tokens.surface,
          child: InkWell(
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const PlayerScreen()),
            ),
            child: Container(
              decoration: BoxDecoration(border: Border(top: BorderSide(color: tokens.hairline))),
              padding: const EdgeInsets.fromLTRB(12, 8, 8, 8),
              child: Row(
                children: [
                  if (ReciterAvatar.hasPortrait(reciter)) ...[
                    ReciterAvatar(reciter: reciter, size: 40, radius: 10),
                    const SizedBox(width: 10),
                  ],
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          surahTitle(context, surah),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AthkarType.amiri(size: 16, color: tokens.ink, weight: FontWeight.w700),
                        ),
                        Text(
                          player.failed ? context.tr('listen.failed') : reciter.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AthkarType.sans(
                            size: 11.5,
                            color: player.failed ? tokens.alert : tokens.muted,
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (player.isLoading)
                    const Padding(padding: EdgeInsets.all(12), child: AthkarSpinner(size: 20))
                  else
                    IconButton(
                      tooltip: context.tr(player.isPlaying ? 'listen.pause' : 'listen.play'),
                      onPressed: player.toggle,
                      icon: Icon(
                        player.isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded,
                        color: tokens.brand,
                        size: 30,
                      ),
                    ),
                  IconButton(
                    tooltip: context.tr('listen.stop'),
                    onPressed: player.stop,
                    icon: Icon(Icons.close_rounded, color: tokens.muted),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

/// «تفعيل المؤقت»: stop after a while, at a time on the clock, or when this
/// surah ends.
Future<void> showSleepTimerSheet(BuildContext context) async {
  final player = RecitationPlayer.instance;
  final arabicIndic = SettingsScope.of(context).arabicNumerals;

  await showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    builder: (sheet) {
      final tokens = AthkarTokens.of(sheet);

      Widget option(String label, VoidCallback onTap, {IconData icon = Icons.timer_outlined}) =>
          ListTile(
            leading: Icon(icon, color: tokens.brand),
            title: Text(label, style: AthkarType.sans(size: 14, color: tokens.ink)),
            onTap: () {
              onTap();
              Navigator.of(sheet).pop();
            },
          );

      return SafeArea(
        child: ListenableBuilder(
          listenable: player,
          builder: (sheet, _) => Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 0, 24, 8),
                child: Text(
                  context.tr('listen.timer.title'),
                  style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
                ),
              ),
              if (player.hasSleepTimer)
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 0, 24, 8),
                  child: Text(
                    player.sleepsAtEndOfSurah
                        ? context.tr('listen.timer.activeEnd')
                        : context.tr('listen.timer.activeAt', {
                            'time': Numerals.time(
                              (player.sleepAt!.hour % 12 == 0 ? 12 : player.sleepAt!.hour % 12),
                              player.sleepAt!.minute,
                              arabicIndic: arabicIndic,
                            ),
                          }),
                    style: AthkarType.sans(size: 12.5, color: tokens.brandInk),
                  ),
                ),
              for (final minutes in const [15, 30, 45, 60])
                option(
                  context.tr('listen.timer.after', {
                    'minutes': Numerals.format(minutes, arabicIndic: arabicIndic),
                  }),
                  () => player.sleepAfter(Duration(minutes: minutes)),
                ),
              option(
                context.tr('listen.timer.endOfSurah'),
                player.sleepAtEndOfSurah,
                icon: Icons.stop_circle_outlined,
              ),
              ListTile(
                leading: Icon(Icons.schedule_outlined, color: tokens.brand),
                title: Text(context.tr('listen.timer.atClock'),
                    style: AthkarType.sans(size: 14, color: tokens.ink)),
                onTap: () async {
                  final now = TimeOfDay.now();
                  final picked = await showTimePicker(
                    context: sheet,
                    initialTime: TimeOfDay(hour: (now.hour + 1) % 24, minute: now.minute),
                  );
                  if (picked != null) player.sleepAtClock(picked);
                  if (sheet.mounted) Navigator.of(sheet).pop();
                },
              ),
              if (player.hasSleepTimer)
                option(
                  context.tr('listen.timer.cancel'),
                  player.cancelSleepTimer,
                  icon: Icons.timer_off_outlined,
                ),
              const SizedBox(height: 8),
            ],
          ),
        ),
      );
    },
  );
}

/// «التحكم بالسرعة».
Future<void> showSpeedSheet(BuildContext context) async {
  final player = RecitationPlayer.instance;
  final arabicIndic = SettingsScope.of(context).arabicNumerals;

  await showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    builder: (sheet) {
      final tokens = AthkarTokens.of(sheet);
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                context.tr('listen.speed.title'),
                style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
              ),
              const SizedBox(height: 14),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                alignment: WrapAlignment.center,
                children: [
                  for (final speed in RecitationPlayer.speeds)
                    AthkarChip(
                      label: '${Numerals.format(speed.toString().replaceAll(RegExp(r'\.0$'), ''), arabicIndic: arabicIndic)}×',
                      selected: player.speed == speed,
                      onTap: () {
                        player.setSpeed(speed);
                        Navigator.of(sheet).pop();
                      },
                    ),
                ],
              ),
            ],
          ),
        ),
      );
    },
  );
}

/// «تكرار مخصص»: ayat A to B, N times. Only offered when the recording has
/// ayah timing — there is no other way to know where an ayah begins.
Future<void> showRangeSheet(BuildContext context) async {
  final player = RecitationPlayer.instance;
  final timings = player.timings;
  final surah = player.surah;
  if (timings == null || timings.isEmpty || surah == null) return;

  final arabicIndic = SettingsScope.of(context).arabicNumerals;
  final first = timings.first.ayah, last = timings.last.ayah;
  var from = player.ayah ?? first;
  var to = from;
  var times = 3;

  await showModalBottomSheet<void>(
    context: context,
    showDragHandle: true,
    isScrollControlled: true,
    builder: (sheet) {
      final tokens = AthkarTokens.of(sheet);

      return StatefulBuilder(
        builder: (sheet, setState) {
          Widget stepper(String label, int value, int min, int max, ValueChanged<int> onChanged) => Row(
                children: [
                  Expanded(child: Text(label, style: AthkarType.sans(size: 14, color: tokens.ink))),
                  IconButton(
                    onPressed: value > min ? () => setState(() => onChanged(value - 1)) : null,
                    icon: const Icon(Icons.remove_circle_outline),
                  ),
                  SizedBox(
                    width: 44,
                    child: Text(
                      Numerals.format(value, arabicIndic: arabicIndic),
                      textAlign: TextAlign.center,
                      style: AthkarType.sans(size: 16, color: tokens.ink, weight: FontWeight.w600),
                    ),
                  ),
                  IconButton(
                    onPressed: value < max ? () => setState(() => onChanged(value + 1)) : null,
                    icon: const Icon(Icons.add_circle_outline),
                  ),
                ],
              );

          return SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    context.tr('listen.range.title'),
                    textAlign: TextAlign.center,
                    style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    surahTitle(context, surah),
                    textAlign: TextAlign.center,
                    style: AthkarType.sans(size: 12.5, color: tokens.muted),
                  ),
                  const SizedBox(height: 10),
                  stepper(context.tr('listen.range.from'), from, first, last, (v) {
                    from = v;
                    if (to < from) to = from;
                  }),
                  stepper(context.tr('listen.range.to'), to, from, last, (v) => to = v),
                  stepper(context.tr('listen.range.times'), times, 1, 99, (v) => times = v),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () {
                      player.setRange(from, to, times: times);
                      Navigator.of(sheet).pop();
                    },
                    child: Text(context.tr('listen.range.apply')),
                  ),
                  if (player.range != null)
                    TextButton(
                      onPressed: () {
                        player.clearRange();
                        Navigator.of(sheet).pop();
                      },
                      child: Text(context.tr('listen.range.clear')),
                    ),
                ],
              ),
            ),
          );
        },
      );
    },
  );
}
