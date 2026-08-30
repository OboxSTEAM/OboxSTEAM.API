# 0010 Fail or Drop Closes the Program Purchase; Retake Requires Rebuy

Date: 2026-08-29

## Status

Accepted

## Context

On main, academic failure is never persisted. Exhausting quiz/assignment/research
attempts only blocks new attempts with a Conflict error; the `ModuleEnrollment`
stays `Active` forever and the student is stuck with no valid path forward. The
only runtime writer of `ModuleEnrollment.Failed` is the attendance policy
(>=20% missed sessions, `SessionAttendanceService`), and nothing closes a
`ProgramEnrollment` at runtime.

The legacy `ClassRedeliveryRequest` ladder handles module-level retake while
keeping the purchase open. Product wants a cleaner commercial ending: failing or
dropping ends the whole program purchase; continuing requires paying a retake
fee and joining a new class whose progress has not reached the failed module.

## Decision

1. **Failing one required module closes the entire `ProgramEnrollment`.**
   Status is split by trigger: `AcademicFail` / `Attendance` -> `Failed`;
   `Withdraw` -> `Dropped`. Both are terminal and both allow rebuy. The close
   records `EndReason`, `EndedModuleId`, and `EndedAt` on the enrollment.
2. **AcademicFail** only for `IsRequiredForModulePass` assignments. While the
   class window is still open, Experiential/Research close when all three hold
   (Theory never uses this path because attempts are unlimited until the window
   ends — see `0012` for window-elapsed Theory close):
   - The latest submission is graded fail (`Graded` with grade < `PassScore`;
     `ReturnedForRevision` does not count).
   - Effective attempts are exhausted: `MaxAttempts` + approved recovery extras
     (`AssessmentAttemptPolicy.GetEffectiveMaxAttemptsAsync`).
   - Decided recovery requests (Approved + Rejected) reached the cap of 2
     (`MaxRecoveryRequestsPerAssignment`).
   Detection is wired at four points: `QuizAttemptService.SubmitQuiz`,
   `AssignmentSubmissionService.GradeAssignment`,
   `ResearchSubmissionService.GradeSubmission` (covers the case where the cap
   was reached earlier and the final graded attempt just failed), and
   `AssessmentRecoveryRequestService.RejectAsync` (covers the case where
   attempts were already exhausted and the second rejection just hit the cap).
   Approving a recovery never closes anything because it grants new attempts.
3. **Attendance**: absence fail additionally closes the purchase with
   `EndReason = Attendance`. Threshold is **50%** as of `0012` (was 20%).
4. **Withdraw**: a self-service endpoint closes the purchase with
   `EndReason = Withdraw` and `EndedModuleId = null`. Open module enrollments
   become `Dropped`; completed modules stay `Completed`.
5. Closing withdraws all Active/Pending class seats of the enrollment
   immediately (the student leaves the class at once) and terminals every
   open `ModuleEnrollment` (ended module `Failed` on AcademicFail/Attendance;
   other Active/Deferred rows `Dropped`).
6. **Rebuy**: a new `PendingPayment` enrollment is created and linked via
   `SourceProgramEnrollmentId`. The unique index on `(StudentId, ProgramId)`
   is relaxed to block only concurrent `PendingPayment`/`Active` rows. Eligible
   classes are `Open` or `InProgress` `Standard` classes of the same program
   that have not started the module the student stopped at, nor any later
   module in `ModuleOrder` (no session of those modules `InProgress`/`Completed`),
   with seats available, no schedule conflict, and not the class the student
   already occupied on the source purchase. Brand-new classes qualify.
   First-time checkout and a `Completed` (100%) retake require `Open` only.
   For `Failed` sources the
   stop module is `EndedModuleId`; for `Dropped` sources it is the first
   not-`Completed` module in `ModuleOrder`. `Completed` does not block rebuy
   either: it links as the source but carries pricing benefit only (item 9)
   and the class list is the same Open catalog as a first purchase.
   The "class has started module" check is re-implemented in the new lifecycle
   service - the legacy redelivery service is not reused. Students pick from
   `GET /api/programs/{id}/rebuy-classes`, which returns Open-only when there is
   no failed/dropped source (`IsRebuy = false`) and the fail/drop catalog when
   there is (`IsRebuy = true`).
7. **Retake fee** = `Program.RetakeFee` (new nullable field) falling back to
   `Program.Price` when null. Retake pricing applies only inside the rebuy
   window (item 8); outside the window the rebuy bills full `Program.Price`.
8. **Rebuy window**: retake pricing and passed-module credit apply only when
   the rebuy checkout starts within 3 calendar months of the source
   enrollment's close date - `EndedAt` for `Failed`/`Dropped`, `CompletedAt`
   for `Completed` (the boundary day is included). After the window the
   rebuy is still allowed but is a fresh start: full `Program.Price`, no
   progress copy. The window applies equally to `Failed` and `Dropped` source
   enrollments.
9. **On payment success**: inside the rebuy window, credit copy is scoped to
   what the **new class** has already taught by wall-clock and session status.
   A session counts as taught when it is `Completed` or its `EndTime` is at or
   before now (the class already passed that slot even if the row is still
   `Scheduled`). A module with no non-cancelled sessions (self-paced /
   unscheduled) is copied whole. A module whose every non-cancelled session is
   already taught is copied whole. Future sessions are not copied — the student
   relearns those with the new class. A part-taught module copies only the
   `ActivityProgress` rows and `Graded` submissions whose activity/assignment
   the class has already taught; the copied enrollment stays `Active` until the
   student finishes the rest. Copied enrollments use the next global
   `AttemptNumber` per (student, module). After copy, module and program
   `ProgressPercent` are recalculated with the live unit formula. Quiz answers,
   evidence rows, and non-graded submissions are not copied. Failed and
   in-progress modules are not copied. Outside the window nothing is copied.
   A `Completed` source never copies progress — its rebuy benefit is retake
   pricing only. The student joins exactly one new `Standard` class (never the
   class they left). No Remedial class is created. Quiz / assignment /
   recovery attempt counts are scoped to the new `ModuleEnrollment`, so a
   rebuy starts a fresh attempt budget except for submissions copied onto
   that enrollment.
10. `Failed`/`Dropped` enrollments keep read-only curriculum access; mutations
    still require `Active`. Reads (curriculum tree, mind map, activity detail)
    block only `PendingPayment`. Student mutations are blocked on terminal
    enrollments at every entry point: curriculum actions (complete activity,
    save checkpoint) plus quiz/assignment/research/retrospective submissions
    and recovery requests (all resolve the module enrollment and now also
    require the parent program enrollment to be `Active`). Manager/Admin
    correction paths stay open on closed enrollments as a backup: attendance
    records remain editable (withdrawn seats and failed module enrollments are
    resolvable), and already-`Graded` submissions can be re-graded by
    Admin/Manager only. A correction that removes the closing condition
    reopens the purchase automatically: attendance corrected below the 50%
    absence threshold reopens a `Failed`/`Attendance` purchase; a grade
    corrected to a pass reopens a `Failed`/`AcademicFail` purchase (attempt
    counts and recovery decisions are untouched - the corrected pass stands)
    **unless** the student already has an `Active` or `PendingPayment`
    enrollment for the same program (Conflict; the closed purchase stays
    closed). Reopen restores `Active` status, clears `EndReason`/`EndedModuleId`/
    `EndedAt`, reactivates the failed module enrollment and every withdrawn
    seat, then recalculates module/program progress.
11. Legacy `ClassRedeliveryRequest` / Remedial / retake checkout stay untouched
    and are not depended upon; cleanup is deferred to a later slice.

## Alternatives Considered

1. Module-level fail only (keep the purchase active, retake single modules via
   the existing ladder) - rejected: product wants the commercial ending at
   program level ("pay to rejoin the course").
2. A single `Dropped` status for every close - rejected: `Failed` vs `Dropped`
   distinguishes system-closed (academic/attendance) from self-withdrawn for
   reporting.
3. Reusing the legacy redelivery candidate scan - rejected: no dependency on
   legacy code; the session-based "class has started module" check is
   re-implemented in the new lifecycle service.
4. Retake fee = always 100% `Program.Price` - superseded by the separate
   `RetakeFee` field with `Price` fallback.
5. Unlimited retake window for the discount and passed-module credit -
   rejected: product wants the retake offer time-boxed to 3 calendar months
   after the purchase closes.

## Consequences

Positive:

- Clear commercial ending; students are never stuck in an unfinishable state.
- Retake revenue is explicit and configurable per program via `RetakeFee`.
- Passed-module credit is preserved across the rebuy, limited to what the
  new class has already taught (unscheduled / self-paced modules copy in full).
- The retake discount and credit are time-boxed (3 calendar months from close),
  encouraging quick re-enrollment while keeping long-dated rebuys full-price.

Tradeoffs:

- Schema change: new `ProgramEnrollment` columns, `Program.RetakeFee`, relaxed
  unique index.
- Progress copy across enrollments and global attempt renumbering add
  implementation care around the `(StudentId, ModuleId, AttemptNumber)` unique
  index.
- Rebuy checkout must resolve the source enrollment's `EndedAt` and apply the
  3-calendar-month window before pricing and before any progress copy.
- Read paths must treat two terminal statuses (`Failed`, `Dropped`) as
  read-only instead of forbidden.

## Follow-Up

- Execution plan: `docs/plans/completed/fail-rebuy-repurchase.md`.
- Amended by `0012` (work-period windows, Theory window-elapsed fail,
  required-only AcademicFail, 50% absence, chuyen ca copy).
- Later slice: legacy redelivery cleanup, FE wiring.
