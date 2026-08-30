# 0012 Cohort Work Windows and Fail Exits

Date: 2026-08-31

## Status

Accepted

## Context

ADR 0011 put assignment deadlines on per-class `AssignmentWindow` sessions, but
generate still consumed a weekly meeting slot per assignment (typically two
hours). Mentors had to stretch those slots by hand. Theory never closed the
purchase (`TryCloseAfterFailedAssignmentAsync` no-ops unlimited modules), so a
required quiz whose window had ended left the student Active with no retry, no
recovery, and no rebuy. Experiential/Research academic fail ignored
`IsRequiredForModulePass` and could fire while a research deliverable was still
`TurnedIn`. Attendance fail at 20% of a short module was one sick day.

Product intent for rebuy stays **chuyen ca**: join a class that has not started
the stop module; credit still follows what the new class has already taught.
Copy and notifications must not call that fee “hoc lai module”.

## Decision

1. **Generate places AssignmentWindow as a work period, not a TKB slot.** Weekly
   `DaysOfWeek` slots are LiveOnline/Offline only. Each assignment opens at the
   related teaching session’s `EndTime` (last live/offline of the course; last
   required live of a research milestone; last live of the module if
   module-scoped; `Class.StartDate` if none) and closes at the next live/offline
   `StartTime` on the class, or `Class.EndDate` end-of-day if there is no next
   live. If that span is shorter than 48 hours, bump close to `Open+48h` clamped
   to class end. If close is still `<=` open, generate fails (extend the class
   end date). Coverage is unchanged: one active AssignmentWindow per assignment.
   AssignmentWindow still skips mentor overlap. Mentors may PUT times. Existing
   timetables are not auto-migrated.

2. **AcademicFail only for required work.** `IsRequiredForModulePass == false`
   never closes the purchase.

3. **Window-elapsed close.** Required assignment, window `EndTime` passed, not
   passed on this module enrollment, no in-progress continuation (`Pending` /
   `ReturnedForRevision` only when `ExpiresAt` is set and still in the future;
   null or elapsed `ExpiresAt` is not a hold), latest row not `TurnedIn` →
   `AcademicFail`. This includes Theory. Experiential/Research leftover attempts
   no longer stick after the window. The exhausted-attempts + two decided
   recoveries path from 0010 remains while the window is still open (also
   required-only). A hosted scan closes students who never return.

4. **Research grade hold.** Never AcademicFail while the latest submission is
   `TurnedIn`. After a **pass** on milestone N, if milestone N+1’s class window
   is closed or has fewer than 48 hours left, set its `EndTime` to
   `max(EndTime, now+48h)` (keep `StartTime`).

5. **Attendance fail threshold is 50%** (`ModuleAbsencePolicy.MaxAbsencePercent`).
   Manager reopen uses the same bar (below 50%).

6. **Rebuy language is chuyen ca.** `Program.RetakeFee` is unchanged. Fail/drop
   close is a new purchase to transfer into another cohort, not a module retake.
   The picker adds a per-module credit hint (`Copied` / `RedoWithClass` /
   `Ahead`) from taught-session status vs the source’s Completed modules.
   Eligibility and `ApplyRebuyCreditsAsync` are unchanged.

7. **Late-join is two-thirds of an open AssignmentWindow.** Self-enroll and the
   rebuy picker block when any not-yet-ended `AssignmentWindow` is at or past
   two-thirds of (`EndTime` − `StartTime`) from `StartTime`. Windows that have
   not opened yet do not block. LiveOnline/Offline sessions do not count.
   `Class.MinHoursBeforeAssignmentJoin` is the generate first-session buffer,
   not this cutoff.

8. **Every assignment has a time limit.** Application validation requires
   `TimeLimitMinutes` greater than 0 for all types. Starting an attempt (and
   returning for revision) sets `Submission.ExpiresAt`.

This amends 0010 (Theory can AcademicFail when a required window elapses;
`IsRequiredForModulePass` gates close; attendance 50%) and 0011 (generate
rewrite; Theory has a fail path after `EndTime`; required attempt timer).

## Alternatives Considered

1. Keep AssignmentWindow on the weekly pattern and rely on mentors to stretch —
   rejected; default must be a multi-day work period.
2. Copy every module the student already passed regardless of the destination
   class — rejected; rebuy is chuyen ca, not a transcript transfer.
3. Leave Theory stuck until class end with no AcademicFail — rejected; no rebuy
   path.
4. Auto-extend only, never fail while waiting for a grade — accepted as the
   `TurnedIn` hold plus N+1 48h bump; remaining required work still fails when
   the window has ended and there is nothing left to grade.

## Consequences

Positive:

- Quizzes and research deliverables get a real work interval between lives.
- Required Theory/research no longer leave an unfinishable Active purchase.
- Optional practice work cannot eject the student from the program.
- One absence in a five-session module no longer fails the purchase.

Tradeoffs:

- Generate needs a class end date that still fits lives plus a 48h tail after
  the last live when there is no following session.
- Existing classes keep old two-hour windows until a mentor edits them.
- FE must read credit hints and the new AcademicFail copy; `RetakeFee` name
  stays.

## Follow-Up

- Execution: `docs/plans/completed/cohort-windows-fail-exits.md`.
- Product: `docs/product/curriculum.md`, `assessment.md`, `enrollment.md`,
  `overview.md`.
- FE: picker credit hint, ModuleFailed copy, generate no longer creates a
  meeting-length assignment slot.
