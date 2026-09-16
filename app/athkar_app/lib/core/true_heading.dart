import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

/// Turns a compass reading into a bearing from **true** north.
///
/// ## Why this file exists
///
/// A magnetometer reports magnetic north. The qibla is computed from true
/// north. The angle between them — the magnetic declination — is under 3° in
/// the Gulf but around 6° in Istanbul and 13° on the American east coast.
/// Ignoring it is the commonest bug in qibla apps and it is visible in a room.
///
/// ## Why the platform, and not a model here
///
/// The obvious shortcut is a tilted-dipole model: take the bearing to the
/// geomagnetic pole and call it the declination. It is ten lines and it is
/// **wrong by ten to fifteen degrees** in exactly the places this app is used —
/// Riyadh comes out at −8.7° when the true figure is about +3.1° — because the
/// earth's field is not a dipole and the non-dipole part is most of the error.
/// Doing that and claiming a couple of degrees of accuracy would be worse than
/// not correcting at all, because nobody would check it.
///
/// Getting it right means the World Magnetic Model: a table of spherical
/// harmonic coefficients reissued every five years. Both platforms already ship
/// one, so this asks them rather than carrying a copy that would silently go
/// stale:
///
/// - **iOS** applies it for us. `CLHeading.trueHeading` is already a true
///   bearing whenever location services are available, and `flutter_compass`
///   surfaces exactly that. There is nothing to add.
/// - **Android** does not. `SensorManager` gives magnetic north, so the
///   declination comes from `android.hardware.GeomagneticField` over the method
///   channel below — the same WMM, maintained by the OS.
///
/// When neither can answer — no location yet, an Android build without the
/// channel — [declination] is null and the caller shows the uncorrected
/// compass, saying so, rather than a confidently wrong arrow.
class TrueHeading {
  TrueHeading._();

  static final instance = TrueHeading._();

  static const _channel = MethodChannel('athkari/geomagnetic');

  /// Cached per location, because the declination does not change as a phone is
  /// turned and the channel should not be crossed on every sensor frame.
  double? _cached;
  double? _cachedLatitude;
  double? _cachedLongitude;

  /// True when the platform's heading already points at true north, so no
  /// correction should be applied on top.
  bool get isAlreadyTrue => !kIsWeb && Platform.isIOS;

  /// The declination in degrees east at a point, or null when it cannot be had.
  ///
  /// Safe to call often: the answer is cached until the reader moves far enough
  /// to matter — a tenth of a degree is roughly eleven kilometres, over which
  /// the declination moves by a small fraction of a degree.
  Future<double?> declination(double latitude, double longitude) async {
    if (isAlreadyTrue) return 0;

    if (_cached != null &&
        _cachedLatitude != null &&
        (latitude - _cachedLatitude!).abs() < 0.1 &&
        (longitude - _cachedLongitude!).abs() < 0.1) {
      return _cached;
    }

    try {
      final value = await _channel.invokeMethod<double>('declination', {
        'latitude': latitude,
        'longitude': longitude,
      });

      if (value == null) return null;

      _cached = value;
      _cachedLatitude = latitude;
      _cachedLongitude = longitude;
      return value;
    } on MissingPluginException {
      // A platform with no implementation — the web build, or a host this app
      // was not built for. The caller falls back to the uncorrected compass.
      return null;
    } on PlatformException catch (error) {
      assert(() {
        debugPrint('[qibla] declination unavailable: ${error.message}');
        return true;
      }());
      return null;
    }
  }

  /// Applies a declination to a magnetic reading, wrapped into [0, 360).
  ///
  /// Static and pure so the arithmetic is testable without a platform — which
  /// is the half of this that can actually go wrong in code rather than in
  /// geophysics.
  static double apply(double magneticHeading, double? declination) {
    final corrected = magneticHeading + (declination ?? 0);
    return (corrected % 360 + 360) % 360;
  }

  /// The signed turn from [heading] to [target], in (−180, 180].
  ///
  /// Positive means turn right. Used for the "turn 12° right" instruction, and
  /// the one piece of arithmetic on this screen that is easy to get backwards.
  static double difference(double target, double heading) =>
      (target - heading + 540) % 360 - 180;
}
