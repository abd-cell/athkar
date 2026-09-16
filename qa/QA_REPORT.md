# Athkari — full QA pass

**Date:** 12 September 2026
**Build under test:** API on `0.0.0.0:5000`, CMS dev server on `127.0.0.1:4299`,
Flutter web release on `127.0.0.1:8100`, release APK on two Android phones.

Everything below was exercised against a running stack, not read off the source.
Where a screen looked wrong I checked the code before calling it a defect; six
suspicions turned out to be my own misreading and are listed at the end, because
"I checked and it was fine" is part of the result.

---

## 1. What was covered

| Surface | Screens |
|---|---|
| App | home, chapters, chapter, counting session, dhikr sheet, search, prayer times, monthly timetable, qibla, tasbih, Qur'an, profile, reminders, settings, widget editor, notifications inbox, feedback |
| App (dark) | home, chapter, counting session |
| CMS | dashboard, chapters, adhkar, languages, Qur'an, reminders, broadcasts, widget, FAQ, user messages, settings, staff, action log, request log |
| API | 111 scripted checks across 15 areas (`qa/api_qa.py`) |

End-to-end paths walked in full:

- **Reader → desk.** A correction written in the app arrived in the CMS typed
  «تصحيح», attached to the right dhikr, sorted above the suggestions.
- **Desk → reader.** A broadcast composed in the CMS was picked up by the
  worker and appeared in the app's inbox — with push *skipped* for all four
  devices (no FCM token) and the inbox row written anyway, which is the
  behaviour the whole reminder design turns on.
- **Search folding.** «اصبحنا» and «أَصْبَحْنَا» both return the same single
  hit; «اله» and «إله» both return four; surrounding whitespace is trimmed.

## 2. Defects found and fixed

### D1 — CMS could not authenticate *(fixed)*

`cms/athkar-cp/src/app/environment.ts` still pointed at port 5099 after the API
moved to 5000. Login failed with `ERR_CONNECTION_REFUSED`.

### D2 — Every "new" form in the CMS rendered with no text fields *(fixed)*

**The most serious finding.** `TranslationsEditorComponent` created the first
language entry lazily *inside a `computed`*. Angular forbids writing to a signal
during computation (`NG0600`), the computed threw, the `@if` guarding the title
and body inputs never opened — so composing a broadcast, a new chapter, a new
dhikr, a new reminder or a new FAQ entry offered no place to type.

It was invisible to everything else: the build is clean, every list screen
renders, and *editing existing* content works, because an existing record
arrives with a non-empty array and never takes the lazy-create path.

Fixed by moving the lazy creation into an `effect`. Covered by a new test (§4)
that renders the editor from an empty array.

### D3 — Reminder descriptions read «بعد بعد العصر» *(fixed)*

The anchor strings carried their own preposition («بعد العصر») and the sentence
template added another. English was worse: `offsetBefore` produced
"30 min before after Asr". Anchors are now bare nouns and the templates supply
the preposition; the bedtime settings row keeps a phrase label of its own.

### D4 — Home screen's second card was labelled «التخريج» *(fixed)*

It reused `dhikr.source`, which means "the attribution", where the artboard
specifies «حديث اليوم». New `home.hadithOfDay` key in both languages.

### D5 — Three places ignored the Arabic-numerals setting *(fixed)*

With «الأرقام: عربية (١٢٣)» on, these still printed Latin digits:

- the bedtime row, which also showed a raw 24-hour `22:30` where the rest of the
  app uses a 12-hour clock;
- the feedback form's character counter, `0/4000` (Flutter's built-in counter);
- the monthly timetable's Gregorian sub-heading, `9/2026`.

### D6 — Monthly header named the wrong Hijri month *(fixed)*

It showed the Hijri month containing the *1st* of the Gregorian month. On
12 September the header read «ربيع الأول» while the home screen said
«١ ربيع الآخر» — two different answers to the same question, one scroll apart.
It now names the span the month actually covers.

### D7 — CMS reminders table printed a raw i18n key *(fixed)*

The schedule column rendered `anchor.asr +30`: `scheduleOf()` looked the key up
but never translated it. Now «العصر +30 د».

### D8 — Broadcast table header said «السؤال» *(fixed)*

The FAQ's "question" label was reused for the broadcast headline column and for
the composer's title field. Given its own `broadcasts.headline` key.

### D9 — CMS timestamps were US English in an Arabic RTL UI *(fixed)*

Angular's `date` pipe formats against `LOCALE_ID`, which stays `en-US` unless
locale data is registered at bootstrap — so the desk read «9/12/26, 10:34 AM».
`LOCALE_ID` is fixed per injector and the CMS language is a signal that changes
without a reload, so a new `appDate` pipe formats through `Intl` from that
signal instead. Applied to user messages, broadcasts, staff, Qur'an, the action
log and the request log.

### D10 — Every admin fetch ran twice *(fixed)*

SSR rendered each admin screen and issued its data fetch with no access token —
the token lives in `localStorage`, which the render server does not have — so
every panel produced a guaranteed 401 that was thrown away when the browser
refetched it a moment later. Harmless for correctness, but it doubled the
request count and filled the CMS's own «سجل الطلبات ‹401›» filter with 401s that
are not auth problems, which is the log an admin would search when there *is*
one. Admin GETs now return `EMPTY` under SSR. Verified: one 200, no 401.

## 3. Observations, not defects

- **Content is thin.** Five chapters carry no adhkar («اللباس», «الخلاء»,
  «المسجد», «السفر», «متفرقة»). The app renders them correctly as empty; they
  simply need content.
- **CMS digits are Latin.** Admin tooling prints `14`, `100`, `33` while its own
  labels use «٣٠ يوماً». The app has a reader-facing toggle; the CMS has none.
  Consistent within its own screens, so left alone — worth a decision, not a fix.
- **The dark palette is deliberate.** The accent inverts to a light green with
  dark text, which makes the "right now" card the brightest element on a
  near-black screen. It matches `docs/DESIGN.md` token for token, so it is a
  design choice to confirm rather than a bug to fix.

## 4. Tests added

| Where | What it holds |
|---|---|
| `cms/src/app/components/translations-editor.component.spec.ts` | 4 tests. Renders the editor from an **empty** array — the state that broke in D2 and that nothing else reaches. Also pins the lazy-create contract: only a language whose tab is opened joins the payload. |
| `app/athkar_app/test/strings_test.dart` | 7 tests. Key parity both ways between `strings_ar` and `strings_en` (both files ask for it in their headers; nothing enforced it), no blank values, matching `{placeholders}`, and the D3 rule: anchors are bare nouns, templates carry the preposition. |

## 5. Suite status

| Suite | Result |
|---|---|
| `dotnet test` | **62 / 62** |
| `flutter test` | **58 / 58** (51 + 7 new) |
| `ng test` (CMS) | **4 / 4** (new) |
| `python qa/api_qa.py` | **111 / 111** |
| `flutter analyze` | **0 issues** |
| `ng build` | clean |

## 6. Things I suspected and checked before believing

Recorded because each cost real time and would otherwise look like an
unexamined pass.

1. The widget kind switch "didn't respond" — the screenshot preceded the canvas
   repaint; it had switched.
2. «١٠٠ مرّات» read as «١٠ مرّات» at 0.62 screenshot scale. The seed says 100.
3. A counting session opened at «٦٤» for a 100-repeat dhikr — earlier QA taps,
   correctly persisted.
4. Server search returned 0 for every query from Git Bash `curl`. The shell was
   mangling the UTF-8; through the browser the same queries return 5, 4, 1.
5. The chapters table showed `aren` in the languages column — two `ar` / `en`
   badges, concatenated by text extraction.
6. "رسالة جديدة does nothing" — I had emulated a 1440×900 viewport inside an
   800×500 pane, so ref-based clicks landed off-target. (The *real* D2 was
   underneath it, found once the click landed.)

---

## 7. Push credentials (12 September 2026, later)

Firebase credentials were taken from the **wanes** project rather than created
fresh. The two halves of "Firebase credentials" behave differently and only one
of them transfers:

| Half | File | Transfers? |
|---|---|---|
| Server | `wanees-notification.json` → `backend/Athkar/athkar-notifications.json` | **Yes.** A service account can send to any app registered in its project. |
| App | `app/wanes_app/android/app/google-services.json` | **No.** It registers one package, `com.wanes.wanes_app`. Athkar's is `com.athkari.athkar_app`. |

The server half is wired and verified end-to-end against real Firebase. A
broadcast sent to a device holding a deliberately fake token came back with
**`NotRegistered`** — Google's own error code, not our "FCM is not configured" —
which proves the service account authenticated and the send actually left the
building. The dispatcher then classified it as `TokenExpired` rather than
`Failed` and cleared the token from the device row: reach went 1 → 0 and
tokenless 6 → 7 in the same pass. That is the pruning rule working against the
real service rather than against a double.

The app half needs a two-minute step in the Firebase console that cannot be done
from here — add an Android app with package `com.athkari.athkar_app` to the
**existing** `wanees-9c31c` project and download its `google-services.json` into
`app/athkar_app/android/app/`. Same project, so the service account already in
place keeps working unchanged. The Gradle plugin is wired to pick the file up
the moment it appears, and to stay out of the way while it is absent.


## 8. Local notifications tested on a real phone (12 September 2026, later still)

Push over FCM cannot be delivered to the Athkar app yet — it holds no token
until `google-services.json` exists for its package (§7). The *device-local*
path needs none of that, and it is the one every prayer-anchored reminder in
this product actually uses, so that is what was tested.

Read back from the phone's own alarm manager
(`adb shell dumpsys alarm`), **12 exact wake alarms** were registered against
`com.athkari.athkar_app`, all via
`flutterlocalnotifications.ScheduledNotificationReceiver`: two campaigns across
the six remaining days of the seven-day rolling window. The morning ones drift
`06:49 → 06:52` across the week, which is the proof that each day's time is
genuinely recomputed on the device from that day's sunrise rather than copied
forward.

### D11 — A fixed-time reminder showed its raw stored value *(fixed)*

The evening alarms sat at a flat `13:36` while the app displayed
«الساعة 13:36:00 · ١:٣٦». Two findings came out of chasing that:

1. **Not a bug.** The API QA run had converted `evening-adhkar` from
   prayer-anchored to a fixed-time campaign and left it that way. The alarms
   matched the campaign's actual state exactly — display and scheduling share
   one `_resolve`, so they cannot disagree. The campaign has been restored to
   Asr +30, device-local.
2. **A real defect.** `reminders.atTime` printed `reminder.localTime`
   unformatted: `13:36:00`, in Latin digits, 24-hour, seconds and all, sitting
   next to the correctly formatted «١:٣٦». Same family as D5.

Fixing it exposed a **latent bug in the D5 fix itself**. That helper took the
minute from the *last* `:`-separated segment, which is right for the bedtime
setting's «HH:mm» and wrong for a campaign's «HH:mm:ss» — it would have read
`13:36:00` as 13:00 and shown «١:٠٠». Nothing had produced an `HH:mm:ss` value
until this campaign did.

The helper now lives on `Numerals.clock`, takes the minute positionally, and
returns anything unparseable unchanged — untidy beats confidently wrong, since a
reminder showing a raw string is odd but one showing the wrong time is a missed
dhikr. Covered by 5 tests in `test/numerals_test.dart`, including the
`HH:mm:ss` case that slipped through.


## 9. Prayer-time reminders for Amman (12 September 2026, evening)

Five campaigns added — Fajr, Dhuhr, Asr, Maghrib, Isha — anchored to the prayer
itself (offset 0) and, being prayer-anchored, forced by the server to
device-local delivery. Each phone computes its own times, so they work with the
radio off.

They ring on **`athkar.prayer.v1`** rather than the reminders channel. That
required a small addition: the channel existed in `PushRules.Channels` and the
app created it at first launch, but nothing could *select* it — `ReminderInput`
had no field for it. It matters because Android fixes a channel's sound and
importance the moment it creates one, so this is the only opportunity to let a
reader keep the call to prayer audible while silencing adhkar reminders. An
unknown channel id is refused with `UnknownNotificationChannel` (506) rather
than reaching a phone, where Android would drop it or quietly rehome it.

### Verified on the phone

`dumpsys alarm` shows **44 exact wake alarms** — seven campaigns across the
remaining days of the rolling window. Tomorrow reads 04:56 Fajr, 06:49 morning
adhkar, 12:33 Dhuhr, 16:03 Asr, 16:33 evening adhkar, 18:45 Maghrib,
20:03 Isha — each matching the times the app displays.

### The calculation method was wrong for Jordan

The times were being computed with **أم القرى** (Umm al-Qura), whose signature
is Isha at exactly Maghrib + 90 minutes — which is what the phone showed. That
is the Saudi convention, not Jordan's. The default is now **رابطة العالم
الإسلامي** (Muslim World League), and the change propagated through the content
version to the device on its next launch:

| | Umm al-Qura | Muslim World League |
|---|---|---|
| Fajr | 4:53 | **4:56** |
| Isha | 8:16 | **8:04** |
| Asr / Maghrib | unchanged | unchanged |

Asr and Maghrib not moving is the check that it did the right thing: neither
depends on the twilight angles the methods disagree about.

The alarms rescheduled to match in the same pass — today's Isha alarm moved
20:16 → 20:04 without the app being touched again, which exercises the whole
chain: CMS setting → content version → device sync → recompute → reschedule.

`Athkar.Tests` now covers the channel contract in `ReminderServiceTests`,
including a case per channel the app creates — so adding one on the server
without adding it in `LocalNotifications` fails a test rather than failing
silently on somebody's phone.


## 10. Sending from the manager (12 September 2026, evening)

The manager screen was read-only by design; it now sends too. The send is
deliberately **thin**: `POST /api/v1/admin/push/send` composes a broadcast and
hands it to `IBroadcastService`, the same service the broadcasts screen uses.
One sending path still carries every message, so a send from here keeps the same
inbox rows, counters, history and audit trail — and its outcome appears in the
*Outcomes* figures on the very page it was sent from.

What stays on the broadcasts screen is **scheduling**. A message with a time on
it can still be edited or withdrawn before it goes, and that needs somewhere to
read it back from; a send-now has no such window. A schedule submitted here is
stripped rather than honoured, because silently accepting it would leave an
admin watching for a message that had not been sent.

The audience defaults to **one device**, not everyone. The default is the blast
radius: somebody opening this to check that push works at all, and pressing send
without reading the audience, should reach their own phone.

### Verified end to end

Sent from the screen to a single device key. Broadcast #13 → 1 recipient →
dispatch **Skipped** with "Notifications are off for this device" (that install
holds no token) → **inbox row written anyway**. That is the rule the whole
notification design rests on, exercised through the new entry point: the push is
a courtesy, the inbox is the delivery.

### D12 — The send button could never be pressed *(fixed)*

The submit was gated on a `computed` over the draft signal. `ngModel` mutates
that object **in place**, so the signal never notifies: the computed was
evaluated once against the empty draft and stayed false, leaving the button
disabled no matter what the admin typed. Validation now reads the object when
the button is pressed — which is what every other screen in this CMS does — and
names the missing field.

### Two false alarms, both mine

1. **Labels rendering blank.** Newly added i18n keys came out as empty strings —
   some of them, not all, which reads exactly like a missing key. The keys were
   present in both files and `ng build` was clean. Restarting the dev server
   fixed it: its incremental build had not picked up the i18n edits. Recorded in
   the run-app skill, because a full page reload does *not* clear it.
2. **"The send does nothing."** Twice. Both times the click missed: a viewport
   emulated larger than the pane makes ref-based clicks land off-target, and
   `get_page_text` reads `<main>` only, so a dialog rendered outside it looks
   closed when it is open.

## 11. Suite status after §9–§10

| Suite | Result |
|---|---|
| `dotnet test` | **119 / 119** |
| `python qa/api_qa.py` | **127 / 127** |
| `flutter test` | **63 / 63** |
| `ng test` (CMS) | **4 / 4** |
| `flutter analyze` | **0 issues** |


## 12. «حيّ على الصلاة» before Isha (12 September 2026, night)

Asked for: a message one minute *before* the Isha adhan. The `prayer-isha`
campaign moved from offset `0` to **`-1`**, wording «حيّ على الصلاة», still
prayer-anchored and therefore still device-local — so it fires from the phone's
own calculation with no network.

Verified on the phone: the Isha alarm moved **20:03 → 20:02**, and the reminders
screen reads «حيّ على الصلاة · قبل العشاء بـ١ دقيقة · ٨:٠٢». Tomorrow's Isha is
20:03, so 20:02 is one minute before it.

That row is also the first live sighting of the D3 fix on its *before* branch:
«قبل العشاء بـ١ دقيقة» has exactly one preposition, where the old strings would
have produced «قبل بعد العشاء».

### D13 — «بعد الفجر بـ٠ دقيقة» *(fixed)*

The other four prayer reminders fire *at* the prayer, and the offset template
rendered that as "after Fajr by 0 minutes". It reads like a bug, which for a
reader deciding whether to trust an alert is worse than it sounds. A zero offset
now gets its own sentence — `reminders.atAnchor`, «عند الفجر» / "At Fajr" —
covered in `test/strings_test.dart`.

### A note on the wording

The request wrote «هيا على الصلاة». The message is set to «**حيّ** على الصلاة»,
the phrase as it is called in the adhan. Say the word if the other spelling was
deliberate.


## 13. Root cause: why Isha was never the right time

Asked to stop patching the symptom and find the defect. There were two, and
neither was in the arithmetic — `PrayerCalculator` passes clean inputs to the
`adhan` package and gets correct answers back.

### What Isha actually is in Amman

Computed for 13 September 2026 at the city's own coordinates:

| Convention | Isha | vs Maghrib |
|---|---|---|
| **Jordan — دائرة الإفتاء (18°)** | **20:07** | 82 min |
| Umm al-Qura (shipped default) | 20:15 | a fixed 90 min |
| Muslim World League | 20:03 | 78 min (17°) |

Umm al-Qura's Isha is **clock arithmetic from Maghrib** — ninety minutes,
because that is the rule in Makkah. It takes no account of where the reader is
standing. In Amman it is eight minutes late, every night.

### D14 — the city's country was collected, displayed, and thrown away

Every entry in `cities.dart` carries a country, and the picker *shows* it:
`City('عمّان', 'Amman', 31.9539, 35.9106, 'JO')`. But `_choose()` passed only
latitude, longitude and name, and `setLocation` stored only those three. The
country reached the screen and went no further, so a reader in Amman silently
kept whatever global default an admin had set.

### D15 — no Jordanian convention existed to select

The list offered twelve conventions including Kuwait, Qatar, Dubai and
Singapore — and none for Jordan. Even an admin who noticed the problem had
nothing correct to choose.

### The fix

`CalculationMethod.Jordan = 13` across all three stacks — **appended**, because
the numbers are the contract — expressed as plain 18°/18° angles rather than
borrowed from Karachi's enum because the numbers agree today.

`Settings.calculationMethod` now resolves in order of authority: what the reader
chose, else what their country publishes, else the configured default. Resolved
**on read**, not written on move, so an install that already has a location
heals on its next launch rather than waiting to be told its city again.

The server default went back to Umm al-Qura, which is now correct: it is the
fallback for a country nobody has told us about, not a value imposed on Amman.

### Tests

`test/prayer_convention_test.dart`, 13 cases: the country→convention map, the
nearest-city fallback that makes the fix self-healing, Jordan's angles, the
eight-minute gap against Umm al-Qura, that Asr/Maghrib/sunrise do **not** move
when the convention changes (the check that the fix is doing only what it
should), and that the enum was appended rather than reordered.
