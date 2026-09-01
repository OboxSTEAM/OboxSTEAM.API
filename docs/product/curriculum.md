# Curriculum Model

## Hierarchy

```text
Program
  ├── Framework? (ProgramFramework — optional blueprint)
  ├── ProgramBoard (expert associations)
  ├── CurriculumReview[] (expert audit rounds; not student ProgramReview)
  ├── Module[]
  │     ├── Course[] (each has a Mentor)
  │     │     └── Activity[]
  │     ├── Assignment[] (module- or course-scoped)
  ├── Class[] (cohorts)
  │     └── ClassSession[]
  │           └── ClassSessionExpert[] (co-teach invite + private mentor feedback)
  └── ProgramReview[]
```

## Program

Represents a sellable STEAM track (e.g. robotics, coding). Key fields: `Code`,
`Name`, `Category`, `Level`, `Price`, `SkillsGained`, `Rating`, `Status`.

`ProgramStatus`: **Draft** (manager is authoring; not open for registration),
**PendingReview** (submitted to the owning expert), **Approved** (ready for
manager publish), **Active** (catalog + purchase/enroll allowed),
**Inactive** (stopped; no new payment or pending enrollment).

Create via API is always **Draft** (omitted or explicit). `PUT` cannot set
`PendingReview` or `Approved`. `Active` ↔ `Inactive` is allowed only when the
program is already in one of those two catalog states. Enrollment and class
creation still require **Active**.

Lifecycle endpoints (Manager/Admin unless noted):

- `POST /api/programs/{id}/submit-review` — Draft only. Runs
  `ProgramFrameworkValidator.ValidateForSubmitAsync`. Attached framework →
  `PendingReview` and `CurriculumReviewSubmitted` to the framework-owning
  expert; no framework → `Approved` (skip expert, still publish, no review
  notification).
- `POST /api/programs/{id}/withdraw-review` — `PendingReview` → `Draft`.
- `POST /api/programs/{id}/publish` — `Approved` → `Active`.
- `GET /api/programs/review-queue` — Expert sees `PendingReview` programs on
  their own frameworks; Manager/Admin see all pending.
- `GET /api/programs/{id}/curriculum-reviews` — decision history.
- `POST /api/programs/{id}/approve-review` — owning Expert only;
  `PendingReview` → `Approved`. Notifies `ForManagers`
  (`CurriculumReviewApproved`). Payload `programId` is the deeplink.
- `POST /api/programs/{id}/request-changes` — owning Expert only;
  `PendingReview` → `Draft`. `comment` is required. Notifies `ForManagers`
  (`CurriculumReviewChangesRequested`); inbox body includes the expert
  comment; payload `programId` is the deeplink.

Curriculum structure (and program metadata update/delete) is locked while
`PendingReview` or `Approved`. After `ChangesRequested` the program is `Draft`
again and can be edited. Optional `frameworkId` on create/update selects an
expert blueprint (`clearFramework` unlinks). Pre-check runs at submit-review,
not on create/update.

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
| Offline | Physical session; may offer QR check-in |

Flags: `RequireQrCheckin`, `RequireMediaEvidence`. Template times on `Activity`
are defaults; cohort-specific times live on `ClassSession`.
`RequireQrCheckin` enables student QR/code check-in; it does not require that
path for attendance or mentor-complete. `POST /api/activity-progresses/mentor-complete-bulk`
completes students with `Present`, `Late`, or `Excused` from either student
QR/code check-in or a mentor/manager roster mark.

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
`ClassSessionExpert` stores a co-teach invitation (`Invited` / `Accepted` /
`Declined`) and private mentor feedback after the session is completed.
Invite/accept/feedback endpoints are not exposed yet.

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
there is no progress. Each activity also exposes class nav `status`
(`completed` | `current` | `available`), optional `classSessionId` /
`sessionStatus` for LiveOnline/Offline, and the root `currentActivityId`
(single class cursor). Live/Offline completion follows the linked session
(`Completed`, or full-roster `Done` after mentor-complete); SelfPaced is
completed when all active students are `Done` or the class has moved past
(a later activity is `current`/`completed`). Assignment `status` is
`completed` (all active students graded), `submitted` (handed-in awaiting
grade), or `available`. Mentors are not locked out of future nodes.

Mentor student-progress (detail pane): roster-complete reads for the assigned
mentor —

- `GET /api/classes/{classId}/activities/{activityId}/student-progress` —
  one row per active enrollment (`NotStart` when no progress), counts, and for
  LiveOnline/Offline the primary session plus attendance fields.
- `GET /api/classes/{classId}/assignments/{assignmentId}/student-progress` —
  one row per active enrollment with the latest class-scoped attempt (or nulls
  if never started), counts, and class nav `status` matching curriculum-progress.

Class APIs are exposed through program and enrollment flows; entities exist in
domain and migrations.

## Materials

Learning assets for **SelfPaced** activities only (video, PDF, doc, etc.). At most
one material per activity. LiveOnline and Offline activities do not have materials.

Types via `MaterialType` enum. API: `/api/materials`.

## Experts

Experts associated with programs via `ProgramBoard` and the `Expert` entity.
`RoleType.Expert` is a dedicated login role. Manager/Admin provision it with
`POST /api/experts` (email + password required); the expert signs in through
`POST /api/auth/login` immediately. Public register does not allow Expert.
Password reset uses forgot-password OTP. `PUT /api/experts/{id}` does not
change credentials; `DELETE` locks the linked user.
Profile credentials: `Specialization` tags, `ExpertDegree`, and
`ExpertPublication`. Manager/Admin CRUD:

- `POST|PUT|DELETE /api/experts/{id}/degrees`
- `POST|PUT|DELETE /api/experts/{id}/publications`

Public reads: `GET /api/experts/{id}` and `GET /api/experts/{id}/profile`.

### Program framework and curriculum review

`ProgramFramework` is an expert-owned blueprint for a content family
(opt-in rules: `MinModules`, `MinOfflineSessions`, `MinLiveSessions`,
`RequireFinalAssessment` — null or `false` means not enforced; `true`
requires ≥1 `ResearchMilestone` with `IsCapstone`). `Category` is a hint/filter
only. `Program.FrameworkId` is optional; null means no expert review (submit goes
to `Approved`, manager still publishes). Attaching a framework always requires
the owning expert to approve, including when the rubric has zero criteria.
Each framework has `FrameworkRubricCriterion` rows (name, description, max
score, display order). Zero criteria is allowed; on approve, scores are
required only when at least one criterion exists (`0 ≤ score ≤ MaxScore` for
every criterion).

API: `/api/program-frameworks` — Expert CRUD on own blueprints; Manager/Admin
may list all and override updates (not create or delete). Category query is a
hint only. Frameworks stay editable while attached programs are
`PendingReview`.

`ProgramFrameworkValidator.ValidateForSubmitAsync` pre-checks a program against
non-null rules and joins every failure into one 400 message. Submit-review
calls it; a failing pre-check does not change status.

`CurriculumReview` is one expert decision round (`Approved` /
`ChangesRequested`) with optional `ReviewCriterionScore` rows. Distinct from
student `ProgramReview` star ratings. Only the framework owner may decide.

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
