# Execution Plan: Cohort Work Windows and Fail Exits

Date: 2026-08-31

## Status

Completed

## Outcome

AssignmentWindow is a multi-day work period generated between live sessions.
Required Theory and leftover Experiential/Research close when that window ends
(with a TurnedIn / in-progress hold). Research pass extends the next milestone
window. Attendance fail is 50%. Rebuy stays chuyen ca; the picker exposes a
credit hint. Optional assignments never AcademicFail.

## Context

- Decision: `docs/decisions/0012-cohort-work-windows-and-fail-exits.md` (amends 0010 and 0011).
- Product: `docs/product/curriculum.md`, `assessment.md`, `enrollment.md`, `overview.md`.
- Navigation used Grep/Read because CodeGraph MCP is unavailable in this session.

## Scope

In scope:

- Generate rewrite: weekly slots = Live/Offline; AssignmentWindow from related
  teaching EndTime to next live StartTime (min 48h, clamp to class end).
- Required-only AcademicFail; window-elapsed close including Theory; TurnedIn
  hold; hosted scan; research N+1 window bump.
- Absence 50%.
- ModuleFailed / enrollment copy as chuyen ca; RebuyClass credit hint.
- Tests and `dotnet test`.

Out of scope:

- FE repository.
- Migrating windows on classes that already have a timetable.
- Renaming `RetakeFee` or removing legacy redelivery.
- Copying student-passed modules the destination class has not taught.

## Approach

1. Authority docs (this file + 0012 + product).
2. Generate placement helper + ClassSessionService rewrite + generate tests.
3. ProgramPurchaseLifecycle close rules, scan hosted service, attempt-start hooks,
   research window extend.
4. Absence 50%, rebuy hint, notification copy.
5. `dotnet test`.

## Risks And Recovery

- Abandoned `Pending` draft with no `ExpiresAt` does **not** block AcademicFail.
  New attempts always set `ExpiresAt` from required `TimeLimitMinutes`.
- Generate fails if class end is before `Open+48h` after the last live —
  same class of error as today’s short date range.
- Rollback: revert the commit; no schema migration in this slice.

## Progress

- [x] Decision and product docs
- [x] Generate work windows
- [x] Fail exits, scan, research extend
- [x] Absence 50%
- [x] Rebuy chuyen ca copy and credit hint
- [x] Tests

## Decisions

- 2026-08-31: Related teaching = last live of course / milestone / module.
- 2026-08-31: Minimum window 48 hours, clamped to class end-of-day.
- 2026-08-31: Credit hint Copied / RedoWithClass / Ahead; eligibility unchanged.
- 2026-08-31: 2/5 absences is 40% and does **not** fail at 50%; 1/2 and 3/5 do.

## Validation

- Focused proof: generate placement, window-elapsed Theory close, optional
  no-close, TurnedIn hold, N+1 extend, absence 50%, rebuy credit hint.
- Repository-required checks: `dotnet test OboxSteam.Test/OboxSteam.Test.csproj`

## Result

Done. Generate places AssignmentWindow between related teaching and the next
live (min 48h). Required window-elapsed work AcademicFails (Theory included);
TurnedIn / in-progress drafts hold; optional assignments never close.
`AssignmentWindowCloseService` scans every 5 minutes. Research pass extends
N+1 when closed or under 48h. Absence fail is 50%. Rebuy catalog exposes
`CreditHint`. Navigation used Grep/Read (CodeGraph unavailable).

Proof: `dotnet test OboxSteam.Test/OboxSteam.Test.csproj` — 1720 passed.
