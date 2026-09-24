import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import 'adhkar_screen.dart';
import 'category_screen.dart';
import 'home_screen.dart';
import 'prayer_times_screen.dart';
import 'profile_screen.dart';
import 'qibla_screen.dart';
import 'listen/listen_widgets.dart';
import 'quran/quran_tab.dart';
import 'tasbih_screen.dart';

/// The seven tabs, as the bottom bar lays them out.
///
/// Seven is past the usual counsel, and it is the shape the app actually has:
/// each is a separate reason a person opens it, and burying the qibla or the
/// timetable behind a "more" menu would make the app worse for the reader who
/// opened it for that. They are all one word long and all reachable in one tap.
///
/// The first tab is the home screen and says so. It used to be labelled
/// «الأذكار», which named the app rather than the screen — and left the chapters
/// themselves with no tab of their own, reachable only through whatever the home
/// screen happened to surface today.
class AppShell extends StatefulWidget {
  const AppShell({super.key, this.pendingRoute, this.onRouteConsumed});

  /// A route from a tapped notification, handled once the tree exists.
  final String? pendingRoute;
  final VoidCallback? onRouteConsumed;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  var _index = 0;

  @override
  void didUpdateWidget(AppShell old) {
    super.didUpdateWidget(old);
    if (widget.pendingRoute != null && widget.pendingRoute != old.pendingRoute) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _consumeRoute());
    }
  }

  @override
  void initState() {
    super.initState();
    if (widget.pendingRoute != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _consumeRoute());
    }
  }

  /// Opens what a notification pointed at.
  ///
  /// The payload is a route string like `category/12`, which is the entire
  /// vocabulary a push has — everything a screen needs is already on the device,
  /// so a notification says *which screen*, never what to put on it.
  void _consumeRoute() {
    final route = widget.pendingRoute;
    widget.onRouteConsumed?.call();

    if (route == null) return;

    final parts = route.split('/');
    if (parts.length != 2) return;

    final id = int.tryParse(parts[1]);
    if (id == null) return;

    switch (parts[0]) {
      case 'category':
        Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => CategoryScreen(categoryId: id)),
        );
      case 'dhikr':
        // A single dhikr opens inside its chapter, because a dhikr on its own
        // has no counter and no next — and the reader tapped a reminder to read,
        // not to look something up.
        Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => CategoryScreen(highlightDhikrId: id)),
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    const screens = [
      HomeScreen(),
      AdhkarScreen(),
      QuranTab(),
      PrayerTimesScreen(),
      TasbihScreen(),
      QiblaScreen(),
      ProfileScreen(),
    ];

    return Scaffold(
      backgroundColor: tokens.paper,
      body: IndexedStack(index: _index, children: screens),
      // The recitation follows the reader across every tab — they can leave the
      // player to check a prayer time and still stop the surah from here.
      bottomNavigationBar: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const MiniPlayer(),
          _BottomBar(
            index: _index,
            onChanged: (value) => setState(() => _index = value),
          ),
        ],
      ),
    );
  }
}

/// The bar itself, drawn rather than themed.
///
/// `NavigationBar` would bring Material 3's pill indicator, its own heights and
/// its own ripple — none of which are in the design. This is the prototype's
/// bar: a translucent parchment strip, a hairline above it, and a soft tinted
/// rectangle behind the selected item.
class _BottomBar extends StatelessWidget {
  const _BottomBar({required this.index, required this.onChanged});

  final int index;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final items = <(IconData, String)>[
      (Icons.home_outlined, context.tr('nav.home')),
      (Icons.auto_awesome_outlined, context.tr('nav.athkar')),
      (Icons.menu_book_outlined, context.tr('nav.quran')),
      (Icons.schedule_outlined, context.tr('nav.prayer')),
      (Icons.radio_button_checked_outlined, context.tr('nav.tasbih')),
      (Icons.explore_outlined, context.tr('nav.qibla')),
      (Icons.person_outline, context.tr('nav.profile')),
    ];

    return Container(
      decoration: BoxDecoration(
        color: tokens.scrimTop,
        border: Border(top: BorderSide(color: tokens.hairline)),
      ),
      padding: EdgeInsets.fromLTRB(
        12,
        8,
        12,
        10 + MediaQuery.of(context).padding.bottom,
      ),
      child: Row(
        children: [
          for (var i = 0; i < items.length; i++)
            Expanded(
              child: _BottomBarItem(
                icon: items[i].$1,
                label: items[i].$2,
                selected: i == index,
                onTap: () => onChanged(i),
              ),
            ),
        ],
      ),
    );
  }
}

class _BottomBarItem extends StatelessWidget {
  const _BottomBarItem({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final color = selected ? tokens.brand : tokens.muted;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(14),
      child: Container(
        constraints: const BoxConstraints(minHeight: 52),
        decoration: BoxDecoration(
          color: selected ? tokens.brandTint : Colors.transparent,
          borderRadius: BorderRadius.circular(14),
        ),
        padding: const EdgeInsets.symmetric(vertical: 7),
        child: Column(
          // Without this the column takes every pixel the row will give it,
          // and the whole bar grows to the height of the screen — which leaves
          // the body with nothing. The bar sizes to its contents.
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 19, color: color),
            const SizedBox(height: 5),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AthkarType.sans(
                size: 10.5,
                color: color,
                weight: selected ? FontWeight.w600 : FontWeight.w400,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
