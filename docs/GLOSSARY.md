# Glossary

Short product terms for OboxSTEAM.API. Process vocabulary lives in
`docs/WORKFLOW.md`.

## Program

Sellable STEAM track. Has `Price`, modules, classes, and enrollments.
`ProgramStatus`: Draft, PendingReview, Approved, Active, Inactive.
Optional `FrameworkId` links to an expert `ProgramFramework` blueprint.

## ProgramFramework

Expert-owned curriculum blueprint assigned to at most one program, with
opt-in constraints and a rubric scorecard (`FrameworkRubricCriterion`).
Null or `false` rules are not enforced. `RequireCapstoneResearchMilestone =
true` requires ≥1 `ResearchMilestone` with `IsCapstone`. With a framework,
only the owning expert may approve or request-changes; board experts may
view and co-teach. Removing an expert from the board unlinks their Invited
and Accepted co-teach rows on that program. No `FrameworkId` is free-form board review (still
`PendingReview`). CRUD: `/api/program-frameworks`.
Blueprint edits are locked unless the framework is unattached or the
attached program is `Draft`.

## CurriculumReview

Expert audit round on a program (not student `ProgramReview`). Scores live on
`ReviewCriterionScore`. With a framework, only the owner decides; without a
framework, any one board expert may approve or request-changes. Comment is
required when requesting changes. Manager may withdraw from
`PendingReview` or `Approved` back to `Draft`.

## ClassSessionExpert

Co-teach invitation on a class session (`Invited` / `Accepted` / `Declined`).
Multiple Invited or Accepted experts per session; the same expert cannot
hold two active invites on one session. Manager may withdraw while
`Invited`. Changing session `StartTime` / `EndTime` clears Invited and
Accepted links (Declined stays) and the manager may invite again. Private mentor feedback is stored on each row after
the session is Completed (`PUT /api/class-session-experts/{id}/feedback`;
students must not see it).

## Module

Stage within a program (`Theory`, `Experiential`, `Research`). Ordered via
`ModuleOrder`; optional `PrerequisiteModuleId`. Retail module price columns
were removed; tuition is program-level. The retake price lives on
`Program.RetakeFee` (nullable, falls back to `Program.Price`).

## Course

Mentor-owned slice of a module containing activities and optional materials.

## Activity

Learning task (`SelfPaced`, `LiveOnline`, `Offline`) inside a course.

## Class

Cohort (đợt học) for a program. Seat capacity is `Class.MaxCapacity`.
`ClassKind`: Standard or Remedial (module-scoped retake class).

## ClassEnrollment

Student seat in a class. `ClassEnrollmentKind`: Primary or Retake.

## Assignment / Submission

Graded work (`Quiz`, `FileUpload`, `Retrospective`, …) and student attempts.

## Failed / Dropped enrollment

Terminal `ProgramEnrollment` states. `Failed` = academic fail (attempts +
recovery cap exhausted) or attendance fail (≥50% missed sessions);
`Dropped` = student withdraw. Closed purchases keep read-only curriculum;
continuing requires a rebuy. See `docs/product/enrollment.md`.

## Rebuy

New purchase of the same program after a `Failed`/`Dropped` (or `Completed`)
enrollment. Within 3 calendar months of the source `EndedAt` (or
`CompletedAt`), the price is `Program.RetakeFee ?? Program.Price` and
completed modules carry over; after the window it is full price from scratch.

## Class re-delivery

`ClassRedeliveryRequest` — **legacy** transfer or remedial path after failed
experiential work, superseded by the fail/drop → rebuy lifecycle.

## Harness (this repo)

Repository protocol: `docs/WORKFLOW.md`, plans, product docs, and
`scripts/bin/harness.exe` for core maintenance. Not a task database.
