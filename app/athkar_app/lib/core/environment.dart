/// Where the API lives.
///
/// Read from a compile-time `--dart-define` so one build command can target a
/// phone, an emulator and the host browser without an edit:
///
/// ```
/// flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1/
/// ```
///
/// The default is the host's LAN address rather than `localhost`, because
/// `localhost` on a phone means the phone. See CLAUDE.md — this is the single
/// most common thing to get wrong when running the app.
class Environment {
  const Environment._();

  static const apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://athkar.technzone.com/api/api/v1/',
  );

  /// How long any one call may take before it is reported as a timeout.
  ///
  /// Deliberately short. Everything this app shows is already on the device;
  /// the network only ever brings *updates*, so a slow server should be given
  /// up on quickly rather than held onto while the reader stares at a spinner.
  static const timeout = Duration(seconds: 15);
}
