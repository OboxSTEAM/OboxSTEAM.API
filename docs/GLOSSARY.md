# Glossary

Short product terms for OboxSTEAM.API. Process vocabulary lives in
`docs/WORKFLOW.md`.

## Program

Sellable STEAM track. Has `Price`, modules, classes, and enrollments.
`ProgramStatus`: Draft, Active, Inactive.

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
recovery cap exhausted) or attendance fail (≥20% missed sessions);
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
