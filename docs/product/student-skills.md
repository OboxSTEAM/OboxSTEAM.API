# Student Skills

## Summary

Students accumulate STEAM and cross-cutting skills as proficiency snapshots
backed by optional evidence (submissions, certificates, media).

## Catalog (`Skill`)

Shared taxonomy entries with:

- `Code` (unique)
- `Name`
- `Category`: Science | Technology | Engineering | Arts | Math | SoftSkill
- Optional `Subcategory` and `Description`

Catalog seed and admin CRUD are not in the schema-only slice.

## Student snapshot (`StudentSkill`)

One active row per `(StudentId, SkillId)` (soft-delete filtered unique index):

| Field | Meaning |
| --- | --- |
| `ProficiencyLevel` | Beginner, Intermediate, Advanced, Expert |
| `Source` | Manual, Llm, Mentor, System |
| `ConfidenceScore` | Optional 0–1 when assessed by LLM/system |
| `LastAssessedAt` | Last assessment time (`CreatedAt` = first recorded) |
| `VerifiedBy` / `VerifiedAt` | Mentor confirmation |
| `EvidenceSummary` / `Reasoning` | Short human- or model-readable notes |

## Evidence (`StudentSkillEvidence`)

Links a snapshot to one or more of:

- `Submission`
- `Certificate`
- `MediaAsset`

Application services must require at least one FK when creating evidence.

## Out of scope (current)

- REST endpoints
- Automatic LLM assessment
- Seed of the full STEAM skill list
