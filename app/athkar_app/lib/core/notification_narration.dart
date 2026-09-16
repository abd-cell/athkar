import '../models/models.dart';
import 'bidi_text.dart';
import 'content_store.dart';

/// Chooses the narration that rides under a reminder's announcement line.
///
/// A notification that says only «حان وقت صلاة الفجر» is a clock. Pulling it
/// down to find a hadith with its takhrij under it is the app's one claim made
/// visible on the lock screen — the same promise the dhikr card makes, in the
/// place a reader actually looks.
///
/// The text comes from the catalogue already on the phone. Nothing is fetched,
/// nothing is composed on the server, and nothing is invented here: the server
/// refuses to publish a dhikr without a book and a number, so anything this
/// picks can be traced.
class NotificationNarration {
  const NotificationNarration._();

  /// The longest narration worth putting on a lock screen.
  ///
  /// Both systems will happily accept more and then cut it — and a narration
  /// shown cut is worse than no narration at all, because the reader cannot see
  /// that it ended early. So a dhikr longer than this is passed over rather
  /// than trimmed; there is almost always a shorter one in the same chapter.
  static const maxLength = 240;

  /// Composes the body of one reminder on one day.
  ///
  /// Returns the announcement alone when there is nothing to attach, which is
  /// an ordinary outcome: a fresh install with no catalogue yet, a chapter whose
  /// every dhikr is long, or a build whose cache predates the takhrij rule.
  static String compose({
    required DeviceReminder reminder,
    required ContentStore content,
    required DateTime when,
    String? cityName,
    String Function(String key)? copy,
  }) {
    final announcement = fill(reminder.body, cityName: cityName, copy: copy);

    final dhikr = pick(reminder: reminder, content: content, when: when);
    if (dhikr == null) return announcement;

    final takhrij = '${dhikr.sourceBook} ${dhikr.sourceReference}';

    // A blank line, not a dash or a bullet: the announcement and the narration
    // are two separate utterances, and running them together would read as one
    // sentence the app had written.
    return '$announcement\n\n${dhikr.arabicText}\n$takhrij';
  }

  /// Fills the placeholders an admin may write into a campaign's wording.
  ///
  /// Today there is one, `{city}`, and it exists because of a rule rather than a
  /// preference: **the server never learns where a reader is**. Coordinates are
  /// not sent, so «حسب التوقيت المحلي لمدينة عمان» cannot be authored in the CMS
  /// — it would be a lie on every phone outside Amman. The campaign carries the
  /// sentence with a hole in it, and the phone, which is the only party that
  /// knows, fills it.
  ///
  /// A reader who has not set a location yet gets a word that is true of
  /// everybody rather than a literal `{city}` on their lock screen. An unknown
  /// placeholder is left alone: it is somebody's typo in the CMS, and eating it
  /// silently would make that typo invisible.
  ///
  /// The value goes in **isolated**. «حسب التوقيت المحلي لمدينة Amman» is an
  /// Arabic sentence with a Latin run at the end, and without an isolate the
  /// space and any punctuation after it are pulled into that run — the sentence
  /// ends up with its full stop on the wrong side. Which way a city name reads
  /// is not knowable in the CMS, so it is isolated whichever way it turns out.
  static String fill(
    String text, {
    String? cityName,
    String Function(String key)? copy,
  }) {
    if (!text.contains('{city}')) return text;

    final city = (cityName ?? '').trim();
    final fallback = copy?.call('reminder.yourLocation') ?? '';

    return text.replaceAll('{city}', BidiText.isolate(city.isNotEmpty ? city : fallback));
  }

  /// The dhikr for one reminder on one day, or null when none qualifies.
  static Dhikr? pick({
    required DeviceReminder reminder,
    required ContentStore content,
    required DateTime when,
  }) {
    final category = _category(reminder, content, when);
    if (category == null) return null;

    final candidates = [
      for (final dhikr in category.adhkar)
        if (dhikr.hasSource && dhikr.arabicText.length <= maxLength) dhikr,
    ];

    if (candidates.isEmpty) return null;

    // Stable for a given reminder and day, and different from one day to the
    // next. Stability is the part that matters: the week's queue is rebuilt on
    // every launch, and a random pick would hand the same morning two different
    // narrations depending on whether the reader happened to open the app.
    //
    // The reminder id is mixed in so two reminders falling on one day do not
    // both land on the same dhikr.
    final day = DateTime(when.year, when.month, when.day)
        .difference(DateTime.utc(2000, 1, 1))
        .inDays;

    return candidates[(day + reminder.id) % candidates.length];
  }

  /// Which chapter to draw from.
  ///
  /// A reminder that names one is answered directly. The prayer campaigns name
  /// none — they are anchored to a time, not to a chapter — so they fall back to
  /// whatever the app would suggest at that hour, which is what the home screen
  /// already shows. Fajr therefore quotes from the morning adhkar, and the two
  /// agree without anybody configuring it twice.
  static AthkarCategory? _category(
    DeviceReminder reminder,
    ContentStore content,
    DateTime when,
  ) {
    if (reminder.categoryId case final id?) {
      final named = content.byId(id);
      if (named != null && named.adhkar.isNotEmpty) return named;
    }

    final suggested = content.suggestedFor(when);
    return suggested.isEmpty ? null : suggested.first;
  }
}
