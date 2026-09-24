import 'package:flutter/material.dart';

import '../../core/arabic_text.dart';
import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/recitation_library.dart';
import '../../core/recitation_player.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../widgets/athkar_ui.dart';
import 'listen_saved_screen.dart';
import 'listen_widgets.dart';
import 'reciter_screen.dart';

/// «الاستماع»: the reciters an editor published, and a way back to where the
/// reader stopped.
///
/// Everything on this screen is drawn from the catalogue already on the phone,
/// so it opens offline. Only pressing play or download reaches out — to the
/// publisher named under each recitation, never to this app's server.
class ListenScreen extends StatefulWidget {
  const ListenScreen({super.key});

  @override
  State<ListenScreen> createState() => _ListenScreenState();
}

class _ListenScreenState extends State<ListenScreen> {
  var _query = '';

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final reciters = state.content.reciters;

    if (reciters.isEmpty) {
      return AthkarEmptyState(
        title: context.tr('listen.empty'),
        body: context.tr('listen.emptyHint'),
        icon: Icons.headphones_outlined,
      );
    }

    // Folded both sides, like every other search in the app: «عبدالباسط» and
    // «عبد الباسط» are the same reciter to the reader typing it.
    final folded = ArabicText.normalize(_query).replaceAll(' ', '');
    final matches = folded.isEmpty
        ? reciters
        : [
            for (final reciter in reciters)
              if (ArabicText.normalize(reciter.name).replaceAll(' ', '').contains(folded)) reciter,
          ];
    final featured = [for (final reciter in reciters) if (reciter.isFeatured) reciter];

    return ListenableBuilder(
      listenable: Listenable.merge([RecitationPlayer.instance, RecitationLibrary.instance]),
      builder: (context, _) {
        final resume = RecitationLibrary.instance.resume;
        final player = RecitationPlayer.instance;

        return CustomScrollView(
          slivers: [
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 12, AthkarSpacing.page, 0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (!player.isActive && resume != null)
                      if (_resolve(reciters, resume.ref) case (final reciter, final recitation)) ...[
                        _ContinueCard(reciter: reciter, recitation: recitation, resume: resume),
                        const SizedBox(height: 16),
                      ],
                    Row(
                      children: [
                        Expanded(
                          child: TextField(
                            onChanged: (value) => setState(() => _query = value),
                            decoration: InputDecoration(
                              hintText: context.tr('listen.search'),
                              prefixIcon: const Icon(Icons.search, size: 20),
                              isDense: true,
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        IconButton.outlined(
                          tooltip: context.tr('listen.saved.title'),
                          onPressed: () => Navigator.of(context).push(
                            MaterialPageRoute(builder: (_) => const ListenSavedScreen()),
                          ),
                          icon: const Icon(Icons.library_music_outlined),
                        ),
                      ],
                    ),
                    if (featured.isNotEmpty && folded.isEmpty) ...[
                      const SizedBox(height: 18),
                      Text(
                        context.tr('listen.featured'),
                        style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
                      ),
                      const SizedBox(height: 10),
                      SizedBox(
                        // Name cards when there are no portraits — nothing is
                        // drawn in place of a picture that does not exist.
                        height: featured.any(ReciterAvatar.hasPortrait) ? 132 : 58,
                        child: ListView.separated(
                          scrollDirection: Axis.horizontal,
                          itemCount: featured.length,
                          separatorBuilder: (_, __) => const SizedBox(width: 12),
                          itemBuilder: (context, index) => _FeaturedReciter(reciter: featured[index]),
                        ),
                      ),
                    ],
                    const SizedBox(height: 18),
                    Text(
                      context.tr('listen.all'),
                      style: AthkarType.amiri(size: 19, color: tokens.ink, weight: FontWeight.w700),
                    ),
                    const SizedBox(height: 4),
                  ],
                ),
              ),
            ),
            if (matches.isEmpty)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.all(32),
                  child: Text(
                    context.tr('listen.noMatch'),
                    textAlign: TextAlign.center,
                    style: AthkarType.sans(size: 13, color: tokens.muted),
                  ),
                ),
              ),
            SliverList.builder(
              itemCount: matches.length,
              itemBuilder: (context, index) => _ReciterRow(reciter: matches[index]),
            ),
            const SliverToBoxAdapter(child: SizedBox(height: 24)),
          ],
        );
      },
    );
  }

  /// The reciter and recording a saved reference points at, if both are still
  /// published — an editor may have withdrawn either since.
  static (Reciter, Recitation)? _resolve(List<Reciter> reciters, SurahRef ref) {
    for (final reciter in reciters) {
      if (reciter.key != ref.reciterKey) continue;
      for (final recitation in reciter.recitations) {
        if (recitation.id == ref.recitationId && recitation.surahs.contains(ref.surah)) {
          return (reciter, recitation);
        }
      }
    }
    return null;
  }
}

class _ContinueCard extends StatelessWidget {
  const _ContinueCard({required this.reciter, required this.recitation, required this.resume});

  final Reciter reciter;
  final Recitation recitation;
  final ListeningResume resume;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final arabicIndic = SettingsScope.of(context).arabicNumerals;

    return AthkarCard(
      onTap: () => startListening(
        context,
        reciter,
        recitation,
        resume.ref.surah,
        startAt: resume.position,
      ),
      child: Row(
        children: [
          if (ReciterAvatar.hasPortrait(reciter)) ...[
            ReciterAvatar(reciter: reciter, size: 48, radius: 12),
            const SizedBox(width: 12),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.tr('listen.continue'),
                  style: AthkarType.sans(size: 11.5, color: tokens.brandInk, weight: FontWeight.w600),
                ),
                Text(
                  surahTitle(context, resume.ref.surah),
                  style: AthkarType.amiri(size: 18, color: tokens.ink, weight: FontWeight.w700),
                ),
                Text(
                  '${reciter.name} · ${listeningClock(resume.position, arabicIndic: arabicIndic)}',
                  style: AthkarType.sans(size: 12, color: tokens.muted),
                ),
              ],
            ),
          ),
          Icon(Icons.play_circle_fill_rounded, color: tokens.brand, size: 36),
        ],
      ),
    );
  }
}

class _FeaturedReciter extends StatelessWidget {
  const _FeaturedReciter({required this.reciter});

  final Reciter reciter;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => ReciterScreen(reciter: reciter)),
      ),
      child: ReciterAvatar.hasPortrait(reciter)
          ? SizedBox(
              width: 96,
              child: Column(
                children: [
                  ReciterAvatar(reciter: reciter, size: 92, radius: 22),
                  const SizedBox(height: 8),
                  Text(
                    reciter.name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.center,
                    style: AthkarType.sans(size: 12.5, color: tokens.ink, weight: FontWeight.w600),
                  ),
                ],
              ),
            )
          : Container(
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(horizontal: 18),
              decoration: BoxDecoration(
                color: tokens.surface,
                border: Border.all(color: tokens.border),
                borderRadius: BorderRadius.circular(18),
              ),
              child: Text(
                reciter.name,
                maxLines: 1,
                style: AthkarType.amiri(size: 16, color: tokens.ink, weight: FontWeight.w700),
              ),
            ),
    );
  }
}

class _ReciterRow extends StatelessWidget {
  const _ReciterRow({required this.reciter});

  final Reciter reciter;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final arabicIndic = SettingsScope.of(context).arabicNumerals;
    final riwayat = reciter.recitations.length;

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page, vertical: 2),
      leading: ReciterAvatar.maybe(reciter, size: 44, radius: 12),
      title: Text(
        reciter.name,
        style: AthkarType.amiri(size: 17, color: tokens.ink, weight: FontWeight.w700),
      ),
      subtitle: Text(
        riwayat == 1
            ? reciter.recitations.first.name
            : context.tr(riwayat == 2 ? 'listen.recitation.two' : 'listen.recitation.count', {
                'count': Numerals.format(riwayat, arabicIndic: arabicIndic),
              }),
        style: AthkarType.sans(size: 12, color: tokens.muted),
      ),
      trailing: Icon(Icons.chevron_left, color: tokens.faint),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => ReciterScreen(reciter: reciter)),
      ),
    );
  }
}
