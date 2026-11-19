# AGENTS.md — working conventions for AI agents in this repository

StatusLight is maintained by an agent software factory: requirements arrive as
issues, an agent implements them on branches with tests, CI gates every PR.

## Factory loop

1. Issues are the requirements. Humans file them, or reviewing agents do; the
   executor may file clarifying issues.
2. One issue → one branch `issue-N-slug` → implementation + tests → PR → CI
   green → merge.
3. Merge gate: **tests pass + builds** — the executing agent merges
   autonomously (owner delegation, 2026-11-19). Human review rounds happen
   when the owner chooses to review.
4. Python code is reviewed by an independent agent reviewer before merge.
5. `RUNLOG.md` records every run: request, steps, timings, defects found by
   tests and by review.

## Repository mechanics (Windows)

- GitHub account: **tempometer**, via the isolated-profile wrapper
  `~/.zcode/cli/scripts/gh-t.sh` (the shared `gh` knows nothing about this
  account; never `gh auth login` a second account into the shared profile).
- Git identity is LOCAL to the clone `C:\projects\statuslight`:
  `tempometer <325643706+tempometer@users.noreply.github.com>`, plus a local
  credential helper that resets the system GCM and routes through `gh-t`
  (without the reset, pushes go out as the personal account → 403).
- Line endings: LF (`.gitattributes` from bootstrap); never run repo-wide
  formatters on Windows (CRLF noise).

## Project direction (owner decisions, 2026-11-19)

- StatusLight is a **format**: the JSON Schema contract
  (`schema/statuslight.schema.json`) plus renderers that produce byte-identical
  output.
- **Python is the primary renderer** (first consumer: OCL, a Python ops
  project). The .NET CLI is the frozen reference renderer that produced the
  conformance goldens; it catches up per-feature when a .NET client appears.
- PyPI publication requires an explicit owner "go" (separate account decision).
- Feature backlog drafts live with the owner; file issues just-in-time.
- Next milestone: issue #8 — Python renderer + conformance suite
  (goldens generated from .NET main, byte-identical output, same CLI flags,
  exit codes and one-line stderr errors).
