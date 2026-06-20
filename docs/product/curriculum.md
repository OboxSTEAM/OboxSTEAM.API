# Curriculum Model

## Hierarchy

```text
Program
  ├── ProgramBoard (expert associations)
  ├── Module[]
  │     ├── Course[] (each has a Mentor)
  │     │     └── Activity[]
  │     ├── Assignment[] (module- or course-scoped)
  ├── Class[] (cohorts)
  └── ProgramReview[]
```

## Program

Represents a sellable STEAM track (e.g. robotics, coding). Key fields: `Code`,
`Name`, `Category`, `Level`, `Price`, `SkillsGained`, `Rating`, `Status`.

API: `/api/programs` — list/detail public; mutations require SuperAdmin or
Manager.

## Module

A stage within a program. Types: `Theory`, `Experiential`, `Research`.

- Ordered via `ModuleOrder`.
- Optional `PrerequisiteModuleId` gates access.
- `IsMandatory`, `Price`, `RetakeFee` support modular purchasing.
- `DefaultSchedulingMode` hints activity scheduling defaults.

API: `/api/modules`.

## Course

A mentor-owned slice of a module containing activities. SelfPaced activities may
have one optional learning material (video, PDF, etc.).

API: `/api/courses`.

## Activity

Individual learning tasks within a course.

| ActivityType | Meaning |
| --- | --- |
| SelfPaced | No fixed schedule; progress tracked individually |
| LiveOnline | Scheduled online session |
| Offline | Physical session; may require QR check-in |

`SchedulingMode` on the activity and `ClassSession` on cohorts define when
students attend. Template times on `Activity` are defaults; cohort-specific
times live on `ClassSession`.

Flags: `RequireQrCheckin`, `RequireMediaEvidence`.

API: `/api/activities`.

## Class and Sessions

`Class` is a running cohort (đợt học) for a program: date range, capacity,
mentor, `MinHoursBeforeAssignmentJoin`, `ScheduleSummary`.

`ClassSession` schedules concrete session instances. `SessionAttendance` records
attendance status per student.

Class APIs are exposed through program and enrollment flows; entities exist in
domain and migrations.

## Materials

Learning assets for **SelfPaced** activities only (video, PDF, doc, etc.). At most
one material per activity. LiveOnline and Offline activities do not have materials.

Types via `MaterialType` enum. API: `/api/materials`.

## Experts

External experts associated with programs via `ProgramBoard` and `Expert`
entity.

API: `/api/experts`.

## Highlight Videos

Per-student highlight reels for a program, processed asynchronously via AWS
MediaConvert. Status tracked on `HighlightVideo`.

API: `/api/programs/{programId}/students/{studentId}/highlight-video`.
