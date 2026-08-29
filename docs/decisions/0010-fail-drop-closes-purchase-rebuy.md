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
2. **AcademicFail condition (all three must hold)** for one required
   assignment (`IsRequiredForModulePass`) of the module:
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
   Approving a recovery never closes anything because it grants new attempts or
   a new deadline.
3. **Attendance**: the existing >=20% absence fail additionally closes the
   purchase with `EndReason = Attendance`.
4. **Withdraw**: a self-service endpoint closes the purchase with
   `EndReason = Withdraw` and `EndedModuleId = null`; no module is marked
   failed.
5. Closing withdraws all Active/Pending class seats of the enrollment
   immediately (the student leaves the class at once).
6. **Rebuy**: a new `PendingPayment` enrollment is created and linked via
   `SourceProgramEnrollmentId`. The unique index on `(StudentId, ProgramId)`
   is relaxed to block only concurrent `PendingPayment`/`Active` rows. Eligible
   classes are `Standard` classes of the same program that have not started the
   module the student stopped at, nor any later module in `ModuleOrder`
   (no session of those modules `InProgress`/`Completed`), with
   seats available and no schedule conflict; brand-new classes qualify. For
   `Failed` sources the stop module is `EndedModuleId`; for `Dropped` sources
   it is the first not-`Completed` module in `ModuleOrder`. `Completed` does
   not block rebuy either: it links as the source but carries pricing benefit
   only (item 9). The "class has started module" check is re-implemented in
   the new lifecycle service - the legacy redelivery service is not reused.
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
9. **On payment success**: inside the rebuy window, `Completed` module
   enrollments are copied to the new enrollment with the next global
   `AttemptNumber` per (student, module), together with their
   `ActivityProgress` rows and their `Graded` submissions (new `Submission.Code`
   per copy; quiz answers, evidence rows, and non-graded submissions are not
   copied). Failed and in-progress modules are not copied. Outside the window
   nothing is copied and every module starts from scratch. A `Completed`
   source never copies progress (every module is already complete) - its
   rebuy benefit is retake pricing only. The student joins exactly one new
   `Standard` class. No Remedial class is created. Lazy curriculum
   provisioning assigns the next global `AttemptNumber` per (student, module)
   so redone modules never collide with prior attempts on the
   `(StudentId, ModuleId, AttemptNumber)` unique index.
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
    reopens the purchase automatically: attendance corrected below the 20%
    absence threshold reopens a `Failed`/`Attendance` purchase; a grade
    corrected to a pass reopens a `Failed`/`AcademicFail` purchase (attempt
    counts and recovery decisions are untouched - the corrected pass stands).
    Reopen restores `Active` status, clears `EndReason`/`EndedModuleId`/
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
- Passed-module credit is preserved across the rebuy.
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

- Execution plan: `docs/plans/active/fail-rebuy-repurchase.md`.
- Later slice: legacy redelivery cleanup, FE wiring, seed/demo data.
