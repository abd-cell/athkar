import 'dart:math' as math;

import 'package:adhan/adhan.dart' as adhan;

import '../models/models.dart' as models;
import 'settings.dart';

/// Prayer times, computed here and nowhere else.
///
/// This file is the reason the server never receives a coordinate. Everything
/// it needs — latitude, longitude, convention, madhab, the reader's per-prayer
/// corrections — is on the device, so the times are exact, instant, and
/// available with the radio off. See `docs/BUSINESS_LOGIC.md` §5.
///
/// It is a thin wrapper over the `adhan` package, and thin on purpose: the
/// astronomy is somebody else's solved problem. What this adds is the mapping
/// from *our* enums to theirs, the reader's manual adjustments, and the two
/// anchors (`bedtime`, the night thirds) that are not prayers.
class PrayerTimetable {
  const PrayerTimetable({
    required this.date,
    required this.fajr,
    required this.sunrise,
    required this.dhuhr,
    required this.asr,
    required this.maghrib,
    required this.isha,
    required this.islamicMidnight,
    required this.lastThirdOfNight,
  });

  final DateTime date;
  final DateTime fajr;
  final DateTime sunrise;
  final DateTime dhuhr;
  final DateTime asr;
  final DateTime maghrib;
  final DateTime isha;

  /// The midpoint between Maghrib and the following Fajr.
  final DateTime islamicMidnight;

  final DateTime lastThirdOfNight;

  /// The six times in the order the design prints them.
  List<(models.PrayerAnchor, DateTime)> get ordered => [
        (models.PrayerAnchor.fajr, fajr),
        (models.PrayerAnchor.sunrise, sunrise),
        (models.PrayerAnchor.dhuhr, dhuhr),
        (models.PrayerAnchor.asr, asr),
        (models.PrayerAnchor.maghrib, maghrib),
        (models.PrayerAnchor.isha, isha),
      ];

  DateTime? at(models.PrayerAnchor anchor) => switch (anchor) {
        models.PrayerAnchor.fajr => fajr,
        models.PrayerAnchor.sunrise => sunrise,
        models.PrayerAnchor.dhuhr => dhuhr,
        models.PrayerAnchor.asr => asr,
        models.PrayerAnchor.maghrib => maghrib,
        models.PrayerAnchor.isha => isha,
        models.PrayerAnchor.islamicMidnight => islamicMidnight,
        models.PrayerAnchor.lastThirdOfNight => lastThirdOfNight,
        // Bedtime is the reader's own, and sunrise is not a prayer — neither is
        // resolvable from astronomy alone, so callers handle them.
        _ => null,
      };

  /// The next prayer after [from], or null when the day's are all behind us —
  /// in which case the caller wants tomorrow's Fajr.
  (models.PrayerAnchor, DateTime)? next(DateTime from) {
    for (final entry in ordered) {
      if (entry.$2.isAfter(from)) return entry;
    }
    return null;
  }
}

class PrayerCalculator {
  const PrayerCalculator._();

  /// The timetable for [day] at the reader's location, or null when no location
  /// is set — which is a supported state, not a failure: the app hides the
  /// prayer strip and everything else keeps working.
  static PrayerTimetable? forDay(Settings settings, DateTime day) {
    final latitude = settings.latitude;
    final longitude = settings.longitude;
    if (latitude == null || longitude == null) return null;

    final coordinates = adhan.Coordinates(latitude, longitude);
    final components = adhan.DateComponents(day.year, day.month, day.day);

    final parameters = parametersFor(settings.calculationMethod)
      ..madhab = _madhab(settings.madhab)
      ..adjustments = _adjustments(settings);

    final times = adhan.PrayerTimes(coordinates, components, parameters);
    final sunnah = adhan.SunnahTimes(times);

    return PrayerTimetable(
      date: DateTime(day.year, day.month, day.day),
      fajr: times.fajr,
      sunrise: times.sunrise,
      dhuhr: times.dhuhr,
      asr: times.asr,
      maghrib: times.maghrib,
      isha: times.isha,
      islamicMidnight: sunnah.middleOfTheNight,
      lastThirdOfNight: sunnah.lastThirdOfTheNight,
    );
  }

  /// Today's timetable.
  static PrayerTimetable? today(Settings settings) =>
      forDay(settings, DateTime.now());

  /// The whole of [month], for the monthly table.
  static List<PrayerTimetable> forMonth(Settings settings, DateTime month) {
    final days = DateTime(month.year, month.month + 1, 0).day;

    return [
      for (var day = 1; day <= days; day++)
        if (forDay(settings, DateTime(month.year, month.month, day)) case final times?) times,
    ];
  }

  /// The next prayer from now, looking into tomorrow when today's are done.
  ///
  /// Returns tomorrow's Fajr rather than null after Isha, because "no next
  /// prayer" is not a state a reader is ever in and a countdown that vanishes
  /// at night looks broken.
  static (models.PrayerAnchor, DateTime)? nextPrayer(Settings settings) {
    final now = DateTime.now();

    if (today(settings)?.next(now) case final upcoming?) return upcoming;

    final tomorrow = forDay(settings, now.add(const Duration(days: 1)));
    return tomorrow == null ? null : (models.PrayerAnchor.fajr, tomorrow.fajr);
  }

  /// The qibla bearing from the reader's location, clockwise from true north.
  static double? qiblaDirection(Settings settings) {
    final latitude = settings.latitude;
    final longitude = settings.longitude;
    if (latitude == null || longitude == null) return null;

    return adhan.Qibla(adhan.Coordinates(latitude, longitude)).direction;
  }

  /// Great-circle distance to Makkah in kilometres.
  static double? distanceToMakkah(Settings settings) {
    final latitude = settings.latitude;
    final longitude = settings.longitude;
    if (latitude == null || longitude == null) return null;

    const radius = 6371.0;
    final makkah = adhan.Qibla.MAKKAH;

    double radians(double degrees) => degrees * math.pi / 180;

    final dLat = radians(makkah.latitude - latitude);
    final dLon = radians(makkah.longitude - longitude);
    final a = _haversine(dLat) +
        math.cos(radians(latitude)) * math.cos(radians(makkah.latitude)) * _haversine(dLon);

    return 2 * radius * math.asin(math.sqrt(a));
  }

  /// The half-versine, `(1 - cos x) / 2` — named because the formula above
  /// reads as the haversine it is rather than as arithmetic.
  static double _haversine(double x) => (1 - math.cos(x)) / 2;

  /// The astronomical parameters for one convention.
  ///
  /// Returns parameters rather than an `adhan.CalculationMethod` because not
  /// every convention a reader needs is one the package ships: Jordan's is a
  /// plain pair of angles, and expressing it as such is more honest than
  /// borrowing Karachi's enum because the numbers happen to agree today.
  static adhan.CalculationParameters parametersFor(models.CalculationMethod method) =>
      switch (method) {
        // دائرة الإفتاء العام الأردنية. Not in the package, so stated outright.
        models.CalculationMethod.jordan =>
          adhan.CalculationParameters(fajrAngle: 18.0, ishaAngle: 18.0),
        _ => _packaged(method).getParameters(),
      };

  static adhan.CalculationMethod _packaged(models.CalculationMethod method) => switch (method) {
        models.CalculationMethod.ummAlQura => adhan.CalculationMethod.umm_al_qura,
        models.CalculationMethod.muslimWorldLeague => adhan.CalculationMethod.muslim_world_league,
        models.CalculationMethod.egyptian => adhan.CalculationMethod.egyptian,
        models.CalculationMethod.karachi => adhan.CalculationMethod.karachi,
        models.CalculationMethod.kuwait => adhan.CalculationMethod.kuwait,
        models.CalculationMethod.qatar => adhan.CalculationMethod.qatar,
        models.CalculationMethod.dubai => adhan.CalculationMethod.dubai,
        models.CalculationMethod.turkey => adhan.CalculationMethod.turkey,
        models.CalculationMethod.northAmerica => adhan.CalculationMethod.north_america,
        models.CalculationMethod.singapore => adhan.CalculationMethod.singapore,
        models.CalculationMethod.tehran => adhan.CalculationMethod.tehran,
        models.CalculationMethod.moonsightingCommittee =>
          adhan.CalculationMethod.moon_sighting_committee,

        // Handled by [parametersFor] before it reaches here.
        models.CalculationMethod.jordan => adhan.CalculationMethod.other,
      };

  static adhan.Madhab _madhab(models.Madhab madhab) =>
      madhab == models.Madhab.hanafi ? adhan.Madhab.hanafi : adhan.Madhab.shafi;

  /// The reader's own per-prayer corrections, in minutes.
  ///
  /// These sit on top of whatever the convention already applies, which is
  /// exactly right: the reader is correcting against the mosque down the road,
  /// not against the astronomy.
  static adhan.PrayerAdjustments _adjustments(Settings settings) {
    final values = settings.adjustments;
    return adhan.PrayerAdjustments(
      fajr: values['fajr'] ?? 0,
      sunrise: values['sunrise'] ?? 0,
      dhuhr: values['dhuhr'] ?? 0,
      asr: values['asr'] ?? 0,
      maghrib: values['maghrib'] ?? 0,
      isha: values['isha'] ?? 0,
    );
  }
}
