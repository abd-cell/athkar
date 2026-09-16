/// Arabic-Indic numerals, as a presentation choice.
///
/// The design sets every figure in ٠١٢٣ — a clock, a repeat count, a countdown.
/// That is a preference and not a fact about the number, so the conversion
/// happens at the edge, when a value is rendered, and never to the value itself.
/// Nothing in this app parses a string it formatted.
class Numerals {
  const Numerals._();

  static const _arabicIndic = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'];

  /// Renders [value] in the reader's chosen digits.
  static String format(Object value, {required bool arabicIndic}) {
    final text = value.toString();
    if (!arabicIndic) return text;

    final buffer = StringBuffer();
    for (final unit in text.codeUnits) {
      buffer.write(
        unit >= 0x30 && unit <= 0x39 ? _arabicIndic[unit - 0x30] : String.fromCharCode(unit),
      );
    }
    return buffer.toString();
  }

  /// A clock, zero-padded on the minutes only — «٤:٥٢», never «٠٤:٥٢», which is
  /// what the design shows.
  static String time(int hour, int minute, {required bool arabicIndic}) {
    final minutes = minute.toString().padLeft(2, '0');
    return '${format(hour, arabicIndic: arabicIndic)}:${format(minutes, arabicIndic: arabicIndic)}';
  }

  /// Reads a *stored* 24-hour clock string back in the app's own format.
  ///
  /// Stored times arrive in two shapes — «HH:mm» from the bedtime setting and
  /// «HH:mm:ss» from a server-side fixed-time campaign — so the minute is taken
  /// positionally. Reading the last segment instead turns «13:36:00» into half
  /// past midnight, which is a plausible enough time that nobody would question
  /// it on screen.
  ///
  /// Anything unparseable is returned as it was stored: a reminder showing a
  /// raw value is untidy, one showing a confident wrong time is a missed dhikr.
  static String clock(String stored, {required bool arabicIndic}) {
    final parts = stored.split(':');

    final hour = int.tryParse(parts.first);
    final minute = parts.length > 1 ? int.tryParse(parts[1]) : null;

    if (hour == null || minute == null || hour < 0 || hour > 23) return stored;
    if (minute < 0 || minute > 59) return stored;

    return time(hour % 12 == 0 ? 12 : hour % 12, minute, arabicIndic: arabicIndic);
  }

  /// A countdown as «h:mm:ss», which is how the next-prayer strip reads.
  static String countdown(Duration remaining, {required bool arabicIndic}) {
    final clamped = remaining.isNegative ? Duration.zero : remaining;
    final hours = clamped.inHours;
    final minutes = clamped.inMinutes.remainder(60).toString().padLeft(2, '0');
    final seconds = clamped.inSeconds.remainder(60).toString().padLeft(2, '0');

    return '${format(hours, arabicIndic: arabicIndic)}:'
        '${format(minutes, arabicIndic: arabicIndic)}:'
        '${format(seconds, arabicIndic: arabicIndic)}';
  }
}
