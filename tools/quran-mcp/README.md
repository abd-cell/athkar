# quran-mcp — canonical Qur'an data for Athkari

Everything under `data/` was fetched from the **quran.ai MCP server**
(`https://mcp.quran.ai`, upstream quran.com). Nothing here is typed from model
memory — that is the point of the server, and it is the same rule the project's
publish gate already enforces: an item on screen must trace to a source.

Re-pull with:

```bash
python tools/quran-mcp/pull.py            # everything
python tools/quran-mcp/pull.py athkar     # one step
```

Steps: `docs editions meta quran translation mushaf athkar`.

## What is in `data/`

| File | Rows | Size |
|---|---|---|
| `surahs.json` | 114 surahs — name, revelation place/order, verses, juz, page, sajdah | 40 KB |
| `quran/ar-uthmani-minimal.json` | 6236 ayat, full Uthmani orthography — **display text** | 1.4 MB |
| `quran/ar-uthmani.json` | 6236 ayat, undiacritised — **search text** | 806 KB |
| `translations/en-sahih-international.json` | 6236 ayat | 1.0 MB |
| `mushaf/pages.json` | 604 pages, 83 665 words with line numbers and QCF glyphs | 22 MB |
| `athkar_quranic.json` | 14 Qur'anic adhkar blocks, 124 ayat, Arabic + search + English | 68 KB |
| `editions.json` | catalogue: 7 Qur'an, 31 translation, 14 tafsir editions | 24 KB |
| `grounding_rules.md`, `skill_guide.md` | the server's own usage contract | 56 KB |

## Three things worth knowing before you use it

- **`ar-uthmani` is *not* the Uthmani script.** It comes back undiacritised
  (`الله لا إله إلا هو`); `ar-uthmani-minimal` is the one with real Uthmani
  orthography (`ٱللَّهُ لَآ إِلَٰهَ إِلَّا هُوَ`). The plain one happens to be a good
  match for `Dhikr.SearchText`, since `ArabicText` folding would strip the
  diacritics anyway — but it is a trap if you reach for it as display text.
- **`fetch_*` paginates silently.** A long surah returns truncated with a
  `continuation` token and no error. Ignoring it costs 1413 ayat and drops
  آية الكرسي on the floor. `_fetch_edition` follows the token; anything new
  calling these tools must too.
- **The English text carries `<sup foot_note="…">` markup.** Strip or render it
  before it reaches a screen.

## What this server does *not* have

It is Qur'an-only: no hadith, so **no hadith-narrated adhkar** — أذكار الصباح
والمساء, أذكار النوم, the du'as after salah. Those keep their existing sourcing
path through the CMS. `athkar_quranic.json` covers exactly the slice that *is*
Qur'an.

## Where this data is used

- `data/athkar_quranic.json` + `data/surahs.json` are copied into
  `backend/Athkar/DataAccess/Seeders/Data/` and read by `QuranicAthkarCatalog`,
  which both `QuranicAthkarSeeder` (fresh database) and `QuranAthkarSyncService`
  (the admin's *مزامنة الأذكار القرآنية* button) work from. Re-running this
  script and copying those two files is how the seeded content is corrected.
- The admin sync talks to the MCP server live, over the same JSON-RPC-over-SSE
  transport this script uses. It is off unless `QuranMcp:Enabled` is true.

### Two schema limits these blocks run into

Whole surahs are longer than the adhkar content model was shaped for, so the
seeder and the sync both trim before writing:

- **`Dhikr.SearchText` can only index 850 characters.** SQL Server caps a
  nonclustered index key at 1700 bytes and does not complain when the index is
  created — it complains on the INSERT, as error 1946. Al-Mulk and as-Sajdah are
  each thirty ayat and blew straight past it, which threw inside startup and
  stopped the API from coming up at all. Search still matches everything up to
  the cut.
- **`TranslationInput.Title` is capped at 500 characters** while the column
  behind it takes 4000. That cap is the CMS's, not the database's, and it
  predates this work — but it means a long dhikr (the seeded hadith ones
  included) cannot be re-saved through the content screen once it exists.

## Building the mushaf package

`build_package.py` turns the pull into the SQLite package the app downloads
(`docs/BUSINESS_LOGIC.md` §7) — the same schema `tools/quran-package/` writes,
but from one source instead of three, so there is no second corpus whose word
counts have to be reconciled against the first.

```bash
python tools/quran-mcp/build_package.py --out mushaf-mcp-v1.db
python tools/quran-mcp/build_package.py --out mushaf-qcf-v1.db --script qcf --fetch-fonts
```

| `--script` | Words drawn as | Size | Fidelity |
|---|---|---|---|
| `text` (default) | ordinary Unicode, in the app's own face | **5.7 MB** | exact line breaks, Amiri letterforms |
| `qcf` | the page's own glyph codes | **157 MB** | letter-perfect — a glyph *is* a word as drawn on that page |

Both write 114 surahs, 6236 ayat, 604 pages × 15 lines, 83,665 words and 4272
waqf marks, and both refuse rather than write something subtly wrong: a missing
page, a word pointing at no ayah, a total that is not 6236 / 114 / 604 / 30.

### The one thing that is not from the MCP

The QCF **outlines**. The server serves the glyph *code* for every word
(`glyph_text`) but has no endpoint for the fonts, so `--fetch-fonts` pulls the
604 page fonts from the same CDN `tools/quran-package/fetch_mushaf_assets.py`
already uses, and converts each woff2 to TTF — Flutter's `FontLoader` takes TTF
and OTF, not woff2.

That the MCP's layout is the **V2 (1421H)** print is verified rather than
assumed: page 1's 36 glyph codes land on exactly the 36 glyphs in that print's
page-1 font, and a 20-page sample covers every glyph. Pairing this layout with
another variant's outlines would draw page 42's words in another print's shapes.

### Known gap

`lines.is_centered` is inferred, not known — it is a fact of the print that no
API here reports. Headings and basmalah lines are centred; ayah lines are not,
which is right everywhere except the short line that ends a surah.

## Notes on wiring it into the app

`mushaf/pages.json` is 22 MB and its `glyph_text` fields need the matching QCF
page fonts to render — ship it as an asset/download, not in the API payload,
and remember `AppConfiguration.ContentVersion` governs anything that syncs.
