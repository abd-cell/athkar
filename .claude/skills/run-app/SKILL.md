---
name: run-app
description: Launch and drive the Athkari stack — the Flutter app (app/athkar_app) on web or a real Android phone, the ASP.NET API it talks to, and the Angular control panel. Use when asked to run, start, build, install or screenshot "the app", to get a build onto a phone, or to confirm a change works in the real app rather than only in tests.
---

# Run the Athkari app

Three runnable stacks. "the app" = the **Flutter** client in `app/athkar_app/`.
The control panel is Angular (`cms/athkar-cp/`); the API is ASP.NET
(`backend/Athkar/`). `docs/BUSINESS_LOGIC.md` is the authority on behaviour;
this file is only about getting things running.

Everything below was verified on this machine on **12 Sep 2026** unless marked
otherwise. Where a step failed first and then worked, the failure is recorded
too — that is usually the useful half.

---

## Toolchain facts

- **Flutter is NOT on PATH.** Always the full path: `C:\flutter\bin\flutter.bat`
  (Flutter 3.47.2, Dart 3.13.2).
- dotnet 9.0.300, node 25.8.2, npm 11.12.1 are on PATH.
- **The global `ng` is Angular 18**, not 22 — `ng new` scaffolds the wrong major
  version. Use `npx --yes @angular/cli@22` for anything that generates a project.
  The repo's own CMS is Angular 22 and `npm start` inside it uses the local CLI,
  so this only bites when scaffolding.
- Database: SQL Server `DESKTOP-SBF2I7A`, database `Athkar`. Migrations apply and
  the seeder runs on API startup.
- Seeded admin: `admin@athkari.app` / `Athkari!2026`.
- Android SDK is at **`C:\Android`** (`ANDROID_HOME`), *not* the Windows default
  `%LOCALAPPDATA%\Android\sdk`. Both directories exist on this machine with
  similar contents, which is a good way to waste ten minutes reading the wrong
  one. **Check `app/athkar_app/android/local.properties` → `sdk.dir` first.**
  `adb.exe` lives at `C:\Android\platform-tools\adb.exe`.

### PowerShell cmdlets that do not work here

Several `Net*` cmdlets fail with `Invalid namespace` on this machine. Do not
spend time debugging them; use the listed fallback.

| Broken | Use instead |
|---|---|
| `Get-NetIPAddress` | `ipconfig` |
| `Get-NetFirewallRule` / `New-NetFirewallRule` | `netsh advfirewall firewall ...` (needs elevation) |

`Start-Process npm ...` also fails with **`%1 is not a valid Win32 application`**
— `npm` is a shell script, not an exe. Wrap it:

```powershell
Start-Process cmd -ArgumentList "/c","npm start -- --port 4299 --host 127.0.0.1 > out.log 2>&1" -WindowStyle Hidden
```

### Start long-running servers detached from PowerShell

Not because they fail from Bash — `dotnet run` was tested from the Bash tool on
12 Sep 2026 and served correctly (`Now listening on: http://127.0.0.1:5123`,
still answering 45s later). The reason is simpler: **a foreground server holds
the tool call open for as long as it runs**, so nothing else can happen until it
is killed, and it goes away with the call. Detach it and keep the pid.

The same applies to `flutter run`, `ng serve`, and `flutter build` — the last
takes minutes and should not block a call either:

```powershell
$p = Start-Process dotnet -ArgumentList "run","--no-launch-profile","--urls","http://0.0.0.0:5000" `
  -WorkingDirectory "C:\Git\claude\Athkar\backend\Athkar" -PassThru `
  -RedirectStandardOutput api.log -RedirectStandardError api.err -WindowStyle Hidden
$p.Id | Out-File api.pid
```

Keep the pid — you will need it, because **a running API locks
`backend/Athkar/bin/Debug/net9.0/Athkar.exe` and `dotnet test` then fails on a
file copy** (`MSB3027 ... being used by another process`). Stop the API before
running the backend tests.

---

## Bring up the API

```powershell
# All interfaces, so a phone on the LAN can reach it. Loopback-only is the
# default trap: everything works from the host and nothing works from a device.
Start-Process dotnet -ArgumentList "run","--no-launch-profile","--urls","http://0.0.0.0:5000" ...
```

The `Athkar` launch profile also serves `https://0.0.0.0:5001`, but the dev cert
is self-signed: browsers time out and Dart throws `HandshakeException`. **Prefer
plain HTTP on :5000.**

Readiness — poll the endpoint, it answers in a couple of seconds:

```bash
for i in $(seq 1 12); do curl -s -m 3 http://127.0.0.1:5000/api/v1/configuration >/dev/null && break; sleep 2; done
curl -s -o /dev/null -w "%{http_code}\n" -m 5 http://127.0.0.1:5000/api/v1/configuration   # expect 200
```

Get an admin token for poking at admin endpoints:

```bash
TOKEN=$(curl -s -X POST http://127.0.0.1:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@athkari.app","password":"Athkari!2026"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

curl -s http://127.0.0.1:5000/api/v1/admin/devices/stats -H "Authorization: Bearer $TOKEN"
```

---

## The API base URL is target-specific — the #1 gotcha

`app/athkar_app/lib/core/environment.dart` reads `API_BASE_URL` from a
compile-time `--dart-define`. The default is the host's **LAN** address, so a
build with no extra flags works on a phone and from Android Studio.

Pass the URL the *target* can resolve:

| Target | URL |
|---|---|
| Real phone on the same Wi-Fi | `http://<host-LAN-IP>:5000/api/v1/` ← the default |
| Host browser | `http://localhost:5000/api/v1/` |
| Android emulator | `http://10.0.2.2:5000/api/v1/` (emulator-only alias) |

**Find the LAN IP with `ipconfig`** (the Wi-Fi adapter's IPv4). It was
`192.168.1.43` on 12 Sep 2026. It is DHCP-assigned — re-check it rather than
trusting what is baked into the files.

When it moves, **three files change together**:

1. `app/athkar_app/lib/core/environment.dart` — the default.
2. `app/athkar_app/android/app/src/main/res/xml/network_security_config.xml` —
   see below. Miss this one and the app installs, opens, and silently fails
   every call, which looks exactly like a broken backend.
3. `cms/athkar-cp/src/app/environment.ts` — the CMS's own copy.

### Android blocks cleartext HTTP

From API 28 onwards, plain `http://` is refused unless the app opts in. The app
ships `network_security_config.xml` permitting cleartext for the LAN IP,
`10.0.2.2`, `localhost` and `127.0.0.1` — **by host**, not through the global
`usesCleartextTraffic` flag, which would allow unencrypted traffic anywhere
including in a release build.

iOS needs no equivalent for this app: the API is reached over the LAN in
development only, and `NSAllowsLocalNetworking` covers it if it ever does.

---

## Run on a real Android phone

This is the path most worth knowing, and it was the one with all the traps.

### 1. Find the device

```bash
ADB="/c/Android/platform-tools/adb.exe"
"$ADB" devices -l
```

Wireless debugging devices appear twice — once as `IP:PORT`, once as an mDNS
name `adb-SERIAL-xxxx._adb-tls-connect._tcp`. Either works as `-s`.

**Wireless connections drop between commands.** A device listed a minute ago
may be gone; `adb: device 'x' not found` usually means exactly that and not a
real problem. Reconnect:

```bash
"$ADB" connect 192.168.1.112:33541
```

### 2. Build the APK

```powershell
Start-Process cmd -ArgumentList "/c","C:\flutter\bin\flutter.bat build apk --release --dart-define=API_BASE_URL=http://192.168.1.43:5000/api/v1/ > apk.log 2>&1" -WindowStyle Hidden
```

Takes 2–7 minutes. Poll the log for a terminal line rather than guessing:

```bash
for i in $(seq 1 110); do
  grep -qa "Built build\|BUILD FAILED\|assembleRelease failed" apk.log && break
  sleep 10
done
grep -a -A12 "What went wrong" apk.log | head -18
```

Output lands at `build/app/outputs/flutter-apk/app-release.apk` (~56 MB).
The release build is **signed with debug keys** (Flutter's template default), so
it installs fine for testing but needs a real keystore before Play.

### 3. Install and launch

```bash
"$ADB" -s "$DEVICE" install -r build/app/outputs/flutter-apk/app-release.apk
"$ADB" -s "$DEVICE" shell monkey -p com.athkari.athkar_app -c android.intent.category.LAUNCHER 1
```

### 4. Prove it actually works

Launching is not evidence. Three checks, in order:

```bash
# a) is it alive, or did it crash on startup?
"$ADB" -s "$DEVICE" shell pidof com.athkari.athkar_app

# b) did it reach the API? (the app registers on every launch)
grep -a -o "\(GET\|POST\) /api/v1/[a-zA-Z0-9/?=&._-]*" api.log | sort | uniq -c

# c) what does it look like?
"$ADB" -s "$DEVICE" exec-out screencap -p > phone.png
```

A healthy first launch produces `POST /api/v1/devices/register` plus
`content/catalog`, `languages/ar/strings`, `reminders` and `quran/check-version`.

**There is no `curl` on these phones** (`/system/bin/sh: curl: inaccessible or
not found`), so the app's own traffic in `api.log` is the connectivity test.

**An all-black screenshot usually means the screen is off, not a crash.** Wake
it and re-shoot:

```bash
"$ADB" -s "$DEVICE" shell input keyevent KEYCODE_WAKEUP
"$ADB" -s "$DEVICE" shell dumpsys window | grep -i "mAwake\|mDreamingLockscreen"
```

For a real crash, the stack is in the crash buffer — the main logcat shows
almost nothing useful:

```bash
"$ADB" -s "$DEVICE" logcat -d -b crash -t 60
```

### 5. The firewall

A rule for inbound TCP 5000 needs an **elevated** shell:

```
netsh advfirewall firewall add rule name="Athkar API (dev, LAN)" dir=in action=allow protocol=TCP localport=5000 profile=private remoteip=localsubnet
```

It was **not needed** on 12 Sep 2026 — both phones reached the API without it.
Testing from the host proves nothing either way (loopback and same-machine
LAN-IP traffic bypass the firewall). Check (b) above instead.

---

## Android build blockers, and what each one actually was

All five of these hit in sequence on 12 Sep 2026 building the first APK. They
are fixed in the repo now; this is here so the *next* plugin addition does not
cost the same hour.

**1. `Cannot inline bytecode built with JVM target 11 into bytecode that is
being built with JVM target 1.8`** — a plugin module that declares no Kotlin
target falls back to 1.8 and then fails against any dependency built at 11+.
Fixed in `android/build.gradle.kts` by pinning **every** subproject to 17.

**2. `Inconsistent JVM-target compatibility ... (1.8) and ... (17)`** — fixing
only Kotlin makes Kotlin and Java disagree. Java is driven by AGP from the
module's own `android.compileOptions`, and the plugin's build script evaluates
*after* the root file, so the DSL has to be set again in `afterEvaluate` to get
the last word. Configuring the `JavaCompile` task is silently overridden.

**3. `options.release` is refused by AGP** ("would bypass the Android
bootclasspath"). Use `sourceCompatibility`/`targetCompatibility`, not `release`.

**4. `Dependency ':flutter_local_notifications' requires core library
desugaring`** — enable `isCoreLibraryDesugaringEnabled` and add
`coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.5")`.

**5. `androidx.glance:glance-appwidget:1.3.0-alpha02 requires ... version 37 or
later`** — pulled by `home_widget` 0.8+. Two sub-traps if you try to satisfy it:

- The SDK is installed as `platforms/android-37.0`, a **minor-versioned**
  platform. `compileSdk = 37` alone reports `Failed to find target with hash
  string 'android-37'` while the platform sits right there; AGP 9 needs
  `compileSdkMinor = 0` as well.
- AGP 9.1 warns that its **maximum recommended compileSdk is 36**, so satisfying
  an alpha dependency drags the whole app past supported ground.

`home_widget` was removed rather than accommodated — it also crashed the app at
launch (`Failed to create an instance of androidx.work.impl.WorkDatabase` under
R8), and its native half was never written. See the note in `pubspec.yaml`.

---

## Run on the web

### Debug is fine for iterating, useless for a quick look

`flutter run -d web-server` serves `index.html` within seconds while the Dart
build keeps going — the console shows `DDC is about to load 964/964 scripts` and
first paint takes well over a minute. Navigating early gives a **black page**,
which reads exactly like a broken app.

**For a visual check, build release and serve it statically** — it loads in
seconds:

```powershell
Start-Process cmd -ArgumentList "/c","C:\flutter\bin\flutter.bat build web --release --dart-define=API_BASE_URL=http://127.0.0.1:5000/api/v1/ > web.log 2>&1" -WindowStyle Hidden
```

```powershell
Start-Process python -ArgumentList "-m","http.server","8100","--bind","127.0.0.1" `
  -WorkingDirectory "C:\Git\claude\Athkar\app\athkar_app\build\web" -PassThru -WindowStyle Hidden
```

Then `preview_start` on `http://127.0.0.1:8100/`.

- The app is **canvas-rendered**: `get_page_text` returns nothing and
  `read_page` is useless. Screenshot, and read the pixels.
- `resize_window` to 390×844 for a phone-shaped view; `colorScheme: "light"` /
  `"dark"` exercises both palettes (the app follows the system theme).
- Flutter web syncs its `Navigator` with browser history, so a reload can
  restore whatever screen you pushed last rather than opening on the home tab.

---

## Run the control panel

```powershell
Start-Process cmd -ArgumentList "/c","npm start -- --port 4299 --host 127.0.0.1 > cms.log 2>&1" `
  -WorkingDirectory "C:\Git\claude\Athkar\cms\athkar-cp" -WindowStyle Hidden
```

- **The dev server's incremental build can miss i18n edits.** Keys added to
  `src/app/i18n/{ar,en}.ts` rendered as empty strings — some of them, not all,
  which reads exactly like a missing key. `ng build` was clean and a full page
  reload did not fix it; restarting `npm start` did. If a label you just added
  renders blank, restart the dev server before you go looking for the bug.
- **Pass `--host 127.0.0.1`.** Without it the dev server binds `localhost` only
  in a way `curl 127.0.0.1:4299` cannot reach — the port answers nothing and the
  browser pane refuses to navigate. With it, everything works.
- A bare `GET /` returns **302**, not 200 — the language guard redirects to
  `/ar/...`. Poll `/ar/login` for a 200 ready signal.
- Sign in at `http://127.0.0.1:4299/ar/login`.

### If deep links 302 to the login page

That is the SSR auth story, not a broken session. The server holds no token, so
every screen's own fetches 401 there as a matter of course. `errorInterceptor`
is deliberately inert during server rendering for exactly this reason
(`core/interceptors/error.interceptor.ts`), and the guards short-circuit on the
`IS_SERVER` token rather than on `PLATFORM_ID` — **the platform id takes the
browser branch on the server here**, which is what caused this in the first
place. If it comes back, check those two files before anything else.

Decisive test, no browser needed:

```bash
curl -s -o /dev/null -w "%{http_code} %{redirect_url}\n" http://127.0.0.1:4299/ar/adhkar   # want 200
```

---

## Check what is already running before launching anything

```bash
netstat -ano | grep LISTENING | grep -E ":(5000|4299|8100|8090)"
```

A busy port fails the launch with `SocketException ... errno = 10048` — a
collision, nothing to debug. Prefer a free port over killing the user's
servers. To stop something you started, use the pid you saved:

```powershell
Stop-Process -Id (Get-Content api.pid) -Force
```

`taskkill //F //IM dart.exe` is a blunt instrument — there are usually several
`dart` processes, including the analysis server.

---

## Verify the whole stack in one pass

```bash
printf "API  "; curl -s -o /dev/null -w "%{http_code}\n" -m 5 http://127.0.0.1:5000/api/v1/configuration
printf "CMS  "; curl -s -o /dev/null -w "%{http_code}\n" -m 8 http://127.0.0.1:4299/ar/login
printf "APP  "; curl -s -o /dev/null -w "%{http_code}\n" -m 5 http://127.0.0.1:8100/
```

And the checks that should pass before calling any change done:

```bash
cd backend && dotnet test                                  # stop the API first
cd app/athkar_app && C:/flutter/bin/flutter.bat analyze    # expected: 0 issues
cd app/athkar_app && C:/flutter/bin/flutter.bat test
cd cms/athkar-cp && npm run build
```

---

## Running from Android Studio

Open `app/athkar_app` (not the repo root), pick the device, Run.

No `--dart-define` is needed: `environment.dart` already defaults to the LAN
address. If the host IP has moved, fix that file and
`network_security_config.xml` first — otherwise the app runs and quietly
reaches nothing.

## iOS

**A Flutter iOS build cannot be produced from Windows** — compilation and
signing need macOS and Xcode. From this machine the only way onto an iPhone is
the web build over the LAN: serve it with `--web-hostname 0.0.0.0` and open
`http://<host-LAN-IP>:<port>` in Safari. A native build needs a Mac or a
cloud-Mac runner.
