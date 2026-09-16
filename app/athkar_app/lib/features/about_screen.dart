import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../core/bootstrap.dart';
import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../widgets/athkar_logo.dart';
import '../widgets/athkar_ui.dart';

/// Who made this, what it rests on, and what it asks for in return.
///
/// The last of those is the only unusual thing here: there is no donate button.
/// The project is an endowment, so the screen asks for a du'a instead — which
/// is both the honest ask and, for this audience, a more meaningful one.
class AboutScreen extends StatelessWidget {
  const AboutScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);
    final config = settings.config;

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('about.title'))),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          AthkarSpacing.page, 24, AthkarSpacing.page, 30),
        children: [
          const Center(child: AthkarLogoTile(size: 76)),
          const SizedBox(height: 16),
          Center(
            child: Text(
              context.tr('app.name'),
              style: AthkarType.amiri(size: 24, color: tokens.ink, weight: FontWeight.w700),
            ),
          ),
          const SizedBox(height: 4),
          Center(
            child: Text(
              context.tr('about.version', {'version': AppState.appVersion}),
              style: AthkarType.sans(size: 11.5, color: tokens.muted),
            ),
          ),

          const SizedBox(height: 22),
          AthkarCard(
            child: Text(
              context.tr('about.waqf'),
              textAlign: TextAlign.center,
              style: AthkarType.sans(size: 13, color: tokens.ink, height: 1.9),
            ),
          ),

          // Named because the project's central claim rests on this person. It
          // is configured from the CMS rather than compiled in, so the row is
          // simply absent until an administrator fills it in — which is better
          // than a placeholder standing in for a scholar's name.
          if (config.reviewingScholar case final scholar?) ...[
            const SizedBox(height: 14),
            AthkarCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    context.tr('about.reviewer'),
                    style: AthkarType.sans(
                      size: 11.5, color: tokens.brand, weight: FontWeight.w500),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    scholar,
                    style: AthkarType.amiri(
                      size: 18, color: tokens.ink, weight: FontWeight.w700),
                  ),
                  if (config.reviewingScholarCredential case final credential?) ...[
                    const SizedBox(height: 4),
                    Text(
                      credential,
                      style: AthkarType.sans(size: 12, color: tokens.muted, height: 1.7),
                    ),
                  ],
                ],
              ),
            ),
          ],

          const SizedBox(height: 14),
          AthkarCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  context.tr('about.sources'),
                  style: AthkarType.sans(
                    size: 11.5, color: tokens.brand, weight: FontWeight.w500),
                ),
                const SizedBox(height: 8),
                // The primary collections, named because the content was taken
                // from them rather than copied from another app's compilation.
                Text(
                  'صحيح البخاري · صحيح مسلم · سنن أبي داود · '
                  'جامع الترمذي · سنن النسائي · سنن ابن ماجه',
                  style: AthkarType.sans(size: 12.5, color: tokens.ink, height: 2),
                ),
              ],
            ),
          ),

          const SizedBox(height: 14),
          AthkarCard(
            child: Column(
              children: [
                Text(
                  context.tr('about.duaa'),
                  style: AthkarType.amiri(
                    size: 19, color: tokens.ink, weight: FontWeight.w700),
                ),
                const SizedBox(height: 8),
                Text(
                  context.tr('about.duaaBody'),
                  textAlign: TextAlign.center,
                  style: AthkarType.sans(size: 12.5, color: tokens.muted, height: 1.9),
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
                  label: context.tr('privacy.title'),
                  icon: Icons.lock_outline,
                  onTap: () => showModalBottomSheet(
                    context: context,
                    builder: (context) => Padding(
                      padding: const EdgeInsets.all(AthkarSpacing.page),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            context.tr('privacy.title'),
                            style: AthkarType.amiri(
                              size: 19, color: tokens.ink, weight: FontWeight.w700),
                          ),
                          const SizedBox(height: 12),
                          Text(
                            context.tr('privacy.body'),
                            textAlign: TextAlign.center,
                            style: AthkarType.sans(
                              size: 13, color: tokens.muted, height: 1.9),
                          ),
                          const SizedBox(height: 20),
                        ],
                      ),
                    ),
                  ),
                ),
                if (config.supportEmail case final email?) ...[
                  const AthkarRule(margin: EdgeInsets.zero),
                  AthkarListRow(
                    label: context.tr('contact.email'),
                    value: email,
                    icon: Icons.mail_outline,
                    onTap: () => launchUrl(Uri(scheme: 'mailto', path: email)),
                  ),
                ],
                if (config.supportWebsite case final website?) ...[
                  const AthkarRule(margin: EdgeInsets.zero),
                  AthkarListRow(
                    label: context.tr('contact.website'),
                    icon: Icons.language,
                    onTap: () => launchUrl(
                      Uri.parse(website),
                      mode: LaunchMode.externalApplication,
                    ),
                  ),
                ],
                const AthkarRule(margin: EdgeInsets.zero),
                AthkarListRow(
                  label: context.tr('about.licenses'),
                  icon: Icons.description_outlined,
                  onTap: () => showLicensePage(
                    context: context,
                    applicationName: context.tr('app.name'),
                    applicationVersion: AppState.appVersion,
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
