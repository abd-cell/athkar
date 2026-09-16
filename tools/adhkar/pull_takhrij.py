#!/usr/bin/env python3
"""Pull حصن المسلم's own takhrij footnotes into data/takhrij.json.

`pull.py` takes the book's text from hisnmuslim.com, whose API carries no book,
no number and no grading — which is why every dhikr it imports lands as an
unpublished draft. This fills that gap from the same book's own footnotes, in a
digitisation that kept them.

Two rules decide everything here, and both exist because a wrong attribution is
worse than none:

1. **Only chapters whose footnotes line up one-to-one are used.** Seven of the
   134 carry more footnotes than adhkar — a dhikr there has two notes, or a note
   belongs to a narration quoted inside another — and once the counts diverge
   nothing says which note belongs to which dhikr. Those chapters are reported,
   not guessed at.

2. **Only what the footnote actually says is recorded.** The book is quoted, not
   summarised: the reference is a substring of its own note. A footnote naming no
   book at all is skipped rather than filed under something plausible.

    python tools/adhkar/pull_takhrij.py
    python tools/adhkar/pull_takhrij.py --out data/takhrij.json --report
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import unicodedata
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent

SOURCE_URL = "https://raw.githubusercontent.com/rn0x/hisn_almuslim_json/main/hisn_almuslim.json"
SOURCE_NAME = "hisn_almuslim_json (حصن المسلم، بحواشي المؤلف)"

# The books the footnotes name, in the form this database already uses for the
# rows an editor entered by hand. Longest first: «صحيح البخاري» must win over
# «البخاري», and «سنن أبي داود» over «أبو داود».
BOOKS = [
    ("صحيح البخاري", ["صحيح البخاري", "البخاري مع الفتح", "البخاري"]),
    ("صحيح مسلم", ["صحيح مسلم", "مسلم"]),
    ("سنن أبي داود", ["صحيح أبي داود", "سنن أبي داود", "أبي داود", "أبو داود"]),
    ("جامع الترمذي", ["صحيح الترمذي", "سنن الترمذي", "الترمذي"]),
    ("سنن النسائي", ["صحيح النسائي", "سنن النسائي", "النسائي"]),
    ("سنن ابن ماجه", ["صحيح ابن ماجه", "سنن ابن ماجه", "ابن ماجه", "ابن ماجة"]),
    ("مسند أحمد", ["مسند أحمد", "أحمد"]),
    ("موطأ مالك", ["موطأ مالك", "الموطأ", "مالك"]),
    ("صحيح ابن حبان", ["صحيح ابن حبان", "ابن حبان"]),
    ("مستدرك الحاكم", ["مستدرك الحاكم", "الحاكم"]),
    ("سنن الدارمي", ["سنن الدارمي", "الدارمي"]),
    ("السنن الكبرى للبيهقي", ["البيهقي"]),
    ("المعجم للطبراني", ["الطبراني"]),
    ("عمل اليوم والليلة للنسائي", ["عمل اليوم والليلة"]),
    ("الأذكار للنووي", ["الأذكار للنووي"]),
    ("عمل اليوم والليلة لابن السني", ["ابن السني"]),
]

# A Qur'anic footnote cites a surah and an ayah and has no chain to grade —
# which is the whole reason HadithGrade.QuranVerse exists.
QURAN = re.compile(r"سورة\s+([^\s،,]+(?:\s+[^\s،,:آ]+)?)\s*[،,]?\s*آية\s*[:：]?\s*(\d+)")

# A locator as the book writes them: volume/page, or an explicit number.
REFERENCE = re.compile(r"(?:برقم\s*)?(\d+\s*/\s*\d+|\d{1,5})")

# Gradings the footnote states outright, and only those. «انظر صحيح الترمذي»
# names a collection, not a verdict on this chain, and reading it as one would
# put a grading on a reader's screen that nobody issued. A note that does not
# grade leaves the field empty for the editor.
GRADINGS = [
    (6, ["متفق عليه"]),
    (1, ["صححه الألباني", "وصححه ووافقه الذهبي", "صححه الذهبي", "صححه النووي"]),
    (2, ["حسنه الألباني", "حسنه الترمذي", "حسنه النووي"]),
]

GRADER = re.compile(r"(?:صححه|حسنه|ضعفه)\s+(الألباني|الذهبي|النووي|ابن باز|الأرناؤوط)")

FOLD = {"آ": "ا", "أ": "ا", "إ": "ا", "ٱ": "ا", "ى": "ي", "ة": "ه", "ؤ": "و", "ئ": "ي", "ء": ""}


def is_diacritic(character):
    code = ord(character)
    return (0x064B <= code <= 0x065F) or character in ("ـ", "ٰ") or (0x06D6 <= code <= 0x06ED)


def normalize(raw):
    """The server's `ArabicText.Normalize`, character for character.

    The two have to agree exactly: this file is matched against the
    `SearchText` column, and a rule on one side only means a row that never
    matches anything.
    """
    if not raw:
        return ""

    out, last_space = [], False
    for character in unicodedata.normalize("NFC", raw):
        if is_diacritic(character):
            continue

        folded = FOLD.get(character)
        if folded is None:
            if "٠" <= character <= "٩":
                folded = chr(ord("0") + ord(character) - ord("٠"))
            elif character.isspace() or unicodedata.category(character).startswith(("P", "S")):
                folded = " "
            else:
                folded = character.lower()

        if folded == " ":
            if not last_space and out:
                out.append(" ")
            last_space = True
            continue
        if folded == "":
            continue

        out.append(folded)
        last_space = False

    return "".join(out).rstrip()


def parse_note(note):
    """The book and locator a footnote names, or None when it names neither.

    The locator is taken from immediately after the book it belongs to, so
    «البخاري 7/167 ومسلم 4/2072» yields al-Bukhari's own 7/167 and not Muslim's.
    """
    text = " ".join(note.split())

    verse = QURAN.search(text)
    if verse:
        return {
            "book": "القرآن الكريم",
            "reference": f"{verse.group(1).strip()}: {verse.group(2)}",
            "grade": 5,  # QuranVerse
            "gradedBy": None,
            "note": text,
        }

    found = None
    for canonical, spellings in BOOKS:
        for spelling in spellings:
            position = text.find(spelling)
            if position >= 0 and (found is None or position < found[1]):
                found = (canonical, position, position + len(spelling))
    if found is None:
        return None

    canonical, _, after = found
    match = REFERENCE.search(text, after, after + 40)
    reference = re.sub(r"\s*/\s*", "/", match.group(1)).strip() if match else None

    grade = None
    for value, markers in GRADINGS:
        if any(marker in text for marker in markers):
            grade = value
            break
    # Agreed upon: both shaykhs narrate it, which the footnote shows by naming
    # both. This is a reading of what the note says, not a verdict of our own.
    if grade is None and "البخاري" in text and "مسلم" in text:
        grade = 6

    grader = GRADER.search(text)

    return {
        "book": canonical,
        "reference": reference,
        "grade": grade,
        "gradedBy": grader.group(1) if grader else None,
        "note": text,
    }


def main():
    parser = argparse.ArgumentParser(description="Pull حصن المسلم's takhrij footnotes.")
    parser.add_argument("--out", type=Path, default=HERE / "data" / "takhrij.json")
    parser.add_argument("--report", action="store_true", help="list what was skipped and why")
    arguments = parser.parse_args()

    with urllib.request.urlopen(SOURCE_URL, timeout=60) as response:
        book = json.loads(response.read().decode("utf-8-sig"))

    entries, unaligned, unparsed = [], [], []

    for title, section in book.items():
        if not isinstance(section, dict):
            continue

        texts = section.get("text") or []
        notes = section.get("footnote") or []

        if len(texts) != len(notes):
            unaligned.append({"chapter": title, "adhkar": len(texts), "footnotes": len(notes)})
            continue

        for text, note in zip(texts, notes):
            parsed = parse_note(note)
            fold = normalize(text)
            if not fold:
                continue
            if parsed is None:
                unparsed.append({"chapter": title, "note": " ".join(note.split())[:80]})
                continue
            entries.append({"fold": fold, "chapter": title, **parsed})

    # A folded text that appears twice with different takhrij cannot be matched
    # by text alone, so it is dropped rather than resolved arbitrarily.
    seen, duplicates = {}, set()
    for entry in entries:
        previous = seen.get(entry["fold"])
        if previous and (previous["book"], previous["reference"]) != (entry["book"], entry["reference"]):
            duplicates.add(entry["fold"])
        seen.setdefault(entry["fold"], entry)

    kept = [entry for fold, entry in seen.items() if fold not in duplicates]

    arguments.out.parent.mkdir(parents=True, exist_ok=True)
    arguments.out.write_text(
        json.dumps({"source": SOURCE_NAME, "url": SOURCE_URL, "entries": kept},
                   ensure_ascii=False, indent=1),
        encoding="utf-8",
    )

    print(f"wrote      {arguments.out}")
    print(f"chapters   {len(book)}, of which {len(unaligned)} have footnotes that do not line up")
    print(f"takhrij    {len(kept)} usable")
    print(f"           {len(unparsed)} footnote(s) name no book, {len(duplicates)} ambiguous by text")
    print(f"graded     {sum(1 for e in kept if e['grade'])}, of which "
          f"{sum(1 for e in kept if e['gradedBy'])} name the grader")
    print(f"referenced {sum(1 for e in kept if e['reference'])} carry a locator")

    if arguments.report:
        print("\nchapters whose footnotes do not line up one-to-one:")
        for row in unaligned:
            print(f"  {row['chapter']}: {row['adhkar']} adhkar, {row['footnotes']} footnotes")
        print("\nfootnotes naming no book (first 10):")
        for row in unparsed[:10]:
            print(f"  {row['chapter']}: {row['note']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
