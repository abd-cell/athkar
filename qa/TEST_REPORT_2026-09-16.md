# Test & UI/UX report — 16 Sep 2026

Scope: the whole working tree, which carries two unlanded features — the
**live radio** (new slice across all three stacks) and **`CategorySection`**
(the أذكار index regrouped by an editor-set field instead of a key list
compiled into the app).

## 1. Suites

| Suite | Command | Result |
|---|---|---|
| Backend | `dotnet test` | **250 passed**, 0 failed |
| App unit | `flutter test` | **144 passed**, 0 failed |
| App static | `flutter analyze` | **0 issues** |
| CMS unit | `ng test --watch=false` | **18 passed** (3 files) |
| CMS build | `npm run build` | clean, 13.7 s |
| App release build | `flutter build web --release` | clean, 81 s — `just_audio` compiles for web |

Two notes on the commands themselves:

- `ng test --browsers=ChromeHeadless` **fails** — the vitest browser provider
  (`@vitest/browser-playwright` or similar) is not a dependency. Plain
  `ng test --watch=false` is the working invocation; worth writing down before
  someone puts the browser flag in CI.
- Cross-stack contract checks I ran by hand all pass: `CategorySection` is
  `0/1/2/3` identically in C#, TypeScript and Dart; CMS i18n is 606 keys in
  both `ar.ts` and `en.ts` with no asymmetry; all 70 `| translate` keys used by
  the new radio and categories templates resolve.

## 2. Defects found

### 2.1 The section chevron points the wrong way in both languages
`app/athkar_app/lib/features/adhkar_screen.dart:199` — `_SectionCard` draws
`Icons.chevron_left`. Material declares that icon with
`matchTextDirection: true` (verified in the SDK, `material/icons.dart:5482`),
so it is **mirrored again** under RTL: in Arabic it points right, which is
backwards for forward navigation, and in English it points left. Every other
disclosure row in the app uses `chevron_right` — `widgets/athkar_ui.dart:318`
even carries a comment spelling this rule out, as does
`features/monthly_screen.dart:161`.

Fix: `Icons.chevron_right`.

### 2.2 The radio reorder endpoint has no control panel behind it
`backend/.../Admin/RadioAdminController.cs:43` serves `PUT admin/radio/reorder`;
the service bumps `ContentVersion` and writes an audit row for it. But
`cms/.../core/api/api.service.ts` has no `reorderRadioStations` — the only
reorder call in the CMS is `reorderWidgetCatalog`. An admin can currently only
reorder stations by typing `sortOrder` integers into the edit dialog, one
station at a time, which is the thing the endpoint exists to replace.

### 2.3 Neither new app-side piece is tested
The backend radio slice has 7 tests (cleartext refused, duplicate key, version
bump, translation replacement, missing station). The app side has none:

- `core/radio_player.dart` — a real state machine (`toggle` on the station
  already playing stops it; a `completed` stream returns to idle; `failed`
  resets on the next press) with nothing asserting any of it.
- `core/content_store.dart` — the radio cache round-trip, and specifically the
  invariant its own comment states: *written even when empty, so withdrawing
  the last station takes the section off the home screen.* That is exactly the
  kind of rule this repo tests elsewhere. `content_store_test.dart` got only a
  compile fix (`section:` added to a fixture).

### 2.4 Accessibility
- **Tap target is 44 dp** (`core/theme.dart:281`). That is Apple's minimum, not
  Android's, where the guidance is 48 dp. The new radio Listen button uses it
  (`AthkarSpacing.tapTarget`).
- **Four icon buttons have no accessible name** — no `tooltip`, so TalkBack and
  VoiceOver announce only "button": `adhkar_screen.dart:75` and
  `category_screen.dart:64` (both are *search* — the same action **is**
  tooltipped on `home_screen.dart:195`, so this is an inconsistency, not a
  policy), and `monthly_screen.dart:164`/`:180` (previous/next month).
- The whole UI contains **one** `Semantics()` widget (`share_sheet.dart:380`).
  The radio card's "connecting" state is a spinner with no announcement.
- In the CMS, **no `<label>` is associated with its input** anywhere — 0
  occurrences of `for=` across the content templates, the new radio dialog
  included.

## 3. Repo-wide issues the new code inherits rather than causes

These are pre-existing patterns; the radio and section work simply follows them.

- **No plural handling in the app, in either language.** `'{count} باباً'`
  reads «١ باباً» at one and «٣ باباً» at three, where Arabic wants باب /
  بابان / أبواب; `'{count} chapters'` reads "1 chapters". Eight count strings
  are affected, `adhkar.section.count` and `reading.times` among them. This sits
  oddly beside `strings_test.dart`, which already enforces that Arabic copy be
  composed rather than concatenated.
- **Delete confirmations show a bare key.** `confirm(row.key)` on nine CMS
  screens, radio included — a dialog with an identifier in it and no question.
  `quran.component.ts:274` already fixed this and documented why ("asked the
  question by showing a filename and nothing else, which is not a question");
  the fix was never carried across.
- **A failed list load renders as an empty state.** `radio.component.ts:47`,
  like its siblings, does `rows.set(response.data?.data ?? [])` with no error
  branch, so an admin whose API is down is told there are no stations.
- **`nameOf()` hardcodes `'ar'`** (radio, categories, adhkar). A station titled
  only in English falls back to showing its raw key.
- Radio's list is fetched at `pageSize: 100` with no pagination. Defensible —
  the list is short by nature — but station 101 is invisible with no signal.

## 4. What is right

Worth recording, because it is most of the diff:

- The radio backend slice observes every house rule: `BumpContentVersion()` on
  all four mutations, an audit call after each, soft delete of the station *and*
  its translations, and an https-only rule enforced server-side with its own
  `ErrorCode` (309) — mapped in the CMS's `errorKey()` and in both i18n files.
  It is the best-tested part of the change.
- `CategorySection` removes a content decision from the app source. The file's
  own previous comment admitted the key lists were wrong; they are gone, and an
  unfiled chapter still draws, in a closing section, rather than vanishing.
- The stream URL renders `dir="ltr"` inside an RTL table — the bidi rule this
  repo cares about, applied without being asked.
- `RadioPlayer` stops rather than pauses a live stream, and keeps *connecting*
  distinct from *playing*. Both are the right calls for a broadcast.
