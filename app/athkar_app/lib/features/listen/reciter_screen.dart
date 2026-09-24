import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/recitation_downloads.dart';
import '../../core/recitation_library.dart';
import '../../core/recitation_player.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../models/models.dart';
import 'listen_widgets.dart';
import 'player_screen.dart';

/// One reciter: his recordings, and the surahs of the one chosen.
///
/// When he has more than one recording the choice is shown at the top, riwaya
/// and all — «حفص عن عاصم - مرتل», «ورش عن نافع» — because they are different
/// readings of the text, not different qualities of the same file, and the
/// reader must choose one knowingly.
class ReciterScreen extends StatefulWidget {
  const ReciterScreen({super.key, required this.reciter, this.initialRecitationId});

  final Reciter reciter;
  final int? initialRecitationId;

  @override
  State<ReciterScreen> createState() => _ReciterScreenState();
}

class _ReciterScreenState extends State<ReciterScreen> {
  late Recitation _recitation = widget.reciter.recitations.firstWhere(
    (r) => r.id == widget.initialRecitationId,
    orElse: () => widget.reciter.recitations.first,
  );

  Future<void> _chooseRecitation() async {
    final chosen = await showModalBottomSheet<Recitation>(
      context: context,
      showDragHandle: true,
      builder: (sheet) {
        final tokens = AthkarTokens.of(sheet);
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                context.tr('listen.recitation.choose'),
                style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
              ),
              const SizedBox(height: 8),
              for (final recitation in widget.reciter.recitations)
                ListTile(
                  title: Text(recitation.name, style: AthkarType.sans(size: 14, color: tokens.ink)),
                  subtitle: Text(
                    context.tr(recitation.hasTiming ? 'listen.timing.yes' : 'listen.timing.none'),
                    style: AthkarType.sans(size: 11.5, color: tokens.muted),
                  ),
                  trailing: recitation.id == _recitation.id
                      ? Icon(Icons.check_rounded, color: tokens.brand)
                      : null,
                  onTap: () => Navigator.of(sheet).pop(recitation),
                ),
              const SizedBox(height: 8),
            ],
          ),
        );
      },
    );

    if (chosen != null) setState(() => _recitation = chosen);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final arabicIndic = SettingsScope.of(context).arabicNumerals;
    final reciter = widget.reciter;
    final recitation = _recitation;

    return Scaffold(
      backgroundColor: tokens.paper,
      appBar: AppBar(backgroundColor: tokens.paper),
      bottomNavigationBar: const MiniPlayer(),
      body: ListenableBuilder(
        listenable: Listenable.merge([
          RecitationPlayer.instance,
          RecitationLibrary.instance,
          RecitationDownloads.instance,
        ]),
        builder: (context, _) {
          final player = RecitationPlayer.instance;

          return CustomScrollView(
            slivers: [
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 0, AthkarSpacing.page, 12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          if (ReciterAvatar.hasPortrait(reciter)) ...[
                            ReciterAvatar(reciter: reciter, size: 76, radius: 20),
                            const SizedBox(width: 14),
                          ],
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  reciter.name,
                                  style: AthkarType.amiri(size: 24, color: tokens.ink, weight: FontWeight.w700),
                                ),
                                Text(recitation.name, style: AthkarType.sans(size: 12.5, color: tokens.muted)),
                              ],
                            ),
                          ),
                        ],
                      ),
                      if (reciter.recitations.length > 1) ...[
                        const SizedBox(height: 12),
                        OutlinedButton.icon(
                          onPressed: _chooseRecitation,
                          icon: const Icon(Icons.unfold_more_rounded, size: 18),
                          label: Text(context.tr(reciter.recitations.length == 2 ? 'listen.recitation.two' : 'listen.recitation.count', {
                            'count': Numerals.format(reciter.recitations.length, arabicIndic: arabicIndic),
                          })),
                        ),
                      ],
                      const SizedBox(height: 14),
                      Row(
                        children: [
                          Expanded(
                            child: FilledButton.icon(
                              onPressed: recitation.surahs.isEmpty
                                  ? null
                                  : () => startListening(context, reciter, recitation, recitation.surahs.first),
                              icon: const Icon(Icons.play_arrow_rounded),
                              label: Text(context.tr('listen.playAll')),
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: OutlinedButton.icon(
                              // The timer governs whatever is playing, so it is
                              // offered once something is.
                              onPressed: player.isActive ? () => showSleepTimerSheet(context) : null,
                              icon: Icon(player.hasSleepTimer ? Icons.timer_rounded : Icons.timer_outlined),
                              label: Text(context.tr(player.hasSleepTimer ? 'listen.timer.on' : 'listen.timer')),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      Text(
                        context.tr(recitation.hasTiming ? 'listen.timing.yes' : 'listen.timing.none'),
                        style: AthkarType.sans(size: 11.5, color: tokens.muted),
                      ),
                    ],
                  ),
                ),
              ),
              SliverList.separated(
                itemCount: recitation.surahs.length,
                separatorBuilder: (_, __) => Divider(height: 1, color: tokens.hairline, indent: 20, endIndent: 20),
                itemBuilder: (context, index) {
                  final surah = recitation.surahs[index];
                  final ref = SurahRef(reciterKey: reciter.key, recitationId: recitation.id, surah: surah);
                  final current = player.isCurrent(recitation, surah);
                  final bookmarked = RecitationLibrary.instance.isBookmarked(ref);

                  return ListTile(
                    contentPadding: const EdgeInsetsDirectional.only(start: 20, end: 8),
                    leading: SizedBox(
                      width: 34,
                      child: current && player.isPlaying
                          ? Icon(Icons.graphic_eq_rounded, color: tokens.brand)
                          : Text(
                              Numerals.format(surah.toString().padLeft(2, '0'), arabicIndic: arabicIndic),
                              style: AthkarType.sans(size: 13.5, color: current ? tokens.brand : tokens.muted),
                            ),
                    ),
                    title: Text(
                      surahTitle(context, surah),
                      style: AthkarType.amiri(
                        size: 18,
                        color: current ? tokens.brand : tokens.ink,
                        weight: current ? FontWeight.w700 : FontWeight.w400,
                      ),
                    ),
                    onTap: () => current
                        ? Navigator.of(context).push(
                            MaterialPageRoute(builder: (_) => const PlayerScreen()),
                          )
                        : startListening(context, reciter, recitation, surah),
                    trailing: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        IconButton(
                          tooltip: context.tr(bookmarked ? 'listen.bookmark.remove' : 'listen.bookmark.add'),
                          onPressed: () => RecitationLibrary.instance.toggleBookmark(ref),
                          icon: Icon(
                            bookmarked ? Icons.bookmark_rounded : Icons.bookmark_border_rounded,
                            color: bookmarked ? tokens.brand : tokens.muted,
                          ),
                        ),
                        SurahDownloadButton(recitation: recitation, surah: surah),
                      ],
                    ),
                  );
                },
              ),
              const SliverToBoxAdapter(child: SizedBox(height: 24)),
            ],
          );
        },
      ),
    );
  }
}
