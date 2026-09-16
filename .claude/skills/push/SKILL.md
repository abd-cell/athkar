---
name: push
description: Commit the working tree and push it to the Athkari GitHub remote. Use when asked to push, commit, "save my work to GitHub", or ship the current changes — it carries the repo's secret-scan and generated-file rules so nothing large or private lands in a public history.
---

# Commit and push Athkari

Remote: `origin` → `https://github.com/abd-cell/athkar.git`, branch **`main`**.
The repo is public, so every step below that looks like bureaucracy is really
about the two things a public history cannot take back: a **secret** and a
**large generated file**.

---

## The sequence

```bash
git status --porcelain -uall | awk '{print $2}' | cut -d/ -f1 | sort | uniq -c | sort -rn
```

Look at the *shape* of the change first — a top-level directory you did not
expect (`graphify-out/`, `tools/.../fonts/`, a `build/`) means an ignore rule is
missing, not that you should commit it.

Then, in order:

1. **Scan for secrets** — see below. Non-negotiable; do it before `git add`.
2. **Scan for size** — see below.
3. `git add -A`
4. `git diff --cached --name-only | wc -l` — sanity-check the count against what
   you actually changed. A one-line fix that stages 300 files is an ignore bug.
5. Commit with a real message (below).
6. `git push` — or `git push -u origin main` if the branch is new.

---

## Scan for secrets

```bash
git status --porcelain -uall | awk '{print $2}' \
  | grep -iE 'secret|credential|password|\.key$|\.pem$|\.jks$|appsettings.*json|local\.properties|google-services|GoogleService-Info'
```

`.gitignore` already blocks the real ones — `athkar-notifications.json` (the
Firebase service-account key, which can push to every install), the Google
services files, `appsettings.Production.json`, `appsettings.Local.json`, `*.env`,
`*.jks`, `*.keystore`. Trust the list, but check any *new* match by eye.

**Already in the history, knowingly:** `backend/Athkar/appsettings.json` carries
the dev `Jwt:Secret`, and `CLAUDE.md` names the seeded admin login
(`admin@athkari.app` / `Athkari!2026`). Both are development values. If either is
ever reused in a deployment, rotating them is a separate job — do not quietly
re-commit a *new* real secret into that same file because "there's already one
there".

---

## Scan for size

```bash
git status --porcelain -uall | awk '{print $2}' | tr -d '"' \
  | while read f; do [ -f "$f" ] && echo "$(stat -c%s "$f") $f"; done | sort -rn | head
```

GitHub warns above 50 MB and rejects above 100 MB per file. The largest file
legitimately in the tree is `tools/quran-mcp/data/mushaf/pages.json` at 22 MB.

Three things are deliberately **not** in git because a script regenerates them:

| Not committed | How to get it back |
|---|---|
| `tools/quran-mcp/data/fonts/` (95 MB, 604 QCF page fonts) | `python tools/quran-package/fetch_mushaf_assets.py --script qcf` |
| `tools/quran-mcp/data/` generally | `python tools/quran-mcp/pull.py` |
| `graphify-out/` (18 MB graph + hashed AST cache) | rebuild with the `graphify` skill |

Plus the ordinary runtime droppings: `*.log` (`backend/Athkar/api.log` reaches
megabytes), `api.out`, `__pycache__/`, `bin/`, `obj/`, `node_modules/`, `dist/`,
`.dart_tool/`, `build/`.

If something large is genuinely new source, commit it. If a script can produce
it, add the ignore rule **with a comment saying which script** — that is the
convention the rest of `.gitignore` follows, and it is why a later reader knows
the file is missing on purpose.

---

## The commit message

Say what changed and why, not which files. Subject in the imperative, under ~70
chars; a body when the change has a reason that is not obvious from the diff.

Cross-stack changes deserve a note that they *are* cross-stack, because this repo
has pairs that must move together:

- `Shareds/Text/ArabicText.cs` ↔ `app/.../core/arabic_text.dart`
- `Shareds/Text/BidiText.cs` ↔ `app/.../core/bidi_text.dart`
- enum numbers in `Shareds/Enums` → `cms/.../core/api/models.ts` → `app/.../models/models.dart`
- i18n keys: `strings_ar.dart` **and** `strings_en.dart`; `i18n/ar.ts` **and** `i18n/en.ts`
- a new `ErrorCode` → the app's `error_messages.dart` + the CMS's `errorKey()`

Every commit ends with:

```
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```

Write it through a heredoc (`git commit -F -`), not `-m` — the messages here are
multi-line and often contain Arabic and em dashes that PowerShell quoting mangles.

---

## Gotchas seen on this machine

- **`git add` prints a wall of CRLF warnings.** `warning: in the working copy of
  'x', LF will be replaced by CRLF` — hundreds of lines, harmless, and enough to
  blow past the tool output limit. Pipe through `| tail`, or check the result
  with `git diff --cached --name-only` instead of reading the add output.
- **Run git through the Bash tool, not PowerShell.** The pipelines above use
  `awk`, `grep -E` and `while read`; PowerShell has none of that syntax and
  several cmdlets are broken on this box (see the `run-app` skill).
- **Don't commit while the API is running.** It holds `Athkar.exe` and locks
  `bin/` — which is ignored, so this only bites if you rebuild mid-commit.
- **Before pushing anything that touches content**, remember
  `AppConfiguration.ContentVersion`: a content change that does not bump it is an
  edit that never reaches a single reader. `docs/BUSINESS_LOGIC.md` is the
  authority.
