# 0009 Adopt Harness Repository Protocol 0.1.10

Date: 2026-08-25

## Status

Accepted

## Context

This consumer installed repository-harness core **0.1.10**. Upstream ended
protocol v1 (SQLite `harness-cli`, intake/story/trace tables) on 2026-08-10.
Local docs still described `harness-cli query matrix`, `FEATURE_INTAKE`, and
related maturity surfaces, which conflicted with the installed binary and
confused agents.

## Decision

1. Day-to-day agent workflow follows `docs/WORKFLOW.md`, `docs/product/*`,
   `docs/plans/`, and executable proof (`dotnet test` / `dotnet build`).
2. Maintain the core with `scripts/bin/harness.exe` (`status`, `doctor`,
   `update`). Do not bootstrap or depend on `harness.db` for new work.
3. Remove obsolete protocol-v1 operating docs from the active doc tree rather
   than archiving them in-repo. Historical decisions `0001`–`0005` remain as
   superseded history; do not treat them as current protocol.
4. Optional story packets stay markdown under `docs/stories/`; they are not
   mirrored into a Harness CLI database.

## Consequences

- Agents read fewer conflicting process docs.
- Multi-session work uses `docs/plans/active/` instead of SQLite stories.
- Product proof claims must cite tests or an explicit gap.
