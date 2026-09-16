import 'package:flutter/material.dart';

import '../core/daily_verse.dart';
import '../core/hijri_date.dart';
import '../core/l10n.dart';
import '../core/numerals.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../models/models.dart';
import '../widgets/athkar_ui.dart';
import '../widgets/prayer_strip.dart';
import 'category_screen.dart';
import 'dhikr_sheet.dart';
import 'location_screen.dart';
import 'monthly_screen.dart';
import 'notifications_screen.dart';
import 'quran/surah_screen.dart';
import 'search_screen.dart';
import 'share_sheet.dart';
import 'session_screen.dart';

/// The first screen: today's date, the next prayer, one dhikr to start on, and
/// what is left of today's chapters.
///
/// Its job is to make the next action obvious without asking a question. The
/// «ذكر الآن» card is chosen from the chapter whose anchor matches this hour, so
/// a reader who opens the app after sunrise finds the morning adhkar already in
/// front of them.
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final suggested = state.content.suggestedFor(DateTime.now());
    final primary = suggested.isEmpty ? null : suggested.first;

    return SafeArea(
      bottom: false,
      child: ListView(
        padding: const EdgeInsets.only(bottom: 24),
        children: [
          _Header(),
          const SizedBox(height: 12),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                NextPrayerStrip(
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const MonthlyScreen()),
                  ),
                ),
                const SizedBox(height: 12),
                PrayerTimesCard(
                  onMonthlyTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const MonthlyScreen()),
                  ),
                ),
                if (!settings.hasLocation) const _LocationPrompt(),
                const SizedBox(height: 16),
                if (primary != null) _RightNowCard(category: primary),
                const SizedBox(height: 20),
                AthkarSectionHeader(
                  title: context.tr('home.readToday'),
                  note: context.tr('home.readToday.subtitle'),
                ),
                const SizedBox(height: 10),
                ..._todayCards(context, state.content.categories),
                const SizedBox(height: 12),
                _TodayProgress(categories: suggested),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// Three cards: a verse, a narration and a supplication.
  ///
  /// Picked by the day of the year rather than at random, so the same three are
  /// there if the reader closes the app and comes back — a "dhikr of the day"
  /// that changes on every rebuild is not a dhikr of the day.
  ///
  /// The verse card is absent until the mushaf is downloaded; see [DailyVerse].
  List<Widget> _todayCards(BuildContext context, List<AthkarCategory> categories) {
    final all = [for (final category in categories) ...category.adhkar];

    final cards = <Widget>[const _VerseCard()];

    if (all.isNotEmpty) {
      final seed = DateTime.now().difference(DateTime(DateTime.now().year)).inDays;

      final narration = all.firstWhere(
        (candidate) => candidate.grade == HadithGrade.muttafaqAlayh,
        orElse: () => all[seed % all.length],
      );
      final supplication = _supplication(all, seed, exclude: narration.id);

      cards.addAll([
        _TodayCard(
          label: context.tr('home.hadithOfDay'),
          icon: Icons.mosque_outlined,
          dhikr: narration,
        ),
        if (supplication != null)
          _TodayCard(
            label: context.tr('home.duaOfDay'),
            icon: Icons.volunteer_activism_outlined,
            dhikr: supplication,
          ),
      ]);
    }

    return [
      for (var i = 0; i < cards.length; i++) ...[
        if (i > 0) const SizedBox(height: 10),
        cards[i],
      ],
    ];
  }

  /// A dhikr that is a supplication — the reader is asking for something, not
  /// praising or remembering. The opening word is what says so in Arabic, and
  /// it is the only signal the catalogue carries: nothing in the data marks a
  /// row as a du'a, so «اللهم» and «ربّنا» do the work.
  static Dhikr? _supplication(List<Dhikr> all, int seed, {required int exclude}) {
    const openings = ['اللهم', 'اللّهم', 'ربنا', 'ربّنا', 'رب ', 'ربّ '];

    final candidates = [
      for (final dhikr in all)
        if (dhikr.id != exclude && openings.any(dhikr.arabicText.trimLeft().startsWith)) dhikr,
    ];
    if (candidates.isEmpty) return null;

    return candidates[seed % candidates.length];
  }
}

class _Header extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final hijri = HijriDate.from(
      DateTime.now(),
      offsetDays: settings.hijriOffset,
      languageCode: settings.languageCode,
    );

    return Padding(
      padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 16, AthkarSpacing.page, 0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  hijri.format(arabicNumerals: settings.arabicNumerals),
                  style: AthkarType.sans(size: 12, color: tokens.muted, letterSpacing: 0.02),
                ),
                const SizedBox(height: 4),
                Text(
                  _greeting(context),
                  style: AthkarType.amiri(
                    size: 27,
                    color: tokens.ink,
                    weight: FontWeight.w700,
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Column(
            children: [
              Row(
                children: [
                  IconButton(
                    onPressed: () => Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => const SearchScreen()),
                    ),
                    icon: Icon(Icons.search, size: 20, color: tokens.ink),
                    tooltip: context.tr('common.search'),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => const NotificationsScreen()),
                    ),
                    icon: Icon(Icons.notifications_none, size: 20, color: tokens.ink),
                    tooltip: context.tr('notifications.title'),
                  ),
                ],
              ),
              if (settings.cityName case final city?)
                AthkarChip(
                  label: city,
                  leading: Container(
                    width: 9,
                    height: 9,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      border: Border.all(color: tokens.brand, width: 2),
                    ),
                  ),
                  trailing: Text(
                    context.tr('home.changeCity'),
                    style: AthkarType.sans(size: 12, color: tokens.muted),
                  ),
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(builder: (_) => const LocationScreen()),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }

  /// The greeting follows the clock, not the prayer times: a reader without a
  /// location still gets a greeting, and «صباح الخير» at 09:00 is right
  /// wherever they are.
  String _greeting(BuildContext context) {
    final hour = DateTime.now().hour;

    return switch (hour) {
      >= 3 && < 6 => context.tr('home.greeting.dawn'),
      >= 6 && < 12 => context.tr('home.greeting.morning'),
      >= 12 && < 16 => context.tr('home.greeting.afternoon'),
      >= 16 && < 21 => context.tr('home.greeting.evening'),
      _ => context.tr('home.greeting.night'),
    };
  }
}

/// The one card on the page filled with brand green — the next thing to read,
/// with the session's progress on it.
class _RightNowCard extends StatelessWidget {
  const _RightNowCard({required this.category});

  final AthkarCategory category;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final dhikr = category.adhkar.isEmpty ? null : category.adhkar.first;
    if (dhikr == null) return const SizedBox.shrink();

    final resumed = state.progress.sessionState(
      category.id,
      daily: category.rhythm == CategoryRhythm.daily,
    );
    final done = resumed?.$1 ?? 0;

    return AthkarBrandCard(
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => SessionScreen(category: category)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${context.tr('home.dhikrNow')} · ${category.name}',
            style: AthkarType.sans(size: 11.5, color: tokens.onBrand.withValues(alpha: 0.72)),
          ),
          const SizedBox(height: 10),
          Text(
            dhikr.arabicText,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
            style: AthkarType.amiri(
              size: 23 * settings.fontScale,
              color: tokens.onBrand,
              height: 1.85,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Container(
                constraints: const BoxConstraints(minHeight: AthkarSpacing.tapTarget),
                padding: const EdgeInsets.symmetric(horizontal: 22),
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: tokens.onBrand,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  context.tr('home.continueSession'),
                  style: AthkarType.sans(size: 14, color: tokens.brand, weight: FontWeight.w600),
                ),
              ),
              const Spacer(),
              Text(
                context.tr('home.ofCount', {
                  'done': Numerals.format(done, arabicIndic: settings.arabicNumerals),
                  'total': Numerals.format(
                    category.adhkar.length,
                    arabicIndic: settings.arabicNumerals,
                  ),
                }),
                style: AthkarType.sans(size: 13, color: tokens.onBrand.withValues(alpha: 0.8)),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The shape the «today» section repeats: a labelled pill, the text, its
/// reference, and a share button.
///
/// The pill and the share button sit on opposite ends of the same row, which
/// under Arabic puts the label at the right and the button at the left — and
/// mirrors on its own in English.
class _TodayCardShell extends StatelessWidget {
  const _TodayCardShell({
    required this.label,
    required this.icon,
    required this.text,
    required this.reference,
    required this.subject,
    this.onTap,
  });

  final String label;
  final IconData icon;
  final String text;

  /// The takhrij line — book, number and grading for a narration, «سورة: آية»
  /// for a verse. It is the project's distinguishing claim, so it is never
  /// optional on a card that shows narrated text.
  final String reference;

  /// What the share sheet is handed — see [ShareSubject].
  final ShareSubject subject;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    return AthkarCard(
      onTap: onTap,
      padding: const EdgeInsets.fromLTRB(18, 14, 18, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _Pill(label: label, icon: icon),
              const Spacer(),
              IconButton(
                onPressed: () => ShareSheet.show(context, subject),
                icon: Icon(Icons.ios_share, size: 18, color: tokens.muted),
                tooltip: context.tr('dhikr.share'),
                visualDensity: VisualDensity.compact,
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints.tightFor(
                  width: AthkarSpacing.tapTarget,
                  height: AthkarSpacing.tapTarget,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            text,
            style: AthkarType.amiri(
              size: 21 * settings.fontScale,
              color: tokens.ink,
              height: 1.95,
            ),
          ),
          if (reference.isNotEmpty) ...[
            const SizedBox(height: 12),
            AthkarSourceLine(text: reference),
          ],
        ],
      ),
    );
  }
}

/// The rounded label at the head of a card: «حديث اليوم» and its icon.
class _Pill extends StatelessWidget {
  const _Pill({required this.label, required this.icon});

  final String label;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Container(
      padding: const EdgeInsets.fromLTRB(10, 5, 5, 5),
      decoration: BoxDecoration(
        color: tokens.brandTint,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: AthkarType.sans(size: 11.5, color: tokens.brandInk, weight: FontWeight.w600),
          ),
          const SizedBox(width: 6),
          Container(
            width: 20,
            height: 20,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: tokens.surface, shape: BoxShape.circle),
            child: Icon(icon, size: 12, color: tokens.brand),
          ),
        ],
      ),
    );
  }
}

/// A dhikr — a narration or a supplication — in the card shape above.
class _TodayCard extends StatelessWidget {
  const _TodayCard({required this.label, required this.icon, required this.dhikr});

  final String label;
  final IconData icon;
  final Dhikr dhikr;

  @override
  Widget build(BuildContext context) {
    return _TodayCardShell(
      label: label,
      icon: icon,
      text: dhikr.arabicText,
      reference: dhikr.hasSource
          ? '${dhikr.sourceBook} ${dhikr.sourceReference} · ${gradeLabel(context, dhikr.grade)}'
          : '',
      subject: ShareSubject.dhikr(
        dhikr,
        grade: dhikr.grade == null ? null : gradeLabel(context, dhikr.grade),
      ),
      onTap: () => DhikrSheet.show(context, dhikr),
    );
  }
}

/// «آية اليوم», which exists only once the mushaf is on the device.
///
/// The card renders nothing at all until the lookup answers, and nothing ever
/// if the reader has not downloaded the package — an empty verse card that
/// invites a download would put a permanent gap on the home screen of every
/// reader who does not want one.
class _VerseCard extends StatefulWidget {
  const _VerseCard();

  @override
  State<_VerseCard> createState() => _VerseCardState();
}

class _VerseCardState extends State<_VerseCard> {
  DailyVerse? _verse;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final store = AppStateScope.read(context).quran;
    final verse = await DailyVerse.forDay(store, DateTime.now());

    if (mounted) setState(() => _verse = verse);
  }

  @override
  Widget build(BuildContext context) {
    final verse = _verse;
    if (verse == null) return const SizedBox.shrink();

    final settings = SettingsScope.of(context);
    final reference = context.tr('home.verseReference', {
      'surah': verse.surah.nameAr,
      'ayah': Numerals.format(verse.ayah.number, arabicIndic: settings.arabicNumerals),
    });

    return _TodayCardShell(
      label: context.tr('home.verseOfDay'),
      icon: Icons.menu_book_outlined,
      text: verse.ayah.text,
      reference: reference,
      subject: ShareSubject(
        heading: verse.surah.nameAr,
        body: verse.ayah.text,
        attribution: reference,
        plainText: '${verse.ayah.text}\n\n$reference',
      ),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => SurahScreen(surah: verse.surah)),
      ),
    );
  }
}

/// «أذكار اليوم · ٢ من ٤ مكتملة» — the one line that says whether today is done.
class _TodayProgress extends StatelessWidget {
  const _TodayProgress({required this.categories});

  final List<AthkarCategory> categories;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final state = AppStateScope.of(context);
    final settings = SettingsScope.of(context);

    final daily = [
      for (final category in state.content.categories)
        if (category.rhythm == CategoryRhythm.daily) category,
    ];
    if (daily.isEmpty) return const SizedBox.shrink();

    final completed = state.progress.completedToday;
    final done = daily.where((category) => completed.contains(category.id)).length;

    return AthkarCard(
      radius: AthkarSpacing.smallCardRadius,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => const CategoryScreen()),
      ),
      child: Row(
        children: [
          Expanded(
            child: Text(
              context.tr('home.todayProgress', {
                'done': Numerals.format(done, arabicIndic: settings.arabicNumerals),
                'total': Numerals.format(daily.length, arabicIndic: settings.arabicNumerals),
              }),
              style: AthkarType.sans(size: 13, color: tokens.ink),
            ),
          ),
          Text(
            context.tr('home.continue'),
            style: AthkarType.sans(size: 12, color: tokens.brand, weight: FontWeight.w500),
          ),
        ],
      ),
    );
  }
}

/// Shown in place of the prayer card until the reader has told the app where
/// they are. Offers the choice rather than prompting for the permission.
class _LocationPrompt extends StatelessWidget {
  const _LocationPrompt();

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: AthkarCard(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => const LocationScreen()),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              context.tr('prayer.noLocation'),
              style: AthkarType.sans(size: 13.5, color: tokens.ink, weight: FontWeight.w500),
            ),
            const SizedBox(height: 4),
            Text(
              context.tr('prayer.noLocationHint'),
              style: AthkarType.sans(size: 11.5, color: tokens.muted, height: 1.6),
            ),
          ],
        ),
      ),
    );
  }
}

/// The reader-facing word for a grading.
String gradeLabel(BuildContext context, HadithGrade? grade) => switch (grade) {
      HadithGrade.sahih => context.tr('dhikr.grade.sahih'),
      HadithGrade.hasan => context.tr('dhikr.grade.hasan'),
      HadithGrade.sahihLighayrihi => context.tr('dhikr.grade.sahihLighayrihi'),
      HadithGrade.hasanLighayrihi => context.tr('dhikr.grade.hasanLighayrihi'),
      HadithGrade.quranVerse => context.tr('dhikr.grade.quranVerse'),
      HadithGrade.muttafaqAlayh => context.tr('dhikr.grade.muttafaqAlayh'),
      null => '',
    };
