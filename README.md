# أذكاري — Athkari

أذكار ومواقيت صلاة وقبلة. بلا إعلانات، بلا حسابات، بلا تتبّع، ويعمل دون إنترنت.
كل ذكر مقترن بمصدره ودرجة إسناده.

An adhkar, prayer-times and qibla app built as an endowment (وقف): free, no
advertising, no analytics, no user accounts, and it works offline. Every dhikr
carries its source and its grading.

---

## The repository

| Path | Stack | Role |
|---|---|---|
| `backend/Athkar/` | ASP.NET Core 9 + EF Core 9 (SQL Server) | the API |
| `cms/athkar-cp/` | Angular 22 (standalone, SSR, signals) | the control panel |
| `app/athkar_app/` | Flutter 3.47 | the reader's app |
| `docs/` | | `BUSINESS_LOGIC.md` (the authority on rules), `DESIGN.md` |

`CLAUDE.md` is the working guide: toolchain, commands, architecture and the
gotchas worth knowing before you hit them.

## Running it

```bash
# 1. API — applies migrations and seeds on first run
cd backend && dotnet run --project Athkar --launch-profile Athkar

# 2. control panel
cd cms/athkar-cp && npm install && npm start

# 3. app
cd app/athkar_app && C:/flutter/bin/flutter.bat run \
  --dart-define=API_BASE_URL=http://localhost:5000/api/v1/
```

The first run seeds one administrator — `admin@athkari.app` / `Athkari!2026` —
fourteen chapters, a set of sourced adhkar, two languages and two reminder
campaigns. **Change that password in any real deployment.**

## Tests

```bash
cd backend && dotnet test                      # stop the API first — it locks the exe
cd app/athkar_app && C:/flutter/bin/flutter.bat test
```

## What it does not do, on purpose

No advertising. No reader accounts or sign-in. No analytics or tracking. No weak
or fabricated narrations, however widely circulated. No stated virtues that are
not established. No permission the app does not actually need — location is
optional, and prayer times are computed on the device precisely so that a
reader's coordinates never leave it.

---

*«مَن دَلَّ على خَيرٍ فَلَهُ مِثلُ أجرِ فاعِلِه» — رواه مسلم*
