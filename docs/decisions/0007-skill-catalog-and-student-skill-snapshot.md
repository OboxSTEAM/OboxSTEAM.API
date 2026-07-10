# 0007 Skill Catalog And StudentSkill Snapshot

Date: 2026-07-10

## Status

Accepted

## Context

Automatic competency assessment and portfolio evidence need a shared STEAM skill
taxonomy. The original `StudentSkill` entity stored free-text skill names and an
integer 1–100 proficiency with no catalog, verification, or evidence links.

## Decision

Split into:

1. `Skill` — shared catalog (category + optional subcategory).
2. `StudentSkill` — per-student proficiency snapshot (4-tier enum, source,
   confidence, last assessed time, mentor verification).
   First recorded time uses `CreatedAt`; no separate `AcquiredAt`.
3. `StudentSkillEvidence` — links to submission / certificate / media.

Proficiency uses `Beginner | Intermediate | Advanced | Expert`. Schema-only in
the first slice; catalog seed and LLM assessment follow later.

## Alternatives Considered

1. Keep free-text `SkillName` on `StudentSkill` — rejected (duplicates, no taxonomy).
2. Integer 1–100 proficiency only — rejected in favor of explicit 4-tier enum.
3. Defer evidence to a later story — rejected; evidence join ships with schema.

## Consequences

Positive:

- Stable skill IDs for LLM mapping and portfolio.
- Evidence can back proficiency claims.

Tradeoffs:

- Legacy `StudentSkills` text columns are dropped without backfill.
- Application must enforce “at least one evidence FK” until a DB check exists.

## Follow-Up

- Seed STEAM skill list.
- API + LLM assessment services.
- Optional product doc updates for assessment flows.
