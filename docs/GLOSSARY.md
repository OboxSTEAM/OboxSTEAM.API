# Glossary

Short product terms for OboxSTEAM.API. Process vocabulary lives in
`docs/WORKFLOW.md`.

## Program

Sellable STEAM track. Has `Price`, modules, classes, and enrollments.
`ProgramStatus`: Draft, Active, Inactive.

## Module

Stage within a program (`Theory`, `Experiential`, `Research`). Ordered via
`ModuleOrder`; optional `PrerequisiteModuleId`. Retail module price / retake
fee columns were removed; tuition is program-level.

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

## Class re-delivery

`ClassRedeliveryRequest` — transfer or remedial path after failed experiential
work; payment amount uses `Program.Price`.

## Harness (this repo)

Repository protocol: `docs/WORKFLOW.md`, plans, product docs, and
`scripts/bin/harness.exe` for core maintenance. Not a task database.
