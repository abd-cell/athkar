# Preparing a mushaf package

The server stores the Qur'an as **one prepared SQLite file**, not as a table of
verses (`docs/BUSINESS_LOGIC.md` §7.1). This directory builds that file.

`build_quran_package.py` joins a verified text with Tanzil's structural metadata
and writes exactly the schema `app/athkar_app/lib/core/quran_library.dart`
reads — nothing more, so a package never carries tables no one queries.

## 1. Get the text

Take it from a source that is actually verified. The app's one distinguishing
claim is that what is on screen is traceable; a mushaf assembled from a random
JSON on the internet would break that claim more thoroughly than any dhikr
could.

| Source | What it gives you | Notes |
|---|---|---|
| **مجمع الملك فهد** — `qurancomplex.gov.sa` | The reference Uthmani orthography of the Madinah mushaf, plus the KFGQPC fonts | The authority. Use it when you can. |
| **Tanzil** — `tanzil.net/download` | `quran-uthmani.txt` in `sura|aya|text` form, and `quran-data.xml` | What this script is written against. Licence: redistribute unmodified, with attribution. |
| **QUL** — `qul.tarteel.ai` | Ready-made SQLite corpora, Madinah page numbers, fonts | Useful as a cross-check for `page`/`juz`. |

Take the **`quran-uthmani`** download, not `quran-simple`: `simple` is the
imla'i spelling, with no waqf marks and no Uthmani orthography. The build
refuses it rather than producing a package whose script does not match the one
declared on upload.

Two files are needed:

- the text, either as Tanzil's plain-text export (`1|1|بِسْمِ ٱللَّهِ ...`) or as
  its `.sql` dump — the script reads both and produces the same bytes;
- `quran-data.xml`, Tanzil's metadata — sura index, the 30 juz starts and the
  604 page starts. Fetch it from `tanzil.net/res/text/metadata/quran-data.xml`.

If you start from the King Fahd Complex text instead, reshape it into the same
`sura|aya|text` lines and keep using Tanzil's metadata for the structure.

## 2. Build

```bash
python tools/quran-package/build_quran_package.py \
  --text quran-uthmani.txt \
  --metadata quran-data.xml \
  --out mushaf-uthmani-v1.db
```

Useful flags:

- `--strip-basmalah` — some Tanzil exports prepend the basmalah to verse 1 of
  every sura but 1 and 9. Strip it if the reader draws the basmalah as a heading;
  keep it if the reader does not. The script says which case your file is in.
- `--no-waqf` — write only `surahs` and `ayahs`. The glyphs still render (they
  are characters in the verse); the tappable explanation sheet simply is not
  offered, and the package is uploaded with `HasWaqfAnnotations = false`.
- `--waqf-types path.json` — a different glyph → ruling table. The default,
  `waqf_types.json`, covers the six marks of the Madinah mushaf.

The build refuses rather than producing something subtly wrong: a missing verse,
a duplicate, a verse the metadata doesn't know, a total that isn't 6236 /
114 / 604 / 30, or a waqf glyph with no ruling all abort it. The app is
defensive by design — it shows an empty index rather than crashing — so a broken
package is silent on a phone. It must not be silent here.

On success it prints the size and the **sha256**. The server computes the same
digest as it writes the upload and deletes the file on a mismatch; compare the
two.

## 3. Upload and publish

The upload accepts `.db`, `.sqlite`, `.sqlite3` and `.zip` only — handing it a
`.sql` dump or a text file comes back as `UnsupportedFileType` (errorCode 8).
What you upload is the built package, never the source corpus.

Upload in the CMS (`POST /api/v1/admin/quran`), choosing the script
(`Uthmani` / `IndoPak` / `Naskh`) and whether the file carries waqf annotations.
Then publish — a separate step on purpose, so a large file is transferred and
verified before a million installs are told about it.

**Versions are immutable and never reused, even after a delete.** An installed
app compares against a number; handing out different bytes under a number it
already holds is the one failure the protocol cannot detect.

## What gets written

```sql
surahs(id, name_ar, name_en, ayah_count, revelation_place)   -- makkah | madinah
ayahs(id, surah_id, ayah_number, text_uthmani, page, juz)    -- id is 1..6236
waqf_marks(id, surah_id, ayah_number, word_index, symbol)    -- optional
waqf_types(symbol, name_ar, ruling_ar)                       -- optional
```

`word_index` counts the words *before* the mark, so a mark attached to the
second word of a verse is `1`, and one opening a verse is `0`.

## 4. The printed-page layer (optional)

The steps above build a mushaf that can be *read*. To show it as it is
*printed* — 604 pages of 15 lines, broken where the press broke them — add the
page layer:

```bash
python tools/quran-package/fetch_mushaf_assets.py --out ./assets
python tools/quran-package/add_page_layout.py --package mushaf-uthmani-v1.db   --assets ./assets --script digitalkhatt --font DigitalKhattV2.otf   --out mushaf-pages-v1.db
```

`fetch_mushaf_assets.py` takes the page grid from QUL's public layout preview
and the word text from quran.com, and never the other way round: both can say
which page a word is on and they disagree — quran.com puts 5:90 on page 123,
where the 1421H print begins that page at 5:91.

Two ways to draw it:

| `--script` | Font | Package | Fidelity |
|---|---|---|---|
| `digitalkhatt` | one, ~0.5 MB (`qul.tarteel.ai/resources/font/247`) | ~8 MB | exact line breaks; justified by stretching the spaces |
| `qcf` | 604, one per page | ~160 MB | letter-perfect: a glyph *is* a word as drawn on that page |

The build reconciles the two sources' word counts per verse rather than by a
blanket rule, because they differ in both directions: «بَعْدَ مَا» is one token
in the text and two words in the layout (2:181, 8:6, 13:37), while «إِلْ
يَاسِينَ» is one in both (37:130). It then rewrites `ayahs.text_uthmani` and
`waqf_marks` from the page words, so one package never carries two spellings of
the same verse.

Known gap: DigitalKhatt has no glyph for U+06EA (11:41) or U+06EB (12:11), two
recitation marks that occur once each; the device falls back to another face for
them. The QCF fonts cover both.
