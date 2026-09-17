import 'package:flutter/material.dart';

import '../core/cities.dart';
import '../core/device_location.dart';
import '../core/l10n.dart';
import '../core/settings.dart';
import '../core/theme.dart';
import '../main.dart';
import '../widgets/athkar_alerts.dart';
import '../widgets/athkar_ui.dart';

/// Where the reader is, by their own choice.
///
/// The list comes first and the permission second, deliberately: an app that
/// opens with a location prompt teaches people to refuse, and this one works
/// perfectly well with a city picked from a list. The GPS row is one option
/// among many, not the happy path.
class LocationScreen extends StatefulWidget {
  const LocationScreen({super.key});

  @override
  State<LocationScreen> createState() => _LocationScreenState();
}

class _LocationScreenState extends State<LocationScreen> {
  var _query = '';
  var _locating = false;
  var _saving = false;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final settings = SettingsScope.of(context);

    final language = settings.languageCode;
    final matches = _query.isEmpty
        ? cities
        : [
            for (final city in cities)
              if (city.nameAr.contains(_query) ||
                  city.nameEn.toLowerCase().contains(_query.toLowerCase()))
                city,
          ];

    return Scaffold(
      appBar: AppBar(title: Text(context.tr('prayer.location'))),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AthkarSpacing.page, 8, AthkarSpacing.page, 12),
            child: TextField(
              onChanged: (value) => setState(() => _query = value),
              decoration: InputDecoration(
                hintText: context.tr('prayer.chooseCity'),
                prefixIcon: Icon(Icons.search, size: 18, color: tokens.muted),
              ),
            ),
          ),
          AthkarCard(
            margin: const EdgeInsets.symmetric(horizontal: AthkarSpacing.page),
            padding: EdgeInsets.zero,
            radius: AthkarSpacing.smallCardRadius,
            child: AthkarListRow(
              label: context.tr('prayer.useMyLocation'),
              icon: Icons.my_location,
              trailing: _locating ? const AthkarSpinner(size: 18) : null,
              onTap: _locating || _saving ? null : _useMyLocation,
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AthkarSpacing.page, 10, AthkarSpacing.page, 6),
            child: Text(
              context.tr('prayer.locationOptional'),
              style: AthkarType.sans(size: 11.5, color: tokens.muted, height: 1.6),
            ),
          ),
          Expanded(
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(
                AthkarSpacing.page, 4, AthkarSpacing.page, 24),
              itemCount: matches.length,
              separatorBuilder: (_, __) => const AthkarRule(margin: EdgeInsets.zero),
              itemBuilder: (context, index) {
                final city = matches[index];

                return AthkarListRow(
                  label: city.name(language),
                  value: city.country,
                  onTap: _saving
                      ? null
                      : () => _choose(
                          city.latitude,
                          city.longitude,
                          city.name(language),
                          country: city.country,
                        ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _choose(
    double latitude,
    double longitude,
    String name, {
    String? country,
  }) async {
    if (_saving) return;
    setState(() => _saving = true);

    final settings = SettingsScope.read(context);
    final state = AppStateScope.read(context);

    try {
      await settings.setLocation(latitude, longitude, name, country: country);
      // Every anchored reminder just moved with the location.
      await state.rescheduleReminders();

      if (mounted) Navigator.of(context).pop();
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  /// The only place in the app that asks for location permission.
  Future<void> _useMyLocation() async {
    setState(() => _locating = true);

    final result = await DeviceLocation.current();

    if (!mounted) return;
    setState(() => _locating = false);

    switch (result) {
      case LocationFix(:final latitude, :final longitude):
        // Named from the nearest city in the built-in list rather than from a
        // reverse-geocoding service: the name is decoration on a prayer table,
        // and it is not worth a network call or a third party seeing a
        // coordinate.
        final nearest = nearestCity(latitude, longitude);

        await _choose(
          latitude,
          longitude,
          nearest?.name(SettingsScope.read(context).languageCode) ?? '',
          // The nearest city carries the country, and the country carries the
          // convention — so "use my location" lands a reader on their own
          // authority's times rather than on whatever the default happened to
          // be. The guess is only as good as the list, which is why it never
          // overrides a convention the reader chose.
          country: nearest?.country,
        );

      case LocationRefused():
        AthkarAlerts.error(context, context.tr('prayer.locationOptional'));
    }
  }

}
