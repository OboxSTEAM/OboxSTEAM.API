# Execution Plan: Fail/Drop Closes Purchase + Rebuy

Date: 2026-08-29

## Status

Active

## Outcome

When a student fails (academic: latest required-assignment submission graded
fail + effective attempts exhausted + recovery cap of 2 reached; or attendance:
>=20% missed sessions) or withdraws, their `ProgramEnrollment` closes
(`Failed`/`Dropped` + `EndReason`), class seats are withdrawn immediately, and
continuing requires paying the program retake fee (`Program.RetakeFee` falling
back to `Program.Price`) and joining exactly one new `Standard` class that has
not started the module the student stopped at, nor any later module in
`ModuleOrder`. Passed modules carry over as `Completed` copies
on the new enrollment; the failed module is redone from scratch. The retake
fee and passed-module credit apply only when the rebuy starts within 3
calendar months of the source enrollment's `EndedAt` (boundary day included);
later rebuys pay full `Program.Price` and start every module from scratch.

## Context

- Decision record: `docs/decisions/0010-fail-drop-closes-purchase-rebuy.md`.
- Product docs: `docs/product/enrollment.md`, `docs/product/overview.md`,
  `docs/GLOSSARY.md` (updated in step 9).
- Key code on main: `ProgramEnrollmentService.GetOrCreatePendingEnrollmentAsync`,
  `ClassSeatHoldService.CreateOrRefreshHoldAsync`,
  `PaymentService.HandlePaymentSuccess`,
  `SessionAttendanceService.TryFailModuleForExcessAbsencesAsync`,
  `AssessmentAttemptPolicy`, `AssessmentRecoveryRequestService`,
  `EnrollmentCurriculumService.BuildCurriculumContextAsync`,
  `CurriculumAccessValidator`.
- Navigation used Grep/Read because CodeGraph MCP is unavailable on this
  machine.

## Scope

In scope:

- Domain/schema: `ProgramPurchaseEndReason`, `ProgramEnrollment` close fields,
  `Program.RetakeFee`, relaxed unique index.
- Close triggers: attendance, academic (4 wire points), withdraw endpoint.
- Rebuy checkout: pending-after-terminal, `SourceProgramEnrollmentId`, class
  eligibility, retake-fee pricing gated by the 3-calendar-month rebuy window
  from the source enrollment's `EndedAt`.
- Payment success: progress copy (inside the rebuy window only) +
  next-attempt provisioning.
- Read-only curriculum for `Failed`/`Dropped`; E2E test; product docs.

Out of scope:

- Removing or disabling legacy `ClassRedeliveryRequest` / Remedial (later
  slice). The new flow must not call into it.
- FE wiring (OboxSTEAM.FE), bundle/voucher, expert review.

## Approach

Ten small steps (see Progress). Each step ends with `dotnet build` +
`dotnet test` green, then pauses for user confirmation before the next step.
No commits unless the user asks. EF migrations are generated with the EF CLI
only. If a step grows, split it before implementing.

## Risks And Recovery

- Relaxed unique index could allow duplicate active purchases - mitigated by
  keeping the filter on `PendingPayment`/`Active` only.
- Attempt renumbering could violate the `(StudentId, ModuleId, AttemptNumber)`
  unique index - mitigated by always computing the next attempt globally per
  student+module (never hardcoding 1).
- Recovery per step: revert the working tree (`git restore`) and delete new
  files; an unapplied migration can be removed with `dotnet ef migrations
  remove`.

## Progress

- [x] Step 0: this plan + ADR 0010 (docs only).
- [x] Step 1: `ProgramPurchaseEndReason` + `ProgramEnrollment` fields +
  `Program.RetakeFee` + relaxed unique index + EF migration
  (`20260829111210_ProgramPurchaseRebuy`; build + 1239 tests green).
  Seed aligned: catalog `RetakeFee` (60% of Price), STD-007 Dropped/Withdraw,
  STD-012 Failed/AcademicFail on MATHFUN, payments stay Success (no auto-refund).
- [x] Step 2: `ProgramPurchaseLifecycle.CloseAsync` (reason -> status:
  fail -> `Failed`, withdraw -> `Dropped`) + attendance trigger.
- [x] Step 3: `TryCloseAfterFailedAssignmentAsync` + wire quiz / assignment /
  research grading + recovery reject.
- [x] Step 4: `WithdrawAsync` + `POST /api/program-enrollments/{id}/withdraw`.
- [x] Step 5: rebuy checkout (pending after terminal, `SourceProgramEnrollmentId`,
  class has-not-started-stop-module-or-later eligibility, `RetakeFee ?? Price` inside
  the 3-calendar-month window from the source `EndedAt`, full `Price` after).
  Implemented in `GetOrCreatePendingEnrollmentAsync` (detect + link latest
  closed source, backfill on reused pending), `ProgramPurchaseLifecycle`
  (`FindRebuySource` / `IsWithinRebuyWindow` / `ResolveCheckoutAmountAsync` /
  `ValidateRebuyClassEligibilityAsync`), pricing applied in both
  `CreateDirectCheckout` and `RequestParentPayment`, class eligibility enforced
  at seat hold. Build + 1284 tests green.
- [x] Step 6: `ApplyRebuyCreditsAsync` on payment success (copies `Completed`
  modules with their `ActivityProgress` rows and `Graded` submissions only
  when the rebuy is inside the window) + next-attempt provisioning in
  curriculum (`EnrollmentCurriculumService` assigns the next global
  `AttemptNumber` per student+module). Build + 1290 tests green.
- [x] Step 7: read-only curriculum for `Failed`/`Dropped` (reads block only
  `PendingPayment`; student mutations - curriculum actions, quiz/assignment/
  research/retrospective submissions, recovery requests - require an `Active`
  program enrollment) + manager backup paths: attendance editable on closed
  enrollments, Admin/Manager re-grade of `Graded` submissions, and automatic
  reopen when the correction removes the closing condition (attendance below
  20% for `Attendance` closes; corrected pass for `AcademicFail` closes).
  Reopen restores PE/ME `Active`, clears close fields, reactivates withdrawn
  seats, then recalculates progress. The "`Completed` credit without cloned
  submissions" half of the original step was superseded by Step 6, which
  copies graded submissions. Build + 1310 tests green.
- [x] Step 8: E2E fail one module → rebuy a new class → finish the redone
  module. Script `obox-rebuy-step8-e2e.ps1` (local, gitignored) against
  Docker `http://localhost:5000`: clear+seed, then 29/29 pass. Happy path
  is STD-026 (attendance fail) on `CLS-FAILREBUY-ELIGIBLE`: retake fee
  600000, Foundations copied (3 ActivityProgress + 1 Graded quiz), Lab
  not copied, lazy Lab `AttemptNumber` 2, quiz 100 / upload 80, Lab
  `Completed 100`. Contrasts: STD-027/038 class eligibility; STD-034 full
  price + no copy; STD-036 retake fee + no copy. Quiz submit now grades
  from merged answers (EF `GetAllAsync` missed unsaved `QuizAnswer` rows,
  which stored `AssignedGrade` 0). Build + 1310 tests green. Seed map:
  `CLS-FAILREBUY-CURRENT` (InProgress), `ELIGIBLE` (Open, Foundations
  started), `BLOCKED` (Open, lab started), `FRESH` (Open, not started);
  closed STD-026/027/034/035/036/037/038; active close-triggers STD-028–
  033. Known gap: rebuy does not reset assignment `MaxAttempts` or the
  recovery cap, so academic-fail students like STD-027 cannot start a
  `MaxAttempts=1` quiz again.
- [ ] Step 9: product docs (`enrollment.md`, `GLOSSARY.md`, `overview.md`) +
  move this plan to `docs/plans/completed/`.

## Decisions

- 2026-08-29: Fail -> PE `Failed`, Withdraw -> PE `Dropped` (split by trigger).
- 2026-08-29: Retake fee is a separate nullable `Program.RetakeFee`, falling
  back to `Program.Price`.
- 2026-08-29: No reuse of legacy `ClassRedeliveryRequestService`; the
  has-started-module check is re-implemented in the new lifecycle service.
- 2026-08-29: `Deferred` is treated like `Active` (blocks rebuy); `Completed`
  does not block rebuy; in-progress (non-failed, non-completed) modules are not
  copied.
- 2026-08-29: Retake pricing and passed-module credit are time-boxed to a
  3-calendar-month rebuy window from the source enrollment's `EndedAt`
  (boundary day included); later rebuys pay full `Program.Price` and copy no
  progress. The window applies equally to `Failed` and `Dropped` sources.
- 2026-08-29: A `Completed` source anchors the window at `CompletedAt` and
  receives retake **pricing only** - no progress copy (every module is already
  complete) and no failed-module class constraint.
- 2026-08-29: Class eligibility blocks classes that have started the stop
  module **or any later module** in `ModuleOrder`. For `Failed` sources the
  stop module is `EndedModuleId`; for `Dropped` sources it is the first
  not-`Completed` module in `ModuleOrder`. `Completed` sources are
  unconstrained.
- 2026-08-29: Rebuy credit copy includes the `Graded` submissions of copied
  `Completed` modules (new `Submission.Code` per copy), not just
  `ActivityProgress`; quiz answers, evidence rows, and non-graded submissions
  stay behind on the source enrollment.
- 2026-08-30: Manager backup on closed purchases: attendance stays editable
  (withdrawn seat + failed module enrollment resolvable) and Admin/Manager may
  re-grade `Graded` submissions. A correction that removes the closing
  condition auto-reopens the purchase (attendance < 20% for `Attendance`
  closes; corrected pass for `AcademicFail` closes) without resetting attempt
  counts or recovery decisions; reopen restores PE/ME `Active`, clears close
  fields, reactivates withdrawn seats, and recalculates progress.
- 2026-08-30: Fail/rebuy seed rebuilt for Step 8: three-module STEAM
  Foundations track with prerequisite chain, four classes (current /
  eligible / blocked / fresh), and closed snapshots STD-034..038 for
  window, withdraw, completed, and fail-at-first-module cases. Active
  close-trigger students keep STD-028..033 after a completed Foundations
  module.
- 2026-08-30: `SubmitQuiz` grades from the merged request answers. Reloading
  `QuizAnswers` before `SaveChanges` returned an empty set on EF Core, so
  live submits stored `AssignedGrade` 0 even when the correct option was
  saved.

## Validation

- Focused proof: unit tests per step under `OboxSteam.Test/UnitTests`.
- Integration or end-to-end proof: `obox-rebuy-step8-e2e.ps1` 29/29 pass
  (2026-08-30, Docker API + seeded `PRG-FAILREBUY`).
- Repository-required checks: `dotnet build` + `dotnet test` green after every
  step (1310 tests at step 8).

## Result

Pending.
