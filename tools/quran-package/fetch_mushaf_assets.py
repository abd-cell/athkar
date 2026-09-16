#!/usr/bin/env python3
"""Fetch what the printed-page layer needs: word placement, line types, fonts.

Two public sources, neither of which needs an account:

  pages/      qul.tarteel.ai's layout preview — the whole page grid: every line,
              its type, whether it is centred, and the words on it
  layout-text/ api.quran.com — the text of each word, keyed by surah:ayah:position
  fonts/      static-cdn.tarteel.ai — the QCF page fonts, as woff2  (--script qcf)

The split is deliberate. Both sites can tell you which page a word is on, and
they *disagree*: quran.com puts 5:90 on page 123, where the 1421H print begins
that page at 5:91. So exactly one source decides the layout — QUL, which is the
layout's publisher — and quran.com is asked only for the text of a word, which
is a fact about the word and not about the page.

QUL's packaged downloads sit behind a sign-in; the preview these read does not.
Everything is cached on disk, so a re-run resumes rather than re-downloads.

Usage:

    python fetch_mushaf_assets.py --out ./assets [--variant v2]
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.request
from pathlib import Path

TOTAL_PAGES = 604
AGENT = {"User-Agent": "athkari-mushaf-package-builder"}

WORDS_URL = ("https://api.quran.com/api/v4/verses/by_page/{page}"
             "?words=true&word_fields=text_uthmani,code_v2,char_type_name&per_page=300")
LINES_URL = "https://qul.tarteel.ai/resources/mushaf-layout/10?page={page}"
FONT_URL = "https://static-cdn.tarteel.ai/qul/fonts/quran_fonts/{variant}/woff2/p{page}.woff2?v=3.1"

LINE_PATTERN = re.compile(
    r'data-line="(\d+)">\s*<div class="line ([^"]*)"(.*?)(?=<div class="line-container"|\Z)',
    re.S)
WORD_PATTERN = re.compile(r'data-location="([^"]+)"')


def get(url, timeout=60):
    request = urllib.request.Request(url, headers=AGENT)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def fetch(name, directory, filename, url_for, parse, minimum):
    directory.mkdir(parents=True, exist_ok=True)
    fetched = kept = 0

    for page in range(1, TOTAL_PAGES + 1):
        target = directory / filename(page)
        if target.exists() and target.stat().st_size >= minimum:
            kept += 1
            continue

        for attempt in range(4):
            try:
                body = parse(get(url_for(page)))
                if len(body) < minimum:
                    raise ValueError(f"{len(body)} bytes is too small to be right")
                target.write_bytes(body)
                fetched += 1
                break
            except Exception as error:
                if attempt == 3:
                    print(f"  FAILED page {page}: {error}", file=sys.stderr)
                time.sleep(1 + attempt * 2)

        if page % 100 == 0:
            print(f"  {name}: {page}/{TOTAL_PAGES}", flush=True)

    print(f"{name}: {fetched} fetched, {kept} already on disk")
    if fetched + kept < TOTAL_PAGES:
        print(f"  {TOTAL_PAGES - fetched - kept} page(s) missing — re-run to retry them",
              file=sys.stderr)


def as_json(body):
    json.loads(body)  # refuse to cache a page that is not parseable
    return body


def as_page(body):
    """One page's grid: each line's number, type, centring and word locations."""
    rows = []
    for number, classes, segment in LINE_PATTERN.findall(body.decode("utf-8", "replace")):
        rows.append({
            "line": int(number),
            "kind": ("surah_name" if "surah-name" in classes
                     else "basmallah" if "bismillah" in classes or "basmallah" in classes
                     else "ayah"),
            "centered": "center" in classes,
            "words": WORD_PATTERN.findall(segment),
        })
    if not rows:
        raise ValueError("no lines in the page")
    return json.dumps(rows, ensure_ascii=False).encode("utf-8")


def main():
    parser = argparse.ArgumentParser(description="Fetch the mushaf page-layout assets.")
    parser.add_argument("--out", required=True, type=Path, help="directory to fill")
    parser.add_argument(
        "--variant",
        default="v2",
        choices=("v1-optimized", "v2", "v4"),
        help="which QCF print to fetch fonts for: v2 is the 1421H Madinah mushaf "
             "(the familiar one, and the heaviest), v4 the 1441H print, "
             "v1-optimized the 1405H print (default: v2)",
    )
    arguments = parser.parse_args()

    fetch("page layout", arguments.out / "pages", lambda p: f"{p:03d}.json",
          lambda p: LINES_URL.format(page=p), as_page, 20)
    fetch("word text", arguments.out / "layout-text", lambda p: f"{p:03d}.json",
          lambda p: WORDS_URL.format(page=p), as_json, 200)
    fetch("fonts", arguments.out / "fonts", lambda p: f"p{p:03d}.woff2",
          lambda p: FONT_URL.format(variant=arguments.variant, page=p), lambda b: b, 2000)

    print("\nNote: the layout fetched here is the V2 (1421H) print. Fetching another "
          "variant's fonts without its own layout would draw page 42's words in page "
          "42's shapes but break the lines in the wrong places.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
