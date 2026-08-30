# 0011 Assignment Deadlines Live on Per-Class AssignmentWindow Sessions

Date: 2026-08-31

## Status

Accepted

## Context

Catalog `Assignment` rows carried `DueDate`, `AvailableFrom`, and `AvailableUntil`.
Every class sharing a module used the same clock, so two cohorts of the same
program collided even when their `ClassSession` calendars differed. Cohort
windows already existed as `ClassSession` rows with `SessionKind = AssignmentWindow`,
but quiz, file, retrospective, and research start/submit still read the catalog
dates. Recovery could grant a personal deadline that also ignored the class window.

## Decision

1. **Catalog assignments have no calendar dates.** Remove `DueDate`,
   `AvailableFrom`, and `AvailableUntil`. Keep `TimeLimitMinutes`, `MaxAttempts`,
   `PassScore`, and question-bank config on `Assignment`. Every assignment type
   requires `TimeLimitMinutes` greater than 0 at the application layer (the
   column stays nullable). New attempts set `Submission.ExpiresAt`.
2. **The class window is the `ClassSession` AssignmentWindow** for that
   `(ClassId, AssignmentId)`. `StartTime` opens new attempts; `EndTime` is the
   hard close for new attempts. An attempt started inside the window still runs
   to its own `ExpiresAt`. There is no separate `AssignmentWindow` entity.
3. **One window per class per assignment.** Application create/generate already
   reject a second active session for the same curriculum item. A filtered unique
   index on `ClassSessions` (`ClassId`, `AssignmentId`) where
   `SessionKind = AssignmentWindow`, not deleted, not cancelled, and
   `AssignmentId` is not null is the database backstop.
4. **Mentors set the window** by updating `StartTime` / `EndTime` (and
   Description) on that session for their assigned class. Manager/Admin retain
   full session update. Times must fall inside `Class.StartDate` / `EndDate`
   with `EndTime > StartTime`. Weekly generate places AssignmentWindow as a
   work period between live sessions (see `0012`); mentors may still stretch
   them afterward.
5. **AssignmentWindow sessions do not occupy the mentor calendar.** Overlap
   checks ignore `SessionKind.AssignmentWindow`.
6. **Recovery grants extra attempts only.** Remove `PersonalDueDate` and
   `PersonalAvailableUntil`. Extra attempts must be used while the class window
   is still open. After `EndTime` there is no recovery path; required work
   AcademicFails as in `0012` (including Theory). Theory still has unlimited
   attempts while the window is open.
7. **Missing window fails closed** with a generic not-available message.
8. **Rebuy** uses the new class’s AssignmentWindow. Catalog dates are gone, so
   there is nothing to copy or reset.

## Alternatives Considered

1. Keep catalog dates as defaults and override per class — two clocks, still
   easy to edit the wrong one.
2. Keep personal recovery deadlines after the class window — rejected; extra
   attempts only, inside the open window.
3. A new `AssignmentWindow` table — rejected; `ClassSession` already stores the
   window.

## Consequences

Positive:

- Two classes of the same module can open and close work independently.
- Mentors own operational deadlines without mutating catalog curriculum.

Tradeoffs:

- FE assignment editor loses date fields; curriculum `dueDate` is the class
  window `EndTime`.
- Theory students who miss the class window cannot recover a personal deadline.
- Existing catalog dates are backfilled onto missing AssignmentWindow rows, then
  dropped.

## Follow-Up

- Product docs: `docs/product/assessment.md`, `curriculum.md`, `enrollment.md`.
- FE: assignment CRUD, curriculum due date source, “window not scheduled” errors.
