# Design system — «مخطوطة»

The visual language, ported from the design prototype (`أذكاري.html`, direction
`1a`). `app/athkar_app/lib/core/theme.dart` is the implementation and the single
source of truth in code; this file records the *reasoning*, which a token file
cannot.

The prototype remains the authority on any value that appears in both.

---

## The direction in one line

Parchment, not paper. Ink, not black. A deep manuscript green used sparingly,
Amiri for anything narrated, and hairlines instead of shadows.

It is meant to read as a printed book rather than as an app, because the content
is a printed book — and because the alternative, stock Material, would make the
Qur'an and a settings toggle look like the same kind of thing.

---

## Colour

Every value below is from the prototype. Where it used `oklch()` — which Flutter
has no equivalent for — the conversion was done once and the original kept in a
comment beside it in `theme.dart`.

### Light

| Token | Value | Used for |
|---|---|---|
| `paper` | `#F4ECE0` | the page |
| `surface` | `#FBF6EE` | cards, raised things |
| `ink` | `#241D16` | primary text |
| `muted` | `#6F6153` | labels, counts, attributions |
| `faint` | `#8C8171` | one step quieter again |
| `hairline` | `rgba(36,29,22,.12)` | rules inside a card |
| `border` | `rgba(36,29,22,.16)` | card and chip outlines |
| `brand` | `#2F5838` (`oklch(0.42 0.07 150)`) | the one accent |
| `brandInk` | `#244D2E` (`oklch(0.38 0.07 150)`) | brand text on a tint |
| `brandTint` | brand at 9% | the next-prayer strip |
| `onBrand` | `#FFFFFF` | on a solid brand fill |

### Dark

Not an inversion — a separate palette, because inverting parchment gives you
grey.

| Token | Value |
|---|---|
| `paper` | `#1B1814` |
| `readingPaper` | `#15130F` (the reading view sits a shade deeper) |
| `surface` | `#232019` |
| `ink` | `#ECE4D6` |
| `muted` | `#9A8F7F` |
| `faint` | `#6F6558` |
| `hairline` | `rgba(236,228,214,.12)` |
| `border` | `rgba(236,228,214,.18)` |
| `brand` | `#5D9669` (`oklch(0.62 0.09 150)`) |
| `brandInk` | `#8EC899` (`oklch(0.78 0.09 150)`) |
| `onBrand` | `#14120F` |

The green lightens in the dark palette rather than staying put: the same
`#2F5838` on near-black has almost no contrast, and a brand colour that cannot
be read is not a brand colour.

### The rule that keeps it coherent

**The brand green appears once per screen.** The "right now" card on the home
screen, the selected tab, the next-prayer strip — one of them, not all of them.
Everything else is ink on parchment. The moment a second thing goes green the
page stops having a subject.

---

## Type

Two families, and the split is the whole character of the direction:

- **Amiri** (serif) carries anything *narrated* — a dhikr, a hadith, a verse, a
  chapter heading, a greeting. It is the face this text has been set in for
  centuries.
- **IBM Plex Sans Arabic** carries the *interface* around it — labels, counts,
  buttons, settings.

A screen that sets a button in Amiri, or a dhikr in the sans, has lost the
distinction the design is built on.

| Role | Face | Size / line height |
|---|---|---|
| Greeting, screen title | Amiri 700 | 27 / 1.3 |
| Section heading | Amiri 700 | 17–22 |
| Dhikr in a card | Amiri 400 | 21 / 1.95 |
| Dhikr in a session | Amiri 400 | 26 / 2.1 |
| Continuous reading | Amiri 400, justified | 22 / 2.3 |
| Qur'an | Amiri 400, justified | 24 / 2.15 |
| Body / labels | Plex Sans 400 | 13 |
| Quiet label, takhrij | Plex Sans 400 | 11.5 |
| Button | Plex Sans 500–600 | 12.5–14 |
| A counter | Plex Sans 300 | 64–84 |

The line heights are large on purpose. Vocalised Arabic carries marks above and
below the baseline, and a comfortable-looking 1.5 collides.

**`fontScale` scales narrated text only.** The reader's size preference moves the
dhikr and leaves the interface where the design put it; scaling a button with it
just breaks the layout.

---

## Shape and space

| | |
|---|---|
| Page margin | 24 |
| Card radius | 18 (small card 14, button 12) |
| Chip / pill | 999 |
| Minimum tap target | **44** |
| Counter circle | 250 |
| Compass dial | 288 |

**No elevation anywhere.** Cards are outlined parchment, not floating paper; a
Material shadow under one breaks the whole look. `CardTheme.elevation` is 0 and
the outline does the work.

---

## Numerals

Arabic-Indic (`٠١٢٣`) by default in Arabic, Western in every other language, and
switchable either way.

This is a *presentation* choice and is applied at the edge, in
`core/numerals.dart`, when a value is rendered. Nothing in the app parses a
string it formatted.

The clock is 12-hour with no meridiem — «١٢:١٧», «٤:٥٢» — which is how prayer
times are read aloud and how every printed timetable sets them. The minutes are
padded; the hour is not.

---

## Screens the design specifies

From the prototype's artboards, all implemented:

| Artboard | Screen |
|---|---|
| 1a | Home — greeting, next prayer, the six times, "right now", today's reading |
| 1a | Session mode — one dhikr, the whole screen counts |
| 1a | Takhrij sheet |
| 2a | Profile — tally, streak, everything else |
| 3a | Widget editor with a live preview |
| 4a–4d | Rate, help topics, suggestions, contact |
| 5a–5b | Qur'an tab and a surah |
| 5c–5d | Chapter index, reading mode |
| 5e | Tasbih |
| 5f–5g | Prayer times, monthly table |
| 5h | Qibla |
| 5i | Reminders |
| 5j | Settings |
| 6a–6c | Theme choice, dark home, dark reading |
| 7a–7b | Calendar, sharing |

---

## Interaction notes worth keeping

These are design decisions, not implementation details, and changing them
changes the product:

- **The session screen is one big button.** Somebody saying a dhikr a hundred
  times is not looking at the screen and should not have to aim. The tap target
  is everything below the progress bar.
- **A completed count advances by itself**, so attention stays on the words.
- **The screen stays awake during a session.** A device that sleeps at
  thirty-three is the single most irritating way that screen can fail.
- **Haptics are confirmation, not decoration**: a light tick per count, a heavier
  one at the target, so a reader can keep their eyes closed.
- **Reset is confirmed.** A mistaken reset at ninety-eight cannot be undone.
- **The streak encourages and never reproaches.** It shows a current and a
  longest run, and nothing anywhere says "you broke it".
- **Empty states are designed, not placeholders.** No location set, no mushaf
  downloaded, nothing synced — each is a state most installs are in for most of
  their life.

---

## The CMS

Same materials at desk density (`cms/athkar-cp/src/styles.scss`). The brand
colour is a CSS custom property set at runtime from the platform configuration,
with the neighbouring shades *derived* (`core/services/brand-color.ts`) — an
admin choosing a colour should not also have to choose a hover state.

The foreground on a filled button is picked by luminance rather than by taste,
because a brand colour light enough to need dark text is a real possibility.
