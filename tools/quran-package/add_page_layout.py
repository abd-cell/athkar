#!/usr/bin/env python3
"""Add the printed-page layer to a mushaf package: words, lines and the font.

`build_quran_package.py` produces a package that can be *read* — verse by verse,
reflowed to whatever width the phone has. This script adds what is needed to
show the mushaf as it is *printed*: 604 pages of 15 lines, each line broken
exactly where the King Fahd Complex broke it.

Two ways to draw those lines, and they differ by two orders of magnitude:

  --script digitalkhatt   One font for the whole mushaf (~0.5 MB). The words are
                          ordinary Unicode, so the line breaks are exact but the
                          justification is ours: the space between words is
                          stretched, where the print stretches the letters.

  --script qcf            The King Fahd Complex page fonts — one font per page,
                          in which a glyph *is* a word as drawn on that page.
                          Letter-perfect, and 159 MB of outlines, because page
                          42's font cannot draw page 43.

Inputs (fetched by `fetch_mushaf_assets.py`):

  pages/NNN.json        the page grid: every line, its type, whether it is
                        centred, and which words sit on it
  layout-text/NNN.json  the text of each word, keyed by surah:ayah:position
  fonts/pNNN.woff2      the QCF page fonts                            (qcf only)

The QCF path needs fontTools with woff support, which nothing else in this repo
uses:

    python -m venv venv && venv/Scripts/python -m pip install "fonttools[woff]" brotli

Usage:

    python add_page_layout.py --package mushaf-uthmani-v1.db --assets ./assets \
        --script digitalkhatt --font DigitalKhattV2.otf --out mushaf-pages-v1.db
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import shutil
import sqlite3
import sys
from collections import defaultdict
from pathlib import Path

TOTAL_PAGES = 604
TOTAL_AYAHS = 6236
# 77,432 words as this layout counts them, plus one marker per verse.
TOTAL_WORDS = 83668
PAGE_LINES = 15  # every page, al-Fatiha included — see `blank` below

# ARABIC END OF AYAH. The digits that follow it are drawn inside the marker, so
# the verse number arrives as one ornament rather than as loose numerals.
END_OF_AYAH = "۝"


def fail(message):
    print("error: " + message, file=sys.stderr)
    raise SystemExit(1)


# --------------------------------------------------------------------------
# words and lines
# --------------------------------------------------------------------------

# The pause marks. They travel attached to the word they follow, so a space
# before one does not start a new word.
WAQF_MARKS = set("ۖۗۘۙۚۛۜ۝۞۩ۤ")


def split_to(source, wanted, key):
    """Cut a verse into exactly the number of words the layout counts.

    The starting point is the text source's own word list, not a split on
    spaces: several of its words *contain* a space — a pause mark after the word
    it applies to, the rub el hizb (۞) before the word it opens, and in 5:52 a
    stray one inside «دائرة». Splitting those apart would invent words.

    The two sources then tokenise a handful of verses apart in each direction,
    so neither a blanket split nor a blanket join is right: «بَعْدَ مَا» is one
    word here and two in the layout (2:181, 8:6, 13:37), while «إِلْ يَاسِينَ»
    is one word in both (37:130). So the split is demand-driven — break a space
    only while a word is still owed, and never one that only holds a mark.
    """
    # The text source leaves a right-to-left mark in 27:26. It draws nothing,
    # the line is laid out right to left anyway, and a font with no glyph for it
    # would be reported as missing coverage.
    tokens = [token.replace("‏", "") for token in source]

    while len(tokens) < wanted:
        for index, token in enumerate(tokens):
            head, space, tail = token.partition(" ")
            if not space:
                continue
            if all(character in WAQF_MARKS for character in tail.replace(" ", "")):
                continue  # that space holds a mark, not a word boundary
            tokens[index:index + 1] = [head, tail]
            break
        else:
            fail(f"{key}: the layout wants {wanted} words and the text gives "
                 f"{len(tokens)}, with nothing left to split")

    if len(tokens) > wanted:
        fail(f"{key}: the layout wants {wanted} words and the text gives {len(tokens)}")
    return tokens


def read_word_text(directory, field, marker_ornament, wanted):
    """The words of every verse, in order, cut the way [wanted] counts them.

    Only the text is taken from here. Which page a word sits on is a question
    this source answers differently from the layout's own publisher — it puts
    5:90 on page 123, where the 1421H print begins that page at 5:91 — so it is
    not asked.
    """
    verses = {}
    markers = 0

    for page in range(1, TOTAL_PAGES + 1):
        path = directory / f"{page:03d}.json"
        if not path.is_file():
            fail(f"{path}: missing — run fetch_mushaf_assets.py first")

        for verse in json.loads(path.read_text(encoding="utf-8"))["verses"]:
            key = verse["verse_key"]
            if key in verses:
                continue
            if key not in wanted:
                fail(f"{key} has text but the layout never places it")

            body, marker = [], None
            for word in verse["words"]:
                text = word.get(field)
                if not text:
                    fail(f"{path}: a word of {key} has no {field}")
                if word.get("char_type_name") == "end":
                    marker = END_OF_AYAH + text if marker_ornament else text
                    markers += 1
                else:
                    body.append(text)

            if marker is None:
                fail(f"{key} has no verse marker")

            # The marker is the verse's last word in the layout too, so the text
            # owes one fewer.
            tokens = split_to(body, wanted[key] - 1, key) if field == "text_uthmani" else body
            verses[key] = tokens + [marker]

    if len(verses) != TOTAL_AYAHS:
        fail(f"{len(verses)} verses of text, expected {TOTAL_AYAHS}")
    if markers != TOTAL_AYAHS:
        fail(f"{markers} verse markers, expected one per verse")
    return verses


def words_per_verse(pages):
    """How many words the layout counts in each verse."""
    counts = defaultdict(int)
    for rows in pages.values():
        for row in rows:
            for location in row["words"]:
                surah, ayah, position = location.split(":")
                key = f"{surah}:{ayah}"
                counts[key] = max(counts[key], int(position))
    return counts


def read_pages(directory):
    """The page grid, exactly as the layout's publisher draws it."""
    pages = {}
    for page in range(1, TOTAL_PAGES + 1):
        path = directory / f"{page:03d}.json"
        if not path.is_file():
            fail(f"{path}: missing — run fetch_mushaf_assets.py first")
        rows = json.loads(path.read_text(encoding="utf-8"))
        if len(rows) != PAGE_LINES:
            fail(f"page {page} has {len(rows)} lines, expected {PAGE_LINES}")
        pages[page] = rows
    return pages


def build(pages, verses):
    """Number the words in reading order and hang the lines off them.

    The surah a header announces is the surah of the words that follow it —
    which may be overleaf: page 76 ends with the header of an-Nisa, and its text
    begins on 77.
    """
    words, lines, word_id = [], [], 0
    used = set()

    for page in range(1, TOTAL_PAGES + 1):
        rows = pages[page]

        for index, row in enumerate(rows):
            kind, line = row["kind"], row["line"]

            if kind == "ayah" and not row["words"]:
                # The first two pages hold eight lines of text on a fifteen-line
                # grid; the rest of the grid is empty, and keeping those rows is
                # what makes al-Fatiha sit where the print sits it rather than
                # stretched over the whole page.
                lines.append((page, line, "blank", False, None, None, None))
                continue

            if kind == "ayah":
                first = word_id + 1
                surah = None
                for location in row["words"]:
                    if location in used:
                        fail(f"{location} appears on more than one line")
                    used.add(location)
                    surah_number, ayah, position = (int(part) for part in location.split(":"))
                    tokens = verses.get(f"{surah_number}:{ayah}")
                    if tokens is None or position > len(tokens):
                        fail(f"{location} is on page {page} but the text has "
                             f"{0 if tokens is None else len(tokens)} word(s) in that verse")
                    word_id += 1
                    words.append((word_id, surah_number, ayah, position, tokens[position - 1], page, line))
                    surah = surah or surah_number
                lines.append((page, line, kind, row["centered"], first, word_id, surah))
                continue

            if row["words"]:
                fail(f"page {page} line {line} is marked {kind} but carries words")

            following = [r for r in rows[index + 1:] if r["words"]]
            if following:
                announced = int(following[0]["words"][0].split(":")[0])
            elif page < TOTAL_PAGES:
                ahead = [r for r in pages[page + 1] if r["words"]]
                announced = int(ahead[0]["words"][0].split(":")[0]) if ahead else None
            else:
                announced = None
            lines.append((page, line, kind, row["centered"], None, None, announced))

    if word_id != TOTAL_WORDS:
        fail(f"placed {word_id} words, expected {TOTAL_WORDS}")
    unplaced = [f"{key}:{index + 1}"
                for key, tokens in verses.items()
                for index in range(len(tokens))
                if f"{key}:{index + 1}" not in used]
    if unplaced:
        fail(f"{len(unplaced)} word(s) have text but no place on any page, "
             f"e.g. {unplaced[:3]}")
    return words, lines


# --------------------------------------------------------------------------
# fonts
# --------------------------------------------------------------------------

def load_font_tools():
    try:
        from fontTools.ttLib import TTFont
        return TTFont
    except ImportError:
        fail("fontTools is not installed — see the note at the top of this file")


def single_font(path):
    """One font for all 604 pages: read as-is, and named by what it calls itself."""
    TTFont = load_font_tools()
    if not path or not path.is_file():
        fail("--script digitalkhatt needs --font pointing at the .otf")
    font = TTFont(path)
    family = font["name"].getDebugName(6) or font["name"].getDebugName(1) or path.stem
    return [(None, family, path.read_bytes())], font.getBestCmap()


def page_fonts(directory):
    """The 604 QCF page fonts, converted from woff2 to the ttf Flutter can load.

    The woff2 is only a compressed wrapper around the same outlines, and Flutter's
    FontLoader has no brotli, so the conversion happens here rather than on the
    phone. They are already subset to each page's own glyphs upstream.
    """
    TTFont = load_font_tools()
    fonts = []
    for page in range(1, TOTAL_PAGES + 1):
        path = directory / f"p{page:03d}.woff2"
        if not path.is_file():
            fail(f"{path}: missing — run fetch_mushaf_assets.py first")
        font = TTFont(path)
        font.flavor = None
        buffer = io.BytesIO()
        font.save(buffer)
        fonts.append((page, f"QCF_P{page:03d}", buffer.getvalue()))
    return fonts


def check_coverage(fonts, words, shared_cmap):
    """Every character a page needs must exist in the font that draws that page.

    A missing one renders as an empty box in the middle of a verse — the kind of
    fault that leaves no trace in a build log and is unmissable on a phone.
    """
    TTFont = load_font_tools()

    needed = defaultdict(set)
    for _, _, _, _, text, page, _ in words:
        needed[page].update(text)

    if shared_cmap is not None:
        absent = {c for chars in needed.values() for c in chars if ord(c) not in shared_cmap}
        return sorted("U+%04X" % ord(c) for c in absent)

    missing = []
    for page, _, data in fonts:
        cmap = TTFont(io.BytesIO(data)).getBestCmap()
        absent = {c for c in needed[page] if ord(c) not in cmap}
        if absent:
            missing.append((page, sorted("U+%04X" % ord(c) for c in absent)))
    if missing:
        head = "; ".join(f"page {p}: {', '.join(c[:4])}" for p, c in missing[:5])
        fail(f"{len(missing)} page font(s) lack characters the page uses — {head}")
    return []


# --------------------------------------------------------------------------
# output
# --------------------------------------------------------------------------

SCHEMA = """
CREATE TABLE words (
    id          INTEGER PRIMARY KEY,
    surah_id    INTEGER NOT NULL,
    ayah_number INTEGER NOT NULL,
    position    INTEGER NOT NULL,
    text        TEXT    NOT NULL,
    page        INTEGER NOT NULL,
    line        INTEGER NOT NULL
);

CREATE INDEX ix_words_page ON words (page, line, id);
CREATE INDEX ix_words_ayah ON words (surah_id, ayah_number, position);

CREATE TABLE lines (
    id            INTEGER PRIMARY KEY,
    page          INTEGER NOT NULL,
    line_number   INTEGER NOT NULL,
    kind          TEXT    NOT NULL,
    is_centered   INTEGER NOT NULL,
    first_word_id INTEGER,
    last_word_id  INTEGER,
    surah_id      INTEGER
);

CREATE INDEX ix_lines_page ON lines (page, line_number);

-- One row per font. `page` is the page it draws, and NULL means it draws every
-- page: a DigitalKhatt package has exactly one row, a QCF package has 604.
CREATE TABLE fonts (
    id     INTEGER PRIMARY KEY,
    page   INTEGER,
    family TEXT NOT NULL,
    data   BLOB NOT NULL
);

CREATE UNIQUE INDEX ix_fonts_page ON fonts (page);
"""


def unify(connection, words):
    """Make the verse tables agree with the page tables, word for word.

    The two layers come from different exports of the same Uthmani text, and
    they spell it slightly differently — «ٱلْكِتَٰبُ» against «ٱلْكِتَـٰبُ». A
    package that carries both would answer "what does this verse say?" one way
    in the verse view and another on the page, which is exactly the kind of
    quiet disagreement this project exists to not have. The page layer wins,
    because it is the text the reader actually sees drawn.

    The waqf marks are rebuilt from the same words for the same reason: their
    `word_index` counts words, and the two layers do not always count them alike.
    """
    verses = defaultdict(list)
    for _, surah, ayah, _, text, _, _ in words:
        if not text.startswith(END_OF_AYAH):
            verses[(surah, ayah)].append(text)

    rewritten = 0
    for (surah, ayah), tokens in verses.items():
        text = " ".join(tokens)
        rows = connection.execute(
            "UPDATE ayahs SET text_uthmani = ? WHERE surah_id = ? AND ayah_number = ?"
            " AND text_uthmani <> ?", (text, surah, ayah, text))
        rewritten += rows.rowcount

    marks = []
    has_waqf = connection.execute(
        "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='waqf_marks'"
    ).fetchone()[0] == 1

    if has_waqf:
        known = {row[0] for row in connection.execute("SELECT symbol FROM waqf_types")}
        for (surah, ayah), tokens in verses.items():
            preceding = 0
            for token in tokens:
                glyphs = [c for c in token if c in known]
                bare = "".join(c for c in token if c not in known).strip()
                for glyph in glyphs:
                    marks.append((surah, ayah, preceding, glyph))
                if bare:
                    preceding += 1
        connection.execute("DELETE FROM waqf_marks")
        connection.executemany(
            "INSERT INTO waqf_marks (surah_id, ayah_number, word_index, symbol)"
            " VALUES (?, ?, ?, ?)", marks)

    return rewritten, len(marks)


def write(path, words, lines, fonts):
    connection = sqlite3.connect(path)
    try:
        existing = {row[0] for row in connection.execute(
            "SELECT name FROM sqlite_master WHERE type='table'")}
        for table in ("words", "lines", "fonts"):
            if table in existing:
                connection.execute(f"DROP TABLE {table}")
        connection.executescript(SCHEMA)
        connection.executemany(
            "INSERT INTO words (id, surah_id, ayah_number, position, text, page, line)"
            " VALUES (?, ?, ?, ?, ?, ?, ?)", words)
        connection.executemany(
            "INSERT INTO lines (page, line_number, kind, is_centered, first_word_id,"
            " last_word_id, surah_id) VALUES (?, ?, ?, ?, ?, ?, ?)",
            [(p, n, k, 1 if c else 0, f, l, s) for p, n, k, c, f, l, s in lines])
        connection.executemany(
            "INSERT INTO fonts (page, family, data) VALUES (?, ?, ?)", fonts)
        rewritten, marks = unify(connection, words)
        connection.commit()
        connection.execute("VACUUM")
        return rewritten, marks
    finally:
        connection.close()


def sha256_of(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description="Add the printed-page layer to a mushaf package.")
    parser.add_argument("--package", required=True, type=Path, help="a package from build_quran_package.py")
    parser.add_argument("--assets", required=True, type=Path, help="the directory fetch_mushaf_assets.py filled")
    parser.add_argument("--out", required=True, type=Path, help="the .db to write")
    parser.add_argument("--script", default="digitalkhatt", choices=("digitalkhatt", "qcf"),
                        help="one font for the mushaf, or one per page (default: digitalkhatt)")
    parser.add_argument("--font", type=Path, help="the .otf/.ttf, for --script digitalkhatt")
    arguments = parser.parse_args()

    if not arguments.package.is_file():
        fail(f"{arguments.package}: not found")

    pages = read_pages(arguments.assets / "pages")
    wanted = words_per_verse(pages)

    if arguments.script == "digitalkhatt":
        verses = read_word_text(arguments.assets / "layout-text", "text_uthmani", True, wanted)
        fonts, shared = single_font(arguments.font)
    else:
        verses = read_word_text(arguments.assets / "layout-text", "code_v2", False, wanted)
        fonts, shared = page_fonts(arguments.assets / "fonts"), None

    numbered, lines = build(pages, verses)
    uncovered = check_coverage(fonts, numbered, shared)

    shutil.copyfile(arguments.package, arguments.out)
    rewritten, marks = write(arguments.out, numbered, lines, fonts)

    size = arguments.out.stat().st_size
    print(f"wrote   {arguments.out}")
    print(f"script  {arguments.script} ({fonts[0][1]})")
    print(f"words   {len(numbered)}")
    print(f"lines   {len(lines)} "
          f"({sum(1 for l in lines if l[2] == 'ayah')} ayah, "
          f"{sum(1 for l in lines if l[2] == 'surah_name')} headers, "
          f"{sum(1 for l in lines if l[2] == 'basmallah')} basmalah, "
          f"{sum(1 for l in lines if l[3])} centred)")
    print(f"fonts   {len(fonts)}, {sum(len(f[2]) for f in fonts)/1e6:.1f} MB of outlines")
    print(f"unified {rewritten} verse(s) respelled to match the page layer, "
          f"{marks} waqf marks rebuilt")
    print(f"size    {size:,} bytes")
    print(f"sha256  {sha256_of(arguments.out)}")

    if uncovered:
        # Not fatal for a shared font: these are rare recitation marks, and the
        # reader sees an empty box only where one actually occurs.
        print(f"\nwarning: the font has no glyph for {', '.join(uncovered)} — "
              "check where they occur before publishing", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
