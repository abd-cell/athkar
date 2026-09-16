import 'package:athkar_app/features/share_sheet.dart';
import 'package:athkar_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The two rules the share sheet exists to keep.
///
/// One is about what a shared *image* must carry — the reference, because a
/// picture of a dhikr with no takhrij on it is precisely the object this
/// project exists to stop circulating. The other is about what a shared *text*
/// must not carry: a wordmark, a link, an invitation to download. The two
/// pull in opposite directions, which is why they are worth holding down.
void main() {
  Dhikr dhikr({String? book, String? reference}) => Dhikr(
        id: 1,
        categoryId: 1,
        sortOrder: 0,
        arabicText: 'سُبْحَانَ اللهِ وَبِحَمْدِهِ',
        repeatCount: 100,
        sourceBook: book,
        sourceReference: reference,
      );

  group('ShareSubject.dhikr', () {
    test('the shared text carries the reference and nothing else', () {
      final subject = ShareSubject.dhikr(
        dhikr(book: 'صحيح البخاري', reference: '٦٤٠٥'),
      );

      expect(subject.plainText, contains('سُبْحَانَ اللهِ وَبِحَمْدِهِ'));
      expect(subject.plainText, contains('صحيح البخاري'));
      expect(subject.plainText, contains('٦٤٠٥'));

      // No app name, no link. A dhikr passed on should read as the dhikr.
      expect(subject.plainText, isNot(contains('أذكاري')));
      expect(subject.plainText, isNot(contains('http')));
    });

    test('the card shows book, number and grading on one line', () {
      final subject = ShareSubject.dhikr(
        dhikr(book: 'صحيح البخاري', reference: '٦٤٠٥'),
        grade: 'صحيح',
      );

      expect(subject.attribution, 'صحيح البخاري · ٦٤٠٥ · صحيح');
    });

    test('a grading is omitted rather than guessed at', () {
      final subject = ShareSubject.dhikr(
        dhikr(book: 'صحيح البخاري', reference: '٦٤٠٥'),
      );

      expect(subject.attribution, 'صحيح البخاري · ٦٤٠٥');
    });

    /// A cache written by an older build can hold a dhikr that predates the
    /// publish-requires-a-source rule. The card then shows no takhrij line —
    /// it does not invent one, and it does not refuse to render.
    test('a dhikr with no source gets no attribution line', () {
      final subject = ShareSubject.dhikr(dhikr());

      expect(subject.attribution, isNull);
      expect(subject.plainText, 'سُبْحَانَ اللهِ وَبِحَمْدِهِ');
    });
  });

  group('ShareDesign', () {
    /// The carousel's dots are built from this list, and the capture reads the
    /// boundary at the current index. A design added without a matching entry
    /// would be swipeable and uncapturable.
    test('every design is distinct, and there are enough to be worth choosing', () {
      expect(ShareDesign.all.length, greaterThanOrEqualTo(2));
      expect(
        ShareDesign.all.map((design) => design.ground).toSet().length,
        ShareDesign.all.length,
      );
    });

    /// The mark sits in [ShareDesign.ground] on a fill of [ShareDesign.accent];
    /// a design whose two were equal would print an invisible «ذ».
    test('the mark is legible on every ground', () {
      for (final design in ShareDesign.all) {
        expect(design.accent, isNot(design.ground), reason: '$design');
        expect(design.ink, isNot(design.ground), reason: '$design');
      }
    });
  });
}
