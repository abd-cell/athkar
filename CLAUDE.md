# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**أذكاري / Athkari** — an adhkar, prayer-times and qibla app, built as an
endowment (وقف): free, no advertising, no analytics, **no user accounts**.

The product's one distinguishing claim is narrow and load-bearing: *every dhikr
on screen can be traced to a book, a number and a grading.* A large share of the
rules in the code exist to keep that true, and the code refuses to publish
content that would break it.

Three stacks in one repo, each with its own toolchain:

| Path | Stack | Role |
|---|---|---|
| `backend/Athkar/` | ASP.NET Core 9 + EF Core 9 (SQL Server) | the API contract every client depends on |
| `cms/athkar-cp/` | Angular 22 (standalone, SSR, signals) | the control panel — where all content lives |
| `app/athkar_app/` | Flutter 3.47 / Dart 3.13 | the reader's app |

`docs/BUSINESS_LOGIC.md` is the authority on domain rules. **Read it before
changing any of them** — particularly §5 (reminders), which is the rule the
whole notification design turns on. `docs/DESIGN.md` covers the visual system.

## The three facts that shape everything

1. **Readers are anonymous installs.** No sign-up, no password, no email, no
   phone number, and *no coordinates ever sent to the server*. The whole of what
   the server knows is the column list of `Areas/Domain/Devices/Device.cs`. Only
   staff authenticate, and only the CMS.

2. **Prayer times are computed on the device.** Consequence: the server cannot
   know when Maghrib is for a reader, so a prayer-anchored reminder is
   *scheduled by the phone*, not pushed. `ReminderService.Apply` forces this
   rather than letting the CMS build a campaign that would silently never fire.

3. **`AppConfiguration.ContentVersion` is the entire sync protocol.** Every
   content change bumps it; the app sends what it has and usually gets back
   "nothing has changed". Forgetting to bump it is not cosmetic — it is an edit
   that never arrives.

## Toolchain

- **Flutter is not on PATH** — always call `C:\flutter\bin\flutter.bat`.
- dotnet 9, Angular CLI 22 (via `npx @angular/cli@22`), node 25 are on PATH.
- Database: SQL Server `DESKTOP-SBF2I7A`, database `Athkar` (see `appsettings.json`).
- Dev login: `admin@athkari.app` / `Athkari!2026`, seeded on first run.
  **Change it in any real deployment** — the seeder logs a warning naming it.
- **`Jwt:Secret` is not in `appsettings.json`** and the API throws on startup
  without it — a fresh clone needs `dotnet user-secrets set "Jwt:Secret" "..."`
  (32 bytes minimum) in `backend/Athkar`, or `Jwt__Secret` in the environment.
  The guard is in `Program.cs` beside the `JwtSettings` bind: an empty key would
  otherwise sign every token in the system and nothing downstream would notice.
- The `.claude/skills/run-app` skill holds the verified, gotcha-annotated
  recipes for running each stack — on the web, on a real Android phone over the
  LAN, and the control panel. Use it rather than re-deriving the commands: it
  records the five Gradle failures that stand between a fresh clone and an APK,
  the PowerShell cmdlets that are broken on this machine, and how to tell "the
  app launched" from "the app works".

## Commands

```bash
# backend (from backend/)
dotnet build Athkar.sln
dotnet run --project Athkar --launch-profile Athkar
dotnet test
dotnet test --filter "FullyQualifiedName~ReminderScheduleTests"
dotnet ef migrations add <Name> --project Athkar --startup-project Athkar
```

Profile `Athkar` serves `http://0.0.0.0:5000` + `https://0.0.0.0:5001` (Swagger
at `/swagger`); profile `https` serves `:5256`/`:7034` on localhost only.
Migrations are applied and the admin account seeded automatically on startup.

Start the API detached from PowerShell (`Start-Process dotnet -ArgumentList
"run",...`) — run in the foreground it holds the tool call open for as long as
it serves. A running API also locks `Athkar.exe`, so **stop it before
`dotnet test`** or the build fails on a file copy.

```bash
# CMS (from cms/athkar-cp/)
npm start -- --host 127.0.0.1   # see the note below
npm run build
```

```bash
# app (from app/athkar_app/)
C:/flutter/bin/flutter.bat analyze
C:/flutter/bin/flutter.bat test
C:/flutter/bin/flutter.bat run -d web-server --web-port 8090 \
  --dart-define=API_BASE_URL=http://localhost:5000/api/v1/
```

`flutter analyze` is expected to stay at 0 issues.

### The API base URL is target-specific — the #1 gotcha

`lib/core/environment.dart` reads `API_BASE_URL` from a compile-time
`--dart-define`, defaulting to the host's **LAN** IP so a phone, an emulator and
the host browser can all reach one backend. Pass the URL the *target* can
resolve: `localhost` (host browser), `10.0.2.2` (Android emulator only), the LAN
IP (real device). Prefer plain HTTP `:5000` — the self-signed cert on `:5001`
gives browsers a timeout and Dart a `HandshakeException`. The CMS has its own
copy in `cms/athkar-cp/src/app/environment.ts`; when the DHCP LAN IP moves,
update both.

### Two more gotchas worth knowing before you hit them

- **`ng serve` binds to `localhost` only.** `curl 127.0.0.1:4200` gets nothing
  back. Pass `--host 127.0.0.1` when anything other than a browser on the same
  machine needs to reach it.
- **The Flutter web debug build is slow to first paint** (DDC ships ~1000
  modules). For a quick visual check, `flutter build web --release` and serve
  `build/web` statically — it loads in seconds instead of a minute.

## Backend architecture

`Controllers → Services → DataAccess → Domain`, cross-cutting in `Shareds/`.
Feature-grouped: every feature has a folder under each of `Areas/Domain`,
`Areas/Services`, `Areas/Controllers`. Adding a feature means adding a slice to
all three, not a new layer.

- **Response envelope.** Every action returns `BaseResponse` / `BaseResponse<T>`
  — never a raw DTO, never `IActionResult`. One documented exception: the Qur'an
  download streams a file (`docs/BUSINESS_LOGIC.md` §7.1).
- **Errors are HTTP 200.** `ExceptionMiddleware` turns a thrown `AppException`
  into 200 + a failed `BaseResponse`; only unhandled exceptions become 500.
  `ValidateModelAttribute` owns invalid-model responses (`SuppressModelStateInvalidFilter`
  is on) so validation failures use the same envelope.
- **Controllers are thin.** Inherit `BaseApiController` (`/api/v1/[controller]`),
  one service call per action, no logic. Admin controllers override the route
  (`[Route("api/v1/admin/content")]`).
- **DI by convention.** Put `[ScopedInjectable]` / `[TransientInjectable]` /
  `[SingletonInjectable]` on the service **interface**;
  `AppServiceExtension.RegisterTypes()` scans the assembly and wires the single
  implementation. No manual registration in `Program.cs` — except the hosted
  services, which are not interfaces.
- **Soft delete lives in the repository, not in EF.** There is no global query
  filter, but `IRepository<T>.Query()` excludes `IsDeleted` rows by default, so
  `SoftDelete(entity)` is enough to hide something everywhere. A caller that
  wants the deleted rows back must ask: `Query(includeDeleted: true)`.
- **Audit is explicit, by design.** Each mutating service calls
  `auditService.LogAsync(AuditActions.X, nameof(Entity), id)` after commit. A
  request filter knows the verb and the route; the service knows that *this* was
  the unpublishing of a disputed hadith. Keep adding the call.
- **Auth.** JWT bearer plus a `UserLogin` session row per device, re-validated on
  every request in `OnTokenValidated`, so logout revokes immediately;
  `ClockSkew = TimeSpan.Zero` is deliberate. Authorize with `[AppAuthorize]` /
  `[AppAuthorize(Roles.Admin)]`, not `[Authorize]`. Roles are a **ladder**
  (`Editor < Admin < SuperAdmin`), so ask `securityManager.IsAtLeast(...)`.
- **Push.** `PushDispatcher` is three separate passes — materialise, send,
  fan-out broadcasts — because each has a different safe retry. FCM is
  credential-gated: with no service-account JSON, the inbox rows are still
  written and only the push is skipped. A development machine without Firebase
  is a supported configuration.
- **Arabic search is folded, and folded once.** `Shareds/Text/ArabicText.cs`
  strips diacritics and unifies letter shapes; the result is **stored** on
  `Dhikr.SearchText` because an index cannot serve a function applied to every
  row. `app/.../core/arabic_text.dart` is the exact mirror — change one and you
  must change the other, and the two test files hold identical cases so you find
  out immediately.
- **A notification states its own direction.** `Shareds/Text/BidiText.cs` puts a
  right- or left-to-left mark at the head of every line it pushes, and isolates
  any value substituted into a sentence. A notification is drawn by the OS, in a
  shade whose base direction comes from the *device's* locale — so an Arabic
  broadcast on a phone set to English is laid out left-to-right, and while the
  letters read correctly the neutrals move: the full stop jumps to the far edge
  and «صحيح البخاري 6405» comes out «6405 صحيح البخاري». `app/.../core/bidi_text.dart`
  is the exact mirror for reminders the phone raises itself, and the two test
  files hold identical cases — including Arabic-Indic digits, which sit in the
  Arabic block but are a *number*, not a direction. `intl`'s
  `detectRtlDirectionality` gets that one wrong (it weighs how much of the text
  is RTL rather than applying the first-strong rule), which is why neither side
  delegates to it.

- **Every `/api/` request is logged** by `ApiLoggerMiddleware`, placed ahead of
  auth so 401/403 are captured too.
- **`Message.Token`, never `Message.Fid`.** The FirebaseAdmin .NET SDK marks
  `Token` obsolete and points at `Fid` — and the two are *not* the same value.
  `Fid` is a Firebase Installation ID; a registration token sent in that field
  comes back `NotRegistered`, which is indistinguishable from a reader who
  uninstalled the app. The dispatcher then retires a perfectly good token and
  that install goes quiet permanently. Verified against the REST API: the same
  token rejected through `Fid` returns HTTP 200 as `token`.
- **A push token is a lease, not a fact.** FCM rotates it whenever it likes, and
  a stale one fails *silently* — FCM accepts a send to a token it has already
  retired. So `devices/push-token` publishes a rotation the moment it happens
  rather than at the next launch, it writes only the token and the switch (never
  a stale language or timezone), and it refuses an unknown key rather than
  upserting a half-made row. `PushManagerService` is the read side: it keeps
  *tokenless*, *muted*, *silent* and *overdue* apart, because all four look like
  "0 delivered" from a broadcast row and each has a different fix.

## CMS architecture

Standalone components + signals, lazy `loadComponent` routes, SSR enabled.

- **Routes are language-first**: `/:languageCode/...` guarded by `languageGuard`.
- **`IS_SERVER`, not `PLATFORM_ID`.** `core/services/platform.ts` is an explicit
  token, set true in exactly one place. The platform id is *supposed* to answer
  "am I on the server" and under this SSR setup it does not — the browser branch
  was taken during server rendering. Anything that reads `localStorage` or
  redirects on a session it cannot see asks this token instead.
- **`errorInterceptor` does nothing during SSR**, and that is load-bearing. The
  server holds no token, so every screen's own fetches 401 there as a matter of
  course; signing out and navigating to login on that basis turned every deep
  link into a 302 to the login form.
- **Interceptor order matters on the way back**: `[auth, error, refresh]` —
  responses unwind in reverse, so `refreshInterceptor` sees a 401 first and can
  renew before `errorInterceptor` ends the session. Calls that must opt out (the
  configuration bootstrap, the refresh call itself) set `SKIP_AUTH_HANDLING` in
  the `HttpContext`.
- **A `signal` holding a form object is not reactive to `ngModel`.** Two-way
  binding mutates that object *in place*, so the signal never notifies and a
  `computed` over it is evaluated once and never again. Gate a submit button on
  a computed like that and it stays disabled however much the admin types.
  Validate by reading the object when the button is pressed, as every other
  screen here does.
- **One typed gateway**, `core/api/api.service.ts`. It returns the envelope
  rather than unwrapping it, because a failure carries an `errorCode` each
  screen maps to its own copy through `errorKey()`.
- **One translations editor** (`components/translations-editor.component.ts`)
  serves chapters, adhkar, reminders, broadcasts and help topics — they all have
  the same shape, and five near-identical forms would drift.
- **i18n**: flat `Record<string, string>` maps in `src/app/i18n/{ar,en}.ts`, read
  through the impure `| translate` pipe. Add every key to **both** files.
- The admin-set brand colour is applied in `provideAppInitializer` before first
  paint (and never rejects) so screens don't flash the default palette.

## Flutter app architecture

Lean stack on purpose — **no state-management package**. `http` +
`shared_preferences` + `intl`, plus `adhan`, `flutter_local_notifications`,
`firebase_messaging`, `geolocator`, `flutter_compass`, `sqflite` and
`google_fonts`.

- **Layers**: `core/` (infrastructure) → `services/` (one class per API area, all
  in `services/services.dart`) → `features/` (screens) → `widgets/` (the UI kit).
  `models/models.dart` holds every DTO.
- **State** is two `InheritedNotifier`s: `SettingsScope` (the reader's choices,
  backed by preferences) and `AppStateScope` (the caches and the sync). That is
  the whole of it.
- **`ApiClient`** is a singleton gateway that **never throws**: transport,
  timeout and parse failures become typed `AppResponse` errors, so screens only
  ever check `success` / `errorMessage`.
- **Launch is offline-first.** `AppState.load()` reads the caches and paints;
  `AppState.sync()` runs afterwards and every step of it is allowed to fail
  without anything visible going wrong.
- **Theming**: `core/theme.dart` is the single source of truth, ported 1:1 from
  the design prototype. The brightness-aware token set comes from the
  `AthkarTokens` `ThemeExtension` — `AthkarTokens.of(context)`. Don't hardcode
  colours in screens. Amiri carries anything narrated; IBM Plex Sans Arabic
  carries the interface around it.
- **UI kit**: `widgets/athkar_ui.dart` (components), `athkar_alerts.dart`
  (toasts, confirms). **Never use `SnackBar`** — go through `AthkarAlerts` — and
  never `CircularProgressIndicator` directly: the app's loader is
  `AthkarSpinner`.
- **Reminders**: `core/reminder_scheduler.dart` rebuilds a rolling **7 days**
  because iOS allows only 64 pending notifications. Ids are
  `campaignId * 100 + dayOffset` so a rebuild replaces rather than duplicates.
  Call `AppState.rescheduleReminders()` after anything that moves a reminder's
  clock: location, method, madhab, an adjustment, the bedtime, a mute.
- **A prayer convention follows the reader's country, not a global default.**
  `Settings.calculationMethod` resolves explicit choice → country → configured
  default, *on read*, so an existing install heals itself. Umm al-Qura's Isha is
  a fixed 90 minutes after Maghrib — a Makkah rule that is eight minutes late in
  Amman — so shipping one default to everyone is a nightly error nobody can see.
  `test/prayer_convention_test.dart` guards it.
- **Android notification channels are immutable once created.** To change how a
  reminder sounds you add a channel, never edit one — hence the version suffix
  on every id in `LocalNotifications`.
- **The qibla is corrected to true north by the platform, not by us.** See
  `core/true_heading.dart`: iOS already reports a true heading, Android's
  declination comes from `GeomagneticField` over a method channel. A
  tilted-dipole model was tried and removed — it is wrong by 10–15° in the Gulf,
  which is worse than no correction because nobody would check it.
- **i18n**: flat key→text maps in `lib/l10n/strings_{ar,en}.dart`, looked up with
  `context.tr('some.key')` and `{placeholder}` interpolation, **merged under** the
  CMS's overlay. Add every key to **both** maps — a miss falls back to Arabic,
  then to the raw key, and `debugPrint`s in debug builds.

## Tests

- **Backend** — `backend/Athkar.Tests`, xUnit with hand-rolled doubles in
  `TestDoubles/` (`InMemoryRepository<T>`, `FakeUnitOfWork`, `FakeAuditService`,
  `FakeSecurityManager`, `FakeAppConfigurationService`) plus an async-query shim
  so EF's async operators run over lists. No database, no mocking framework —
  extend the fakes rather than introducing one.
- **App** — `app/athkar_app/test/*`. The Arabic-folding cases are deliberately
  identical to the server's, so a divergence fails on both sides.
- The tests worth having here are the ones about time and text: DST transitions,
  the weekday bit set, the folding rules, and the publish-requires-a-source rule.

## Cross-stack conventions

- Enums cross all three stacks (`Shareds/Enums` → `core/api/models.ts` →
  `models/models.dart`). **The numbers are the contract** — reordering a member
  silently re-labels data everywhere.
- A new failure mode means: add an `ErrorCode`, then map it in the app's
  `error_messages.dart` and in the CMS's `errorKey()` + i18n files.
- Anything an admin can change lives in the CMS, not in a constant — content,
  wording, languages, reminder schedules, prayer defaults, the brand colour.

## QA

A full pass is recorded in `qa/QA_REPORT.md` (12 Sep 2026). Two gotchas it
surfaced that are easy to hit again:

- **A `computed` may not write to a signal** (Angular `NG0600`). When one does,
  it throws, the `@if` around it never opens, and the screen renders *missing*
  rather than broken — no build error, no console-visible failure unless you
  look. `translations-editor.component.spec.ts` guards this one.
- **A stored clock string is not a displayable one.** Times are stored 24-hour
  and arrive in two shapes — «HH:mm» from the bedtime setting, «HH:mm:ss» from a
  server-side fixed-time campaign. Render them through `Numerals.clock`, never
  raw, and never take the minute from the last `:` segment: that reads
  «13:36:00» as 13:00 and shows a confident wrong time.
- **Arabic copy is composed, not concatenated.** Anchor names are bare nouns;
  the sentence template supplies «بعد»/«قبل». `test/strings_test.dart` enforces
  that, plus key parity between `strings_ar.dart` and `strings_en.dart`.
