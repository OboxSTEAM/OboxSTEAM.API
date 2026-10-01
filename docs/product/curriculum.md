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

`ProgramStatus`: **Draft** (manager is authoring and the advisor reviews in the
advisory chat; not open for registration), **Approved** (the advisor approved
the current curriculum version; ready for manager publish), **Active** (published; class creation and purchase/enroll
allowed when a recruiting cohort exists), **Inactive** (stopped; no new payment
or pending enrollment).

Student catalog list `GET /api/programs?status=Active` further requires at least
one Standard class in **Open** with remaining seats (aligned with
`GET .../open-classes` and checkout). Active programs that are still waiting on
class setup / mentor / open enrollment do not appear in that list.

Create via API is always **Draft** (omitted or explicit). `PUT` cannot set
`Approved`; only the approval endpoints move a program in or out of it. `Active` ↔ `Inactive` is allowed only when the
program is already in one of those two catalog states. Enrollment, class
creation, opening enrollment, and starting a class still require **Active**.

Lifecycle endpoints (Manager/Admin unless noted). Chat, change log, workspace,
and error codes: see [Advisory Chat and Approval](#advisory-chat-and-approval).

- `PUT /api/programs/{id}/advisor` — assign the one responsible active Expert
  with a linked login (`Draft` or `Approved`, else 409 `INVALID_STATUS`).
  Assignment adds the expert to `ProgramBoard` and posts an `AdvisorChanged`
  system message; on an `Approved` program the approval is revoked
  (`AdvisorChanged`) and the program returns to `Draft`.
- `POST /api/programs/{id}/approval/request` — `Draft` only; requires an
  advisor with an active linked login (`ADVISOR_REQUIRED`,
  `ADVISOR_LOGIN_REQUIRED`). Posts `ApprovalRequested` and notifies the
  advisor (`CurriculumApprovalRequested`). Does not change status.
- `POST /api/programs/{id}/approval` — advisor only (Expert). Body
  `{ curriculumVersion, comment? }`. `Draft` only; `curriculumVersion` must
  equal the program's (`CURRICULUM_VERSION_STALE`); no `Open` pins
  (`APPROVAL_BLOCKED`); the live framework check must pass
  (`FRAMEWORK_CHECK_FAILED`, check in `data`). Creates a `ProgramApproval`
  with a curriculum snapshot, resolves `Addressed` pins, moves to `Approved`,
  posts `Approved`, and notifies managers (`CurriculumReviewApproved`).
- `POST /api/programs/{id}/approval/revoke` — Manager/Admin or the advisor,
  `Approved` only; optional `{ reason }`. Revokes the active approval
  (`ManagerReopened` / `ExpertRevoked`), returns to `Draft`, posts
  `ApprovalRevoked`. A manager reopen notifies the advisor; an advisor revoke
  notifies managers (`CurriculumApprovalRevoked`).
- `POST /api/programs/{id}/publish` — `Approved` → `Active`; the active
  approval must cover the current curriculum version
  (`CURRICULUM_VERSION_STALE`). Notifies managers (`CurriculumReviewPublished`).
- `GET /api/programs/{id}/advisory` — workspace: status, curriculum version,
  advisor, participants (managers, advisor, board experts), capabilities
  (`canPost`, `canPin`, `canResolvePin`, `canEditCurriculum`,
  `canRequestApproval`, `canApprove`, `canRevokeApproval`, `canPublish`), the
  active approval, pin/unread counts, `frameworkCheckPassed`, and change counts
  since the last approval.
- `GET /api/programs/advisory-mine` — paginated assigned advisory programs
  (advisor/board for Expert; all for Manager/Admin).
- `GET /api/programs/{id}/framework-check` — structured expected/actual checks
  against the pinned framework version.
- Advisory chat: `/api/programs/{id}/advisory-discussion/*` (messages,
  mentions, pins, attachments).
- Removed (410 `ENDPOINT_REMOVED`): `submit-review`, `withdraw-review`,
  `approve-review`, `request-changes`, `PUT review-submissions/{id}/draft`,
  `GET review-queue`, and the advisory thread write routes. The old review and
  thread read routes stay read-only until they are removed.

Curriculum edits are locked only by live cohorts (a class `InProgress`, or an
`Open` class with `Active` enrollments). An edit while an approval is active
revokes it (`CurriculumEdited`); an `Approved` program returns to `Draft`.
Optional `frameworkId` on create/update selects an expert blueprint
(`clearFramework` unlinks). The framework check runs at approval, not on
create/update.

## Advisory Chat and Approval

One realtime chat per program plus a single versioned approval by the
responsible advisor. Participants are Manager/Admin, the advisor
(`Program.AdvisorExpertId`), and `ProgramBoard` experts. Only the advisor
approves. Realtime events and notifications: `docs/product/notifications.md`.

### Curriculum version and change log

`Program.CurriculumVersion` (int64, starts at 0) increments once per
`SaveChanges` that mutates curriculum. It is returned on program DTOs and the
workspace. Curriculum = program content fields (`name`, `code`, `description`,
`level`, `category`, `estimatedDuration`, `skillsGained`, `thumbnailUrl`, skill
links), modules, courses, activities, assignments, research milestones,
milestone-activity links, materials. Not curriculum (no bump, allowed on Active
programs): `price`, `retakeFee`, `status`, framework, advisor, board, ratings.

An EF Core `SaveChanges` interceptor writes `CurriculumChange` rows in the same
transaction (`version`, actor, `targetType`, `targetId`, `changeKind`
`Created`/`Updated`/`Deleted`/`Moved`/`Reordered`, typed `fieldsJson`
`[{ fieldKey, before, after }]`, parent and order before/after, label and
ancestor path snapshots). Rules live in `CurriculumChangeRecorder`:

- Only fields in `CurriculumChangeFieldCatalog` count. Skill links are recorded
  on the Program as `skill:{skillId}`; milestone links on the milestone as
  `activityLink:{activityId}` / `activityLinkRequired:{activityId}`.
- Creating or deleting the program records nothing. A cascaded delete records
  only the topmost component. Sibling order shifts from insert/delete/move are
  not recorded separately. Seeding runs with recording suppressed.
- No concurrency token on `CurriculumVersion`: two simultaneous saves may share
  a version; consolidation orders rows by `(version, at)`.

`GET /api/programs/{id}/curriculum/changes` — participants. Query `base`:
`lastApproval` (default; `start` if never approved), `lastSeen`, `start`, or
`version:N`; optional `to` (default current). Invalid → 400. Returns
`{ fromVersion, toVersion, currentVersion, seenVersion, summary { created,
updated, deleted, moved }, items[] }`. Each item: `targetType`, `targetId`,
`label`, `path[]`, `changeKind`, `fields[] { fieldKey, label, valueType,
before, after }`, `moved?`, `reorderedChildren[]`, `changedBy[]`,
`lastChangedAt`, `isUnseen`.

- `label` is an English default (clients may localize by `fieldKey`); dynamic
  keys get `Skill: {name}`, `Linked activity: {name}`,
  `Required before submission: {name}`. `valueType`: `ShortText`, `LongText`,
  `Number`, `DurationMinutes`, `Enum`, `Boolean`, `List`, `Media`.
- `moved` = `{ fromParentLabel, toParentLabel, fromOrder, toOrder }` for
  `Moved`, single-item `Reordered`, and `Updated` items whose order changed.
  `reorderedChildren` is set on a parent when two or more children reordered.
- Items are in current tree order; deleted items follow their deepest surviving
  ancestor. `summary.moved` counts `Moved` and `Reordered`.
- Net consolidation: several updates → first `before`, last `after`; a field
  back to its original value is dropped (an empty `Updated` item too); created
  then updated → `Created` with final values (`before` null); created then
  deleted → omitted; updated then deleted → `Deleted`; reorders under one parent
  → one `Reordered` item on the parent; move to another parent → `Moved`.

`POST /api/programs/{id}/curriculum/changes/seen` — body `{ version }`; stores
`max(seenVersion, version)` per user (drives `isUnseen`, `unseenChangeCount`);
`version` > current → 400.

Editing session message (no background job): on each curriculum save by user
U, if U's latest `CurriculumUpdated` message is under 10 minutes old (from its
last update) and U has posted no user message since, its payload (`toVersion`,
`changeCount`) is updated, `editedAt` bumped, and it moves to the end of the
stream (new `sequence`). Otherwise a new one is posted with `fromVersion` = the
version before the save. Saves without a user post nothing. `changeCount` is
the net item count. A Manager/Admin chat message closes that manager's session.

### Approval

`ProgramApproval`: `curriculumVersion` (approved), `fromVersion` (previous
approval's version or 0), `frameworkVersionId`, `frameworkCheckJson`,
`curriculumSnapshotJson`, `approvedByExpertId`, `approvedAt`, `comment`
(≤ 2000), `revokedAt`, `revokedByUserId`, `revokeReason` (`ManagerReopened`,
`CurriculumEdited`, `ExpertRevoked`, `AdvisorChanged`). At most one
non-revoked approval per program.

Approve checks, in order: caller is the advisor (403); status `Draft`
(409 `INVALID_STATUS`); `curriculumVersion` matches (409
`CURRICULUM_VERSION_STALE`); no `Open` pins (409 `APPROVAL_BLOCKED`, message
includes the count); framework check passes (409 `FRAMEWORK_CHECK_FAILED`,
`FrameworkCheckDto` in `value.data`). On success, in one transaction: create
the approval, resolve every `Addressed` pin, set `Approved`, post `Approved`,
notify managers.

Auto-revoke: any curriculum save while an approval is active (or the program is
`Approved`) revokes it (`CurriculumEdited`) in the same save and posts
`ApprovalRevoked` before the session message. `Approved` returns to `Draft`;
`Active`/`Inactive` keep their status (re-approval of live programs is out of
scope). Later edits find no active approval and only extend the session
message.

Workspace `GET /api/programs/{id}/advisory` returns `programId`, `status`,
`curriculumVersion`, `advisorExpertId`, `advisorName`, `participants[]
{ userId, name, role, isAdvisor }` (active managers, then the advisor, then
board experts), `capabilities`, `approval` (active approval or null:
`id`, `curriculumVersion`, `approvedAt`, `approvedByName`, `comment`),
`openPinCount`, `addressedPinCount`, `unreadCount`, `frameworkCheckPassed`
(live), `changesSinceApprovalCount` (net items for `base=lastApproval`),
`unseenChangeCount`, `latestSequence`. Approval request/approve/revoke return
the workspace DTO.

| Capability | Rule |
| --- | --- |
| `canPost` | participant |
| `canPin`, `canResolvePin` | advisor or board expert |
| `canEditCurriculum` | Manager/Admin and status `Draft` or `Approved` |
| `canRequestApproval` | Manager/Admin, `Draft`, advisor assigned with a login |
| `canApprove` | caller is the advisor and status `Draft` |
| `canRevokeApproval` | `Approved` and caller is Manager/Admin or the advisor |
| `canPublish` | Manager/Admin, `Approved`, approval version = `curriculumVersion` |

Board experts and the advisor get read access to the program's modules,
courses, activities, assignments, research milestones (and links), and
material signed URLs.

### Mention index

`GET /api/programs/{id}/mention-targets` — participants. Flat list of every
component in curriculum tree order: `{ targetType, targetId, label, code,
path[] { targetType, targetId, label }, moduleId, courseId, activityId, order }`.
`order` is the zero-based tree index; `activityId` is set for activities and
materials. Tree order: program; each module; then courses → activities (each
followed by its material) → course assignments, or for Research modules
milestones → linked activities (+ material) → deliverable assignment; then the
module's remaining assignments by code. Each component appears once.

### Discussion

Base `/api/programs/{id}/advisory-discussion`, participants only. Store:
`ProgramAdvisoryDiscussionMessage` (`sequence` per program, monotonic; `kind`
`User`/`System`; `text` ≤ 4000 with `@[Type:uuid]` tokens). Mentions reuse
`ProgramAdvisoryReference` (`Node` / `WorkingDraft`) via
`ProgramAdvisoryDiscussionMessageReference`. Target types: `Program`,
`Module`, `Course`, `Activity`, `Assignment`, `ResearchMilestone`, `Material`.

Message DTO: `id`, `programId`, `sequence`, `cursor` (`programId:sequence`),
`kind`, `authorUserId`, `authorName`, `authorRole`, `text`, `clientMessageId`,
`systemEvent { code, payload }`, `references[]` (by first token occurrence),
`attachments[]`, `pin { status, pinnedByName, pinnedAt, addressedByName,
addressedAt, resolvedByName, resolvedAt }`, `createdAt`, `editedAt`,
`isDeleted`. `systemEvent` and `pin` are null when not applicable. Removed
messages (`RemovedAt`, separate from global soft delete) are tombstones: empty
text, no references, attachments, or pin; excluded from filters and counts.

| Method | Route | Who | Body / query | Result |
| --- | --- | --- | --- | --- |
| GET | `/messages` | participant | `before?`, `after?`, `pageSize` (1–100, default 30), `targetType?`, `targetId?` | `{ messages[], before, after, hasMoreBefore, hasMoreAfter }` |
| GET | `/messages/{messageId}` | participant | | Message DTO |
| POST | `/messages` | participant | `{ text, attachmentIds[], clientMessageId }` | Message DTO; idempotent on author + `clientMessageId` |
| PATCH | `/messages/{messageId}` | author | `{ text }` | Message DTO; mentions re-parsed |
| DELETE | `/messages/{messageId}` | author | | Removal (tombstone; pin removed) |
| POST | `/messages/{messageId}/pin` | advisor or board expert | | Pin `Open` (idempotent) |
| DELETE | `/messages/{messageId}/pin` | advisor or board expert | | Pin removed (idempotent) |
| POST | `/messages/{messageId}/pin/actions` | see below | `{ action }` | Message DTO |
| GET | `/pins` | participant | `status?` | Message DTO[] by `pinnedAt` |
| GET | `/mention-counts` | participant | | `[{ targetType, targetId, messageCount, openPinCount }]` |
| POST | `/read` | participant | `{ cursor?, lastDisplayedSequence }` | Read cursor |
| POST | `/attachments` | participant | multipart `file` | AttachmentDto |
| GET | `/attachments/{attachmentId}/url` | participant | | `{ url, expiresAt }` (15 minutes) |

Pin actions: `MarkAddressed` (Manager/Admin, `Open` → `Addressed`), `Reopen`
(advisor or board expert, `Addressed`/`Resolved` → `Open`), `Resolve` (advisor
or board expert, `Open`/`Addressed` → `Resolved`). Unpinned message or wrong
status → 409 `INVALID_STATUS`. Only `Open` pins block approval. System
messages cannot be pinned, edited, or removed. `targetType` + `targetId` (both
or neither, else 400) and mention counts match the exact component only;
descendants are not rolled up.

Posting rules: tokens are `@[Type:uuid]` (type case-insensitive; malformed
tokens stay plain text). Targets must belong to the program (400
`MENTION_TARGET_INVALID`). Text ≤ 4000 (`MESSAGE_TOO_LONG`), ≤ 20 distinct
mentions (`TOO_MANY_MENTIONS`), ≤ 10 attachments (`TOO_MANY_ATTACHMENTS`),
text and attachments not both empty (`MESSAGE_EMPTY`). `attachmentIds` must be
the caller's unsent attachments for this program (`ATTACHMENT_INVALID`).

System event codes and payloads:

| Code | Payload | Posted when |
| --- | --- | --- |
| `CurriculumUpdated` | `{ actorUserId, actorName, fromVersion, toVersion, changeCount }` | Editing session (updated in place) |
| `ApprovalRequested` | `{ requestedByName }` | `POST approval/request` |
| `Approved` | `{ approvalId, curriculumVersion, approvedByName, comment? }` | Approval |
| `ApprovalRevoked` | `{ approvalId, reason, actorName, comment? }` | Any revoke path |
| `Published` | `{ publishedByName }` | Publish |
| `AdvisorChanged` | `{ previousAdvisorName?, newAdvisorName? }` | `PUT advisor` |

Attachments (`ProgramAdvisoryDiscussionAttachment`, S3 key
`advisory/{programId}/{attachmentId}/{fileName}`): AttachmentDto `{ id,
fileName, contentType, sizeBytes, kind (Image/File), uploaderUserId,
createdAt }`. ≤ 20 MB (400 `ATTACHMENT_TOO_LARGE`). Allowed by content type
and extension: `png`, `jpg`, `jpeg`, `gif`, `webp`, `pdf`, `doc`, `docx`,
`ppt`, `pptx`, `xls`, `xlsx`, `zip`; otherwise or on a content type that does
not match the extension, 400 `ATTACHMENT_TYPE_NOT_ALLOWED` (empty or
`application/octet-stream` is accepted). Unsent attachments are visible only
to the uploader and purged (S3 object + row) 24 hours after upload by an hourly
job. The URL of an attachment on a removed message returns 404.

Save as material: `POST /api/materials/from-discussion-attachment`
`{ attachmentId, activityId, title }` — Manager/Admin; `CurriculumEditGuard`
(an `Approved` program auto-revokes). The activity must be `SelfPaced`, in the
attachment's program, with no material (409 `MATERIAL_ACTIVITY_INVALID`); the
attachment must be sent (400 `ATTACHMENT_INVALID`); material type and size
rules apply (`ppt`, `xls`, `zip`, etc. → 400 `ATTACHMENT_TYPE_NOT_ALLOWED`).
The S3 object is copied to the material key space; returns the material DTO.

### Error codes

| Code | HTTP | Where |
| --- | --- | --- |
| `CURRICULUM_VERSION_STALE` | 409 | approve, publish |
| `APPROVAL_BLOCKED` | 409 | approve (open pins) |
| `FRAMEWORK_CHECK_FAILED` | 409 | approve (`data` = `FrameworkCheckDto`) |
| `FRAMEWORK_UNAVAILABLE` | 409 | pinned framework version not published |
| `INVALID_STATUS` | 409 | lifecycle or pin action in the wrong status |
| `ADVISOR_REQUIRED`, `ADVISOR_LOGIN_REQUIRED` | 400 | approval request |
| `MENTION_TARGET_INVALID` | 400 | post/edit message |
| `MESSAGE_EMPTY`, `MESSAGE_TOO_LONG`, `TOO_MANY_MENTIONS`, `TOO_MANY_ATTACHMENTS` | 400 | post/edit message |
| `ATTACHMENT_INVALID`, `ATTACHMENT_TOO_LARGE`, `ATTACHMENT_TYPE_NOT_ALLOWED` | 400 | attachments, save as material |
| `MATERIAL_ACTIVITY_INVALID` | 409 | save as material |
| `FRAMEWORK_RULES_INVALID` | 400 | framework version save |
| `ENDPOINT_REMOVED` | 410 | removed endpoints |

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

1. `POST /api/classes` always creates **Draft**. The program must be **Active**. `StartDate` must be at least 14 days out. Mentor is optional. Reassigning `ProgramId` on `PUT` also requires the target program to be **Active**.
2. Generate the timetable (`POST /api/class-sessions/generate`, or add sessions manually). Coverage is one active session per LiveOnline/Offline activity plus each assignment.
3. When coverage is complete, the class becomes **ReadyForMentor** (automatically after generate/create, or `POST /api/classes/{id}/ready-for-mentor`). Mentors request assignment from the board (`GET /api/class-mentor-requests/board`). Students cannot enroll.
4. After a mentor is assigned, `POST /api/classes/{id}/open` moves **ReadyForMentor → Open**. The program must still be **Active**. Students may enroll only in this status.
5. `POST /api/classes/{id}/start` (or auto-start when full and `StartDate` has arrived) moves **Open → InProgress**. The program must still be **Active**. Auto-start skips the class when the program is not Active. Enrollment closes.
6. `POST /api/classes/{id}/complete` moves **InProgress → Completed**.

If sessions are deleted or cancelled so coverage no longer matches the curriculum, **ReadyForMentor** returns to **Draft**.

`ClassSession` schedules concrete session instances. `SessionKind` mirrors the
curriculum item: **LiveOnline**, **Offline**, or **AssignmentWindow**. LiveOnline
join links live on `MeetingUrl` (separate from free-text `Location`).
`SessionAttendance` records attendance status per student.
Student QR/code check-in for Offline sessions:

| Endpoint | Who | Notes |
| --- | --- | --- |
| `POST /api/class-sessions/{id}/checkin-token` | Mentor / Manager / Admin | Rotate QR UUID + 6-digit code (~60s TTL). Live 6-digit codes are unique across non-expired tokens. |
| `POST /api/class-sessions/{id}/checkin` | Student | Web / session-scoped. Body: exactly one of `{ "token" }` or `{ "code" }`. |
| `POST /api/class-sessions/checkin-by-token` | Student | Mobile scan-first. Same body; resolves live token/code → session (no `sessionId` in path). Code must match exactly one non-expired live token (0 or >1 → fail closed). |

`ClassSessionExpert` stores a co-teach invitation (`Invited` / `Accepted` /
`Declined`) and private mentor feedback after the session is completed.
Students must not see feedback fields. Multiple Invited or Accepted experts
are allowed on one session. The same expert cannot hold two active invites
on the same session. After `Declined` or manager withdraw, that expert (or
another) may be invited again. Changing `StartTime` or `EndTime` applies
immediately, then soft-deletes every `Invited` and `Accepted` co-teach row
and notifies those experts (`ClassSessionExpertClearedOnReschedule`).
`Declined` rows stay. Title, location, and other non-time edits do not
touch co-teach links. Time changes are blocked while the session is
`InProgress` (`400`). `Completed` and `Cancelled` sessions cannot be
modified at all. Manager may invite experts again after a time change.
Deleting a session, or moving it to `Cancelled`, also soft-deletes every
`Invited` and `Accepted` row and notifies those experts
(`ClassSessionCancelled`). Deleting the class (Draft / ReadyForMentor / Open
with no students) does the same for every remaining session. `GET /mine`
and the manager list omit invitations whose session is soft-deleted.
Removing an expert from `ProgramBoard` (or replacing their program list, or
deleting the expert) soft-deletes that expert's `Invited` and `Accepted`
rows on the program's sessions (`ClassSessionExpertInvitationWithdrawn`).
`Declined` rows stay.

Co-teach API (`/api/class-session-experts`):

- `POST /` — Manager/Admin invites a `ProgramBoard` expert to a **Scheduled
  Offline** session.
- `GET /mine` — Expert lists own invitations.
- `GET /` — Manager/Admin list (`classId` / `sessionId` / `expertId` / `status`).
- `POST /{id}/accept` and `POST /{id}/decline` — owning Expert; accept is
  blocked (`409`) on calendar overlap with another Accepted Offline/LiveOnline
  session.
- `POST /{id}/withdraw` — Manager/Admin, **Invited only**. Accepted cannot be
  withdrawn.
- `PUT /{id}/feedback` — owning Accepted expert, session **Completed**. Upserts
  one class-level overview (`MentorFeedback` + rating 1–5). Declined experts and
  `Cancelled` sessions cannot submit. Feedback is private: Expert (own row),
  Manager/Admin, and the class Mentor may read it. Students never receive
  feedback fields.

Session reads:

- `GET /api/classes/{classId}/sessions` and `GET .../sessions/{id}` expose
  `hasAcceptedExpert`, `coTeach` (first Accepted card, backward compatible),
  and `coTeaches` (all Accepted cards: name, title, avatar, specialization,
  degrees). These routes do not include feedback.
- `GET .../sessions/with-students/{sessionId}` adds `coTeachFeedback` /
  `coTeachFeedbacks` (`invitationId`, `expertId`, `expertName`, `comment`,
  `rating`, `feedbackAt`) only for Manager, Admin, and the assigned class
  Mentor. Students still see the public cards.

When an Offline session with at least one Accepted expert first becomes
**Completed**, each Accepted expert is notified to submit feedback. Submitting
or updating feedback notifies the class mentor.

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
Milestone create accepts a missing `timeLimitMinutes` unless the deliverable
is a Quiz; that field is the quiz clock, not the class window.

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

Material files are not public while a program is `Draft` or `Approved`.
Managers and Admins can receive an authorized preview; Experts only for
programs where they are the advisor or a board member (else 403);
students use enrollment-scoped access. Once a program is `Active`, the activity
material endpoint may be called without enrollment and returns a time-limited
preview URL. The S3 `materials/*` prefix is excluded from the bucket's anonymous
read policy.

## Experts

Experts associated with programs via `ProgramBoard` and the `Expert` entity.
`RoleType.Expert` is a dedicated login role. Manager/Admin provision it with
`POST /api/experts` (email required; temporary password auto-generated and
emailed — create rolls back if email fails); the expert signs in through
`POST /api/auth/login` immediately. Public register does not allow Expert.
Password reset uses forgot-password OTP. `PUT /api/experts/{id}` does not
change credentials; `DELETE` locks the linked user.
Profile credentials: `Specialization` tags, `ExpertDegree`, and
`ExpertPublication`. Manager/Admin CRUD:

- `POST|PUT|DELETE /api/experts/{id}/degrees`
- `POST|PUT|DELETE /api/experts/{id}/publications`

Public reads: `GET /api/experts/{id}` and `GET /api/experts/{id}/profile`.

### Program framework and curriculum review

`ProgramFramework` is a reusable expert-authored identity. Its immutable
published `ProgramFrameworkVersion` rows hold description, academic guidance,
and optional rules: module count (min/max), courses per non-Research module
(min/max), total hours (min/max), max activity minutes, activity duration
required, Offline/LiveOnline session minimums and share of activities,
assignment in every module, valid assignment pass score, materials per
SelfPaced activity, category match, description length, skills gained, thumbnail,
and capstone research milestone. Blank or `false` rules are unrestricted;
values are ≥ 0, each min ≤ its max, and the two ratios sum to ≤ 100
(`FRAMEWORK_RULES_INVALID`). The rubric was removed. A program pins one published
version and many programs may pin the same version, even when they have
different responsible advisors. Publishing a newer version never changes an
existing program. Managers explicitly adopt a newer version while curriculum
is editable. Framework `Category` is guidance only and never an eligibility
gate.

Published versions and referenced history are immutable. Draft updates are
partial: `null` leaves a rule unchanged and `clear<Field>` turns a numeric rule
off.

API: `/api/program-frameworks` — Expert CRUD on own blueprints; Manager/Admin
may list and read all but cannot write another expert's blueprint.
Create/archive/version publishing stay Expert-author-only. Category query is a
hint only. Authoring occurs on one draft version. The old rubric and criteria
routes return 410 `ENDPOINT_REMOVED`. Archive prevents new assignment while retaining existing
pins and history. Version routes live under
`/api/program-frameworks/{id}/versions`.

Rule fields and `FrameworkRuleEvaluator` check codes (checks are emitted only
for rules that are on):

| Field | Check code |
| --- | --- |
| `minModules`, `maxModules` | `MinModules`, `MaxModules` |
| `minCoursesPerModule`, `maxCoursesPerModule` | `CoursesPerModule` |
| `minTotalHours`, `maxTotalHours` | `TotalHours` |
| `maxActivityMinutes` | `MaxActivityDuration` |
| `requireActivityDuration` | `ActivityDurationSet` |
| `minOfflineRatioPercent`, `minLiveRatioPercent` | `OfflineRatio`, `LiveRatio` |
| `minOfflineSessions`, `minLiveSessions` | `MinOfflineSessions`, `MinLiveSessions` |
| `requireAssignmentPerModule` | `AssignmentPerModule` |
| `requireAssignmentPassScore` | `AssignmentPassScore` |
| `minMaterialsPerActivity` | `MaterialsPerActivity` |
| `requireCategoryMatch` | `CategoryMatch` |
| `minDescriptionLength` | `DescriptionLength` |
| `minSkillsGained` | `SkillsGained` |
| `requireThumbnail` | `ThumbnailSet` |
| `requireCapstoneResearchMilestone` | `RequireCapstoneResearchMilestone` |

Each check is `{ code, label, expected, actual, passed,
affectedCurriculumLinks[] }` (failing components; empty for program-level
checks). `TotalHours` compares minutes (`hours × 60`); null or ≤ 0 durations
count as 0 and fail `ActivityDurationSet`. Ratios are `count / all activities
× 100` (0 with no activities). `CoursesPerModule` applies to non-Research
modules. `AssignmentPerModule` counts any assignment whose `moduleId` is the
module. `AssignmentPassScore` requires `0 < passScore ≤ maxPoints`.
`MaterialsPerActivity` counts SelfPaced activities. `MaxModules` links the
modules beyond the maximum by order. `SkillsGained` counts `ProgramSkill`
links. `CategoryMatch` compares `Program.Category` with the framework category.
The advisory-board frozen-snapshot highlights keep only the original four
checks.

`ProgramFrameworkValidator.ValidateForSubmitAsync` runs the same checks and
joins every failure into one 400 `FRAMEWORK_CHECK_FAILED` message for the old
submit-review path, which now returns 410; approval uses the live check (409).

`CurriculumReview` is one expert decision round (`Approved` /
`ChangesRequested`) with a comment. Distinct from
student `ProgramReview` star ratings. Only `Program.AdvisorExpertId` may make
the formal decision; framework authorship does not grant that authority.
Other board experts may advise and may be invited to co-teach. The responsible
advisor cannot be removed from the board until the program is reassigned.
Decisions attach to a `ProgramReviewSubmission` when present. Legacy history without a genuine
curriculum snapshot reports `snapshotAvailable=false`.

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
