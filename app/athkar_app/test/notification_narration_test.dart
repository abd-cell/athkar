import 'dart:convert';

import 'package:athkar_app/core/content_store.dart';
import 'package:athkar_app/core/notification_narration.dart';
import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// The narration that rides under a reminder's announcement.
///
/// Three rules carry the whole feature, and each one is here because breaking
/// it would be invisible on a developer's screen and obvious on a reader's:
///
/// - **Nothing unsourced.** The takhrij is the app's one claim; a line on a lock
///   screen that cannot be traced is the claim broken in the most public place
///   it appears.
/// - **Nothing truncated.** A narration cut mid-sentence reads as a complete
///   one, and the reader has no way to see that it ended early.
/// - **Stable for a day.** The week's queue is rebuilt on every launch, so a
///   pick that moved would give the same morning different narrations depending
///   on whether the reader happened to open the app.
void main() {
  const arabic = 'سُبْحَانَ اللَّهِ وَبِحَمْدِهِ، سُبْحَانَ اللَّهِ الْعَظِيمِ';

  Map<String, dynamic> dhikr({
    required int id,
    String text = arabic,
    String? book = 'صحيح البخاري',
    String? reference = '٦٤٠٥',
  }) =>
      {
        'id': id,
        'categoryId': 1,
        'sortOrder': id,
        'arabicText': text,
        'repeatCount': 1,
        'sourceBook': book,
        'sourceReference': reference,
      };

  Future<ContentStore> storeWith(List<Map<String, dynamic>> adhkar) async {
    SharedPreferences.setMockInitialValues({
      'content.catalog': jsonEncode([
        {
          'id': 1,
          'key': 'morning',
          'name': 'أذكار الصباح',
          'sortOrder': 1,
          'anchor': PrayerAnchor.sunrise.value,
          'adhkar': adhkar,
        },
      ]),
    });

    return ContentStore.load();
  }

  DeviceReminder reminder({
    int id = 1,
    int? categoryId = 1,
    String body = 'حان وقت أذكار الصباح',
  }) =>
      DeviceReminder(
        id: id,
        key: 'morning-adhkar',
        categoryId: categoryId,
        kind: ReminderKind.prayerAnchored,
        anchor: PrayerAnchor.sunrise,
        offsetMinutes: 30,
        days: 127,
        isUserAdjustable: true,
        androidChannelId: 'athkar.reminders.v1',
        version: 1,
        title: 'أذكار الصباح',
        body: body,
      );

  final when = DateTime(2026, 9, 13, 5, 30);


  group('the {city} placeholder', () {
    // The server never learns where a reader is — coordinates are not sent — so
    // «حسب التوقيت المحلي لمدينة عمان» cannot be authored in the CMS. The
    // campaign carries the sentence with a hole in it and the phone fills it.
    String copy(String key) => key == 'reminder.yourLocation' ? 'موقعك' : key;

    // The value goes in wrapped in a first-strong isolate, so a Latin city name
    // cannot drag the sentence's punctuation into its own run. See
    // `bidi_text_test.dart` for what that is worth on a lock screen.
    String isolated(String value) => '\u2068$value\u2069';

    test('is filled with the city the reader chose', () {
      expect(
        NotificationNarration.fill(
          'حسب التوقيت المحلي لمدينة {city}',
          cityName: 'عمّان',
          copy: copy,
        ),
        'حسب التوقيت المحلي لمدينة ${isolated('عمّان')}',
      );
    });

    test('falls back to a word true of everybody when no city is set', () {
      // A fresh install has no location. A literal «{city}» on a lock screen is
      // the worst of the three possible outcomes.
      for (final none in [null, '', '   ']) {
        expect(
          NotificationNarration.fill(
            'حسب التوقيت المحلي لمدينة {city}',
            cityName: none,
            copy: copy,
          ),
          'حسب التوقيت المحلي لمدينة ${isolated('موقعك')}',
        );
      }
    });

    test('leaves wording that has no placeholder exactly as written', () {
      const plain = 'ابدأ يومك بذكر الله.';
      expect(NotificationNarration.fill(plain, cityName: 'عمّان', copy: copy), plain);
    });

    test('leaves an unknown placeholder alone rather than eating it', () {
      // It is somebody's typo in the CMS. Swallowing it silently is how a typo
      // survives to production.
      expect(
        NotificationNarration.fill('{town} شيء', cityName: 'عمّان', copy: copy),
        '{town} شيء',
      );
    });

    test('the announcement is filled before the narration is appended', () async {
      final content = await storeWith([dhikr(id: 1)]);

      final body = NotificationNarration.compose(
        reminder: reminder(body: 'حسب التوقيت المحلي لمدينة {city}'),
        content: content,
        when: when,
        cityName: 'عمّان',
        copy: copy,
      );

      expect(body, startsWith('حسب التوقيت المحلي لمدينة ${isolated('عمّان')}'));
      expect(body, isNot(contains('{city}')));
    });
  });

  test('the announcement carries the narration and its takhrij', () async {
    final content = await storeWith([dhikr(id: 1)]);

    final body = NotificationNarration.compose(
      reminder: reminder(),
      content: content,
      when: when,
    );

    expect(body, startsWith('حان وقت أذكار الصباح'));
    expect(body, contains(arabic));
    expect(body, contains('صحيح البخاري ٦٤٠٥'));
  });

  test('an unsourced dhikr is never quoted', () async {
    // The server refuses to publish one, but a cache written by an older build
    // may still hold it — and the lock screen is the last place to discover
    // that the takhrij promise has a hole in it.
    final content = await storeWith([dhikr(id: 1, book: null, reference: null)]);

    final body = NotificationNarration.compose(
      reminder: reminder(),
      content: content,
      when: when,
    );

    expect(body, 'حان وقت أذكار الصباح');
  });

  test('a narration too long to show whole is passed over, not cut', () async {
    final content = await storeWith([
      dhikr(id: 1, text: 'ا' * (NotificationNarration.maxLength + 1)),
    ]);

    expect(
      NotificationNarration.compose(reminder: reminder(), content: content, when: when),
      'حان وقت أذكار الصباح',
    );
  });

  test('a long dhikr does not hide a short one in the same chapter', () async {
    final content = await storeWith([
      dhikr(id: 1, text: 'ا' * (NotificationNarration.maxLength + 1)),
      dhikr(id: 2),
    ]);

    expect(
      NotificationNarration.compose(reminder: reminder(), content: content, when: when),
      contains(arabic),
    );
  });

  test('the same reminder on the same day always picks the same narration', () async {
    // The rebuild runs on every launch. A pick that moved would rewrite
    // tomorrow's reminder each time the reader opened the app.
    final content = await storeWith([
      dhikr(id: 1, reference: '١'),
      dhikr(id: 2, reference: '٢'),
      dhikr(id: 3, reference: '٣'),
    ]);

    final first = NotificationNarration.pick(reminder: reminder(), content: content, when: when);
    final again = NotificationNarration.pick(
      reminder: reminder(),
      content: content,
      // Same day, later hour — the queue is rebuilt whenever the app opens.
      when: DateTime(2026, 9, 13, 21, 0),
    );

    expect(first!.id, again!.id);
  });

  test('a different day gives a different narration', () async {
    final content = await storeWith([
      dhikr(id: 1, reference: '١'),
      dhikr(id: 2, reference: '٢'),
    ]);

    final today = NotificationNarration.pick(reminder: reminder(), content: content, when: when);
    final tomorrow = NotificationNarration.pick(
      reminder: reminder(),
      content: content,
      when: when.add(const Duration(days: 1)),
    );

    expect(today!.id, isNot(tomorrow!.id));
  });

  test('two reminders on one day do not both quote the same dhikr', () async {
    final content = await storeWith([
      dhikr(id: 1, reference: '١'),
      dhikr(id: 2, reference: '٢'),
    ]);

    final morning = NotificationNarration.pick(
      reminder: reminder(id: 1),
      content: content,
      when: when,
    );
    final prayer = NotificationNarration.pick(
      reminder: reminder(id: 2),
      content: content,
      when: when,
    );

    expect(morning!.id, isNot(prayer!.id));
  });

  test('a reminder naming no chapter falls back to the hour', () async {
    // The prayer campaigns name no category — they are anchored to a time, not
    // a chapter — so Fajr quotes from whatever the home screen would be showing
    // at that hour rather than quoting nothing.
    final content = await storeWith([dhikr(id: 1)]);

    final body = NotificationNarration.compose(
      reminder: reminder(categoryId: null),
      content: content,
      when: when,
    );

    expect(body, contains(arabic));
  });

  test('an empty catalogue is an ordinary outcome, not a crash', () async {
    SharedPreferences.setMockInitialValues({});
    final content = await ContentStore.load();

    expect(
      NotificationNarration.compose(reminder: reminder(), content: content, when: when),
      'حان وقت أذكار الصباح',
    );
  });
}
