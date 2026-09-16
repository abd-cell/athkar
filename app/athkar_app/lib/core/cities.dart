import '../models/models.dart';

/// A short list of cities, so the app can offer prayer times without ever
/// asking for location.
///
/// Compiled in rather than fetched: it is a few kilobytes, it is needed on the
/// very first launch, and a reader choosing their city should not have to be
/// online to do it. Coordinates are city-centre and rounded — prayer times move
/// by about a minute per 25 km, so more precision would be false precision.
///
/// The list is not meant to be exhaustive. It covers the cities the app's first
/// readers are in; anyone elsewhere uses "use my location", which is one tap.
class City {
  const City(this.nameAr, this.nameEn, this.latitude, this.longitude, this.country);

  final String nameAr;
  final String nameEn;
  final double latitude;
  final double longitude;
  final String country;

  String name(String languageCode) => languageCode == 'ar' ? nameAr : nameEn;
}

/// The prayer-time convention each country's own authority publishes.
///
/// This map is the fix for a quiet, real error: the app stored every city's
/// country, showed it in the picker, and then threw it away — so a reader in
/// Amman kept whatever global default the admin had set. That default was Umm
/// al-Qura, which puts Isha at a fixed ninety minutes after Maghrib because
/// that is the rule in Makkah. In Amman it is eight minutes late, every night.
///
/// A convention is not a preference and not a rounding difference. It is the
/// mosque's time or it is not, and a reader has no way to know the app is
/// quietly using another country's rule.
///
/// Anywhere unlisted keeps the configured default, which is the honest answer
/// when nobody has told us what the local authority uses.
const countryCalculationMethods = <String, CalculationMethod>{
  'SA': CalculationMethod.ummAlQura,
  'JO': CalculationMethod.jordan,
  'AE': CalculationMethod.dubai,
  'KW': CalculationMethod.kuwait,
  'QA': CalculationMethod.qatar,
  'EG': CalculationMethod.egyptian,
  'TR': CalculationMethod.turkey,
  'PK': CalculationMethod.karachi,
  'SG': CalculationMethod.singapore,
  'IR': CalculationMethod.tehran,
};

/// What [country] uses, or null when we have not been told.
CalculationMethod? methodForCountry(String? country) =>
    country == null ? null : countryCalculationMethods[country.toUpperCase()];

const cities = <City>[
  City('مكة المكرمة', 'Makkah', 21.3891, 39.8579, 'SA'),
  City('المدينة المنورة', 'Madinah', 24.5247, 39.5692, 'SA'),
  City('الرياض', 'Riyadh', 24.7136, 46.6753, 'SA'),
  City('جدة', 'Jeddah', 21.4858, 39.1925, 'SA'),
  City('الدمام', 'Dammam', 26.4207, 50.0888, 'SA'),
  City('أبها', 'Abha', 18.2164, 42.5053, 'SA'),
  City('تبوك', 'Tabuk', 28.3838, 36.5550, 'SA'),
  City('عمّان', 'Amman', 31.9539, 35.9106, 'JO'),
  City('إربد', 'Irbid', 32.5556, 35.8500, 'JO'),
  City('العقبة', 'Aqaba', 29.5320, 35.0063, 'JO'),
  City('القدس', 'Jerusalem', 31.7683, 35.2137, 'PS'),
  City('غزة', 'Gaza', 31.5017, 34.4668, 'PS'),
  City('بيروت', 'Beirut', 33.8938, 35.5018, 'LB'),
  City('دمشق', 'Damascus', 33.5138, 36.2765, 'SY'),
  City('حلب', 'Aleppo', 36.2021, 37.1343, 'SY'),
  City('بغداد', 'Baghdad', 33.3152, 44.3661, 'IQ'),
  City('البصرة', 'Basra', 30.5081, 47.7835, 'IQ'),
  City('أربيل', 'Erbil', 36.1911, 44.0092, 'IQ'),
  City('الكويت', 'Kuwait City', 29.3759, 47.9774, 'KW'),
  City('الدوحة', 'Doha', 25.2854, 51.5310, 'QA'),
  City('المنامة', 'Manama', 26.2285, 50.5860, 'BH'),
  City('مسقط', 'Muscat', 23.5880, 58.3829, 'OM'),
  City('دبي', 'Dubai', 25.2048, 55.2708, 'AE'),
  City('أبوظبي', 'Abu Dhabi', 24.4539, 54.3773, 'AE'),
  City('الشارقة', 'Sharjah', 25.3463, 55.4209, 'AE'),
  City('صنعاء', 'Sanaa', 15.3694, 44.1910, 'YE'),
  City('عدن', 'Aden', 12.7855, 45.0187, 'YE'),
  City('القاهرة', 'Cairo', 30.0444, 31.2357, 'EG'),
  City('الإسكندرية', 'Alexandria', 31.2001, 29.9187, 'EG'),
  City('الخرطوم', 'Khartoum', 15.5007, 32.5599, 'SD'),
  City('طرابلس', 'Tripoli', 32.8872, 13.1913, 'LY'),
  City('تونس', 'Tunis', 36.8065, 10.1815, 'TN'),
  City('الجزائر', 'Algiers', 36.7538, 3.0588, 'DZ'),
  City('الدار البيضاء', 'Casablanca', 33.5731, -7.5898, 'MA'),
  City('الرباط', 'Rabat', 34.0209, -6.8416, 'MA'),
  City('إسطنبول', 'Istanbul', 41.0082, 28.9784, 'TR'),
  City('أنقرة', 'Ankara', 39.9334, 32.8597, 'TR'),
  City('لندن', 'London', 51.5074, -0.1278, 'GB'),
  City('باريس', 'Paris', 48.8566, 2.3522, 'FR'),
  City('برلين', 'Berlin', 52.5200, 13.4050, 'DE'),
  City('نيويورك', 'New York', 40.7128, -74.0060, 'US'),
  City('تورونتو', 'Toronto', 43.6532, -79.3832, 'CA'),
  City('كوالالمبور', 'Kuala Lumpur', 3.1390, 101.6869, 'MY'),
  City('جاكرتا', 'Jakarta', -6.2088, 106.8456, 'ID'),
  City('كراتشي', 'Karachi', 24.8607, 67.0011, 'PK'),
  City('لاهور', 'Lahore', 31.5204, 74.3587, 'PK'),
];

/// The listed city closest to a coordinate.
///
/// Used for two things that both want the same answer: naming a GPS fix, and
/// working out which country's prayer convention applies to a reader who has
/// never told us. It is a guess, and a coarse one near a border — which is
/// exactly why it never overrides a convention the reader chose themselves.
City? nearestCity(double? latitude, double? longitude) {
  if (latitude == null || longitude == null) return null;

  City? nearest;
  var best = double.infinity;

  for (final city in cities) {
    // Squared degrees: wrong as a distance, perfectly good as an ordering,
    // and free.
    final distance = (city.latitude - latitude) * (city.latitude - latitude) +
        (city.longitude - longitude) * (city.longitude - longitude);

    if (distance < best) {
      best = distance;
      nearest = city;
    }
  }

  return nearest;
}
