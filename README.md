# StatusLight

Tiny .NET CLI: a YAML status file in — a **self-contained HTML traffic-light board** out.

This repository doubles as a **live demo of an agent software factory**:

- requirements arrive as **GitHub issues** (written by humans),
- an **AI agent** implements each one on a feature branch **with unit tests**,
- every pull request is **gated by CI** (build + tests),
- merges happen **only on green**,
- every factory run is recorded in [RUNLOG.md](RUNLOG.md) with timings.

> Honest by design: all feature code in this repository is written by AI agents.
> Humans own the requirements, the review, and the merge button.

## Quickstart

    dotnet run --project src/StatusLight -- --input examples/deploy.yml --output board.html

Input format ([examples/deploy.yml](examples/deploy.yml)):

```yaml
title: Deploy health
items:
  - name: API
    state: green        # green | yellow | red (case-insensitive)
    note: v2.4.1 live   # optional
```

Any schema violation, unknown key (typos included), unreadable file or
unwritable output produces a one-line `stderr` message and exit code 1 —
never a stack trace. The output is written via a temp file + rename, so a
failed write never destroys a previously good board.

## Boards: sections, custom states, staleness (v2)

The flat list above is the minimal (v0.1) schema — it keeps working unchanged.
The full board schema ([examples/traffic-board.yml](examples/traffic-board.yml))
renders a card grid in the spirit of an ops traffic board:

- **sections** — cards with name, "Full name — essence" subtitle, facts, next
  steps, waiting-on, link chips; card colour is the explicit `state` or the
  rollup (worst item state by rank)
- **custom state vocabulary** — `states:` defines keys, labels, severity ranks
  and `#RRGGBB` colours: `green/yellow/red` by default, add `gray` (parked),
  `closed` (done with verdict), or your own. Declare states best-first: equal
  ranks tie-break by declaration order, and the rollup takes the worst rank.
- **facts with tags** — items without a state render as fact lines; a `tag`
  (≤16 chars) renders as a chip, for substream labels
- **freshness as a first-class citizen** — every card carries `updated`
  (`YYYY-MM-DD HH:MM`, **UTC**, date AND time); the page emits the exact UTC
  instant so every viewer computes the same age, flags `⚠ future timestamp`
  instead of hiding it, and `⚠ stale` past `stale_after_hours` (default 48,
  per-card override)
- **link safety** — `links:` hrefs must be http/https/mailto or relative;
  `javascript:` / `data:` are rejected at load time
- **queue & closed lanes** — parked items and finished items with verdicts,
  so nothing disappears silently
- **theme** — `auto` (light/dark by preference), `light`, or `dark`

```yaml
states:
  green:  { label: "on plan" }
  yellow: { label: "needs decision", rank: 1 }
  red:    { label: "broken", rank: 2 }
sections:
  - name: Payments
    updated: "2026-11-18 09:12"
    items:
      - { name: API, state: green, note: v2.4.1 }
      - { tag: LATENCY, note: "p99 stable at 340ms" }
    next: [rotate keys, cut release 2.5]
```

Try it:

    dotnet run --project src/StatusLight -- --input examples/traffic-board.yml --output board.html

## Status

MVP delivered in [PR #2](https://github.com/tempometer/statuslight/pull/2) — the first
factory run: issue → agent implementation with tests → CI-gated PR (see
[RUNLOG.md](RUNLOG.md)). Further features arrive as issues.

## License

[MIT](LICENSE)
