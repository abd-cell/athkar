import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The catalogue's own shape — the parsing the whole offline story rests on.
void main() {
  group('Dhikr', () {
    test('is publishable only with both halves of a source', () {
      const withBoth = Dhikr(
        id: 1,
        categoryId: 1,
        sortOrder: 0,
        arabicText: 'نص',
        repeatCount: 1,
        sourceBook: 'صحيح مسلم',
        sourceReference: '٢٦٩١',
      );
      const bookOnly = Dhikr(
        id: 2,
        categoryId: 1,
        sortOrder: 0,
        arabicText: 'نص',
        repeatCount: 1,
        sourceBook: 'صحيح مسلم',
      );
      const neither = Dhikr(
        id: 3,
        categoryId: 1,
        sortOrder: 0,
        arabicText: 'نص',
        repeatCount: 1,
      );

      expect(withBoth.hasSource, isTrue);
      expect(bookOnly.hasSource, isFalse);
      expect(neither.hasSource, isFalse);
    });

    test('survives a round trip through JSON', () {
      const original = Dhikr(
        id: 7,
        categoryId: 2,
        sortOrder: 3,
        arabicText: 'سُبْحَانَ اللهِ',
        repeatCount: 33,
        translation: 'Glory be to Allah',
        sourceBook: 'صحيح مسلم',
        sourceReference: '٥٩٧',
        grade: HadithGrade.sahih,
      );

      final restored = Dhikr.fromJson(original.toJson());

      expect(restored.id, original.id);
      expect(restored.arabicText, original.arabicText);
      expect(restored.repeatCount, original.repeatCount);
      expect(restored.translation, original.translation);
      expect(restored.grade, HadithGrade.sahih);
    });

    test('a payload missing every optional field still parses', () {
      // A cache written by an older build, or a language with no translation:
      // neither is an error, and neither may throw.
      final dhikr = Dhikr.fromJson(const {'id': 1, 'arabicText': 'نص'});

      expect(dhikr.repeatCount, 1);
      expect(dhikr.translation, isNull);
      expect(dhikr.grade, isNull);
      expect(dhikr.hasSource, isFalse);
    });
  });

  group('AthkarCategory', () {
    test('counts a full run as the sum of the repetitions', () {
      const category = AthkarCategory(
        id: 1,
        key: 'after-prayer',
        name: 'أذكار بعد الصلاة',
        sortOrder: 0,
        rhythm: CategoryRhythm.none,
        anchor: PrayerAnchor.none,
        section: CategorySection.adhkar,
        adhkar: [
          Dhikr(id: 1, categoryId: 1, sortOrder: 0, arabicText: 'سبحان الله', repeatCount: 33),
          Dhikr(id: 2, categoryId: 1, sortOrder: 1, arabicText: 'الحمد لله', repeatCount: 33),
          Dhikr(id: 3, categoryId: 1, sortOrder: 2, arabicText: 'الله أكبر', repeatCount: 34),
        ],
      );

      expect(category.totalRepeats, 100);
    });
  });

  group('Catalog', () {
    test('an up-to-date answer carries no categories', () {
      final catalog = Catalog.fromJson(const {
        'version': 12,
        'languageCode': 'ar',
        'isUpToDate': true,
        'categories': [],
      });

      expect(catalog.isUpToDate, isTrue);
      expect(catalog.version, 12);
      expect(catalog.categories, isEmpty);
    });
  });
}
