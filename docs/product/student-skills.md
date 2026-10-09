# Student Skills

## Summary

A shared STEAM `Skill` catalog backs program, class, mentor, and student
skills. Programs declare the skills they teach (`ProgramSkill`). A student's
achieved skills are computed at read time from completed programs, issued
certificates, and capstone portfolio items, and are curated on the student
portfolio. The `StudentSkill` / `StudentSkillEvidence` snapshot tables exist in
the schema but no application code reads or writes them yet.

## Catalog (`Skill`)

Shared taxonomy entries with:

- `Code` (max 50, unique index across all rows, including soft-deleted)
- `Name` (max 255)
- `Category`: Science | Technology | Engineering | Arts | Math | SoftSkill
- Optional `Subcategory` (max 100) and `Description`

Seeded via `SeedService.SeedSkillsAsync` (adds catalog codes not already
present among non-deleted rows) as part of `SeedAllDataAsync`.

Endpoints (`SkillController`):

| Route | Roles | Behavior |
| --- | --- | --- |
| `GET /api/skills` | Mentor, Manager, Admin | Paged catalog without soft-deleted rows. Query: `search` (code, name, subcategory), `category`, `page` (>= 1), `pageSize` (1–100, default 50), `sortBy` (`name` default, `code`, `category`, `createdAt`), `isDescending`. |
| `POST /api/skills` | Manager | Creates a row from `code`, `name`, `category`, optional `subcategory` and `description` (trimmed). Duplicate `code` (case-insensitive, including soft-deleted rows) returns 409. Returns 201. |

There are no update or delete endpoints for catalog rows. Admin is not allowed
on `POST /api/skills`.

Module `LearningOutcomes` stay as free-text on `Module`.

## Program skills (`ProgramSkill`)

Links a program to catalog skills. One active row per `(ProgramId, SkillId)`
(soft-delete filtered unique index). Optional `ModuleId` scopes a link to one
module; the API never sets it (null means the whole program).

- Set through `skillIds` on program create (omitted or null means none) and
  update (null leaves links unchanged; an empty list clears them). Ids must be
  non-empty, distinct, and reference non-deleted skills (400 otherwise).
- On update, a change to the skill set is subject to the same curriculum edit
  guard as other curriculum fields (`CurriculumEditGuard`).
- Program list, detail, and curriculum responses include `skills`; the program
  list filter `skillsGained` matches catalog skill name or code.
- Certificates list the program's skill names (`SkillsAcquired`).
- Seeded via `SeedService.SeedProgramSkillsAsync` (idempotent per pair).

## Achieved skills (portfolio)

`PortfolioSkillCoordinator` computes a student's achieved skills from
`ProgramSkill` links:

| Evidence (`SkillEvidenceType`) | Grants |
| --- | --- |
| `Program` | Every skill of a program whose `ProgramEnrollment` is `Completed` |
| `Certificate` | Program-wide skills, plus module-scoped skills matching the certificate's module |
| `Capstone` | Program-wide skills, plus module-scoped skills matching the capstone portfolio item's module |

Curation is stored per portfolio in `PortfolioSkill` (`IsVisible` default
true, `IsPinned`, `DisplayOrder`; unique per `(PortfolioId, SkillId)`). New
achieved skills are appended automatically.

- `PUT /api/portfolios/me/skills` (Student) replaces curation. The request
  must list every achieved skill exactly once and nothing else; `DisplayOrder`
  cannot be negative; at most 6 skills can be pinned.
- `POST /api/portfolios/me/sync` (Student) refreshes achieved skills without
  dropping curation.
- Public portfolio responses omit hidden skills and evidence `programId`.

## Student snapshot (`StudentSkill`, schema only)

One active row per `(StudentId, SkillId)` (soft-delete filtered unique index):

| Field | Meaning |
| --- | --- |
| `ProficiencyLevel` | Beginner, Intermediate, Advanced, Expert (default Beginner) |
| `Source` | Manual, Llm, Mentor, System (default Manual) |
| `ConfidenceScore` | Optional 0–1 when assessed by LLM/system |
| `LastAssessedAt` | Last assessment time (`CreatedAt` = first recorded) |
| `VerifiedBy` / `VerifiedAt` | Mentor confirmation (`VerifiedBy` set null if the verifier is deleted) |
| `EvidenceSummary` / `Reasoning` | Short human- or model-readable notes |

## Snapshot evidence (`StudentSkillEvidence`, schema only)

Links a snapshot to one or more of `Submission`, `Certificate`, `MediaAsset`.
Each FK is unique per snapshot among active rows. The entity contract says at
least one FK must be set, enforced in the application layer; no service
creates these rows yet.

## Out of scope (current)

- REST endpoints for `StudentSkill` / `StudentSkillEvidence`
- Automatic LLM assessment
- Durable LearningOutcome → Skill join table
- Seeding per-student `StudentSkill` rows
