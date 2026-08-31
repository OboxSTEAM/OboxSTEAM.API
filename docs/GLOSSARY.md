# Glossary

Short product terms for OboxSTEAM.API. Process vocabulary lives in
`docs/WORKFLOW.md`.

## Program

Sellable STEAM track. Has `Price`, modules, classes, and enrollments.
`ProgramStatus`: Draft, PendingReview, Approved, Active, Inactive.
Optional `FrameworkId` links to an expert `ProgramFramework` blueprint.

## ProgramFramework

Expert-owned curriculum blueprint with opt-in constraints and a rubric
scorecard (`FrameworkRubricCriterion`). Null or `false` rules are not
enforced. `RequireFinalAssessment = true` requires ≥1 capstone research
milestone. Zero rubric criteria is allowed (submit without expert wait);
≥1 criterion requires expert review. CRUD: `/api/program-frameworks`.

## CurriculumReview

Expert audit round on a program (not student `ProgramReview`). Scores live on
`ReviewCriterionScore`.

## ClassSessionExpert

Co-teach invitation on a class session (`Invited` / `Accepted` / `Declined`)
plus private mentor feedback (students must not see feedback).

## Module

Stage within a program (`Theory`, `Experiential`, `Research`). Ordered via
`ModuleOrder`; optional `PrerequisiteModuleId`. Retail module price columns
were removed; tuition is program-level. Continuity / in-window rebuy is
**50% of `Program.Price`** for 1 month after close (Active continuity: same
50% with no expiry). Full `Price` after the window. `Program.RetakeFee` is
legacy unused for checkout.

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
enrollment. Within **1 calendar month** of the source `EndedAt` (or
`CompletedAt`), the price is **50% of `Program.Price`** and completed modules
carry over (scoped to what the new class has taught); after the window it is
full price from scratch.

## Class continuity / re-delivery

Active purchase: student picks another Standard class at **50%** (no expiry
while Active). After fail/drop: same catalog via rebuy. Prefer
`POST .../class-redelivery-requests/{id}/cancel` to drop an open request.
Program quit remains `POST .../program-enrollments/{id}/withdraw`.

## Harness (this repo)

Repository protocol: `docs/WORKFLOW.md`, plans, product docs, and
`scripts/bin/harness.exe` for core maintenance. Not a task database.
