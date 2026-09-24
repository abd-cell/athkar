import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/audio_engine.dart';
import 'core/bootstrap.dart';
import 'core/l10n.dart';
import 'core/recitation_downloads.dart';
import 'core/recitation_library.dart';
import 'core/settings.dart';
import 'core/theme.dart';
import 'features/shell.dart';
import 'features/splash_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Portrait only. Every screen in the design is a single column of parchment,
  // and a landscape line of Amiri at 22pt runs far past a comfortable measure.
  await SystemChrome.setPreferredOrientations([
    DeviceOrientation.portraitUp,
    DeviceOrientation.portraitDown,
  ]);

  // Before anything can touch the shared player: the background service has
  // to own it from the start, or it refuses to serve it at all.
  await AudioEngine.initialize();

  // Both read local state only — a preferences read and a directory listing —
  // and neither is allowed to hold up the first frame on failure.
  await Future.wait([
    RecitationLibrary.instance.load(),
    RecitationDownloads.instance.load(),
  ]).catchError((_) => <void>[]);

  runApp(const AthkarApp());
}

class AthkarApp extends StatefulWidget {
  const AthkarApp({super.key});

  @override
  State<AthkarApp> createState() => _AthkarAppState();
}

class _AthkarAppState extends State<AthkarApp> with WidgetsBindingObserver {
  AppState? _state;

  /// Set by a notification tap before the tree is ready, and consumed by the
  /// shell once it is. A tap that arrives during a cold start must not be lost.
  String? _pendingRoute;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _start();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  /// Repaint the home-screen widget whenever the app comes back.
  ///
  /// Its values go stale on a clock rather than on an event — a countdown, the
  /// next prayer, the dhikr for this hour — and a reader returning to the app
  /// is the best signal available that somebody is looking at the phone.
  /// Android's own half-hourly refresh is the backstop, not the mechanism.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed) return;

    _state?.pushWidget();

    // Resume is the only moment the app can notice a notification permission
    // the reader changed in the OS settings — revoking it fires no callback,
    // it just quietly invalidates the token.
    _state?.refreshPushToken();
  }

  Future<void> _start() async {
    final startedAt = DateTime.now();
    final state = await AppState.load();

    // Preferences usually load faster than the splash can draw its mark, and a
    // cold start that flashes half a logo looks like a fault. Hold the rest of
    // the sequence out — it costs a moment once per launch, before anything the
    // reader could be waiting on.
    final elapsed = DateTime.now().difference(startedAt);
    if (elapsed < SplashScreen.minimumDuration) {
      await Future<void>.delayed(SplashScreen.minimumDuration - elapsed);
    }

    // The caches are enough to paint everything, so the app is handed over now
    // and the network is consulted afterwards.
    if (mounted) setState(() => _state = state);

    await state.sync(onNotificationTapped: _handleRoute);
  }

  void _handleRoute(String route) {
    if (mounted) setState(() => _pendingRoute = route);
  }

  @override
  Widget build(BuildContext context) {
    final state = _state;

    if (state == null) {
      // Still loading preferences — a few milliseconds. The splash carries the
      // mark rather than a blank frame.
      return MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: AthkarTheme.light(),
        darkTheme: AthkarTheme.dark(),
        home: const SplashScreen(),
      );
    }

    return AnimatedBuilder(
      animation: Listenable.merge([state, state.settings]),
      builder: (context, _) {
        final settings = state.settings;
        final locale = Locale(settings.languageCode);

        return SettingsScope(
          settings: settings,
          child: AppStateScope(
            state: state,
            child: MaterialApp(
              debugShowCheckedModeBanner: false,
              title: 'أذكاري',
              theme: AthkarTheme.light(),
              darkTheme: AthkarTheme.dark(),
              themeMode: settings.themeMode,
              locale: locale,
              supportedLocales: [
                for (final code in AppLocalizations.supported) Locale(code),
              ],
              localizationsDelegates: [
                AppLocalizationsDelegate(settings.stringsOverlay),
                ...GlobalMaterialLocalizations.delegates,
              ],
              home: AppShell(
                pendingRoute: _pendingRoute,
                onRouteConsumed: () => setState(() => _pendingRoute = null),
              ),
            ),
          ),
        );
      },
    );
  }
}

/// Puts [AppState] in the tree. Same pattern as [SettingsScope] — an inherited
/// notifier, no state-management package.
class AppStateScope extends InheritedNotifier<AppState> {
  const AppStateScope({super.key, required AppState state, required super.child})
      : super(notifier: state);

  static AppState of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AppStateScope>()!.notifier!;

  static AppState read(BuildContext context) =>
      context.getInheritedWidgetOfExactType<AppStateScope>()!.notifier!;
}
