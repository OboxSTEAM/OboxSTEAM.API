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
not started the failed module. Passed modules carry over as `Completed` copies
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
- [ ] Step 2: `ProgramPurchaseLifecycle.CloseAsync` (reason -> status:
  fail -> `Failed`, withdraw -> `Dropped`) + attendance trigger.
- [ ] Step 3: `TryCloseAfterFailedAssignmentAsync` + wire quiz / assignment /
  research grading + recovery reject.
- [ ] Step 4: `WithdrawAsync` + `POST /api/program-enrollments/{id}/withdraw`.
- [ ] Step 5: rebuy checkout (pending after terminal, `SourceProgramEnrollmentId`,
  class has-not-started-failed-module eligibility, `RetakeFee ?? Price` inside
  the 3-calendar-month window from the source `EndedAt`, full `Price` after).
- [ ] Step 6: `ApplyRebuyCreditsAsync` on payment success (copies `Completed`
  modules only when the rebuy is inside the window) + next-attempt
  provisioning in curriculum.
- [ ] Step 7: read-only curriculum for `Failed`/`Dropped` + `Completed` credit
  without cloned submissions.
- [ ] Step 8: E2E test - fail one module -> rebuy a new class -> finish the
  redone module.
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

## Validation

- Focused proof: unit tests per step under `OboxSteam.Test/UnitTests`.
- Integration or end-to-end proof: step 8 scenario test.
- Repository-required checks: `dotnet build` + `dotnet test` green after every
  step.

## Result

Pending.
