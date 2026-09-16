#!/usr/bin/env python3
"""Pull the أبواب and adhkar of حصن المسلم from hisnmuslim.com into data/adhkar.json.

What this source gives, and what it does not:

  gives    132 أبواب in the book's own order, the Arabic of every dhikr, and the
           repeat count.
  does NOT give a book, a hadith number or a grading. Those fields are simply
           absent from the API.

That absence decides how the import behaves downstream. This project's one
distinguishing claim is that every dhikr on screen traces to a book, a number
and a grading, and `ErrorCode.SourceRequired` enforces it at the only door
content comes through. So the sync writes every dhikr from here as an
*unpublished draft*: visible to editors, invisible to readers, until somebody
adds the takhrij. The أبواب themselves are published, because a chapter is
structure and not a narration.

    python tools/adhkar/pull.py
    python tools/adhkar/pull.py --out data/adhkar.json
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

HERE = Path(__file__).resolve().parent

INDEX_URL = "https://www.hisnmuslim.com/api/ar/husn_ar.json"
CHAPTER_URL = "https://www.hisnmuslim.com/api/ar/{id}.json"

EXPECTED_CHAPTERS = 132

# The three أبواب that mean exactly what a chapter already in this database
# means, so importing them as new ones would stand a second «أذكار النوم» beside
# the first. Keyed by the book's own chapter id.
#
# Deliberately short, and every entry verified against the titles the API
# actually returns rather than guessed from the ids — id 19 is «دعاء السجود»,
# not the mosque chapter it looks like it ought to be.
#
# حصن المسلم splits what this app joins (دخول الخلاء and الخروج منه are two
# chapters there and one `khala` here) and joins what this app splits (أذكار
# الصباح والمساء is one chapter there and `morning` + `evening` here). Folding
# those together is an editorial judgement about which narration belongs where,
# and an importer does not get to make it: the other 129 chapters arrive under
# their own names as `hisn-<id>` and an editor merges whatever they want merged.
# The sync reports the overlap; it never acts on it.
ALIASES = {
    1: "waking",         # أذكار الاستيقاظ من النوم
    28: "sleep",         # أذكار النوم
    25: "after-prayer",  # الأذكار بعد السلام من الصلاة
}


def fetch(url: str) -> dict:
    request = urllib.request.Request(
        url, headers={"User-Agent": "athkari-adhkar-import/1 (+tools/adhkar)"})
    with urllib.request.urlopen(request, timeout=60) as response:
        # The files are served with a UTF-8 BOM, which json.loads refuses.
        return json.loads(response.read().decode("utf-8-sig"))


def clean(text: str) -> str:
    """Strip the source's own presentation from the narration.

    Every dhikr arrives wrapped in parentheses and often with a trailing full
    stop — typography of the printed book, not part of what is said. Non-breaking
    spaces and doubled spaces come along too.
    """
    text = text.replace(" ", " ").replace("‏", "").strip()
    text = re.sub(r"\s+", " ", text)

    # The wrapping parentheses, but only when they really do wrap the whole
    # thing. Several entries open with a bracket they never close — the source
    # puts the reciter's instruction and the narration in one string — and
    # stripping a lone bracket there would eat a character of the text instead
    # of a mark of punctuation.
    body = text[:-1].rstrip(".").strip() if text.endswith(".") else text
    if body.startswith("(") and body.endswith(")") and body.count("(") == body.count(")"):
        body = body[1:-1].strip()

    return body.strip()


def key_for(chapter_id: int) -> str:
    return ALIASES.get(chapter_id, f"hisn-{chapter_id}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, default=HERE / "data" / "adhkar.json")
    args = parser.parse_args()

    print(f"index: {INDEX_URL}", flush=True)
    index = fetch(INDEX_URL)

    root = next(iter(index.values()))
    if len(root) != EXPECTED_CHAPTERS:
        print(f"warning: {len(root)} chapters, expected {EXPECTED_CHAPTERS}", file=sys.stderr)

    print(f"fetching {len(root)} chapters ...", flush=True)

    def grab(entry: dict) -> tuple[dict, dict]:
        return entry, fetch(CHAPTER_URL.format(id=entry["ID"]))

    chapters = []
    with ThreadPoolExecutor(max_workers=8) as pool:
        for index_in_order, (entry, body) in enumerate(pool.map(grab, root)):
            chapter_id = int(entry["ID"])
            title = entry["TITLE"].strip()

            items = next(iter(body.values()), [])
            adhkar = []
            for item in items:
                text = clean(item.get("ARABIC_TEXT") or "")
                if not text:
                    continue
                adhkar.append({
                    "text": text,
                    "repeat": max(1, int(item.get("REPEAT") or 1)),
                })

            chapters.append({
                "key": key_for(chapter_id),
                "source_id": chapter_id,
                "title_ar": title,
                "sort_order": index_in_order,
                "adhkar": adhkar,
            })

            if (index_in_order + 1) % 40 == 0:
                print(f"  {index_in_order + 1}/{len(root)}", flush=True)

    total = sum(len(c["adhkar"]) for c in chapters)

    if total == 0:
        print("error: the source returned no adhkar at all", file=sys.stderr)
        return 1

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8") as f:
        json.dump({
            "source": "hisnmuslim.com",
            "note": "No takhrij or grading in this source; every dhikr imports as a draft.",
            "chapters": chapters,
        }, f, ensure_ascii=False, indent=1)

    size = args.out.stat().st_size
    print()
    print(f"wrote {args.out}  ({size // 1024} KB)")
    print(f"  {len(chapters)} chapters, {total} adhkar")
    print(f"  {sum(1 for c in chapters if not c['key'].startswith('hisn-'))} mapped onto existing chapters")
    print()
    print("None of these carry a source, so the sync writes them unpublished.")
    print("Copy it next to the seeders to ship it:")
    print("  cp tools/adhkar/data/adhkar.json backend/Athkar/DataAccess/Seeders/Data/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
