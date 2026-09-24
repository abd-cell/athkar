import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/l10n.dart';
import '../../core/theme.dart';
import '../listen/listen_screen.dart';
import 'quran_screen.dart';

/// The Qur'an tab: the mushaf to read, and the reciters to listen to.
///
/// One tab rather than two, because the bottom bar already carries seven and
/// because they are one book — a reader moves between reading a surah and
/// hearing it. The choice is remembered, so someone who only ever listens
/// does not land on the mushaf each time.
class QuranTab extends StatefulWidget {
  const QuranTab({super.key});

  @override
  State<QuranTab> createState() => _QuranTabState();
}

class _QuranTabState extends State<QuranTab> {
  static const _kListening = 'quran.tab.listening';

  var _listening = false;

  @override
  void initState() {
    super.initState();
    SharedPreferences.getInstance().then((prefs) {
      if (mounted) setState(() => _listening = prefs.getBool(_kListening) ?? false);
    });
  }

  Future<void> _choose(bool listening) async {
    setState(() => _listening = listening);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_kListening, listening);
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Scaffold(
      backgroundColor: tokens.paper,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AthkarSpacing.page, 10, AthkarSpacing.page, 4),
              child: SegmentedButton<bool>(
                showSelectedIcon: false,
                segments: [
                  ButtonSegment(
                    value: false,
                    icon: const Icon(Icons.menu_book_outlined, size: 18),
                    label: Text(context.tr('quran.tab.read')),
                  ),
                  ButtonSegment(
                    value: true,
                    icon: const Icon(Icons.headphones_outlined, size: 18),
                    label: Text(context.tr('quran.tab.listen')),
                  ),
                ],
                selected: {_listening},
                onSelectionChanged: (value) => _choose(value.first),
              ),
            ),
            Expanded(
              child: IndexedStack(
                index: _listening ? 1 : 0,
                children: const [QuranScreen(), ListenScreen()],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
