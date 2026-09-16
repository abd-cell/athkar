import 'package:geolocator/geolocator.dart';

/// A location fix, or a typed reason there isn't one.
///
/// Nothing here ever throws, and nothing prompts on its own. The permission
/// dialog appears exactly once in this app: when the reader taps "use my
/// location". Everything else — the whole app, including prayer times for a
/// manually chosen city — works without ever asking.
sealed class LocationResult {
  const LocationResult();
}

class LocationFix extends LocationResult {
  const LocationFix(this.latitude, this.longitude);

  final double latitude;
  final double longitude;
}

/// Why there is no fix, in terms a screen can turn into a sentence.
enum LocationFailure {
  /// The OS-level location service is switched off.
  serviceDisabled,

  /// The reader said no this time.
  denied,

  /// The reader said no permanently; only Settings can undo it, so the app
  /// should stop asking and offer the city picker instead.
  deniedForever,

  /// The sensors gave nothing back in a reasonable time.
  unavailable,
}

class LocationRefused extends LocationResult {
  const LocationRefused(this.reason);

  final LocationFailure reason;
}

class DeviceLocation {
  const DeviceLocation._();

  /// Asks for one fix.
  ///
  /// Only ever called from an explicit tap — see the note above. Low accuracy
  /// on purpose: prayer times change by a minute across tens of kilometres, so
  /// a city-level fix is exactly as good as a rooftop one and costs a fraction
  /// of the battery and the wait.
  static Future<LocationResult> current() async {
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        return const LocationRefused(LocationFailure.serviceDisabled);
      }

      var permission = await Geolocator.checkPermission();

      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }

      if (permission == LocationPermission.deniedForever) {
        return const LocationRefused(LocationFailure.deniedForever);
      }

      if (permission == LocationPermission.denied) {
        return const LocationRefused(LocationFailure.denied);
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.low,
          timeLimit: Duration(seconds: 12),
        ),
      );

      return LocationFix(position.latitude, position.longitude);
    } catch (_) {
      return const LocationRefused(LocationFailure.unavailable);
    }
  }
}
