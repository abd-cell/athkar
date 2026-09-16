#!/usr/bin/env python3
"""Build a mushaf package (`docs/BUSINESS_LOGIC.md` §7) from the quran.ai MCP pull.

`tools/quran-package/build_quran_package.py` builds the same file from Tanzil
plus QUL's layout preview. This builds it from one source instead: everything
here comes from `pull.py`, which read it from the MCP server, which reads it
from quran.com. One source means the two spellings problem does not arise —
there is no second corpus to reconcile a word count against.

What it writes (the schema `app/athkar_app/lib/core/quran_library.dart` and
`core/mushaf.dart` read, and nothing else):

    surahs(id, name_ar, name_en, ayah_count, revelation_place)
    ayahs(id, surah_id, ayah_number, text_uthmani, page, juz)
    waqf_marks(id, surah_id, ayah_number, word_index, symbol)
    waqf_types(symbol, name_ar, ruling_ar)
    words(id, surah_id, ayah_number, position, text, page, line)
    lines(id, page, line_number, kind, is_centered, first_word_id, last_word_id, surah_id)
    fonts(id, page, family, data)

Three ways to draw the page, and they differ by an order of magnitude:

  --script text   The words as ordinary Unicode, drawn in whatever face the app
                  has. Exact line breaks, ~6 MB, no download. The default.
  --script qcf    The King Fahd Complex page fonts, one per page, in which a
                  glyph *is* a word as drawn on that page. Letter-perfect, and
                  ~60 MB. The MCP already serves the glyph codes for every word
                  (`glyph_text`); only the outlines come from elsewhere, because
                  there is nowhere in the MCP's API to get them.
  --font FILE     One font for the whole book (DigitalKhatt), supplied by you.

The QCF layout the MCP serves is the V2 (1421H) print — verified, not assumed:
page 1's 36 glyph codes land on exactly the 36 glyphs in that print's page-1
font. Fetching another variant's outlines would draw page 42's words in another
print's shapes.

One honest gap: **`is_centered` is inferred, not known.** It is a fact of the
print that no API here reports. Surah headings and basmalah lines are centred;
ayah lines are left justified, which is right everywhere except the short line
that ends a surah.

Usage:

    python tools/quran-mcp/build_package.py --out mushaf-mcp-v1.db
    python tools/quran-mcp/build_package.py --out m.db --script qcf --fetch-fonts
    python tools/quran-mcp/build_package.py --out m.db --no-pages   # verses only
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import sqlite3
import sys
from collections import defaultdict
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

HERE = Path(__file__).resolve().parent
DATA = HERE / "data"

TOTAL_AYAHS = 6236
TOTAL_SURAHS = 114
TOTAL_PAGES = 604
TOTAL_JUZ = 30

# The waqf glyphs of the Madinah mushaf, with their rulings. Reused verbatim from
# tools/quran-package/waqf_types.json so the two builders cannot disagree about
# what a mark means.
WAQF_TYPES_PATH = HERE.parent / "quran-package" / "waqf_types.json"

SCHEMA = """
CREATE TABLE surahs (
  id INTEGER PRIMARY KEY,
  name_ar TEXT NOT NULL,
  name_en TEXT NOT NULL,
  ayah_count INTEGER NOT NULL,
  revelation_place TEXT NOT NULL
);

CREATE TABLE ayahs (
  id INTEGER PRIMARY KEY,
  surah_id INTEGER NOT NULL,
  ayah_number INTEGER NOT NULL,
  text_uthmani TEXT NOT NULL,
  page INTEGER NOT NULL,
  juz INTEGER NOT NULL
);
CREATE INDEX ix_ayahs_surah ON ayahs (surah_id, ayah_number);
CREATE INDEX ix_ayahs_page ON ayahs (page);

CREATE TABLE waqf_types (
  symbol TEXT PRIMARY KEY,
  name_ar TEXT NOT NULL,
  ruling_ar TEXT NOT NULL
);

CREATE TABLE waqf_marks (
  id INTEGER PRIMARY KEY,
  surah_id INTEGER NOT NULL,
  ayah_number INTEGER NOT NULL,
  word_index INTEGER NOT NULL,
  symbol TEXT NOT NULL
);
CREATE INDEX ix_waqf_marks_ayah ON waqf_marks (surah_id, ayah_number);
"""

PAGE_SCHEMA = """
CREATE TABLE words (
  id INTEGER PRIMARY KEY,
  surah_id INTEGER NOT NULL,
  ayah_number INTEGER NOT NULL,
  position INTEGER NOT NULL,
  text TEXT NOT NULL,
  page INTEGER NOT NULL,
  line INTEGER NOT NULL
);
CREATE INDEX ix_words_page ON words (page);

CREATE TABLE lines (
  id INTEGER PRIMARY KEY,
  page INTEGER NOT NULL,
  line_number INTEGER NOT NULL,
  kind TEXT NOT NULL,
  is_centered INTEGER NOT NULL,
  first_word_id INTEGER,
  last_word_id INTEGER,
  surah_id INTEGER
);
CREATE INDEX ix_lines_page ON lines (page);

CREATE TABLE fonts (
  id INTEGER PRIMARY KEY,
  page INTEGER,
  family TEXT NOT NULL,
  data BLOB NOT NULL
);
"""

LINES_PER_PAGE = 15


def die(message: str) -> None:
    """Refuse rather than write something subtly wrong.

    The app is defensive by design — a package missing a table shows an empty
    index rather than crashing — so a broken package is silent on a phone. It
    must not be silent here.
    """
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def read(name: str):
    path = DATA / name
    if not path.exists():
        die(f"{path} is missing. Run: python {HERE / 'pull.py'}")
    with path.open(encoding="utf-8") as f:
        return json.load(f)


# ── surahs and ayahs ───────────────────────────────────────────────────────

def build_surahs(db, surahs) -> None:
    if len(surahs) != TOTAL_SURAHS:
        die(f"expected {TOTAL_SURAHS} surahs, the pull has {len(surahs)}")

    db.executemany(
        "INSERT INTO surahs (id, name_ar, name_en, ayah_count, revelation_place) "
        "VALUES (?, ?, ?, ?, ?)",
        [(s["number"], s["name_arabic"], s["name_simple"],
          s["verses_count"], s["revelation_place"]) for s in surahs],
    )


def juz_of_ayah(juz_rows) -> dict[str, int]:
    """Every ayah key mapped to its juz, by walking the 30 boundaries.

    The boundaries are inclusive verse keys, so this expands them rather than
    comparing — a comparison would need surah/ayah ordering logic that the
    global ayah id already gives for free.
    """
    order: dict[str, int] = {}
    for row in juz_rows:
        order[row["start"]] = row["juz"]
    return order


def global_ids(surahs) -> tuple[dict[str, int], list[str]]:
    """«2:255» → 1..6236, in mushaf order. The ids the page data already uses."""
    keys, by_key = [], {}
    for surah in surahs:
        for ayah in range(1, surah["verses_count"] + 1):
            key = f"{surah['number']}:{ayah}"
            by_key[key] = len(keys) + 1
            keys.append(key)
    return by_key, keys


def build_ayahs(db, surahs, text, pages_of_ayah, juz_rows) -> None:
    by_key, keys = global_ids(surahs)
    if len(keys) != TOTAL_AYAHS:
        die(f"the surah table sums to {len(keys)} ayat, not {TOTAL_AYAHS}")

    starts = juz_of_ayah(juz_rows)
    if len(starts) != TOTAL_JUZ:
        die(f"expected {TOTAL_JUZ} juz boundaries, got {len(starts)}")

    rows, juz = [], 1
    for key in keys:
        juz = starts.get(key, juz)
        surah, ayah = key.split(":")

        body = text.get(key)
        if not body:
            die(f"{key}: no text in the pull — re-run pull.py, it paginates silently")

        page = pages_of_ayah.get(key)
        if page is None:
            die(f"{key}: no page. Run the mushaf step of pull.py, or pass --no-pages")

        rows.append((by_key[key], int(surah), int(ayah), body, page, juz))

    db.executemany(
        "INSERT INTO ayahs (id, surah_id, ayah_number, text_uthmani, page, juz) "
        "VALUES (?, ?, ?, ?, ?, ?)",
        rows,
    )


# ── waqf ───────────────────────────────────────────────────────────────────

def build_waqf(db, text) -> int:
    """The pause marks, read off the text itself.

    `word_index` counts the words *before* the mark, so a mark on the second
    word is 1 and one opening a verse is 0 — the same contract
    `tools/quran-package` writes and `dhikr`-side code reads.
    """
    if not WAQF_TYPES_PATH.exists():
        print(f"  no {WAQF_TYPES_PATH.name}; skipping waqf marks")
        return 0

    with WAQF_TYPES_PATH.open(encoding="utf-8") as f:
        types = json.load(f)

    symbols = {t["symbol"]: t for t in types} if isinstance(types, list) else types
    db.executemany(
        "INSERT INTO waqf_types (symbol, name_ar, ruling_ar) VALUES (?, ?, ?)",
        [(s, t["name_ar"], t["ruling_ar"]) for s, t in symbols.items()],
    )

    marks = []
    for key, body in text.items():
        surah, ayah = (int(part) for part in key.split(":"))
        for index, word in enumerate(body.split()):
            for symbol in symbols:
                if symbol in word:
                    marks.append((len(marks) + 1, surah, ayah, index, symbol))

    db.executemany(
        "INSERT INTO waqf_marks (id, surah_id, ayah_number, word_index, symbol) "
        "VALUES (?, ?, ?, ?, ?)",
        marks,
    )
    return len(marks)


# ── the printed page ───────────────────────────────────────────────────────

QCF_FONT_URL = "https://static-cdn.tarteel.ai/qul/fonts/quran_fonts/v2/woff2/p{page}.woff2"


def fetch_qcf_fonts(cache: Path) -> None:
    """Download the 604 QCF page fonts, once, into a cache directory.

    These are the only bytes in this build that do not come from the MCP: it
    serves the glyph *codes* for every word but not the outlines to draw them
    with. The V2 (1421H) print is not a guess — page 1's 36 codes land on
    exactly the 36 glyphs in that print's page-1 font.
    """
    import urllib.request

    cache.mkdir(parents=True, exist_ok=True)
    todo = [p for p in range(1, TOTAL_PAGES + 1) if not (cache / f"p{p}.woff2").exists()]
    if not todo:
        return

    print(f"  fetching {len(todo)} page fonts ...", flush=True)

    def grab(page: int) -> None:
        target = cache / f"p{page}.woff2"
        # The CDN answers 403 to urllib's default User-Agent.
        request = urllib.request.Request(
            QCF_FONT_URL.format(page=page),
            headers={"User-Agent": "athkari-mushaf-build/1 (+tools/quran-mcp)"},
        )
        with urllib.request.urlopen(request, timeout=60) as r:
            body = r.read()
        if len(body) < 1024:
            die(f"page {page}: the font came back as {len(body)} bytes, which is not a font")
        target.write_bytes(body)

    with ThreadPoolExecutor(max_workers=8) as pool:
        for index, _ in enumerate(pool.map(grab, todo), 1):
            if index % 100 == 0:
                print(f"    {index}/{len(todo)}", flush=True)


def build_qcf_fonts(db, cache: Path) -> int:
    """Convert each woff2 to a TTF and store it against its page.

    Flutter's FontLoader takes TTF and OTF and not woff2, so the compression
    has to come off here rather than on the phone.
    """
    from fontTools.ttLib import TTFont

    total = 0
    for page in range(1, TOTAL_PAGES + 1):
        source = cache / f"p{page}.woff2"
        if not source.exists():
            die(f"page {page}: {source} is missing — run with --fetch-fonts")

        font = TTFont(source)
        font.flavor = None          # woff2 -> plain TTF
        buffer = io.BytesIO()
        font.save(buffer)
        body = buffer.getvalue()

        db.execute(
            "INSERT INTO fonts (id, page, family, data) VALUES (?, ?, ?, ?)",
            (page, page, f"QCF_P{page:03d}", body),
        )
        total += len(body)

        if page % 100 == 0:
            print(f"    converted {page}/{TOTAL_PAGES}", flush=True)

    return total


def build_pages(db, mushaf, surahs, script: str):
    """`words` and `lines`, and the ayah text rebuilt from the page words.

    The page words and the verse editions spell the same verse differently —
    «بِسۡمِ» on the page against «بِسْمِ» in the text edition — because the page data
    is written for the QCF fonts. Carrying both would put two spellings of one
    verse in a single package, so the page text wins wherever it exists and the
    ayah text is rewritten from it.
    """
    by_key, _ = global_ids(surahs)
    key_of_id = {v: k for k, v in by_key.items()}

    word_rows, line_rows = [], []
    text_of_ayah: dict[str, list[str]] = defaultdict(list)
    page_of_ayah: dict[str, int] = {}
    word_id = 0

    for number in range(1, TOTAL_PAGES + 1):
        page = mushaf.get(str(number))
        if page is None:
            die(f"page {number} is missing from the pull")

        headers = page.get("surah_headers") or []
        with_words = {line["line_number"]: line for line in page["lines"]}

        # Where the ornamental heading and the basmalah sit. The MCP reports
        # only lines that carry words, and says a heading "appears before line
        # N" — so the heading takes the row above N, and the basmalah, when the
        # surah has one, the row between them.
        special: dict[int, tuple[str, int]] = {}
        for header in headers:
            before = header.get("appears_before_line") or 1
            chapter = header["chapter_id"]
            if header.get("bismillah_pre"):
                if before - 2 >= 1:
                    special[before - 2] = ("surah_name", chapter)
                if before - 1 >= 1:
                    special[before - 1] = ("basmallah", chapter)
            elif before - 1 >= 1:
                special[before - 1] = ("surah_name", chapter)

        for row in range(1, LINES_PER_PAGE + 1):
            line = with_words.get(row)

            if line is None:
                kind, chapter = special.get(row, ("blank", None))
                line_rows.append((len(line_rows) + 1, number, row, kind,
                                  1 if kind != "blank" else 0, None, None, chapter))
                continue

            first = word_id + 1
            surah_on_line = None

            for word in line["words"]:
                key = key_of_id.get(word["verse_id"])
                if key is None:
                    die(f"page {number}: word points at verse {word['verse_id']}, which is not an ayah")

                surah, ayah = (int(part) for part in key.split(":"))
                surah_on_line = surah_on_line or surah
                page_of_ayah.setdefault(key, number)

                # In the QCF print a glyph *is* a word as drawn on this page, so
                # the column holds the code and the page's own font draws it.
                # The ayah text below always keeps the real Unicode: a reader
                # copying a verse must get letters, not private-use codepoints.
                drawn = word["glyph_text"] if script == "qcf" else word["text"]
                if not drawn:
                    die(f"page {number}: word {word['word_id']} has no {script} text")

                word_id += 1
                word_rows.append((word_id, surah, ayah, word["position_in_verse"],
                                  drawn, number, row))

                # The end-of-verse marker is a word on the page and not a word of
                # the verse, so it is drawn but never joins the ayah text.
                if word["char_type_name"] == "word":
                    text_of_ayah[key].append(word["text"])

            line_rows.append((len(line_rows) + 1, number, row, "ayah", 0,
                              first, word_id, surah_on_line))

    db.executemany(
        "INSERT INTO words (id, surah_id, ayah_number, position, text, page, line) "
        "VALUES (?, ?, ?, ?, ?, ?, ?)",
        word_rows,
    )
    db.executemany(
        "INSERT INTO lines (id, page, line_number, kind, is_centered, "
        "first_word_id, last_word_id, surah_id) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
        line_rows,
    )

    return {key: " ".join(parts) for key, parts in text_of_ayah.items()}, page_of_ayah, len(word_rows)


def build_font(db, path: Path | None) -> None:
    if path is None:
        return
    if not path.exists():
        die(f"{path} does not exist")

    db.execute(
        "INSERT INTO fonts (id, page, family, data) VALUES (1, NULL, ?, ?)",
        (path.stem, path.read_bytes()),
    )


# ── entry point ────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", required=True, type=Path, help="the .db to write")
    parser.add_argument("--no-pages", action="store_true",
                        help="verses only, no printed-page layer")
    parser.add_argument("--script", choices=("text", "qcf"), default="text",
                        help="how the page words are drawn (default: text)")
    parser.add_argument("--fetch-fonts", action="store_true",
                        help="download the QCF page fonts into --font-cache")
    parser.add_argument("--font-cache", type=Path, default=HERE / "data" / "fonts",
                        help="where the downloaded page fonts are kept")
    parser.add_argument("--font", type=Path,
                        help="one font for the whole book (e.g. DigitalKhattV2.otf)")
    args = parser.parse_args()

    if args.script == "qcf" and args.no_pages:
        die("--script qcf needs the page layer; drop --no-pages")
    if args.script == "qcf" and args.font:
        die("--script qcf carries one font per page; --font is for a single face")

    if args.out.exists():
        args.out.unlink()

    surahs = read("surahs.json")
    juz_rows = read("juz.json")
    text = read("quran/ar-uthmani-minimal.json")

    db = sqlite3.connect(args.out)
    db.executescript(SCHEMA)

    print("surahs ...", flush=True)
    build_surahs(db, surahs)

    page_of_ayah: dict[str, int] = {}

    if not args.no_pages:
        db.executescript(PAGE_SCHEMA)
        print("printed page ...", flush=True)
        page_text, page_of_ayah, words = build_pages(
            db, read("mushaf/pages.json"), surahs, args.script)
        print(f"  {words} words on {TOTAL_PAGES} pages")

        missing = set(text) - set(page_text)
        if missing:
            die(f"{len(missing)} ayat have no page words (e.g. {sorted(missing)[:3]})")
        text = page_text

        if args.script == "qcf":
            print("page fonts ...", flush=True)
            if args.fetch_fonts:
                fetch_qcf_fonts(args.font_cache)
            total = build_qcf_fonts(db, args.font_cache)
            print(f"  {TOTAL_PAGES} fonts, {total / (1024 * 1024):.1f} MB of outlines")
        else:
            build_font(db, args.font)
            if args.font is None:
                print("  no font: the app draws these line breaks in its own face")
    else:
        # Without the page layer the ayah still needs a page number, and the
        # surah metadata carries the range each one spans. Good enough for a
        # verse reader, which is all a --no-pages package is.
        for surah in surahs:
            for ayah in range(1, surah["verses_count"] + 1):
                page_of_ayah[f"{surah['number']}:{ayah}"] = surah["page"]["start"]

    print("ayahs ...", flush=True)
    build_ayahs(db, surahs, text, page_of_ayah, juz_rows)

    print("waqf ...", flush=True)
    marks = build_waqf(db, text)
    print(f"  {marks} marks")

    db.commit()
    db.execute("VACUUM")
    db.close()

    digest = hashlib.sha256(args.out.read_bytes()).hexdigest()
    size = args.out.stat().st_size

    print()
    print(f"wrote {args.out}")
    print(f"  {size / (1024 * 1024):.1f} MB")
    print(f"  sha256 {digest}")
    print()
    print("Upload it in the CMS (Qur'an screen), choosing script = Uthmani and")
    print(f"  waqf annotations = {'yes' if marks else 'no'}, then publish.")
    print("The server hashes what it writes — compare the digest above.")


if __name__ == "__main__":
    main()
