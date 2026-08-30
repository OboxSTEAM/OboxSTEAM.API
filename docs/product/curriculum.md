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

`ProgramStatus`: **Draft** (not open for registration), **Active** (catalog +
purchase/enroll allowed), **Inactive** (stopped; no new payment or pending
enrollment). Create defaults to Draft when status is omitted.

API: `/api/programs` — list/detail public; mutations require Admin or
Manager.

## Module

A stage within a program. Types: `Theory`, `Experiential`, `Research`.

- Ordered via `ModuleOrder`.
- Optional `PrerequisiteModuleId` gates access.
- `IsMandatory` and `LearningOutcomes` describe the stage. There is no
  module-level retail `Price` or `RetakeFee`; catalog tuition is `Program.Price`.

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

Flags: `RequireQrCheckin`, `RequireMediaEvidence`. Template times on `Activity`
are defaults; cohort-specific times live on `ClassSession`.

API: `/api/activities`.

## Class and Sessions

`Class` is a running cohort (đợt học) for a program: date range, capacity,
mentor, `MinHoursBeforeAssignmentJoin` (generate first-session buffer),
`ScheduleSummary`.

Lifecycle (`ClassStatus`): **Draft → ReadyForMentor → Open → InProgress → Completed**.
`Cancelled` is stored but has no public cancel endpoint.

1. `POST /api/classes` always creates **Draft**. `StartDate` must be at least 14 days out. Mentor is optional.
2. Generate the timetable (`POST /api/class-sessions/generate`, or add sessions manually). Coverage is one active session per LiveOnline/Offline activity plus each assignment.
3. When coverage is complete, the class becomes **ReadyForMentor** (automatically after generate/create, or `POST /api/classes/{id}/ready-for-mentor`). Mentors request assignment from the board (`GET /api/class-mentor-requests/board`). Students cannot enroll.
4. After a mentor is assigned, `POST /api/classes/{id}/open` moves **ReadyForMentor → Open**. Students may enroll only in this status.
5. `POST /api/classes/{id}/start` (or auto-start when full and `StartDate` has arrived) moves **Open → InProgress**. Enrollment closes.
6. `POST /api/classes/{id}/complete` moves **InProgress → Completed**.

If sessions are deleted or cancelled so coverage no longer matches the curriculum, **ReadyForMentor** returns to **Draft**.

`ClassSession` schedules concrete session instances. `SessionKind` mirrors the
curriculum item: **LiveOnline**, **Offline**, or **AssignmentWindow**. LiveOnline
join links live on `MeetingUrl` (separate from free-text `Location`).
`SessionAttendance` records attendance status per student.

**AssignmentWindow** is the per-class work window for that assignment (one
active row per `(ClassId, AssignmentId)`). `StartTime` / `EndTime` are the
open and hard close for new quiz, file, retrospective, and research
attempts. An attempt already in progress may continue after `EndTime` until
submit. AcademicFail holds a draft only while `Submission.ExpiresAt` is in the
future. Generate does **not** put these on the weekly meeting pattern: lives
and offlines take `DaysOfWeek` slots; each AssignmentWindow opens at the
related teaching session’s `EndTime` (last live/offline of the course, or of
the research milestone’s required lives, or of the module) and closes at the
next live/offline `StartTime` (or class end). A generated window is at least
48 hours, clamped to `Class.EndDate`. Mentors may then change the times.
AssignmentWindow rows do not count as mentor calendar busy time and do not
require attendance. SelfPaced activities are never scheduled. Research
milestone create/update/delete uses the same curriculum edit lock as
assignment CRUD (no InProgress class; no Open class with Active students).

Mentor rollup: `GET /api/classes/{classId}/curriculum-progress` aggregates
activity and assignment progress for active class enrollments (assigned mentor
only). Modules/activities/assignments are always returned with zero counts when
there is no progress.

Class APIs are exposed through program and enrollment flows; entities exist in
domain and migrations.

## Materials

Learning assets for **SelfPaced** activities only (video, PDF, doc, etc.). At most
one material per activity. LiveOnline and Offline activities do not have materials.

Types via `MaterialType` enum. API: `/api/materials`.

## Experts

External experts associated with programs via `ProgramBoard` and `Expert`
entity. Profile credentials: `Specialization` tags, `ExpertDegree`, and
`ExpertPublication`. Manager/Admin CRUD:

- `POST|PUT|DELETE /api/experts/{id}/degrees`
- `POST|PUT|DELETE /api/experts/{id}/publications`

Public reads: `GET /api/experts/{id}` and `GET /api/experts/{id}/profile`.

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
