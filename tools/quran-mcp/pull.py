"""Pull canonical Qur'an data from the quran.ai MCP server into tools/quran-mcp/data/.

Everything here is fetched from the server, never from model memory — that is the
whole point of the server (see data/grounding_rules.md).

    python tools/quran-mcp/pull.py [step ...]

Steps: meta, juz, editions, quran, translation, mushaf, athkar, docs  (default: all)
"""
import json, os, sys, time
from concurrent.futures import ThreadPoolExecutor
from mcp_client import C

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
NONCE = None

# The Qur'anic adhkar: verses the sunna names as a dhikr in its own right.
# refs only — every text below is fetched, nothing is typed from memory.
ATHKAR = [
    ("fatiha",        "1:1-7",      "الفاتحة"),
    ("baqarah-opening","2:1-5",     "أوائل البقرة"),
    ("ayat-al-kursi", "2:255",      "آية الكرسي"),
    ("baqarah-closing","2:285-286", "خواتيم البقرة"),
    ("imran-tafakkur","3:190-200",  "آيات التفكر"),
    ("kahf-opening",  "18:1-10",    "أوائل الكهف"),
    ("kahf-closing",  "18:107-110", "خواتيم الكهف"),
    ("hashr-closing", "59:22-24",   "خواتيم الحشر"),
    ("sajdah",        "32:1-30",    "السجدة"),
    ("mulk",          "67:1-30",    "الملك"),
    ("kafirun",       "109:1-6",    "الكافرون"),
    ("ikhlas",        "112:1-4",    "الإخلاص"),
    ("falaq",         "113:1-5",    "الفلق"),
    ("nas",           "114:1-6",    "الناس"),
]

DISPLAY_ED = "ar-uthmani-minimal"   # true Uthmani orthography (with ٱ)
SEARCH_ED  = "ar-uthmani"           # undiacritised — matches ArabicText folding
EN_ED      = "en-sahih-international"


def client():
    c = C(); c.init(); return c


def save(name, obj):
    p = os.path.join(DATA, name)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1)
    print(f"  -> {name} ({os.path.getsize(p)//1024} KB)", flush=True)


def step_docs(c):
    for tool, name in [("fetch_grounding_rules", "grounding_rules.md"),
                       ("fetch_skill_guide", "skill_guide.md")]:
        txt = c.call(tool, {})
        with open(os.path.join(DATA, name), "w", encoding="utf-8") as f:
            f.write(txt if isinstance(txt, str) else json.dumps(txt, ensure_ascii=False))
        print(f"  -> {name}", flush=True)


def step_editions(c):
    out = {}
    for t in ("quran", "translation", "tafsir"):
        out[t] = c.call_struct("list_editions", {"edition_type": t, "grounding_nonce": NONCE})["editions"]
    save("editions.json", out)


def step_meta(c):
    surahs = []
    for n in range(1, 115):
        m = c.call_struct("fetch_quran_metadata", {"surah": n})
        s = m["surah"][0]
        s["juz"] = m["juz"]; s["page"] = m["page"]; s["sajdah"] = m["sajdah"]
        surahs.append(s)
        if n % 20 == 0: print(f"    meta {n}/114", flush=True)
    save("surahs.json", surahs)
    return surahs


def step_juz(c):
    """The 30 juz boundaries.

    `ayahs.juz` is one column in the package and there is no per-ayah call that
    would fill it cheaply — asking the metadata tool 6236 times to learn 30
    facts. So ask it 30 times for the boundaries and interpolate."""
    out = []
    for n in range(1, 31):
        m = c.call_struct("fetch_quran_metadata", {"juz": n})
        out.append({"juz": n,
                    "start": m["ayah"]["start_verse_key"],
                    "end": m["ayah"]["end_verse_key"]})
    save("juz.json", out)


def _ayah_ranges(surahs):
    return [f"{s['number']}:1-{s['verses_count']}" for s in surahs]


def _fetch_edition(tool, edition, ranges):
    """Fetch one edition over the given ayah ranges, following pagination.

    Long surahs come back truncated with a `continuation` token; without
    following it you silently lose ayat (al-Baqarah stops well before 2:255)."""
    out = {}
    def work(rng):
        c = client()
        rows, args = [], {"ayahs": rng, "editions": [edition], "grounding_nonce": NONCE}
        while True:
            r = c.call_struct(tool, args)
            rows += r["results"].get(edition, [])
            cont = (r.get("pagination") or {}).get("continuation")
            if not cont:
                return rows
            args = {"continuation": cont, "grounding_nonce": NONCE}
    with ThreadPoolExecutor(max_workers=6) as ex:
        for rows in ex.map(work, ranges):
            for row in rows:
                out[row["ayah"]] = row["text"]
    return out


def step_quran(surahs):
    ranges = _ayah_ranges(surahs)
    for ed in (DISPLAY_ED, SEARCH_ED):
        print(f"  fetching {ed} ...", flush=True)
        save(f"quran/{ed}.json", _fetch_edition("fetch_quran", ed, ranges))


def step_translation(surahs):
    print(f"  fetching {EN_ED} ...", flush=True)
    save(f"translations/{EN_ED}.json", _fetch_edition("fetch_translation", EN_ED, _ayah_ranges(surahs)))


def step_mushaf():
    pages = {}
    def fetch(p):
        c = client()
        return p, c.call_struct("fetch_mushaf", {"page": p, "grounding_nonce": NONCE})
    with ThreadPoolExecutor(max_workers=6) as ex:
        for i, (p, d) in enumerate(ex.map(fetch, range(1, 605)), 1):
            pages[str(p)] = d
            if i % 100 == 0: print(f"    mushaf {i}/604", flush=True)
    save("mushaf/pages.json", pages)


def step_athkar(c):
    out = []
    for key, ref, name_ar in ATHKAR:
        ar = c.call_struct("fetch_quran", {"ayahs": ref, "editions": [DISPLAY_ED, SEARCH_ED],
                                           "grounding_nonce": NONCE})
        en = c.call_struct("fetch_translation", {"ayahs": ref, "editions": [EN_ED],
                                                 "grounding_nonce": NONCE})
        disp = {r["ayah"]: r["text"] for r in ar["results"][DISPLAY_ED]}
        srch = {r["ayah"]: r["text"] for r in ar["results"][SEARCH_ED]}
        tr   = {r["ayah"]: r["text"] for r in en["results"][EN_ED]}
        out.append({
            "key": key, "name_ar": name_ar, "reference": ref,
            "source": {"server": "quran.ai MCP", "upstream": "quran.com",
                       "editions": [DISPLAY_ED, SEARCH_ED, EN_ED]},
            "ayahs": [{"ayah": k, "text": disp[k], "search_text": srch.get(k), "en": tr.get(k)}
                      for k in ar["ayahs"]],
        })
        print(f"    {key} ({ref}) {len(out[-1]['ayahs'])} ayat", flush=True)
    save("athkar_quranic.json", out)


def main():
    global NONCE
    os.makedirs(DATA, exist_ok=True)
    steps = sys.argv[1:] or ["docs", "editions", "meta", "juz", "quran", "translation", "mushaf", "athkar"]
    c = client()
    rules = c.call("fetch_grounding_rules", {})
    NONCE = rules.split("GROUNDING_NONCE:")[1].split()[0].strip()
    print("grounding nonce:", NONCE, flush=True)

    surahs = None
    def need_surahs():
        nonlocal surahs
        if surahs is None:
            p = os.path.join(DATA, "surahs.json")
            surahs = json.load(open(p, encoding="utf-8")) if os.path.exists(p) else step_meta(c)
        return surahs

    for s in steps:
        t0 = time.time(); print(f"[{s}]", flush=True)
        if s == "docs": step_docs(c)
        elif s == "editions": step_editions(c)
        elif s == "meta": surahs = step_meta(c)
        elif s == "juz": step_juz(c)
        elif s == "quran": step_quran(need_surahs())
        elif s == "translation": step_translation(need_surahs())
        elif s == "mushaf": step_mushaf()
        elif s == "athkar": step_athkar(c)
        else: print("  unknown step"); continue
        print(f"[{s}] done in {time.time()-t0:.1f}s", flush=True)


if __name__ == "__main__":
    main()
