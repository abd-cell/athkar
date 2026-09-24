# أذكاري — Business logic

The authority on domain rules. Where this document and the code disagree, one of
them is a bug; read this before changing any rule, and update it when you do.

`docs/DESIGN.md` covers the visual system. `CLAUDE.md` covers the toolchain.

---

## 1. What this is

An adhkar, prayer-times and qibla app, built as an endowment (وقف): free, with
no advertising, no analytics, and no account.

Three stacks in one repository:

| Path | Stack | Role |
|---|---|---|
| `backend/Athkar/` | ASP.NET Core 9 + EF Core 9 (SQL Server) | the API contract every client depends on |
| `cms/athkar-cp/` | Angular 22 (standalone, SSR, signals) | the control panel — where all content lives |
| `app/athkar_app/` | Flutter 3.47 / Dart 3.13 | the reader's app |

The product's distinguishing claim is narrow and load-bearing: **every dhikr on
screen can be traced to a book, a number and a grading.** Most of the rules below
exist to keep it true.

---

## 2. Who exists

**Readers are anonymous installs.** There is no sign-up, no password, no email
and no phone number anywhere near a reader. The whole of what the server knows
about one is the column list of `Areas/Domain/Devices/Device.cs`:

- a UUID the app minted for itself on first launch (`DeviceKey`),
- a platform, a push token, a language, an IANA timezone, a two-letter region,
- whether notifications are wanted, and when the app last called in.

That key identifies an *install*, not a person. Clearing app data mints a new
one; two phones are two devices with nothing joining them. This is intended.

**Staff are the only accounts.** `Areas/Domain/Staff/User.cs`, with roles that
form a ladder rather than a set: `Editor < Admin < SuperAdmin`. Only the CMS
authenticates.

**`X-Device-Key` is not authentication.** It says which install is calling. It
gates endpoints where writing to *somebody's* row is harmless and writing to
nobody's is not — the inbox, feedback. Anything that could matter if forged
sits behind `[AppAuthorize]`. See `Shareds/Security/IDeviceContext.cs`.

### Consequences worth stating

- A reply to feedback is delivered to the device that wrote in, and only while
  that install exists. The app says so rather than asking for an email.
- "Delete my data" means `DELETE /devices`: the push token is cleared and the
  row and inbox are soft-deleted. Content and progress stay on the phone,
  because they were never anywhere else.
- Streaks, counters, favourites and coordinates live in `SharedPreferences` on
  the phone. There is no endpoint that accepts them.

---

## 3. Content

### The shape

`AthkarCategory` (a chapter) → `Dhikr` (one remembrance) → translations of each.

The **Arabic is not a translation**. It lives on `Dhikr.ArabicText` and every
other language hangs off it in `DhikrTranslation`. A payload in any language
carries the Arabic.

### Publishing requires a source

`ContentAdminService` refuses to publish a `Dhikr` without both `SourceBook` and
`SourceReference` (`ErrorCode.SourceRequired`). A draft may lack them; a reader
never sees one that does. The seeder obeys the same rule — content that arrived
through a back door is exactly the content nobody checks.

`HadithGrade` has no member for weak or fabricated. Content like that is not
stored and filtered, it is never entered.

`Virtue` is on the *translation* because the stated reward is itself narrated
text, and it is written down only where a sound narration states it.

### Search folds the text

Arabic is written with marks a typist omits. `Shareds/Text/ArabicText.cs` folds
both the corpus and the query: diacritics removed, one shape per letter family
(أ إ آ ٱ → ا, ى → ي, ة → ه), digits normalised. The folded form is **stored** on
`Dhikr.SearchText` — an index cannot serve a function applied to every row.

`app/athkar_app/lib/core/arabic_text.dart` is the exact mirror. A rule added on
one side only makes the app search for something the server never wrote down.

### Sync is one integer

`AppConfiguration.ContentVersion` is bumped by every content change a reader
would see. The app sends what it has; the usual answer is `isUpToDate: true` and
an empty payload. That comparison is the entire protocol.

**Forgetting to bump it is not a cosmetic bug — it is an edit that never
arrives.** Every mutating method on `IContentAdminService` calls
`IAppConfigurationService.BumpContentVersion()` — and so does every mutating
method on `IRadioAdminService`, because stations ride the same payload.

### Audio radio — the one content the app keeps no copy of

`RadioStation` is a name, a broadcaster, a logo and a stream URL, published from
the console like anything else and carried in `CatalogOutput.Radios` rather than
on an endpoint of its own. The list is cached and therefore on the home screen
offline; only pressing play needs a network, and the card says so when it fails.

Three rules:

- **The stream must be https, and stay https.** The scheme is checked on the
  server, for a draft as much as for a published row: a cleartext stream is
  refused by iOS's ATS and by Android's default network policy, so an `http://`
  station is not a station that plays badly — it is one that is silent on every
  phone, and the refusal happens where nobody in the console can see it.

  What the server's check *cannot* see is a redirect. `stream.radiojar.com`,
  which is what mp3quran.net publishes for the Saudi broadcaster's own
  «إذاعة القرآن الكريم», answers an https request with a 302 to an `http://`
  node — and the phone refuses to follow it, as does ExoPlayer, which declines
  cross-protocol redirects by default. That is why the seeded station is
  mp3quran's own https-terminated stream and not the broadcaster's: the whole
  chain has to hold, and only a device can tell you it does.
- **It is a URL, not a file.** Which is the whole reason the row exists in the
  database instead of in the app: a stream that moves is an edit an editor makes
  and every phone has at the next sync.
- **A live stream is stopped, never paused.** `core/radio_player.dart` re-opens
  the URL on play, because a paused position in a broadcast is a position the
  broadcast has already left.

---

## 4. Languages

`AppLanguage` is a table, not an enum, so adding Turkish is an afternoon of
translation in the CMS rather than a release of three clients.

- Exactly one row is `IsDefault`; it cannot be disabled and ends every fallback
  chain.
- Arabic (`ContentRules.SourceLanguage`) cannot be disabled or deleted: the
  corpus is written in it.
- `UiString` is an **overlay**, not a replacement. The app ships a complete set
  of strings compiled in and merges the CMS's on top. A typo in a button is
  fixable the same day; an install that has never reached the network still
  renders complete sentences. See `app/.../core/l10n.dart`.
- Resolution order is identical everywhere, and lives in one place
  (`ILanguageResolver`): the asked language if enabled, else the default. A
  per-service reimplementation is how a catalogue ends up in Arabic while the
  reminder pointing at it arrives in English.

---

## 5. Reminders — the one rule that shapes everything

The product makes two promises that pull in opposite directions:

1. Reminders land at exactly the right minute, with no network.
2. An admin changes their wording and timing from the CMS, with no app release.

Neither delivery route gives both, so **the campaign says which it is** and the
split is visible in the CMS rather than hidden in code
(`ReminderDelivery`):

| | `ServerPush` | `DeviceLocal` |
|---|---|---|
| Who raises it | the server, over FCM | the phone, from a locally scheduled notification |
| Needs network | yes, at the moment it fires | no |
| Timing owned by | the admin, to the minute | the phone's own clock and calculations |
| Can be prayer-anchored | **no** | yes |

### Why prayer-anchored means device-local, always

Prayer times are computed from coordinates. **The server never receives a
reader's coordinates** (§2), so it cannot know when Maghrib is where they are
standing. `ReminderService.Apply` therefore *forces* a `PrayerAnchored` campaign
to `DeviceLocal` rather than letting the CMS create one that would silently never
fire.

Prayer times themselves are computed in `app/.../core/prayer_times.dart` with
the `adhan` package — never fetched, never pushed.

### Server-push scheduling

Three separate passes, because each has a different safe retry
(`Areas/Services/Notifications/PushDispatcher.cs`):

1. **Materialise** — for each enabled fixed-time `ServerPush` campaign, write a
   `PushDispatch` row per device per occurrence in the next
   `PushRules.DispatchHorizonMinutes`. A campaign says "21:30" and means 21:30
   *where the reader is*, so the walk happens in the device's own local calendar
   (`ReminderSchedule.Occurrences`) and only the final instant is converted.
   The unique index on `(CampaignId, DeviceId, ScheduledAtUtc)` makes running
   this twice a no-op.
2. **Send** — everything due and pending. A dispatch older than
   `DispatchGraceMinutes` is *dropped*, not delivered: a morning-adhkar reminder
   arriving at noon tells the reader the app is not to be relied on.
3. **Broadcasts** — scheduled one-off messages become dispatch rows and the
   broadcast moves to `Sending`, which is a real state the CMS can show.

The **inbox row is written whether or not the push lands**. A reader with
notifications off, or whose phone was in a tunnel, still finds the message. The
push is a courtesy; the inbox is the delivery.

FCM reporting `Unregistered` clears the device's token, so no later campaign
pays for a dead install again.

### Device-local scheduling

`app/.../core/reminder_scheduler.dart` rebuilds a rolling week on every launch,
after a sync, and whenever anything that moves a reminder's clock changes — the
location, the method, the madhab, an adjustment, the bedtime, a mute.

**iOS allows 64 pending notifications.** An anchored reminder is a different
clock time every day, so it cannot be a repeating notification: each day is its
own entry. Seven days across a handful of campaigns fits; scheduling further
would silently drop the far end. Ids are `campaignId * 100 + dayOffset`, so a
rebuild replaces rather than duplicates.

### What a reminder actually says

A notification that reads only «حان وقت صلاة الفجر» is a clock, and this app is
not a clock. Under the announcement line the phone appends **a narration from
its own catalogue, with its takhrij** — the same promise the dhikr card makes,
in the place a reader actually looks.

`app/.../core/notification_narration.dart` composes it, and three rules hold it
to the project's claim:

- **Nothing unsourced.** Only a dhikr with a book and a number is quoted. The
  server refuses to publish one without them, but a cache written by an older
  build may still hold one, and the lock screen is the last place to discover a
  hole in the takhrij promise.
- **Nothing truncated.** A narration longer than `maxLength` is passed over
  rather than trimmed. A hadith cut mid-sentence reads as a whole one, and the
  reader cannot see that it ended early.
- **Stable for a day, different the next.** The week's queue is rebuilt on every
  launch, so the pick is a function of the date and the reminder id, never of
  chance. Otherwise the same morning would carry different narrations depending
  on whether the reader happened to open the app.

The text comes from the catalogue already on the device. Nothing is fetched and
nothing is composed on the server — which also means this enrichment belongs to
*locally scheduled* reminders. A server-pushed broadcast carries the admin's
wording and nothing else, because the server has no idea which adhkar a given
install holds.

### A notification says which way it runs

Everywhere else in the app a `Directionality` settles the question. A
notification has none: it is drawn by Android or iOS, and the shade takes its
base direction from the **device's locale**, not from the text. An Arabic
reminder on a phone set to English is therefore laid out left-to-right.

The letters are never the problem — the bidi algorithm gets those right. What
moves is everything neutral around them:

| Written | Rendered in the wrong base direction |
|---|---|
| «حان الآن موعد صلاة الفجر.» | the full stop lands at the left edge |
| «صحيح البخاري 6405» | «6405 صحيح البخاري» |
| «لمدينة Amman.» | the stop joins the Latin run |

So the direction travels in the string, as the invisible marks that exist for
it. Two jobs, not interchangeable:

- **Isolate** (`FSI`…`PDI`) wraps a value *inserted into* a sentence — the
  `{city}`, a number — so it cannot disturb its neighbours whichever way it
  runs. Applied whether or not it matches: which way a city name reads is
  decided by the reader's settings, long after the sentence was written.
- **Mark** (`RLM`/`LRM`) leads each *line*, stating its base direction. Per line
  rather than per message, because a reminder for an English reader is an
  English announcement with an Arabic narration under it — one mark for the
  whole body would have to be wrong about one of them.

Both stacks implement it because both draw notifications: the phone for
scheduled reminders, the server for a push that arrives while the app is shut.
`app/.../core/bidi_text.dart` and `Shareds/Text/BidiText.cs` hold the same rules
and the same test cases.

The marks go only on the copy handed to the OS. The inbox row is stored clean —
it is rendered inside the app, where a `Directionality` settles it, and control
characters written into stored text would be there for good.

### Only prayer times may interrupt

`athkar.prayer.v1` is raised at iOS's `timeSensitive` interruption level — the
«عاجلة» badge — so it breaks through Focus and Do Not Disturb. Nothing else is.
A prayer time is the one thing here that is worthless a few minutes late; a
reader who silenced their phone deliberately should not also be interrupted for
an announcement from us.

It needs `com.apple.developer.usernotifications.time-sensitive` on the iOS
target (`ios/Runner/Runner.entitlements`). Without the entitlement iOS downgrades
the level silently and the notification still arrives, merely respecting Focus —
degraded, never broken.

### Android channels are immutable

A channel's sound and importance are fixed the moment Android creates it.
Re-creating one with different settings does nothing, and the original survives
an app update. **To change how a reminder sounds you add a channel, never edit
one** — hence the version suffix on every id in `PushRules.Channels`.

### The field a token goes in

`FcmSender.Build` sets `Message.Token`, and there is a `#pragma` holding it
there against the compiler's advice. The FirebaseAdmin .NET SDK deprecates
`Token` in favour of `Fid`, but they carry different things: a **registration
token** (`APA91b…`, ~150 characters) is not a **Firebase Installation ID**.

Sent in the wrong field, FCM answers `NotRegistered` — the same answer it gives
for an app that has been uninstalled. So the failure is silent and
self-inflicting: every push fails, the dispatcher dutifully clears each token as
dead, and every install goes permanently unreachable while the manager screen
reports a growing *tokenless* column and nothing that looks like a fault.

If push ever stops working across the board, check this line before anything
else. The test that settles it in one step is to call the REST API directly with
the same service account —
`POST https://fcm.googleapis.com/v1/projects/<id>/messages:send` with
`{"message":{"token":"…"}}`. A 200 there and a `NotRegistered` through the SDK
means the field, not the token.

### The push token's life after registration

The token is not a fact about an install, it is a lease. FCM rotates a
registration token on its own schedule — a restore onto a new phone, a long idle
stretch, its own housekeeping — and the app hears about it through a callback
that can fire at any moment.

**A rotation reaches the server immediately** (`devices/push-token`), not at the
next cold launch. This matters because nothing fails when it does not: FCM
accepts a send to a token it has already retired, so the sole symptom of a stale
token is a reader quietly hearing less from the app, which nobody reports.

Three rules hold the lease honest:

- The endpoint writes **only** the token and the notification switch. A rotation
  knows nothing about the reader's language or timezone, and writing back stale
  copies of those would undo a change made on another screen.
- It **refuses an unknown device key** rather than upserting. A token arriving
  for a key the server has never seen is a refresh that raced the first
  registration; inventing a row from it would leave a device with no language
  and no timezone in every future fan-out. The app registers on every launch, so
  the token lands a moment later anyway.
- A retired token is **cleared from the device row** the moment FCM says so
  (`PushStatus.TokenExpired`), which is what stops every later campaign paying
  to rediscover the same dead install. A plain failure never clears it: a
  network blip is not an uninstall.

The app also re-checks on resume, because revoking notification permission in
the OS settings fires no callback at all — the app simply finds it has no token
the next time it looks.

### Reading the pipeline

When a broadcast reports "4 targeted, 0 delivered", that is not yet a
diagnosis. Four unrelated things produce it, each with a different fix:

| What it looks like | What it is | Where to see it |
|---|---|---|
| No token on any install | permission never granted | manager → *tokenless* |
| Tokens held, notifications off | the readers chose it | manager → *muted* |
| Nothing sending at all | no Firebase credentials | manager → the notice at the top |
| Pending rows past their moment | the sender worker is stopped | manager → *overdue* |

The push manager exists to keep these apart; summing them into one "not
delivered" figure is what makes the wrong one get chased.

**Scheduling** stays on the broadcasts screen — a message with a time on it can
still be edited or withdrawn, and that needs somewhere to read it back from. The
manager sends only *now*, through the same service, so one code path still
carries every message.

Each of the four diagnoses above now has something an admin can do about it:

| Diagnosis | What the console offers |
|---|---|
| No token on any install | **Sessions & devices** — the tokenless installs as rows, each with its key and the tail of its token |
| Tokens held, notifications off | the same screen, filtered to *muted* |
| No Firebase credentials | the notice, unchanged — this one is a deployment fix |
| Pending rows past their moment | **Run now**, which calls the three dispatcher passes in the workers' order |

And the delivery log beneath the counters carries every dispatch with the error
Firebase actually returned, where a failed send can be retried and a queued one
withdrawn. Two rules hold there, both decided on the server so the screen cannot
disagree with the sender:

- **A delivered dispatch is never retried.** The reader already has the message,
  and a second copy of a 5am reminder is not a fix for anything.
- **A retry never writes a second inbox row.** The inbox row is written on the
  first attempt whatever FCM then says, so `PushDispatch.InboxWritten` is what
  stops a retry putting a duplicate in front of a reader who never saw the
  failure.

A cancelled dispatch is recorded as *skipped*, not deleted: the row is the
evidence that somebody stopped it on purpose, and a missing row reads as a bug
in the sender.

A skip is a correct outcome, not a failure, so the delivery rate counts only
what was actually attempted. Counting muted readers as failures would turn the
rate into a measure of how many people want push — a real question, but not that
one.

### The convention belongs to the reader's country, not to a global default

A calculation method is not a preference. Umm al-Qura puts Isha at a **fixed
ninety minutes** after Maghrib because that is the rule in Makkah; the Muslim
World League uses a 17° twilight angle; Jordan's own authority uses 18°. For
Amman in mid-September those give 20:15, 20:03 and 20:07 — a spread of twelve
minutes, which is the difference between praying with the mosque and praying
alone.

So `Settings.calculationMethod` resolves three answers in order of authority:

1. **What the reader chose.** An answer, never overridden.
2. **What their country's authority publishes** (`countryCalculationMethods`).
3. **The configured default**, for a country we have not been told about.

It is resolved *on read* rather than written when the location moves, so an
install that already has a location corrects itself on the next launch instead
of waiting to be told its city again. The country comes from what the reader
picked, falling back to the nearest listed city — a guess, and a coarse one near
a border, which is exactly why it never outranks an explicit choice.

Jordan's convention is expressed as plain angles rather than borrowed from a
package method whose numbers happen to agree today. A new convention is
**appended** to `CalculationMethod`, never slotted in: the numbers are the
contract across all three stacks.

### Battery managers

Manufacturer battery managers, particularly on Xiaomi, Oppo and Huawei, stop
background apps and prevent exact alarms firing. The app cannot fix this; it
explains it, per manufacturer, in `battery_help_screen.dart`. This is the
single most asked question an app like this receives.

---

## 6. Prayer times, qibla and the calendar

All computed on the device.

- **Method and madhab** default from `AppConfiguration` (so a first launch in
  Riyadh shows Umm al-Qura) and are then the reader's.
- **Per-prayer manual adjustment**, ±60 minutes, sits on top of the convention.
  This is the feature that decides whether somebody keeps the app: a timetable
  that disagrees with the mosque down the road by two minutes is, to its reader,
  simply wrong.
- **The qibla applies magnetic declination.** A compass reads magnetic north;
  the qibla is computed from true north. The difference is under 2° in the Gulf
  but ~5° in Istanbul and over 15° on the American east coast. See
  `core/magnetic_declination.dart`, which documents the tilted-dipole
  approximation it uses and what it is not (the WMM).
- **A device with no magnetometer** gets the bearing as a number rather than a
  compass that never moves.
- **The Hijri date is adjustable by ±1 day.** Sighting differs by country; an
  app that insists on one arithmetic answer is wrong for a large share of its
  readers.
- **Location is never required.** The app ships a city list
  (`core/cities.dart`); the permission prompt appears exactly once, when the
  reader taps "use my location". With no location the prayer section is simply
  absent and everything else works.

---

## 7. The Qur'an

### 7.1 One file per mushaf, not a table of verses

Each mushaf is uploaded through the CMS as a **prepared SQLite file**, stored
whole with a version and a checksum, and downloaded once by the app.

A verse table on this server would be a second, worse copy of a corpus that
already exists in verified form, and every read would re-assemble a page the app
can query itself. So:

- `POST /api/v1/admin/quran` — multipart upload. The server hashes what it
  wrote, refuses on a checksum mismatch, and deletes the partial file.
- Versions are **immutable and unique within an edition, across deleted rows**:
  an installed app compares against a number, so re-using one would leave it
  holding a different file under a number it thinks it has. The scope is the
  edition, because two mushafs are two sequences — Warsh reaching version 3 says
  nothing about Hafs.
- Upload and publish are separate steps, so a large file is transferred and
  verified before a million installs are told about it. Exactly one package
  **per edition** is published at a time; publishing one mushaf leaves the
  others on the shelf.
- `GET /api/v1/quran/editions` → the published mushafs, default first: slug,
  name, script, version, size, sha256, release notes.
- `GET /api/v1/quran/check-version?edition=` → edition, name, version, size,
  sha256, release notes. Omitting `edition` answers with the **default** one.
- `GET /api/v1/quran/download?edition=` → the bytes, with range requests enabled
  so an interrupted download resumes. **The only endpoint that does not answer
  with `BaseResponse`** — wrapping a mushaf in an envelope would mean base64-ing
  it in memory. The checksum travels in `X-Content-Sha256`.

An edition that is *named* but has nothing published answers with nothing rather
than falling back to the default: a device that has been following Warsh must be
told Warsh is gone, not handed Hafs under the name it stored.

The app writes to a `.part` file and moves it into place only after the checksum
matches: a truncated mushaf must fail loudly now, not show up as missing surahs
weeks later. Each edition is stored as its own `quran_<edition>.db` under its own
version key, so a reader may keep more than one — Hafs and Warsh are different
texts, not two versions of one, and each is a couple of hundred megabytes the
reader chose to spend.

**Which mushaf a device gets when it names none.** Exactly one published package
carries `IsDefault`, and it is what a fresh install and every install shipped
before editions existed receives. Publishing the first mushaf claims it
automatically, a new version of the default edition inherits it, and withdrawing
it hands it to the oldest mushaf still published — a default nobody can download
would leave every fresh install with no mushaf and no error to show for it.

**The upgrade path.** A build from before editions stored one `quran.db` under
one version number and had no name for the mushaf, because the server had none
to give. On the first sync afterwards the app asks without naming an edition,
learns which mushaf the default is, and files the bytes already on disk under
that name — `QuranStore.adoptLegacy`. Re-downloading would cost a reader hundreds
of megabytes to end up exactly where they were.

### 7.2 The expected schema

The contract with whoever prepares the file (`core/quran_library.dart` reads it,
defensively — a package missing a table produces an empty index, never a crash):

```sql
surahs(id, name_ar, name_en, ayah_count, revelation_place)
ayahs(id, surah_id, ayah_number, text_uthmani, page, juz)
```

`tools/quran-package/` builds a file to this schema from a verified text
(King Fahd Complex / Tanzil) and prints the checksum to compare after upload.

### 7.3 Waqf marks (الوقف والابتداء)

Two levels, and the file declares which it supports through
`QuranPackage.HasWaqfAnnotations`:

**Level one — glyphs in the text (always).** The Uthmani text carries the marks
as Unicode characters (ۖ ۗ ۘ ۙ ۚ ۛ). They render as part of the verse; the app
adds nothing. This is what every package supports.

**Level two — tappable explanations (optional).** Two further tables make a mark
answerable:

```sql
waqf_marks(id, surah_id, ayah_number, word_index, symbol)
waqf_types(symbol, name_ar, ruling_ar)
```

Tapping a verse then opens a sheet naming each mark and its ruling — الوقف
اللازم, الجائز, صلى, قلى, المعانقة. A package without these tables simply does
not open the sheet, which is better than opening an empty one.

### 7.4 The printed page (optional)

A package may also carry the mushaf as it is *printed* — 604 pages of 15 lines,
each line broken where the King Fahd Complex broke it — in three more tables:

```sql
words(id, surah_id, ayah_number, position, text, page, line)
lines(id, page, line_number, kind, is_centered, first_word_id, last_word_id, surah_id)
fonts(id, page, family, data)          -- page NULL: one font for the whole book
```

`kind` is `ayah`, `surah_name`, `basmallah` or `blank`; `is_centered` says
whether a line is centred rather than justified, which is a fact of the print
and not derivable from the words — page 583's line 7 ends a surah and is still
justified, while page 528's line 9 ends one and is centred.

Three things this settles:

- **The font travels in the package, not in the app.** Which script a mushaf is
  drawn in is a property of that mushaf; an app that shipped one face would have
  to ship them all. `MushafFonts` loads it at runtime.
- **The app probes for these tables rather than being told.** Unlike
  `HasWaqfAnnotations`, there is no flag: the file is already on the device, so
  reading its schema costs nothing and cannot disagree with the truth.
- **Page fidelity has a price, and it is the font.** In a King Fahd Complex page
  font one glyph is one word *as drawn on that page*, so the 604 of them come to
  159 MB. A single-font package (DigitalKhatt) keeps the exact line breaks for
  about 8 MB in total, and justifies by stretching the space between words where
  the press stretches the letters. `tools/quran-package/` builds either.

### 7.5 Recitations (الاستماع)

Recorded recitation comes from a publisher — mp3quran.net today — and the rules
are about keeping three things apart: the **catalogue** (ours), the **audio**
(theirs), and the **choice of what to offer** (an editor's).

- **The server keeps the catalogue and nothing else.** A `Reciter` has
  `Recitation`s (a publisher's «مصحف»: «حفص عن عاصم - مرتل»); each is a folder
  URL, a surah list and an optional ayah-timing id. No audio is stored or
  relayed here. Reciters ride the catalogue payload like radio stations, so the
  list browses offline and `ContentVersion` is the sync.
- **The phone fetches audio and timing from the publisher directly.** Surah `n`
  is `ServerUrl + n:000 + ".mp3"`; timing is `TimingUrl + n`. The server never
  sees what anybody listens to — there is no endpoint that could be told.
- **That has a privacy cost, and the reader is told it — in the FAQ, not in a
  dialog.** The publisher sees the reader's IP address and the surah requested.
  The Privacy section of the FAQ says so (seeded on existing databases too, by
  question, never resurrecting one an editor deleted), and a downloaded surah
  never asks the publisher again. A confirmation in front of the first listen
  was tried and removed at the product owner's request.
- **The publisher is credited as the source in the FAQ** (Qur'an section, «من أين
  تأتي التلاوات الصوتية؟»), and in the lock-screen media metadata. The player
  itself carries no credit line, at the product owner's request. The credit is `Mp3Quran:SourceName` /
  `SourceUrl`. Whether the publisher's terms permit a given deployment's use is
  for that deployment to settle with them; the console says so above the sync.
- **A sync writes drafts and never publishes.** It moves only the publisher's own
  facts (folder URL, surah list, timing) on existing rows; names, portrait,
  featured flag, order and the published switch are the editor's. A reciter an
  editor deleted is skipped; a recording the publisher dropped is reported, not
  withdrawn. Applying a sync is Admin; curating is Editor.
- **A reciter cannot be published with nothing to play**, and withdrawing his last
  published recording withdraws him — the catalogue would drop him anyway, and a
  switch that lies is worse than one that turns itself off.
- **Timing belongs to a recording, not to a reciter.** One of a reciter's
  recordings can have it and another not. Without it the player says «لا يوجد
  توقيت لهذه التلاوة» and hides verse tracking and custom-range repeat, which
  cannot work without knowing where an ayah begins.
- **Portraits are optional and never synced.** The publisher supplies none, a
  photograph carries rights of its own, and a reciter without one is drawn by his
  initial.
- **Downloading is always the reader's tap, one surah at a time.** A whole
  recording is a gigabyte or more.

---

## 8. Broadcasts

A one-off message an admin composes. Separate from a reminder campaign because
they are different kinds of object: a campaign is a rule that keeps producing
sends, a broadcast is a single event with a status and a result.

- `Draft → Scheduled → Sending → Sent | Failed`, plus `Cancelled`.
- The CMS's **Send** button schedules rather than sends, so one code path
  carries every message and a fan-out over a million devices never blocks a
  request.
- A sent broadcast is immutable. Some readers already have the old wording on a
  lock screen.
- **`Failed` means nothing at all got through.** A broadcast where most readers
  simply have push off is a successful broadcast with an honest `SkippedCount`.

---

## 9. Widgets

A widget is the one surface of this app a reader sees **without opening it**,
which is also what makes it the one nobody can correct in the moment. Every rule
below follows from that.

### The server names them; the app draws them

A widget gets a fraction of a second of CPU and no network, so what it can draw
is decided at build time. The catalogue therefore holds **names for renderers**,
not layouts: `WidgetCatalogItem.Key` matches an entry in the app's registry
(`features/widgets/widget_designs.dart`), and an app that has never heard of a
key **skips it silently**.

That asymmetry is the whole point. The console can add next year's widget today
without breaking an install from last year, and can withdraw one from every
phone at the next sync — but it can never conjure a design onto a phone that
does not already hold the code for it.

Two consequences that are easy to get wrong:

- **`DesignCount` is a cap, not a count.** The app takes the smaller of it and
  what the build implements. Raising it above the app's number does nothing;
  lowering it withdraws a design without an app update, which is the direction
  that has to work.
- **A key is immutable once created.** Every phone that has placed the widget
  holds the old key, and renaming it on the server would not rename it there —
  it would make the entry stop matching anything the app can draw. The service
  ignores the key on update and the console does not offer the field.

An entry with no wording in either the reader's language or the default is
dropped from the gallery rather than shown under its key. A key is a developer's
handle; nobody should meet one.

### The previews are the reader's own data

Every preview in «الويدجت المتوفرة» is drawn from the reader's city, their
timetable, their Hijri correction and a dhikr out of the catalogue on their
phone. A gallery of mock widgets showing 5:00 for Fajr in a city nobody lives in
is a catalogue of pictures; this one shows the thing itself, so what the reader
chooses between is what they get.

The same rule that governs every other surface governs these: **a dhikr appears
with its takhrij**, and the Qur'an widgets draw an ayah out of the published
content rather than one compiled into the app — content compiled in is beyond
any editor's reach once it has shipped.

### Customisation is a permission, not a preference

Colour, transparency and a background photograph are the reader's, stored on the
phone and never sent anywhere. They are nonetheless admin-gated, because each of
them can break the promise the widget makes: a transparent panel over a
photographic wallpaper can render a dhikr unreadable, and an unreadable dhikr on
a home screen is worse than no widget — the reader cannot see that anything is
wrong, they simply stop reading it.

So `WidgetSettings` carries `AllowBackgroundColor`, `AllowTransparency`,
`AllowBackgroundImage` and `AllowCustomWidget`, the app resolves permissions
*before* the reader's choice, and opacity is clamped to
`WidgetAppearance.minOpacity` **on read** rather than on write — a value stored
by an older build must not be able to produce an unreadable widget.

### «ويدجت اختياري» — the one text with no takhrij

The reader may pin their own words. It is the single place in the app where text
reaches a screen carrying no attribution, and it stays honest for reasons that
are structural rather than stylistic: the text is the reader's own, typed on
their own phone, **never uploaded**, never shown to anyone else, and printed
under an attribution line as their note rather than in the position a source
would occupy. The admin can withdraw the feature and sets the length, which is a
legibility limit before it is a storage one.

### The default, and how a choice reaches the home screen

Two halves of one path, and it is the path that decides whether the gallery is a
feature or a picture book.

**The default.** `WidgetSettings.DefaultWidgetKey` names the gallery entry a
reader gets before they have chosen anything; the console sets it, and the
gallery manager marks the row so it can be seen where the question is actually
asked. It is refused if it names an entry that is missing or hidden, and it is
cleared automatically when that entry is hidden or deleted — pointing every
fresh install at a widget the gallery no longer offers is the kind of fault
nobody would see. Null is a *setting*, not an absence: it means "whatever the
reader's own build can draw first", which is the right answer for an install a
release behind this server.

**The choice.** The reader's pick is stored on the phone and resolved by
`WidgetCatalog.resolveSelection` in three steps — their own choice, then the
admin's default, then the first entry this build can draw — with every step
subject to the same two tests: the admin still offers it, and this build can
still draw it. `WidgetBridge` then composes finished strings for that key and
hands them to the launcher. Until that existed, the gallery recorded a design
and nothing else: a reader could browse thirty widgets, pick one, and find the
old two-kind layout on their home screen.

**Repainting is wired to the same event as rescheduling.**
`AppState.rescheduleReminders()` pushes the widget first, because every change
that moves a reminder's clock — the location, the method, the madhab, an
adjustment — moves what the widget shows just as surely. Wired separately, they
drifted: choosing a city rebuilt the notifications and left «لم يُحدَّد موقع بعد»
on the home screen until the next sync.

### The lock screen, and why most readers cannot use it

The catalogue offers twenty-two lock-screen entries. On the phone in a reader's
hand today, almost none of them can be placed, and the app says so rather than
letting them hunt.

The platform history is the whole explanation:

- **Android 4.2–4.4** had third-party lock-screen widgets, declared with
  `android:widgetCategory="keyguard"`. **Removed in Lollipop.**
- **Android 5 to 15** had none at all. Nothing an app declares changes that.
- **Android 16 QPR1 and later** brought them back, and made them **opt-out** —
  a widget is eligible unless it declares `not_keyguard`. The panel is reaching
  devices gradually, starting with tablets, so a phone *on* Android 16 may still
  not offer one.
- **iOS** has had them since iOS 16, but this project ships no WidgetKit
  extension yet, so أذكاري is in no iOS list either.

`athkar_widget_info.xml` therefore declares `home_screen|keyguard` and an
`initialKeyguardLayout`. That costs nothing, leaves the home-screen flag
untouched, and makes the widget eligible everywhere eligibility is expressed —
the legacy API, an OEM lock screen that filters on the flag, and the modern
panel that does not need it. What it cannot do is add the feature to a system
that lacks it.

One consequence worth stating plainly, because it shapes what the gallery can
honestly promise: **on Android a lock-screen widget is not a separate widget.**
There is one `AppWidgetProvider`, so where the panel exists it shows whichever
entry the reader already chose for the home screen. The twenty-two lock entries
are a shape the *app's own gallery* previews and iOS would one day place
individually; they are not twenty-two things an Android reader can pick between.

So the copy is platform-specific rather than shared. The help screen prints the
Android path or the iOS path first depending on the reader's own platform, and
each carries a caveat under it; the gallery's note under the lock list says the
same thing in one line. Printing the iOS instruction — "press and hold the lock
screen, then Customise" — to an Android reader sends them looking for a list
that does not exist, and what they conclude is that the app is broken.

### One native layout, not thirty

`res/layout/athkar_widget.xml` renders every entry the gallery offers: a date, a
headline, a title, an optional five-column row of prayer times, and a subtitle.
The app decides which of those carry text.

Thirty RemoteViews layouts would be thirty files to keep in step with the Dart
previews, and a launcher failing to inflate one of them shows a blank rectangle
with nothing to say why. The five columns are fixed rather than a collection
view, because a widget's `ListView` needs a `RemoteViewsService`, a bound remote
adapter and a second process wake-up — for a row whose length never changes.

The two lists cross the channel joined on a unit separator rather than as a
`StringSet`: a set has no order, and here the order *is* the content.

That one-layout fact is also what lets the **console** show an admin what a
widget looks like on a phone without reimplementing thirty designs. The gallery
manager draws a phone frame with that same arrangement, filling only the slots
the chosen entry fills — a hand-kept mirror of `WidgetBridge._build`, in
`cms/athkar-cp/src/app/features/widget/widget-shapes.ts` and pinned by a spec
against the keys the server seeds, so a new entry cannot arrive without a shape.

The preview is honest about the three things it cannot know or does not do:

- **The values are a sample**, labelled as one — a plausible Makkah day. No
  reader's coordinates ever reach this server, so real times are not available
  to it and never will be. What is faithful is the arrangement.
- **The several designs a row offers are an in-app thing.** The home screen gets
  this one arrangement whichever design the reader picked. An admin who thought
  otherwise would lower a design count expecting a home screen to change.
- **A lock-screen entry is drawn as not drawn.** This build ships no
  lock-screen widget on either platform, so those entries are browsable in the
  app's gallery and placeable on no screen. A mockup there would tell an admin
  the entry works.

### The version still moves

Any catalogue change bumps `WidgetSettings.Version`. Without it, hiding a widget
would be a change no phone had any reason to notice — the settings it syncs
against would look untouched — and the withdrawn entry would sit in readers'
galleries until something unrelated happened to bump it.

---

## 10. Conventions that cross all three stacks

- **Response envelope.** Every action returns `BaseResponse` / `BaseResponse<T>`
  — never a raw DTO, never `IActionResult`. The Qur'an download is the one
  documented exception (§7.1).
- **Errors are HTTP 200.** `ExceptionMiddleware` turns an `AppException` into
  200 + a failed envelope; only unhandled exceptions become 500.
  `ValidateModelAttribute` owns invalid-model responses so validation failures
  use the same envelope.
- **Error copy is localised on the client.** The server sends numeric
  `ErrorCode`s; each client maps them
  (`app/.../core/error_messages.dart`, CMS `i18n/*.ts`). A new failure mode
  means a code plus a mapping in both.
- **Enums cross all three stacks** (`Shareds/Enums` → `core/api/models.ts` →
  `models/models.dart`). The numbers are the contract; reordering a member
  silently re-labels data everywhere.
- **DI by convention.** `[ScopedInjectable]` on the service *interface*;
  `AppServiceExtension.RegisterTypes()` wires the single implementation. No
  manual registration in `Program.cs`.
- **Controllers are thin.** Inherit `BaseApiController`, one service call per
  action, no logic.
- **Soft delete lives in the repository.** There is no global query filter, but
  `IRepository<T>.Query()` excludes deleted rows by default; a caller that wants
  them asks (`Query(includeDeleted: true)`).
- **Audit is explicit.** Each mutating service calls
  `auditService.LogAsync(...)` after commit. A request filter knows the verb and
  the route; the service knows this was the unpublishing of a disputed hadith.
- **Every `/api/` request is logged** by `ApiLoggerMiddleware`, placed ahead of
  auth so 401/403 are captured too.

---

## 11. Deliberate non-goals

Scope discipline matters as much as features. The following are excluded on
purpose, and adding one is a product decision, not a chore:

- Advertising, of any kind.
- Reader accounts, sign-in, or any profile on the server.
- Analytics or tracking SDKs.
- Weak or fabricated narrations, however widely circulated.
- Stated virtues that are not established.
- Any permission the app does not actually need.
