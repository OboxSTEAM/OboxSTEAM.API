# Overview

## Current Behavior

`MentorSkill` stores `MentorId`, `SkillId`, `ProficiencyLevel`, and optional
`Notes`. Mentor profile APIs expose that thin link. There is no years /
description field, no structured evidence, and no mentor-controlled public flag.

## Target Behavior (this story slice — schema)

- Enrich `MentorSkill` with `YearsOfExperience`, `Description`, `IsPublic`.
- Add `MentorSkillEvidence` child entity for certificates / credentials.
- Wire DbContext, soft-delete filters, relationships, and EF migration.
- Product contract: mentors self-manage skills; no verification fields.

API, seed enrichment, and unit-test expansion follow in a later story slice.

## Affected Users

- Mentor (future self-service profile)
- Manager / Admin (future staffing views)
- Student (future public skill visibility)

## Affected Product Docs

- `docs/product/mentor-skills.md` (new)
- `docs/product/permissions.md`
- `docs/product/overview.md`
- `docs/decisions/0008-mentor-self-managed-skill-profiles.md`

## Non-Goals (this slice)

- REST endpoint / DTO / service changes
- Seed data enrichment
- Unit tests for new API behavior
- Manager verification workflow
