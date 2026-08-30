# Execution Plan: Class Assignment Windows

Date: 2026-08-31

## Status

Complete (follow-up 2026-08-31)

## Outcome

Quiz, file, retrospective, and research new attempts use the student’s class
`ClassSession` AssignmentWindow (`StartTime` / `EndTime`). Catalog `Assignment`
has no calendar dates. Recovery grants extra attempts only, usable while that
window is open. Mentors may PUT the window times for their class.

## Context

- Decision: `docs/decisions/0011-class-assignment-windows.md`.
- Product: `docs/product/assessment.md`, `curriculum.md`, `enrollment.md`.
- Navigation used Grep/Read because CodeGraph MCP is unavailable in this session.

## Scope

In scope:

- Drop catalog dates on `Assignment` and personal dates on recovery.
- Unique AssignmentWindow per `(ClassId, AssignmentId)` plus backfill.
- Resolve helper, attempt validators, curriculum/parent, mentor PUT, overlap skip.
- Seed, tests, EF migration via CLI.

Out of scope:

- Two-day gap rule or generate rewrite to place windows between live slots.
- New `AssignmentWindow` entity.
- FE repository.

## Approach

1. Authority docs (this file + 0011 + product).
2. Domain + DbContext unique index; generate migration; insert backfill SQL
   before dropping columns.
3. `AssignmentWindowPolicy` + rewire readers; recovery extra-attempts only.
4. Mentor PUT AssignmentWindow times; skip mentor overlap for those sessions.
5. Tests and `dotnet test`.

## Risks And Recovery

- Unique index fails if a class already has two active windows for one assignment
  — backfill skips existing windows; create/generate already reject duplicates.
- Rollback: revert the migration (restores columns; backfilled sessions remain).

## Progress

- [x] Decision and product docs
- [x] Schema, unique index, backfill migration
- [x] Window policy, overlap, generate uniqueness
- [x] Mentor PUT
- [x] Attempt / curriculum / parent / recovery rewire
- [x] Tests
- [x] Follow-up: in-progress continues after close; research milestone curriculum lock;
  curriculum/parent calendar status; PE-scoped class resolve; AssignmentWindow
  `RequiresAttendance` forced false

## Decisions

- 2026-08-31: Recovery is extra attempts only (no personal deadline).
- 2026-08-31: No two-day rule; generate keeps weekly slots.
- 2026-08-31: Unique index on `ClassSessions`, not a new table.
- 2026-08-31: New attempts require an open class window; in-progress drafts continue until submit.
- 2026-08-31: Resolve the student’s class from the program enrollment seat; do not steal another class’s window.
- 2026-08-31: Research milestone mutations use `CurriculumEditGuard` (same as assignments). Do not auto-create windows.

## Validation

- Focused proof: unit tests for window resolve, uniqueness, overlap skip,
  mentor PUT, recovery, research milestone DTOs, in-progress after close,
  PE-scoped class, curriculum calendar lock, AssignmentWindow attendance,
  research milestone curriculum lock.
- Repository-required checks: `dotnet test OboxSteam.Test/OboxSteam.Test.csproj`

## Result

Implemented. Proof: `dotnet test OboxSteam.Test/OboxSteam.Test.csproj` — 1702 passed, 0 failed.

Migration `20260830191832_AssignmentWindowPerClass` backfills Standard-class AssignmentWindow rows from catalog dates, then drops `Assignments.DueDate` / `AvailableFrom` / `AvailableUntil` and recovery personal dates, then creates the filtered unique index.

Follow-up: in-progress drafts continue after `EndTime`; research milestone mutations use `CurriculumEditGuard`; curriculum/parent nav uses the class window; resolve uses the program-enrollment class seat; AssignmentWindow `RequiresAttendance` is always false. Navigation used Grep/Read because CodeGraph MCP is unavailable.
