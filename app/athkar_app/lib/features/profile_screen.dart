import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../widgets/athkar_ui.dart';
import 'about_screen.dart';
import 'faq_screen.dart';
import 'favourites_screen.dart';
import 'feedback_screen.dart';
import 'reminders_screen.dart';
import 'settings_screen.dart';
import 'widgets/widgets_hub_screen.dart';

/// Everything about the reader's own use of the app, in one place.
///
/// «حسابي» with no account behind it: the tally, the streak and the favourites
/// all live on this phone. That is the point — the screen that would be a
/// profile in another app is, here, a demonstration that there is nothing to
/// sign into.
class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final digits = settings.arabicNumerals;

    return SafeArea(
      bottom: false,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 16, AthkarSpacing.page, 30),
        children: [
          Text(
            context.tr('profile.title'),
            style: AthkarType.amiri(size: 22, color: tokens.ink, weight: FontWeight.w700),
          ),

          const SizedBox(height: 14),
          AthkarCard(
            child: Row(
              children: [
                _Stat(
                  label: context.tr('profile.streak'),
                  value: context.tr('profile.streakDays', {
                    'count': Numerals.format(
                      state.progress.currentStreak, arabicIndic: digits),
                  }),
                ),
                Container(width: 1, height: 38, color: tokens.hairline),
                _Stat(
                  label: context.tr('profile.tally'),
                  value: context.tr('profile.totalDhikr', {
                    'count': Numerals.format(state.progress.totalDhikr, arabicIndic: digits),
                  }),
                ),
                Container(width: 1, height: 38, color: tokens.hairline),
                _Stat(
                  label: context.tr('nav.athkar'),
                  value: context.tr('profile.sessions', {
                    'count': Numerals.format(state.progress.sessionCount, arabicIndic: digits),
                  }),
                ),
              ],
            ),
          ),

          const SizedBox(height: 14),
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('profile.favourites'),
                  icon: Icons.bookmark_outline,
                  value: Numerals.format(settings.favourites.length, arabicIndic: digits),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const FavouritesScreen()),
                  ),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('profile.reminders'),
                  icon: Icons.notifications_none,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const RemindersScreen()),
                  ),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('widgets.hub.title'),
                  icon: Icons.widgets_outlined,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const WidgetsHubScreen()),
                  ),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('profile.settings'),
                  icon: Icons.tune,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const SettingsScreen()),
                  ),
                ),
              ],
            ),
          ),

          const SizedBox(height: 14),
          AthkarCard(
            padding: EdgeInsets.zero,
            child: Column(
              children: [
                AthkarListRow(
                  label: context.tr('profile.faq'),
                  icon: Icons.help_outline,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const FaqScreen()),
                  ),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('profile.feedback'),
                  icon: Icons.edit_outlined,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const FeedbackScreen()),
                  ),
                ),
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('profile.about'),
                  icon: Icons.info_outline,
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const AboutScreen()),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Expanded(
      child: Column(
        children: [
          Text(
            value,
            textAlign: TextAlign.center,
            style: AthkarType.sans(size: 13, color: tokens.ink, weight: FontWeight.w600),
          ),
          const SizedBox(height: 4),
          Text(label, style: AthkarType.sans(size: 11, color: tokens.muted)),
        ],
      ),
    );
  }
}
