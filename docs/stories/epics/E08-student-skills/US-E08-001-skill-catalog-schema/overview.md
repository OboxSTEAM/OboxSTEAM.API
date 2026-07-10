# Overview

## Current Behavior

`StudentSkill` stored free-text `SkillName` / `SkillType` and an integer
`ProficiencyLevel` (1–100) with no catalog, evidence, or verification fields.
No application services used the table yet.

## Target Behavior

- Shared `Skill` catalog (STEAM categories + soft skills).
- `StudentSkill` is a student–skill proficiency snapshot (4-tier enum).
- `StudentSkillEvidence` links snapshots to submission / certificate / media.
- Schema only in this story — no seed, API, or LLM assessment yet.

## Affected Users

- Future: Student, Mentor, Manager (portfolio / competency features).

## Affected Product Docs

- `docs/product/student-skills.md` (new)
- `docs/product/overview.md` (capability map link)

## Non-Goals

- Skill catalog seed data
- REST API / services
- LLM auto-assessment
- Historical assessment event log entity
