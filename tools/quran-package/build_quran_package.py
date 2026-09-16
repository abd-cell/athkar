#!/usr/bin/env python3
"""Build the prepared mushaf SQLite file the CMS uploads and the app downloads.

The schema this writes is the contract documented in `docs/BUSINESS_LOGIC.md`
7.2/7.3 and read by `app/athkar_app/lib/core/quran_library.dart`. Nothing here
invents text: it takes a verified corpus (Tanzil / King Fahd Complex) plus
Tanzil's metadata and joins them.

The app is deliberately defensive and shows "nothing here" rather than crashing
on a bad package, which is exactly why this script is not: every structural
check below aborts the build. A mushaf that is wrong must fail on this machine,
not on a reader's phone.

Usage:

    python build_quran_package.py \
        --text quran-uthmani.txt \
        --metadata quran-data.xml \
        --out mushaf-uthmani-v1.db

See README.md for where those two inputs come from.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sqlite3
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

TOTAL_AYAHS = 6236
TOTAL_SURAHS = 114
TOTAL_PAGES = 604
TOTAL_JUZS = 30

# The six waqf glyphs of the Madinah mushaf. They are characters inside the
# verse text -- the app renders them as part of the string and adds nothing.
WAQF_SYMBOLS = "ۖۗۘۙۚۛ"

# Combining marks are allowed between the letters: suras 95 and 97 spell the
# basmalah with a shadda on the ba, and a regex that assumes one kasra each
# would leave those two with a doubled basmalah under --strip-basmalah.
HARAKAT = "[ؐ-ًؚ-ٰٟۖ-ۭ]*"
BASMALAH_RE = re.compile(r"^\s*" + HARAKAT.join(["ب", "س", "م"]) + HARAKAT + r"\s")


def fail(message):
    print("error: " + message, file=sys.stderr)
    raise SystemExit(1)


# --------------------------------------------------------------------------
# inputs
# --------------------------------------------------------------------------

def read_verses(path):
    """Read the corpus, in whichever of Tanzil's two export shapes it came."""
    if path.suffix.lower() == ".sql":
        return read_sql(path)
    return read_text(path)


def read_sql(path):
    """Tanzil's SQL dump: INSERT ... VALUES (id, sura, aya, 'text'), ...

    Only the tuples are read -- the CREATE TABLE around them describes a MySQL
    schema that has nothing to do with the one this script writes. In each tuple
    the last quoted string is the verse and the two integers before it are its
    sura and aya, which holds for both the 3- and 4-column dumps Tanzil ships.
    """
    body = path.read_text(encoding="utf-8-sig")
    verses = {}

    for values in re.finditer(r"\bVALUES\b(.*?);", body, re.IGNORECASE | re.DOTALL):
        for tuple_text in split_tuples(values.group(1)):
            fields = parse_fields(tuple_text)
            strings = [f for f in fields if isinstance(f, str)]
            numbers = [f for f in fields if isinstance(f, int)]
            if not strings or len(numbers) < 2:
                continue
            surah, ayah = numbers[-2], numbers[-1]
            text = strings[-1].strip()
            if not text:
                fail(f"{path}: empty verse {surah}:{ayah}")
            if (surah, ayah) in verses:
                fail(f"{path}: duplicate verse {surah}:{ayah}")
            verses[(surah, ayah)] = text

    if not verses:
        fail(f"{path}: no INSERT ... VALUES tuples found -- is this a Tanzil dump?")
    return verses


def split_tuples(text):
    """Yield the `(...)` groups of a VALUES clause, respecting quoted strings."""
    depth, start, quote, escaped = 0, None, None, False
    for index, character in enumerate(text):
        if quote:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == quote:
                quote = None
            continue
        if character in "'\"":
            quote = character
        elif character == "(":
            if depth == 0:
                start = index + 1
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0 and start is not None:
                yield text[start:index]
                start = None


def parse_fields(tuple_text):
    """Split one tuple into ints and strings, unescaping MySQL's backslashes."""
    fields, buffer, quote, escaped = [], [], None, False

    def flush():
        raw = "".join(buffer).strip()
        buffer.clear()
        if not raw or raw.upper() == "NULL":
            return
        try:
            fields.append(int(raw))
        except ValueError:
            pass  # a bare word (a column name, a function) -- not data we want

    for character in tuple_text:
        if quote:
            if escaped:
                buffer.append({"n": "\n", "r": "\r", "t": "\t"}.get(character, character))
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == quote:
                fields.append("".join(buffer))
                buffer.clear()
                quote = None
            else:
                buffer.append(character)
            continue
        if character in "'\"":
            buffer.clear()
            quote = character
        elif character == ",":
            flush()
        else:
            buffer.append(character)
    flush()
    return fields


def read_text(path):
    """Tanzil's plain-text format: sura|aya|text, with '#' comment lines."""
    verses = {}
    with path.open(encoding="utf-8-sig") as handle:
        for number, raw in enumerate(handle, start=1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split("|", 2)
            if len(parts) != 3:
                fail(f"{path}:{number}: expected 'sura|aya|text', got {line[:40]!r}")
            try:
                surah, ayah = int(parts[0]), int(parts[1])
            except ValueError:
                fail(f"{path}:{number}: sura and aya must be numbers")
            text = parts[2].strip()
            if not text:
                fail(f"{path}:{number}: empty verse {surah}:{ayah}")
            if (surah, ayah) in verses:
                fail(f"{path}:{number}: duplicate verse {surah}:{ayah}")
            verses[(surah, ayah)] = text
    if not verses:
        fail(f"{path}: no verses found")
    return verses


def read_metadata(path):
    """Tanzil's quran-data.xml: sura index, juz starts and page starts."""
    root = ET.parse(path).getroot()

    surahs = []
    for element in root.findall("./suras/sura"):
        place = (element.get("type") or "").lower()
        surahs.append(
            {
                "id": int(element.get("index")),
                "ayah_count": int(element.get("ayas")),
                "name_ar": element.get("name"),
                "name_en": element.get("tname") or element.get("ename") or "",
                # Tanzil says Meccan/Medinan; the app's contract says makkah/madinah.
                "revelation_place": "makkah" if place.startswith("mecc") else "madinah",
            }
        )

    juz_starts = [
        (int(e.get("sura")), int(e.get("aya"))) for e in root.findall("./juzs/juz")
    ]
    page_starts = [
        (int(e.get("sura")), int(e.get("aya"))) for e in root.findall("./pages/page")
    ]

    if len(surahs) != TOTAL_SURAHS:
        fail(f"{path}: {len(surahs)} suras, expected {TOTAL_SURAHS}")
    if len(juz_starts) != TOTAL_JUZS:
        fail(f"{path}: {len(juz_starts)} juzs, expected {TOTAL_JUZS}")
    if len(page_starts) != TOTAL_PAGES:
        fail(f"{path}: {len(page_starts)} pages, expected {TOTAL_PAGES}")

    return surahs, juz_starts, page_starts


# --------------------------------------------------------------------------
# assembly
# --------------------------------------------------------------------------

def build_ayahs(verses, surahs, juz_starts, page_starts, strip_basmalah):
    """Flatten to the canonical 1..6236 order and attach page and juz."""
    offset_of = {}
    ordered = []
    for surah in surahs:
        for ayah in range(1, surah["ayah_count"] + 1):
            key = (surah["id"], ayah)
            if key not in verses:
                fail(f"verse {surah['id']}:{ayah} is missing from the text file")
            offset_of[key] = len(ordered)
            ordered.append(key)

    extra = set(verses) - set(offset_of)
    if extra:
        sample = ", ".join(f"{s}:{a}" for s, a in sorted(extra)[:5])
        fail(f"the text file has {len(extra)} verse(s) not in the metadata: {sample}")
    if len(ordered) != TOTAL_AYAHS:
        fail(f"assembled {len(ordered)} verses, expected {TOTAL_AYAHS}")

    def boundaries(starts):
        marks = []
        for index, key in enumerate(starts, start=1):
            if key not in offset_of:
                fail(f"metadata points at a verse that does not exist: {key[0]}:{key[1]}")
            marks.append((offset_of[key], index))
        marks.sort()
        if marks[0][0] != 0:
            fail("metadata does not start at 1:1")
        return marks

    def assign(marks):
        values = [0] * len(ordered)
        for position, current in enumerate(marks):
            start = current[0]
            end = marks[position + 1][0] if position + 1 < len(marks) else len(ordered)
            for index in range(start, end):
                values[index] = current[1]
        if 0 in values:
            fail("internal: boundary assignment did not cover every verse")
        return values

    pages = assign(boundaries(page_starts))
    juzs = assign(boundaries(juz_starts))

    stripped = 0
    rows = []
    for index, (surah_id, ayah) in enumerate(ordered):
        text = verses[(surah_id, ayah)]
        if strip_basmalah and ayah == 1 and surah_id not in (1, 9) and BASMALAH_RE.match(text):
            # Drop the leading basmalah (four words) and keep the verse itself
            # untouched. Sura 1 counts it as a verse; sura 9 has none.
            words = text.split(None, 4)
            text = words[4].strip() if len(words) > 4 else ""
            if not text:
                fail(f"stripping the basmalah emptied {surah_id}:1 -- check the source")
            stripped += 1
        rows.append(
            {
                "id": index + 1,
                "surah_id": surah_id,
                "ayah_number": ayah,
                "text_uthmani": text,
                "page": pages[index],
                "juz": juzs[index],
            }
        )
    return rows, stripped


def build_waqf_marks(rows, known_symbols):
    """One row per glyph, recording which word of the verse it follows.

    `word_index` is 1-based and counts the words *before* the mark, which is the
    order the app's sheet reads them in. A mark opening a verse gets 0.
    """
    marks = []
    unknown = set()
    for row in rows:
        preceding = 0
        for word in row["text_uthmani"].split():
            glyphs = [character for character in word if character in WAQF_SYMBOLS]
            bare = "".join(c for c in word if c not in WAQF_SYMBOLS).strip()
            for glyph in glyphs:
                if glyph not in known_symbols:
                    unknown.add(glyph)
                marks.append(
                    {
                        "surah_id": row["surah_id"],
                        "ayah_number": row["ayah_number"],
                        "word_index": preceding,
                        "symbol": glyph,
                    }
                )
            if bare:
                preceding += 1
    if unknown:
        listed = ", ".join("U+%04X" % ord(c) for c in sorted(unknown))
        fail("the text carries waqf glyphs with no entry in waqf_types.json: " + listed)
    return marks


# --------------------------------------------------------------------------
# output
# --------------------------------------------------------------------------

SCHEMA = """
CREATE TABLE surahs (
    id               INTEGER PRIMARY KEY,
    name_ar          TEXT    NOT NULL,
    name_en          TEXT    NOT NULL,
    ayah_count       INTEGER NOT NULL,
    revelation_place TEXT    NOT NULL
);

CREATE TABLE ayahs (
    id           INTEGER PRIMARY KEY,
    surah_id     INTEGER NOT NULL,
    ayah_number  INTEGER NOT NULL,
    text_uthmani TEXT    NOT NULL,
    page         INTEGER,
    juz          INTEGER
);

CREATE INDEX ix_ayahs_surah ON ayahs (surah_id, ayah_number);
CREATE INDEX ix_ayahs_page  ON ayahs (page);
"""

WAQF_SCHEMA = """
CREATE TABLE waqf_types (
    symbol    TEXT PRIMARY KEY,
    name_ar   TEXT NOT NULL,
    ruling_ar TEXT NOT NULL
);

CREATE TABLE waqf_marks (
    id          INTEGER PRIMARY KEY,
    surah_id    INTEGER NOT NULL,
    ayah_number INTEGER NOT NULL,
    word_index  INTEGER NOT NULL,
    symbol      TEXT    NOT NULL
);

CREATE INDEX ix_waqf_marks_ayah ON waqf_marks (surah_id, ayah_number, word_index);
"""


def write_database(path, surahs, rows, waqf_types, waqf_marks):
    if path.exists():
        path.unlink()
    connection = sqlite3.connect(path)
    try:
        connection.executescript(SCHEMA)
        connection.executemany(
            "INSERT INTO surahs (id, name_ar, name_en, ayah_count, revelation_place)"
            " VALUES (:id, :name_ar, :name_en, :ayah_count, :revelation_place)",
            surahs,
        )
        connection.executemany(
            "INSERT INTO ayahs (id, surah_id, ayah_number, text_uthmani, page, juz)"
            " VALUES (:id, :surah_id, :ayah_number, :text_uthmani, :page, :juz)",
            rows,
        )
        if waqf_marks is not None:
            connection.executescript(WAQF_SCHEMA)
            connection.executemany(
                "INSERT INTO waqf_types (symbol, name_ar, ruling_ar)"
                " VALUES (:symbol, :name_ar, :ruling_ar)",
                waqf_types,
            )
            connection.executemany(
                "INSERT INTO waqf_marks (surah_id, ayah_number, word_index, symbol)"
                " VALUES (:surah_id, :ayah_number, :word_index, :symbol)",
                waqf_marks,
            )
        connection.commit()
        # Smaller download, and the app only ever reads this file.
        connection.execute("VACUUM")
    finally:
        connection.close()


def sha256_of(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(
        description="Build the mushaf SQLite package the CMS uploads."
    )
    parser.add_argument(
        "--text",
        required=True,
        type=Path,
        help="the corpus: Tanzil's sura|aya|text export, or its .sql dump",
    )
    parser.add_argument("--metadata", required=True, type=Path, help="Tanzil quran-data.xml")
    parser.add_argument("--out", required=True, type=Path, help="the .db file to write")
    parser.add_argument(
        "--waqf-types",
        type=Path,
        default=Path(__file__).with_name("waqf_types.json"),
        help="the glyph-to-ruling table (default: alongside this script)",
    )
    parser.add_argument(
        "--no-waqf",
        action="store_true",
        help="write only the required tables; the app then renders the glyphs "
        "without offering the explanation sheet (upload with HasWaqfAnnotations = false)",
    )
    parser.add_argument(
        "--strip-basmalah",
        action="store_true",
        help="drop the basmalah some Tanzil exports prepend to verse 1 of every "
        "sura but 1 and 9",
    )
    arguments = parser.parse_args()

    for path in (arguments.text, arguments.metadata):
        if not path.is_file():
            fail(f"{path}: not found")

    verses = read_verses(arguments.text)
    surahs, juz_starts, page_starts = read_metadata(arguments.metadata)
    rows, stripped = build_ayahs(verses, surahs, juz_starts, page_starts, arguments.strip_basmalah)

    prefixed = sum(
        1
        for row in rows
        if row["ayah_number"] == 1
        and row["surah_id"] not in (1, 9)
        and BASMALAH_RE.match(row["text_uthmani"])
    )
    if prefixed and not arguments.strip_basmalah:
        print(
            f"note: {prefixed} sura(s) still open verse 1 with the basmalah. That is "
            "right for some mushaf layouts and wrong for others -- pass "
            "--strip-basmalah if the reader draws it as a heading.",
            file=sys.stderr,
        )

    waqf_types = None
    waqf_marks = None
    if not arguments.no_waqf:
        if not arguments.waqf_types.is_file():
            fail(f"{arguments.waqf_types}: not found (or pass --no-waqf)")
        waqf_types = json.loads(arguments.waqf_types.read_text(encoding="utf-8"))
        waqf_marks = build_waqf_marks(rows, {entry["symbol"] for entry in waqf_types})
        if not waqf_marks:
            fail(
                "the text carries no waqf glyphs at all, so this is a simple "
                "(imla'i) corpus rather than an Uthmani one. Build it from "
                "quran-uthmani, or pass --no-waqf and upload it as Naskh."
            )

    write_database(arguments.out, surahs, rows, waqf_types, waqf_marks)

    size = arguments.out.stat().st_size
    digest = sha256_of(arguments.out)
    print(f"wrote   {arguments.out}")
    print(f"surahs  {len(surahs)}")
    print(f"ayahs   {len(rows)}")
    print(f"pages   {len({row['page'] for row in rows})}")
    print(f"juz     {len({row['juz'] for row in rows})}")
    if waqf_marks is not None:
        print(f"waqf    {len(waqf_marks)} marks over {len(waqf_types)} types")
    else:
        print("waqf    none (upload with HasWaqfAnnotations = false)")
    if stripped:
        print(f"note    stripped the basmalah from {stripped} opening verses")
    print(f"size    {size:,} bytes")
    print(f"sha256  {digest}")
    print()
    print("The server recomputes this checksum as it writes the upload and refuses on")
    print("a mismatch -- compare it with what the CMS reports after the upload.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
