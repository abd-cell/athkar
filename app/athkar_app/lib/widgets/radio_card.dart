import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/radio_player.dart';
import '../core/theme.dart';
import 'athkar_ui.dart';
import '../models/models.dart';

/// «إذاعات صوتية» — the live stations, on the home screen.
///
/// Absent entirely when the catalogue carries none: an empty heading is a
/// promise the app is not keeping, and a reader whose admin publishes no
/// station should simply not see the section.
class RadioSection extends StatelessWidget {
  const RadioSection({super.key, required this.stations});

  final List<RadioStation> stations;

  @override
  Widget build(BuildContext context) {
    if (stations.isEmpty) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          context.tr('home.radio'),
          style: AthkarType.amiri(
            size: 17,
            color: AthkarTokens.of(context).ink,
            weight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 10),
        for (var i = 0; i < stations.length; i++) ...[
          if (i > 0) const SizedBox(height: 10),
          RadioStationCard(station: stations[i]),
        ],
      ],
    );
  }
}

/// One station: its name, its broadcaster, its logo, and the one button.
///
/// The button is the whole card — a station either plays or it does not, and
/// there is no volume, no seek and no playlist to put beside it. What the card
/// owes the reader instead is an honest state: *connecting* is not *playing*,
/// and a stream that would not open says so rather than sitting silent with a
/// pause icon on it.
class RadioStationCard extends StatelessWidget {
  const RadioStationCard({super.key, required this.station});

  final RadioStation station;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return ListenableBuilder(
      listenable: RadioPlayer.instance,
      builder: (context, _) {
        final player = RadioPlayer.instance;
        final playing = player.isPlaying(station);
        final connecting = player.isBusy(station) && !playing;
        final failed = player.failed && player.station?.id == station.id;

        return Container(
          padding: const EdgeInsets.fromLTRB(18, 16, 18, 16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AthkarSpacing.cardRadius),
            // A gradient rather than the flat brand fill of the «ذكر الآن» card,
            // so the two green cards on the page are not mistaken for each
            // other — one is today's reading, this one is a broadcast.
            gradient: LinearGradient(
              begin: AlignmentDirectional.topStart,
              end: AlignmentDirectional.bottomEnd,
              colors: [
                tokens.brand,
                Color.lerp(tokens.brand, tokens.onBrand, 0.28) ?? tokens.brand,
              ],
            ),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      station.name,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: AthkarType.amiri(
                        size: 21,
                        color: tokens.onBrand,
                        weight: FontWeight.w700,
                        height: 1.4,
                      ),
                    ),
                    if (station.provider case final provider?) ...[
                      const SizedBox(height: 4),
                      Text(
                        provider,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AthkarType.sans(
                          size: 12.5,
                          color: tokens.onBrand.withValues(alpha: 0.72),
                        ),
                      ),
                    ],
                    const SizedBox(height: 14),
                    // Aligned rather than stretched: the button is as wide as
                    // its own words, which is what keeps «استماع مباشر» reading
                    // as a button and not as a second card.
                    Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: _ListenButton(
                        playing: playing,
                        connecting: connecting,
                        onPressed: () => RadioPlayer.instance.toggle(station),
                      ),
                    ),
                    if (failed) ...[
                      const SizedBox(height: 8),
                      Text(
                        context.tr('home.radio.failed'),
                        style: AthkarType.sans(
                          size: 11.5,
                          color: tokens.onBrand.withValues(alpha: 0.85),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: 14),
              _Logo(station: station),
            ],
          ),
        );
      },
    );
  }
}

/// The station's logo, or the app's own glyph when there is none — and when
/// there is one that will not load, which on a home screen is the same thing.
class _Logo extends StatelessWidget {
  const _Logo({required this.station});

  final RadioStation station;

  static const double _size = 76;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    final fallback = Icon(
      Icons.radio_outlined,
      size: 34,
      color: tokens.onBrand.withValues(alpha: 0.85),
    );

    return SizedBox(
      width: _size,
      height: _size,
      child: station.logoUrl == null
          ? fallback
          : Image.network(
              station.logoUrl!,
              fit: BoxFit.contain,
              errorBuilder: (_, __, ___) => fallback,
              // No spinner while it loads: the logo is decoration, and a
              // placeholder flickering beside the name is worse than a logo
              // that simply appears.
              loadingBuilder: (_, child, progress) =>
                  progress == null ? child : const SizedBox.shrink(),
            ),
    );
  }
}

/// «استماع مباشر», and what it becomes once it is pressed.
class _ListenButton extends StatelessWidget {
  const _ListenButton({
    required this.playing,
    required this.connecting,
    required this.onPressed,
  });

  final bool playing;
  final bool connecting;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);

    return Material(
      color: tokens.brandInk,
      borderRadius: BorderRadius.circular(999),
      child: InkWell(
        onTap: onPressed,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          // No `alignment` on the container: giving one makes it take every
          // pixel its constraints allow, which is how a pill button becomes a
          // full-width bar. The row inside is what sets the width.
          constraints: const BoxConstraints(minHeight: AthkarSpacing.tapTarget),
          padding: const EdgeInsetsDirectional.fromSTEB(18, 0, 16, 0),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (connecting)
                AthkarSpinner.mono(tokens.onBrand, size: 16)
              else
                Icon(
                  playing ? Icons.stop_rounded : Icons.radio,
                  size: 18,
                  color: tokens.onBrand,
                ),
              const SizedBox(width: 8),
              Text(
                context.tr(
                  connecting
                      ? 'home.radio.connecting'
                      : playing
                          ? 'home.radio.stop'
                          : 'home.radio.listen',
                ),
                style: AthkarType.sans(
                  size: 13.5,
                  color: tokens.onBrand,
                  weight: FontWeight.w600,
                ),
              ),
              if (!connecting && !playing) ...[
                const SizedBox(width: 8),
                // The live dot, the one thing on the card that is not the
                // brand's own colour — it is the word «مباشر» drawn rather
                // than written.
                Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(color: tokens.alert, shape: BoxShape.circle),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
