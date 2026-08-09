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

API: `/api/programs` — list/detail public; mutations require Admin or
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

Per-student highlight reels for a **class**, processed asynchronously via AWS
MediaConvert. Model: `HighlightVideoStack` (up to 3 per student/class) with
`HighlightVideoItem` outputs (up to 4 per stack). Source clips come from
`MediaAsset` videos for that class (`ClassId` required; optional
`ClassSessionId`). Only videos with a **verified** face `MediaTag` for the
student are used. Mentor late tags (no face timeline) are treated as scene-only
participation / project credit: full video when no strength is set, or
activity clips from the media label timeline when `StrengthDescription` is
set. Optional `StrengthDescription` otherwise filters via Bedrock + label
timeline (face windows when a face timeline exists).

API: `/api/highlight-video/stacks` (`classId` query/body; optional `studentId`).
Trim / add-segment / delete under `/api/highlight-video/stacks/{stackId}/...`.
AWS completion: `/api/webhooks/aws`.
Completed reels are attached to a portfolio Gallery section via
`POST /api/portfolios/me/media/from-highlight-reel` (copies into portfolio-owned
Video media). Sync no longer creates `HighlightReel` project items.
