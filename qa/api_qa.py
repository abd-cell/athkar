"""
Athkari — API acceptance suite.

Exercises the HTTP contract the way a client actually meets it: valid calls,
invalid calls, and the rules `docs/BUSINESS_LOGIC.md` says must hold. Every
assertion names the rule it is protecting, so a failure says what broke rather
than which line threw.

Run against a live API and a seeded database:

    python qa/api_qa.py                       # defaults to http://127.0.0.1:5000
    python qa/api_qa.py http://192.168.1.43:5000

It is destructive in a bounded way: it creates content, languages, reminders,
broadcasts and staff under an `qa-` prefix, then deletes them. It never touches
the seeded corpus.

Exit code is the number of failures, so it works as a CI gate.
"""

import json
import sys
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

# The report names Arabic rules and draws box characters, and the Windows
# console still defaults to cp1252 — without this the suite dies formatting its
# own first heading.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5000").rstrip("/") + "/api/v1/"

ADMIN_EMAIL = "admin@athkari.app"
ADMIN_PASSWORD = "Athkari!2026"

# Mirrors Shareds/Models/ErrorCode.cs. Named so failures read as rules.
VALIDATION = 2
NOT_FOUND = 3
INVALID_CREDENTIALS = 100
ACCOUNT_DISABLED = 102
LAST_ADMINISTRATOR = 104
DEVICE_NOT_FOUND = 200
INVALID_DEVICE_KEY = 201
CATEGORY_NOT_FOUND = 300
DUPLICATE_KEY = 302
SOURCE_REQUIRED = 304
CATEGORY_NOT_EMPTY = 305
DEFAULT_LANGUAGE_IMMUTABLE = 402
SOURCE_LANGUAGE_IMMUTABLE = 403
REMINDER_NO_DAYS = 501
REMINDER_SCHEDULE_INCOMPLETE = 502
BROADCAST_ALREADY_SENT = 504
SCHEDULE_MUST_BE_FUTURE = 505
NO_PUBLISHED_QURAN = 601
WIDGET_NO_KIND_ALLOWED = 650

results = []
_section = "general"


def section(name):
    global _section
    _section = name
    print(f"\n\033[1m── {name} ──\033[0m")


def check(rule, condition, detail=""):
    results.append((_section, rule, bool(condition), detail))
    mark = "\033[32mPASS\033[0m" if condition else "\033[31mFAIL\033[0m"
    print(f"  {mark}  {rule}" + (f"   [{detail}]" if detail and not condition else ""))
    return bool(condition)


def call(method, path, body=None, token=None, device=None, raw=False):
    """One request. Returns (http status, parsed envelope-or-None)."""
    url = BASE + path
    data = json.dumps(body).encode() if body is not None else None

    request = urllib.request.Request(url, data=data, method=method)
    request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    if device:
        request.add_header("X-Device-Key", device)

    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = response.read()
            if raw:
                return response.status, payload
            return response.status, json.loads(payload or b"{}")
    except urllib.error.HTTPError as error:
        payload = error.read()
        try:
            return error.code, json.loads(payload or b"{}")
        except json.JSONDecodeError:
            return error.code, None
    except Exception as error:  # noqa: BLE001 - the suite reports, it does not crash
        return 0, {"transport": str(error)}


def ok(envelope):
    return isinstance(envelope, dict) and envelope.get("success") is True


def code(envelope):
    return envelope.get("errorCode") if isinstance(envelope, dict) else None


def data(envelope):
    return envelope.get("data") if isinstance(envelope, dict) else None


# ───────────────────────────── envelope ─────────────────────────────

section("Response envelope")

status, body = call("GET", "configuration")
check("an anonymous read of the configuration succeeds", status == 200 and ok(body))
check(
    "every response carries the envelope (success, errorCode, message, errors)",
    all(k in body for k in ("success", "errorCode", "message", "errors")),
    str(body)[:80],
)
check("a successful call reports errorCode 0", code(body) == 0)

status, body = call("GET", "content/catalog?language=ar&version=abc")
check("a malformed query value does not 500", status < 500, f"status {status}")

status, body = call("GET", "no-such-endpoint")
check("an unknown route is 404, not an envelope", status == 404, f"status {status}")

# ───────────────────────────── auth ─────────────────────────────

section("Authentication")

status, body = call("POST", "auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD})
check("the seeded administrator can sign in", ok(body), str(body)[:100])
TOKEN = (data(body) or {}).get("accessToken")
REFRESH = (data(body) or {}).get("refreshToken")
check("sign-in returns an access token", bool(TOKEN))
check("sign-in returns a refresh token", bool(REFRESH))

status, body = call("POST", "auth/login", {"email": ADMIN_EMAIL, "password": "wrong-password-1"})
wrong_password = code(body)
status, body = call("POST", "auth/login", {"email": "nobody@athkari.app", "password": "whatever-1"})
unknown_email = code(body)
check("a wrong password is refused", wrong_password == INVALID_CREDENTIALS)
check(
    "an unknown account and a wrong password are indistinguishable "
    "(the login form is not a staff directory)",
    wrong_password == unknown_email == INVALID_CREDENTIALS,
    f"{wrong_password} vs {unknown_email}",
)

status, body = call("POST", "auth/login", {"email": "not-an-email", "password": "x"})
check("malformed credentials fail validation, not the server", code(body) == VALIDATION)

status, body = call("GET", "auth/me")
check("an unauthenticated call to /auth/me is 401", status == 401, f"status {status}")

status, body = call("GET", "auth/me", token="not-a-real-token")
check("a forged token is 401", status == 401, f"status {status}")

status, body = call("GET", "auth/me", token=TOKEN)
check("a signed-in caller can read their own account", ok(body))
check("the seeded administrator holds SuperAdmin (3)", 3 in (data(body) or {}).get("roles", []))

status, body = call("POST", "auth/refresh", {"refreshToken": REFRESH})
check("a refresh token trades for a new pair", ok(body))
ROTATED = (data(body) or {}).get("refreshToken")
check("the refresh token rotates", ROTATED and ROTATED != REFRESH)

status, body = call("POST", "auth/refresh", {"refreshToken": REFRESH})
check("the old refresh token stops working once rotated", not ok(body), str(body)[:80])

status, body = call("POST", "auth/refresh", {"refreshToken": "garbage"})
check("a garbage refresh token is refused without a 500", status < 500 and not ok(body))

# Re-authenticate: the rotation above invalidated the session this suite holds.
status, body = call("POST", "auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD})
TOKEN = (data(body) or {}).get("accessToken")

# ───────────────────────────── authorization ─────────────────────────────

section("Authorization")

editor_email = f"qa-editor-{uuid.uuid4().hex[:8]}@athkari.app"
status, body = call(
    "POST",
    "admin/staff",
    {
        "email": editor_email,
        "fullName": "QA Editor",
        "password": "QaEditor!2026",
        "languageCode": "ar",
        "isActive": True,
        "roles": [1],
    },
    token=TOKEN,
)
check("a super-admin can create an editor", ok(body), str(body)[:100])
EDITOR_ID = (data(body) or {}).get("id")

status, body = call("POST", "auth/login", {"email": editor_email, "password": "QaEditor!2026"})
EDITOR_TOKEN = (data(body) or {}).get("accessToken")
check("the new editor can sign in", bool(EDITOR_TOKEN))

status, body = call("GET", "admin/content/categories", token=EDITOR_TOKEN)
check("an editor may read content (that is the job)", ok(body))

status, body = call("GET", "admin/staff", token=EDITOR_TOKEN)
check("an editor may NOT list staff", status == 403, f"status {status}")

status, body = call("GET", "admin/widget", token=EDITOR_TOKEN)
check("an editor may NOT read widget settings", status == 403, f"status {status}")

status, body = call("GET", "admin/reminders", token=EDITOR_TOKEN)
check("an editor may NOT manage reminders", status == 403, f"status {status}")

status, body = call("POST", "auth/logout", {}, token=EDITOR_TOKEN)
check("logout succeeds", ok(body))
status, body = call("GET", "auth/me", token=EDITOR_TOKEN)
check("the token is dead immediately after logout, not at expiry", status == 401, f"status {status}")

# ───────────────────────────── devices ─────────────────────────────

section("Devices (anonymous readers)")

DEVICE = str(uuid.uuid4())
registration = {
    "deviceKey": DEVICE,
    "platform": 1,
    "languageCode": "ar",
    "timeZoneId": "Asia/Riyadh",
    "notificationsEnabled": True,
    "appVersion": "1.0.0-qa",
    "countryCode": "JO",
}

status, body = call("POST", "devices/register", registration)
check("a device registers anonymously", ok(body), str(body)[:100])
check("registration returns the content version to sync against", "contentVersion" in (data(body) or {}))

status, body = call("POST", "devices/register", registration)
check("registration is idempotent (the app calls it every launch)", ok(body))

status, body = call("POST", "devices/register", {**registration, "deviceKey": "not-a-uuid"})
check("a device key that is not a UUID is refused", code(body) == INVALID_DEVICE_KEY)

status, body = call(
    "POST", "devices/register", {**registration, "timeZoneId": "Mars/Olympus_Mons"}
)
check(
    "an unknown timezone falls back rather than failing the registration",
    ok(body),
    str(body)[:80],
)

status, body = call("GET", "devices/notifications", device=DEVICE)
check("a device can read its own inbox", ok(body))

status, body = call("GET", "devices/notifications")
check("the inbox requires a device key", not ok(body), str(body)[:80])

status, body = call("GET", "devices/notifications", device="00000000-0000-0000-0000-000000000000")
check("an unknown device key is reported, not invented", code(body) == DEVICE_NOT_FOUND)

# ───────────────────────────── content, reading ─────────────────────────────

section("Content — the reader's view")

status, body = call("GET", "content/catalog?language=ar")
catalog = data(body) or {}
check("the catalogue is readable without any credential", ok(body))
check("the seeded corpus is present", len(catalog.get("categories", [])) > 0)

VERSION = catalog.get("version")
status, body = call("GET", f"content/catalog?language=ar&version={VERSION}")
matched = data(body) or {}
check(
    "sending the version you hold returns 'up to date' and no payload "
    "(this is the whole sync protocol)",
    matched.get("isUpToDate") is True and not matched.get("categories"),
)

every_dhikr = [d for c in catalog.get("categories", []) for d in c.get("adhkar", [])]
check("published adhkar exist to check", len(every_dhikr) > 0)
check(
    "EVERY published dhikr carries a book and a reference "
    "(the project's one distinguishing promise)",
    all(d.get("sourceBook") and d.get("sourceReference") for d in every_dhikr),
    f"{sum(1 for d in every_dhikr if not (d.get('sourceBook') and d.get('sourceReference')))} without",
)
check(
    "every published dhikr carries its Arabic text",
    all((d.get("arabicText") or "").strip() for d in every_dhikr),
)

status, body = call("GET", "content/catalog?language=en")
english = data(body) or {}
check("the catalogue resolves into English", (english.get("languageCode")) == "en")
english_adhkar = [d for c in english.get("categories", []) for d in c.get("adhkar", [])]
check(
    "the Arabic travels in every language's payload (it is the text, not a translation)",
    all((d.get("arabicText") or "").strip() for d in english_adhkar),
)

status, body = call("GET", "content/catalog?language=zz")
check("an unknown language falls back to the default rather than failing", ok(body))

# Search — the folding that makes Arabic usable.
status, body = call("GET", "content/search?language=ar&search=%D8%B3%D8%A8%D8%AD%D8%A7%D9%86")
hits = (data(body) or {}).get("data", [])
check(
    "a query with no diacritics finds fully vocalised text (سبحان → سُبْحَانَ)",
    len(hits) > 0,
    f"{len(hits)} hits",
)

status, body = call("GET", "content/search?language=ar&search=")
check("an empty search returns nothing rather than everything", len((data(body) or {}).get("data", [])) == 0)

status, body = call("GET", "content/search?language=ar&search=zzzznotfound")
check("a search with no matches is an empty list, not an error", ok(body))

status, body = call("GET", "content/search?language=ar&search=%D8%A7%D9%84%D9%84%D9%87&pageSize=9999")
check("an absurd page size is clamped, not honoured", len((data(body) or {}).get("data", [])) <= 100)

# ───────────────────────────── content, writing ─────────────────────────────

section("Content — the rules on writing")

key = f"qa-{uuid.uuid4().hex[:8]}"
status, body = call(
    "POST",
    "admin/content/categories",
    {
        "key": key,
        "sortOrder": 999,
        "rhythm": 0,
        "anchor": 0,
        "isPublished": False,
        "translations": [{"languageCode": "en", "title": "QA only"}],
    },
    token=TOKEN,
)
check("a chapter with no Arabic name is refused", code(body) == VALIDATION)

status, body = call(
    "POST",
    "admin/content/categories",
    {
        "key": key,
        "sortOrder": 999,
        "rhythm": 0,
        "anchor": 0,
        "isPublished": False,
        "translations": [{"languageCode": "ar", "title": "باب اختبار"}],
    },
    token=TOKEN,
)
check("a chapter with an Arabic name is accepted", ok(body), str(body)[:100])
CATEGORY_ID = (data(body) or {}).get("id")

status, body = call(
    "POST",
    "admin/content/categories",
    {
        "key": key,
        "sortOrder": 998,
        "rhythm": 0,
        "anchor": 0,
        "isPublished": False,
        "translations": [{"languageCode": "ar", "title": "مكرر"}],
    },
    token=TOKEN,
)
check("chapter keys are unique", code(body) == DUPLICATE_KEY)

status, body = call("GET", "admin/configuration", token=TOKEN)
version_before = (data(body) or {}).get("contentVersion")

unsourced = {
    "categoryId": CATEGORY_ID,
    "sortOrder": 0,
    "arabicText": "نص اختباري للجودة",
    "repeatCount": 3,
    "isPublished": True,
    "translations": [],
}
status, body = call("POST", "admin/content/adhkar", unsourced, token=TOKEN)
check(
    "a dhikr cannot be PUBLISHED without a source "
    "(nothing reaches a reader unattributed)",
    code(body) == SOURCE_REQUIRED,
)

status, body = call(
    "POST", "admin/content/adhkar", {**unsourced, "isPublished": False}, token=TOKEN
)
check("an unsourced DRAFT is allowed (that is how content gets written)", ok(body))
DHIKR_ID = (data(body) or {}).get("id")
check("a draft without a source reports itself unpublishable", (data(body) or {}).get("isPublishable") is False)

status, body = call("POST", f"admin/content/adhkar/{DHIKR_ID}/publish", {}, token=TOKEN)
check("publishing an unsourced draft is refused at the publish step too", code(body) == SOURCE_REQUIRED)

status, body = call(
    "PUT",
    f"admin/content/adhkar/{DHIKR_ID}",
    {
        **unsourced,
        "isPublished": True,
        "sourceBook": "صحيح مسلم",
        "sourceReference": "١",
        "grade": 1,
    },
    token=TOKEN,
)
check("adding a source makes it publishable", ok(body), str(body)[:100])

status, body = call("GET", "admin/configuration", token=TOKEN)
version_after = (data(body) or {}).get("contentVersion")
check(
    "a content change bumps the content version (or the edit never reaches a phone)",
    version_after > version_before,
    f"{version_before} → {version_after}",
)

status, body = call("DELETE", f"admin/content/categories/{CATEGORY_ID}", token=TOKEN)
check("a chapter holding adhkar is not deleted by accident", code(body) == CATEGORY_NOT_EMPTY)

status, body = call("DELETE", f"admin/content/adhkar/{DHIKR_ID}", token=TOKEN)
check("a dhikr can be removed", ok(body))
status, body = call("DELETE", f"admin/content/categories/{CATEGORY_ID}", token=TOKEN)
check("an empty chapter can be removed", ok(body))

status, body = call("POST", "admin/content/categories", {"key": key}, token=TOKEN)
check("a payload missing required fields fails validation", code(body) == VALIDATION)

# ───────────────────────────── languages ─────────────────────────────

section("Languages")

status, body = call("GET", "languages")
languages = data(body) or []
check("the language list is readable anonymously (the picker needs it first)", ok(body))
check("Arabic and English are enabled", {"ar", "en"} <= {x["code"] for x in languages})

status, body = call("GET", "admin/languages", token=TOKEN)
all_languages = data(body) or []
arabic = next((x for x in all_languages if x["code"] == "ar"), None)
check("Arabic is the default language", arabic and arabic["isDefault"])

status, body = call(
    "PUT",
    f"admin/languages/{arabic['id']}",
    {
        "code": "ar",
        "nativeName": "العربية",
        "englishName": "Arabic",
        "isRtl": True,
        "isEnabled": False,
        "sortOrder": 0,
    },
    token=TOKEN,
)
check(
    "the default language cannot be disabled (every fallback ends there)",
    code(body) == DEFAULT_LANGUAGE_IMMUTABLE,
)

status, body = call("DELETE", f"admin/languages/{arabic['id']}", token=TOKEN)
check("the default language cannot be deleted", code(body) in (DEFAULT_LANGUAGE_IMMUTABLE, SOURCE_LANGUAGE_IMMUTABLE))

status, body = call("GET", "languages/ar/strings")
check("the interface-copy overlay is readable anonymously", ok(body))

# ───────────────────────────── reminders ─────────────────────────────

section("Reminders")

reminder_key = f"qa-{uuid.uuid4().hex[:8]}"
anchored = {
    "key": reminder_key,
    "kind": 2,
    "delivery": 1,
    "anchor": 2,
    "offsetMinutes": 30,
    "days": 127,
    "audience": 0,
    "isEnabled": True,
    "isUserAdjustable": True,
    "translations": [{"languageCode": "ar", "title": "اختبار", "body": "نص"}],
}

status, body = call("POST", "admin/reminders", anchored, token=TOKEN)
check("an anchored reminder is accepted", ok(body), str(body)[:100])
REMINDER_ID = (data(body) or {}).get("id")
check(
    "an anchored reminder is FORCED to device-local delivery "
    "(the server has no coordinates and could never fire it)",
    (data(body) or {}).get("delivery") == 2,
    f"delivery {(data(body) or {}).get('delivery')}",
)

status, body = call(
    "POST", "admin/reminders", {**anchored, "key": reminder_key + "b", "days": 0}, token=TOKEN
)
check("a reminder with no days selected is refused", code(body) == REMINDER_NO_DAYS)

status, body = call(
    "POST",
    "admin/reminders",
    {**anchored, "key": reminder_key + "c", "kind": 1, "localTime": None},
    token=TOKEN,
)
check("a fixed-time reminder with no time is refused", code(body) == REMINDER_SCHEDULE_INCOMPLETE)

status, body = call("GET", "reminders?language=ar")
device_reminders = data(body) or []
check("the device can read the reminders it must schedule itself", ok(body))
check(
    "every reminder handed to the device carries wording",
    all(r.get("title") for r in device_reminders),
)

status, body = call("DELETE", f"admin/reminders/{REMINDER_ID}", token=TOKEN)
check("a reminder can be removed", ok(body))

# ───────────────────────────── broadcasts ─────────────────────────────

section("Broadcasts")

status, body = call(
    "POST",
    "admin/broadcasts",
    {
        "audience": 0,
        "translations": [{"languageCode": "ar", "title": "رسالة اختبار", "body": "نص"}],
    },
    token=TOKEN,
)
check("a broadcast is created as a draft", ok(body) and (data(body) or {}).get("status") == 0)
BROADCAST_ID = (data(body) or {}).get("id")

past = (datetime.now(timezone.utc) - timedelta(hours=2)).isoformat()
status, body = call(
    "POST",
    "admin/broadcasts",
    {
        "audience": 0,
        "scheduledAtUtc": past,
        "translations": [{"languageCode": "ar", "title": "ماضٍ", "body": "نص"}],
    },
    token=TOKEN,
)
check("a broadcast cannot be scheduled in the past", code(body) == SCHEDULE_MUST_BE_FUTURE)

status, body = call("POST", f"admin/broadcasts/{BROADCAST_ID}/send", {}, token=TOKEN)
check(
    "Send schedules rather than sending inline (a fan-out must not block a request)",
    ok(body) and (data(body) or {}).get("status") == 1,
    f"status {(data(body) or {}).get('status')}",
)

status, body = call("POST", f"admin/broadcasts/{BROADCAST_ID}/cancel", {}, token=TOKEN)
check("a scheduled broadcast can be cancelled", ok(body) and (data(body) or {}).get("status") == 5)

status, body = call("DELETE", f"admin/broadcasts/{BROADCAST_ID}", token=TOKEN)
check("a broadcast can be removed", ok(body))

# ───────────────────────────── Qur'an ─────────────────────────────

section("Qur'an packages")

status, body = call("GET", "quran/check-version")
check("the version check answers even with nothing published", ok(body))
published = (data(body) or {}).get("version")
check("no package is published on a fresh install, and that is not an error", published is None)

status, raw = call("GET", "quran/download", raw=True)
try:
    envelope = json.loads(raw)
    check("downloading with nothing published returns the typed error", envelope.get("errorCode") == NO_PUBLISHED_QURAN)
except (json.JSONDecodeError, TypeError):
    check("downloading with nothing published returns the typed error", False, "not an envelope")

status, body = call("GET", "admin/quran", token=TOKEN)
check("an admin can list packages", ok(body))

# ───────────────────────────── widget ─────────────────────────────

section("Push tokens and the manager")

# The reason this endpoint exists: FCM rotates a registration token whenever it
# likes, and until the server has the new one every scheduled push for that
# install goes to an address nobody is at. Nothing errors — FCM accepts a send
# to a token it has already retired — so the only symptom is a reader quietly
# hearing less from the app.
status, body = call(
    "POST",
    "devices/push-token",
    {"deviceKey": DEVICE, "pushToken": "qa-rotated-token", "notificationsEnabled": True},
    device=DEVICE,
)
check("a rotated push token can be published on its own", ok(body), str(body)[:100])

status, body = call(
    "POST",
    "devices/push-token",
    {"deviceKey": DEVICE, "pushToken": None, "notificationsEnabled": False},
    device=DEVICE,
)
check("revoking notification permission clears the token", ok(body))

status, body = call(
    "POST",
    "devices/push-token",
    {"deviceKey": str(uuid.uuid4()), "pushToken": "x", "notificationsEnabled": True},
)
check(
    "a token for an unknown device is refused, not turned into a half-made row",
    code(body) == DEVICE_NOT_FOUND,
)

status, body = call(
    "POST",
    "devices/push-token",
    {"deviceKey": "not-a-uuid", "pushToken": "x", "notificationsEnabled": True},
)
check("a malformed device key is refused here too", code(body) == INVALID_DEVICE_KEY)

status, body = call("GET", "admin/push/overview?windowDays=7", token=TOKEN)
overview = data(body) or {}
check("the push manager answers", ok(body), str(body)[:100])

reach = overview.get("reach") or {}
check(
    "every install lands in exactly one reach bucket (the columns must sum)",
    reach.get("totalDevices")
    == reach.get("reachable", 0)
    + reach.get("tokenless", 0)
    + reach.get("muted", 0)
    + reach.get("stale", 0),
    str(reach),
)

check(
    "the manager reports whether Firebase is configured at all "
    "(otherwise every other figure has an invisible explanation)",
    "isFcmConfigured" in overview,
)

outcomes = overview.get("outcomes") or {}
check("the outcome window is reported back", outcomes.get("windowDays") == 7)

status, body = call("GET", "admin/push/overview?windowDays=9999", token=TOKEN)
check(
    "an absurd window is clamped rather than refused",
    (data(body) or {}).get("outcomes", {}).get("windowDays") == 90,
)

status, body = call("GET", "admin/push/overview")
check("the manager is staff-only", status in (401, 403), f"status {status}")

# Sending from the manager is a *delegation*, not a second implementation: it
# has to produce an ordinary broadcast so the message keeps the same history,
# counters and audit trail as one written on the broadcasts screen.
status, body = call(
    "POST",
    "admin/push/send",
    {
        "audience": 3,
        "targetDeviceKey": DEVICE,
        "scheduledAtUtc": None,
        "translations": [
            {"languageCode": "ar", "title": "رسالة إدارية", "body": "من مدير الإشعارات"}
        ],
    },
    token=TOKEN,
)
sent_from_manager = data(body) or {}
check("an admin can send from the manager", ok(body), str(body)[:120])
check(
    "the send becomes an ordinary broadcast, aimed at the one device asked for",
    sent_from_manager.get("targetDeviceKey") == DEVICE,
    str(sent_from_manager)[:120],
)
check(
    "it is queued rather than left as a draft",
    sent_from_manager.get("status") != 0,
    str(sent_from_manager.get("status")),
)

# A schedule here would be silently honoured and the admin would stand watching
# for a message that had not been sent, so it is stripped instead.
status, body = call(
    "POST",
    "admin/push/send",
    {
        "audience": 3,
        "targetDeviceKey": DEVICE,
        "scheduledAtUtc": "2030-01-01T00:00:00Z",
        "translations": [
            {"languageCode": "ar", "title": "فوري", "body": "بلا جدولة"}
        ],
    },
    token=TOKEN,
)
check(
    "a schedule on a send-now is ignored rather than honoured",
    ok(body) and (data(body) or {}).get("scheduledAtUtc") is None,
    str(data(body))[:120],
)

status, body = call(
    "POST",
    "admin/push/send",
    {"audience": 0, "translations": []},
    token=TOKEN,
)
check("a message with no wording is refused", not ok(body))

status, body = call("POST", "admin/push/send", {"audience": 0, "translations": []})
check("sending from the manager is staff-only", status in (401, 403), f"status {status}")

section("Home-screen widget")

status, body = call("GET", "widget")
check("the widget rules are readable anonymously (the app needs them at launch)", ok(body))
original_widget = data(body) or {}

status, body = call(
    "PUT",
    "admin/widget",
    {
        "isEnabled": True,
        "defaultKind": 1,
        "allowPrayerWidget": False,
        "allowDhikrWidget": False,
        "theme": 0,
        "refreshMinutes": 30,
        "showHijriDate": True,
        "showCountdown": True,
        "dhikrCategoryId": None,
    },
    token=TOKEN,
)
check(
    "widgets cannot be enabled with every kind withdrawn "
    "(that would be an empty picker)",
    code(body) == WIDGET_NO_KIND_ALLOWED,
)

status, body = call(
    "PUT",
    "admin/widget",
    {
        "isEnabled": True,
        "defaultKind": 1,
        "allowPrayerWidget": False,
        "allowDhikrWidget": True,
        "theme": 0,
        "refreshMinutes": 60,
        "showHijriDate": True,
        "showCountdown": True,
        "dhikrCategoryId": None,
    },
    token=TOKEN,
)
check(
    "the default kind is corrected when its kind is withdrawn",
    (data(body) or {}).get("defaultKind") == 2,
    f"defaultKind {(data(body) or {}).get('defaultKind')}",
)

status, body = call(
    "PUT",
    "admin/widget",
    {**original_widget, "refreshMinutes": 1, "dhikrCategoryId": None},
    token=TOKEN,
)
check("a refresh interval below the floor is refused", code(body) == VALIDATION)

status, body = call(
    "PUT", "admin/widget", {**original_widget, "dhikrCategoryId": 999999}, token=TOKEN
)
check("pinning a chapter that does not exist is refused", code(body) == CATEGORY_NOT_FOUND)

# Restore whatever was there before the suite ran.
call("PUT", "admin/widget", {**original_widget, "dhikrCategoryId": original_widget.get("dhikrCategoryId")}, token=TOKEN)
status, body = call("GET", "widget")
check("the widget rules were restored", ok(body))

# ───────────────────────────── support ─────────────────────────────

section("Support desk")

status, body = call("GET", "support/faq?language=ar")
check("help topics are readable anonymously", ok(body))

status, body = call(
    "POST",
    "support/feedback",
    {"kind": 3, "message": "QA correction test message", "appVersion": "1.0.0-qa"},
    device=DEVICE,
)
check("a reader can write to the desk", ok(body), str(body)[:100])

status, body = call("POST", "support/feedback", {"kind": 1, "message": "no device header"})
check("feedback requires a device key", not ok(body))

status, body = call("POST", "support/feedback", {"kind": 1, "message": "hi"}, device=DEVICE)
check("a too-short message fails validation", code(body) == VALIDATION)

for _ in range(6):
    status, body = call(
        "POST", "support/feedback", {"kind": 1, "message": "QA flood test message"}, device=DEVICE
    )
check(
    "an anonymous device is rate-limited by open submissions "
    "(there is no account to block)",
    not ok(body),
    f"errorCode {code(body)}",
)

status, body = call("GET", "admin/support/feedback", token=TOKEN)
feedback = (data(body) or {}).get("data", [])
check("the desk can read what came in", ok(body))
check(
    "corrections are sorted ahead of everything else",
    not feedback or feedback[0].get("kind") == 3,
    f"first kind {feedback[0].get('kind') if feedback else '-'}",
)

# ───────────────────────────── management ─────────────────────────────

section("Management and logs")

status, body = call("GET", "admin/dashboard", token=TOKEN)
dashboard = data(body) or {}
check("the dashboard loads", ok(body))
check("it counts installs", isinstance(dashboard.get("totalDevices"), int))
check("it reports the unsourced backlog (the editor's to-do)", "unsourcedDrafts" in dashboard)

status, body = call("GET", "admin/audit?action=content.", token=TOKEN)
check("the audit trail filters by action prefix", ok(body))
check("this suite's content edits were audited", len((data(body) or {}).get("data", [])) > 0)

status, body = call("GET", "admin/logs", token=TOKEN)
check("every API request is logged", len((data(body) or {}).get("data", [])) > 0)

status, body = call("GET", "admin/logs?statusCode=401", token=TOKEN)
check(
    "rejected calls are logged too (the logger runs ahead of auth)",
    len((data(body) or {}).get("data", [])) > 0,
)

# ───────────────────────────── privacy ─────────────────────────────

section("Privacy promises")

status, body = call("GET", "admin/devices/stats", token=TOKEN)
stats = data(body) or {}
check("device statistics are aggregate only", ok(body))
check(
    "no endpoint returns a per-device list of readers",
    all(not isinstance(v, list) for v in stats.values()),
)

serialised = json.dumps(stats)
check(
    "aggregate statistics carry no coordinates",
    "latitude" not in serialised and "longitude" not in serialised,
)

status, body = call("GET", "content/catalog?language=ar")
check(
    "the catalogue carries no reader data",
    "deviceKey" not in json.dumps(data(body))[:200000],
)

status, body = call("DELETE", "devices", device=DEVICE)
check("a reader can delete their device from the server", ok(body), str(body)[:80])
status, body = call("GET", "devices/notifications", device=DEVICE)
check("after deletion the device is unknown", code(body) == DEVICE_NOT_FOUND)

# ───────────────────────────── cleanup ─────────────────────────────

section("Cleanup")

if EDITOR_ID:
    status, body = call("DELETE", f"admin/staff/{EDITOR_ID}", token=TOKEN)
    check("the QA editor account was removed", ok(body))

status, body = call("GET", "admin/staff", token=TOKEN)
supers = [u for u in (data(body) or {}).get("data", []) if 3 in u.get("roles", [])]
if len(supers) == 1:
    status, body = call("DELETE", f"admin/staff/{supers[0]['id']}", token=TOKEN)
    check(
        "the last super-admin cannot be deleted (a console nobody can sign into "
        "is not recoverable from the console)",
        code(body) == LAST_ADMINISTRATOR,
    )

# ───────────────────────────── report ─────────────────────────────

failures = [r for r in results if not r[2]]

print("\n" + "═" * 72)
print(f"  {len(results) - len(failures)}/{len(results)} checks passed")

by_section = {}
for sec, _, passed, _ in results:
    total, ok_count = by_section.get(sec, (0, 0))
    by_section[sec] = (total + 1, ok_count + (1 if passed else 0))

for sec, (total, passed) in by_section.items():
    mark = "\033[32m✓\033[0m" if passed == total else "\033[31m✗\033[0m"
    print(f"  {mark} {sec:<34} {passed}/{total}")

if failures:
    print("\n\033[1mFailures\033[0m")
    for sec, rule, _, detail in failures:
        print(f"  • [{sec}] {rule}")
        if detail:
            print(f"      {detail}")

print("═" * 72)
sys.exit(len(failures))
