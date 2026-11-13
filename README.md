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

Any schema violation or unreadable file produces a one-line `stderr` message
and exit code 1 — never a stack trace.

## Status

Bootstrap in progress — see [issue #1](https://github.com/tempometer/statuslight/issues/1) for the MVP slice.

## License

[MIT](LICENSE)
