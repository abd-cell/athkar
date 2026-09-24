import 'package:flutter/material.dart';

import '../../core/l10n.dart';
import '../../core/numerals.dart';
import '../../core/recitation_downloads.dart';
import '../../core/recitation_library.dart';
import '../../core/settings.dart';
import '../../core/theme.dart';
import '../../main.dart';
import '../../models/models.dart';
import '../../widgets/athkar_alerts.dart';
import '../../widgets/athkar_ui.dart';
import 'listen_widgets.dart';

/// The reader's own shelf: the surahs they bookmarked and the ones they
/// downloaded — and how much room the downloads take, with a way to give it
/// back, because audio is the one thing in this app that can fill a phone.
class ListenSavedScreen extends StatelessWidget {
  const ListenSavedScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final arabicIndic = SettingsScope.of(context).arabicNumerals;
    final reciters = AppStateScope.of(context).content.reciters;

    (Reciter, Recitation)? find(String? reciterKey, int recitationId) {
      for (final reciter in reciters) {
        if (reciterKey != null && reciter.key != reciterKey) continue;
        for (final recitation in reciter.recitations) {
          if (recitation.id == recitationId) return (reciter, recitation);
        }
      }
      return null;
    }

    return Scaffold(
      backgroundColor: tokens.paper,
      appBar: AppBar(backgroundColor: tokens.paper, title: Text(context.tr('listen.saved.title'))),
      bottomNavigationBar: const MiniPlayer(),
      body: ListenableBuilder(
        listenable: Listenable.merge([RecitationLibrary.instance, RecitationDownloads.instance]),
        builder: (context, _) {
          final bookmarks = RecitationLibrary.instance.bookmarks;
          final downloads = RecitationDownloads.instance;
          final saved = downloads.saved;

          if (bookmarks.isEmpty && saved.isEmpty) {
            return AthkarEmptyState(
              title: context.tr('listen.saved.empty'),
              body: context.tr('listen.saved.emptyHint'),
              icon: Icons.bookmark_border_rounded,
            );
          }

          Widget row(Reciter reciter, Recitation recitation, int surah, {Widget? trailing}) => ListTile(
                contentPadding: const EdgeInsetsDirectional.only(start: 20, end: 8),
                leading: ReciterAvatar.maybe(reciter, size: 40, radius: 10),
                title: Text(
                  surahTitle(context, surah),
                  style: AthkarType.amiri(size: 17, color: tokens.ink, weight: FontWeight.w700),
                ),
                subtitle: Text('${reciter.name} · ${recitation.name}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AthkarType.sans(size: 11.5, color: tokens.muted)),
                trailing: trailing,
                onTap: () => startListening(context, reciter, recitation, surah, queue: [surah]),
              );

          return ListView(
            children: [
              if (bookmarks.isNotEmpty) ...[
                _Heading(context.tr('listen.saved.bookmarks')),
                for (final ref in bookmarks)
                  if (find(ref.reciterKey, ref.recitationId) case (final reciter, final recitation))
                    row(
                      reciter,
                      recitation,
                      ref.surah,
                      trailing: IconButton(
                        tooltip: context.tr('listen.bookmark.remove'),
                        onPressed: () => RecitationLibrary.instance.toggleBookmark(ref),
                        icon: Icon(Icons.bookmark_rounded, color: tokens.brand),
                      ),
                    ),
              ],
              if (saved.isNotEmpty) ...[
                _Heading(
                  context.tr('listen.saved.downloads', {
                    'size': Numerals.format(
                      (downloads.totalBytes / (1024 * 1024)).toStringAsFixed(0),
                      arabicIndic: arabicIndic,
                    ),
                  }),
                ),
                for (final entry in saved)
                  // A download whose recording an editor has since withdrawn is
                  // still on the phone and still the reader's; it is listed so
                  // it can be deleted, even though it can no longer be opened.
                  if (find(null, entry.recitationId) case (final reciter, final recitation))
                    row(reciter, recitation, entry.surah,
                        trailing: SurahDownloadButton(recitation: recitation, surah: entry.surah))
                  else
                    ListTile(
                      contentPadding: const EdgeInsetsDirectional.only(start: 20, end: 8),
                      title: Text(surahTitle(context, entry.surah)),
                      subtitle: Text(context.tr('listen.saved.withdrawn')),
                      trailing: IconButton(
                        tooltip: context.tr('listen.download.delete'),
                        onPressed: () => downloads.delete(entry.recitationId, entry.surah),
                        icon: Icon(Icons.delete_outline, color: tokens.muted),
                      ),
                    ),
                Padding(
                  padding: const EdgeInsets.all(20),
                  child: OutlinedButton.icon(
                    onPressed: () async {
                      final confirmed = await AthkarAlerts.confirm(
                        context,
                        title: context.tr('listen.saved.deleteAll'),
                        body: context.tr('listen.saved.deleteAllBody'),
                        confirmLabel: context.tr('listen.download.delete'),
                        destructive: true,
                      );
                      if (confirmed) await downloads.deleteAll();
                    },
                    icon: const Icon(Icons.delete_sweep_outlined),
                    label: Text(context.tr('listen.saved.deleteAll')),
                  ),
                ),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _Heading extends StatelessWidget {
  const _Heading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 18, AthkarSpacing.page, 6),
      child: Text(text, style: AthkarType.amiri(size: 18, color: tokens.ink, weight: FontWeight.w700)),
    );
  }
}
