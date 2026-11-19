# RUNLOG — agent factory execution log

Every production run of the StatusLight agent factory: the request (issue), the
execution (branch, PR, CI), the timings, and what the human actually did.

| Run | Issue | Branch | PR | Tests | CI | Wall clock | Human input |
|-----|-------|--------|-----|-------|----|------------|-------------|
| 1 | [#1](https://github.com/tempometer/statuslight/issues/1) MVP: YAML → HTML board | `issue-1-mvp-html-board` | [#2](https://github.com/tempometer/statuslight/pull/2) | 33/33 (CI) | ✅ green (3 runs) | ≈5 min to green CI; ≈2 h with 2 review rounds | wrote issue; 2 review rounds; merge approval |
| 2 | [#5](https://github.com/tempometer/statuslight/issues/5) Boards v2 | `issue-5-boards-v2` | [#6](https://github.com/tempometer/statuslight/pull/6) | 89/89 (CI) | ✅ green (4 runs) | issue→CI ≈7 min; 2 review rounds +1 day | issue in chat; 2 review rounds (10+10 findings, all fixed); merge delegated ("tests+build") |

## Run 1 — 2026-11-13, issue #1 (MVP)

- **Request**: issue #1 — YAML status file → self-contained HTML board (schema,
  exit codes, stderr diagnostics, HTML escaping, xUnit tests, CI).
- **Executed by**: AI agent (ZCode session, GLM model), on branch
  `issue-1-mvp-html-board`.
- **Started**: 2026-11-13 20:04 UTC.
- **Steps**: scaffold solution (net10.0, .slnx, xUnit) → domain model →
  validated YAML loader (`BoardFile`, all failures as `BoardFileException` with
  path + reason) → self-contained HTML renderer with dark-mode support → CLI
  (`--input/--output`, clean stderr errors, exit 0/1) → 19 unit tests →
  CI workflow (build + test on push/PR) → docs: README quickstart, example
  input, this log.
- **Notes**: SDK 10 creates `.slnx` (new XML solution format) instead of
  `.sln`; kept as the modern default, CI pins .NET SDK 10.0.x.
- **Local verification before push**: `dotnet test` — all green (19/19).
- **CI result**: [run 43498806775](https://github.com/tempometer/statuslight/actions/runs/43498806775) —
  build + test **green in 21 s** (first-ever CI run, includes restore).
- **Timeline (UTC)**: issue ~20:03 → run start 20:04:24 → implementation pushed 20:08 →
  CI green ~20:09. **Issue → green CI ≈ 5 minutes.**
- **Review round** (same day): human review in PR #2 — **10 inline comments**
  (I/O exception-filter gaps on both sides, flag-without-value and flag-like
  values, silently dropped unknown YAML keys, multi-document input accepted,
  no same-path guard / non-atomic write, state conveyed by colour alone,
  missing viewport meta + collapsed multi-line notes, redundant filters,
  no tests for `Main`, stale README status). **All 10 fixed** in the review-fix
  commit: strict schema + document count, broad I/O catches, value-aware arg
  parsing, same-path rejection, temp-file+rename write, text state labels,
  viewport + `pre-line` notes, README updated; suite 19 → **32 tests**
  (CLI-level tests for `Main` added). Reviewer failure scenarios re-run
  end-to-end — all produce one-line stderr + exit 1.
- **Re-review** (human, 22:04 UTC): all 10 comments confirmed fixed ("good to
  merge"); 4 non-blocking nits left. Disposition: 2 nits fixed pre-merge
  (usage note for `-`-prefixed values; `(empty)` label and target-path naming
  in write diagnostics — 33 tests now), 2 filed as follow-up issues
  (human-readable unknown-key error; symlink/hardlink same-file detection).
- **Merge**: **squash-merged as 5ceac9b at 22:10 UTC** (issue #1 auto-closed),
  authorized by the human re-review. Follow-ups filed: #3 (human-readable
  unknown-key error), #4 (symlink/hardlink same-file detection).

## Run 2 — 2026-11-18, issue #5 (Boards v2)

- **Request**: issue #5 — universal traffic-light board: sections as cards,
  custom state vocabulary (labels, ranks, colours), fact lines with tags,
  links, queue/closed lanes, per-card `updated` with client-side staleness,
  light/dark/auto theme; v0.1 flat schema stays byte-compatible.
- **Executed by**: AI agent (ZCode session, GLM model), branch
  `issue-5-boards-v2`. **Started**: 2026-11-18 20:13 UTC.
- **Steps**: model (`StateInfo`/`Section`/`Board` with legacy constructor) →
  v2 loader (states vocabulary, section/item/link/queue/closed validation,
  rollup = worst item rank) → cards renderer (grid, tag chips, next steps,
  links, queue/closed lanes, age + `⚠ stale` script, themed palettes) →
  36 new tests (33 → **69/69**) → `examples/traffic-board.yml` + README.
- **Mid-run fixes found by own tests**: lost v0.1 case-insensitive states in
  the new `Resolve` (restored); tag-requires-note must precede the
  name-or-note check; C# interpolated raw strings reject `{{` escapes
  (block-style YAML in tests instead).
- **Local verification before push**: 69/69 tests; live render of
  `examples/traffic-board.yml` — rollup correct (mixed cards → worst state),
  queue/closed lanes present, `data-upd`/`data-stale-after` wired.
- **CI result**: green across all four runs (21–51 s).
- **Timeline (UTC)**: issue 18.11 20:13 → CI green 20:20; two review rounds fixed same day and next; **merged 19.11 20:56** (squash 6c81432, issue #5 auto-closed).
- **Merge gate changed by owner (19.11)**: tests pass + builds → agent merges autonomously; final human review waived. Python code will be reviewed by an independent agent instead of the owner.

### Run 2 — review round (2026-11-18, same day)

- **Human review**: 10 inline comments (2 blockers first). All 10 verified
  against the code and fixed in the review-fix commit:
  1. `theme: auto` emitted broken CSS (bare vars in `@media` + stray `}`) —
     palette blocks rebuilt with proper `:root` selectors; test now asserts
     the media-block selector and balanced braces
  2. link schemes unvalidated — allowlist at load (http/https/mailto or
     relative); `javascript:`/`data:`/`vbscript:` rejected with a named error
  3. fact items dropped `name` — rendered as a bold lead-in; name-only facts OK
  4. `state`+`tag` items lost the tag — chip now rendered in the traffic row
  5. `updated` had no timezone — documented as UTC; renderer emits the exact
     ISO instant (`data-upd`) + display string (`data-disp`); future
     timestamps flagged `⚠` instead of clamped to `0h ago`
  6. stock rank/colour lookup was case-sensitive while `Resolve` was not —
     normalized
  7. rollup ties broke alphabetically with a crowded rank 0 — `StateInfo.Order`
     added; equal ranks tie-break by declaration order (best-first vocabulary)
  8. legacy flat boards silently ignored v2 keys — `board`/`queue`/`closed`
     top-level and per-item `tag` now rejected in the legacy path
  9. legacy list output had drifted (aria-hidden dropped, inline styles added,
     key instead of label) — restored byte-parity for stock states; custom
     states get label + inline colour only when their colour differs
  10. two run-1 review tests had been dropped in the suite rewrite — restored
      (`unknown key`, `multiple documents`) + a section-board variant
- Tests **69 → 82/82** locally; live checks: fixed `@media ... {:root{` CSS,
  ISO-Z `data-upd`, `javascript:` link rejected with exit 1.

### Run 2 — review round 2 (2026-11-19)

- **Human re-review at 497515d**: all ten round-1 findings confirmed fixed,
  "no blockers remain"; ten new, smaller findings (four marked fix-before-
  merge). All ten fixed in the round-2 commit:
  1. stock gray/closed rendered uncoloured in the legacy list (regression from
     the parity fix) — inline styles now apply to everything except the v0.1
     trio wearing stock colours; `IsPlainV01Stock` via `States.Stock`
  2. custom states without `rank` silently tied with green — `rank` now
     required for non-stock states
  3. staleness compared rounded hours (up to 30 min late) — the threshold now
     compares exact milliseconds; rounding is display-only
  4. README v2 snippet failed to load — `board: { title: ... }` added;
     snippet verified by loading it verbatim (exit 0)
  5. card grid overflowed 360px phones — `minmax(min(24rem,100%),1fr)`
  6. case-variant state keys could coexist and resolved arbitrarily —
     case-insensitive duplicates rejected at load
  7. schema routing keyed only on `sections` — now any v2 key (board/queue/
     closed) routes to the v2 loader; empty `sections:` reports the right
     error; stray legacy `title:` on a v2 board rejected
  8. `.inote` lacked `white-space: pre-line` — multi-line notes keep breaks
     in traffic rows
  9. third copy of the stock colour table — `States.Stock` is the single
     source; renderer keeps no local hex table
  10. `Updated` re-parsed in the renderer — `Section.UpdatedUtc` (DateTime,
      parsed once at load); renderer only formats, never parses
- Tests **82 → 89/89** locally. Live checks (reviewer read the code, did not
  run it): gray+stock inline in the legacy list, README snippet loads (exit 0),
  empty `sections:` error, rank-less custom state error.

| 3 | [#7](https://github.com/tempometer/statuslight/issues/7) JSON Schema | `issue-7-json-schema` | [#9](https://github.com/tempometer/statuslight/pull/9) | 98/98 (CI) | ✅ green, 41 s | ≈7 min (issue → merge, autonomous) | merge gate delegated (tests+build) |

## Run 3 — 2026-11-19, issue #7 (JSON Schema)

- **Context**: owner decision 19.11 — StatusLight becomes a multi-language
  format (Python primary, .NET reference renderer); the schema is the shared
  contract. Merge gate changed: tests pass + builds ⇒ agent merges autonomously.
- **Executed by**: AI agent, branch `issue-7-json-schema`, started 20:57 UTC.
- **Steps**: `schema/statuslight.schema.json` (draft 2020-12, oneOf v2|legacy,
  additionalProperties:false throughout, patterns for updated/date/color,
  theme enum, tag maxLength 16) → SchemaTests validating both examples and
  seven negative cases via JsonSchema.Net → README producer section.
- **Gotcha found by tests**: untyped YAML→JSON conversion loses scalar types
  (everything became strings, `integer` fields failed). Fixed with a
  YamlStream-based converter applying standard YAML implicit typing (plain
  scalars → number/bool/null; quoted → string) — matching what PyYAML's
  safe_load hands to jsonschema. The schema itself stays strict.
- **Local verification**: 98/98 (89 + 9 schema tests); CI green 41 s.
- **Merge**: autonomous per delegated gate — squash (merged 21:03 UTC), issue #7 auto-closed.
