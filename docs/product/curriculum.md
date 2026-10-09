# Curriculum Model

## Hierarchy

```text
Program
  ├── Framework? (ProgramFramework — optional blueprint)
  ├── ProgramBoard (expert associations)
  ├── ProgramApproval[] (advisor approvals of a curriculum version)
  ├── Module[]
  │     ├── Course[]
  │     │     └── Activity[]
  │     │           └── Material? (SelfPaced only)
  │     ├── ResearchMilestone[] (Research modules; linked activities)
  │     ├── Assignment[] (module- or course-scoped)
  ├── Class[] (cohorts)
  │     └── ClassSession[]
  │           └── ClassSessionExpert[] (co-teach invite + private mentor feedback)
  └── ProgramReview[]
```

## Program

Represents a sellable STEAM track (e.g. robotics, coding). Key fields: `Code`,
`Name`, `Category`, `Level`, `Price`, `Rating`, `Status`. Catalog skills are
`ProgramSkill` links (`SkillIds` on create/update, `Skills` on responses).

`ProgramStatus`: **Draft** (manager is authoring and the advisor reviews in the
advisory chat; not open for registration), **Approved** (the advisor approved
the current curriculum version; ready for manager publish), **Active** (published; class creation and purchase/enroll
allowed when a recruiting cohort exists), **Inactive** (stopped; no new payment
or pending enrollment).

Student catalog list `GET /api/programs?status=Active` further requires at least
one Standard class in **Open** with remaining seats (aligned with
`GET .../open-classes` and checkout). Active programs that are still waiting on
class setup / mentor / open enrollment do not appear in that list.

Create via API is always **Draft** (omitted or `Draft`; any other `status` → 400).
Create accepts an optional `advisorExpertId` (same rules as `PUT .../advisor`;
the expert is added to `ProgramBoard`). `PUT` cannot change status except
`Active` ↔ `Inactive` when the program is already in one of those two catalog
states (otherwise 400); only the approval and publish endpoints move a program
into or out of `Draft`/`Approved`. Enrollment, class creation, opening
enrollment, and starting a class still require **Active**.

Lifecycle endpoints (Manager/Admin unless noted). Chat, change log, workspace,
and error codes: see [Advisory Chat and Approval](#advisory-chat-and-approval).

- `PUT /api/programs/{id}/advisor` — assign the one responsible active Expert
  with an active linked login (`Draft` or `Approved`, else 409
  `INVALID_STATUS`; missing or invalid expert → 400). Assignment adds the
  expert to `ProgramBoard` (role "Responsible advisor") when missing. Only
  when the advisor actually changes: posts an `AdvisorChanged` system message
  and, on an `Approved` program, revokes the approval (`AdvisorChanged`) and
  returns the program to `Draft`.
- `POST /api/programs/{id}/approval/request` — `Draft` only; the program must
  have a framework (409 `FRAMEWORK_REQUIRED`); requires an
  advisor with an active linked login (`ADVISOR_REQUIRED`,
  `ADVISOR_LOGIN_REQUIRED`). Posts `ApprovalRequested` and notifies the
  advisor (`CurriculumApprovalRequested`). Does not change status.
- `POST /api/programs/{id}/approval` — advisor only (Expert). Body
  `{ curriculumVersion, comment? }`. Framework required (409
  `FRAMEWORK_REQUIRED`). `Draft` only; `curriculumVersion` must
  equal the program's (`CURRICULUM_VERSION_STALE`); no `Open` pins
  (`APPROVAL_BLOCKED`); the live framework check must pass
  (`FRAMEWORK_CHECK_FAILED`, check in `data`). Creates a `ProgramApproval`
  with a curriculum snapshot, resolves `Addressed` pins, moves to `Approved`,
  posts `Approved`, and notifies managers (`CurriculumReviewApproved`).
- `POST /api/programs/{id}/approval/revoke` — Manager/Admin or the advisor,
  `Approved` only; optional `{ reason }` (≤ 2000, stored as
  `revokeComment`). Revokes the active approval (`ManagerReopened` /
  `ExpertRevoked`), returns to `Draft`, posts `ApprovalRevoked`. A manager
  reopen notifies the advisor (when the advisor has a login and is not the
  caller); an advisor revoke notifies managers. Both use notification type
  `CurriculumApprovalRevoked`.
- `POST /api/programs/{id}/publish` — with a framework: `Approved` → `Active`
  (else 409 `INVALID_STATUS`); the active approval must cover the current
  curriculum version (`CURRICULUM_VERSION_STALE`). Without a framework there is
  no approval step: `Draft` or `Approved` → `Active` directly (other statuses
  409 `INVALID_STATUS`). Posts `Published` and notifies managers
  (`CurriculumReviewPublished`).
- `POST /api/programs/{id}/framework-version` — Manager/Admin. Body
  `{ frameworkVersionId }`. Upgrades the pinned framework version; see
  [Framework version upgrade](#framework-version-upgrade).
- `GET /api/programs/{id}/advisory` — workspace: status, curriculum version,
  `frameworkVersionNumber`, `latestFrameworkVersionNumber`,
  `hasNewerFrameworkVersion`, advisor, participants (managers, advisor, board
  experts), capabilities (`canPost`, `canPin`, `canResolvePin`,
  `canEditCurriculum`, `canRequestApproval`, `canApprove`, `canRevokeApproval`,
  `canPublish`, `canUpgradeFrameworkVersion`), the active approval, pin/unread
  counts, `frameworkCheckPassed`, and change counts since the last approval.
- `GET /api/programs/advisory-mine` — Manager/Admin/Expert; paginated
  assigned advisory programs (advisor/board for Expert, 403 without an expert
  profile; all for Manager/Admin). Query `page`, `pageSize`, `status`,
  `unreadOnly`. Each item: `programId`, `code`, `name`, `status`,
  `frameworkVersionNumber`, `isAdvisor`, `latestActivityAt` (latest chat
  message create/edit, including removed messages, else program
  update/create), `unreadCount` (messages after the read cursor, excluding the
  caller's own, same rule as the workspace), `openPinCount`, `approvalState`
  (`None` = never approved, `Approved` = active approval, `Revoked` = all
  approvals revoked). `unreadOnly` keeps items with `unreadCount > 0`.
  Ordered by `latestActivityAt` desc, then name.
- `GET /api/programs/{id}/framework-check` — advisory participants only
  (Manager/Admin, the advisor, board experts); structured expected/actual
  checks against the pinned framework version.
- Advisory chat: `/api/programs/{id}/advisory-discussion/*` (messages,
  mentions, pins, attachments).
- Removed (410 `ENDPOINT_REMOVED`, routes stay registered): `submit-review`,
  `withdraw-review`, `approve-review`, `request-changes`, `GET review-queue`,
  `{id}/curriculum-reviews`, `{id}/review-submissions/*` (list, detail,
  `changes`, draft GET/PUT), `{id}/advisory-threads/*` (list, detail,
  `pins`, messages, writes, `{threadId}/read`), `{id}/advisory/board`,
  `{id}/advisory/timeline`, `{id}/advisory-read`,
  `{id}/advisory-references/*`, `advisory-anchor-fields`, and the framework
  `rubric` / `criteria*` routes.

Curriculum edits are locked only by live cohorts (a class `InProgress`, an
`Open` class with `Active` enrollments, or an `Open` class with a live seat hold
whose program enrollment has a `Pending` Stripe/parent payment): 409
`CURRICULUM_LOCKED_COHORT`. A plain 5-minute seat hold without an open payment
does not lock. The same lock and code apply to program delete, thumbnail upload,
framework version upgrade, and a program update that changes a curriculum field
(values equal to the current ones do not count); `status` and `price` updates
stay allowed. An edit while an approval is active revokes it (`CurriculumEdited`).
See [Approval](#approval) for which statuses return to `Draft`.

Optional `frameworkId` / `frameworkVersionId` on create selects an expert
blueprint. After create the framework is locked: `PUT /api/programs/{id}` returns
409 `FRAMEWORK_LOCKED` when `frameworkId` or `frameworkVersionId` is non-null and
differs from the stored value, or `clearFramework: true` is sent for a program
that has a framework. Null, omitted, and matching values are ignored. Use the
upgrade endpoint to move to a newer version of the same framework. The framework
check runs at approval and after an upgrade (workspace `frameworkCheckPassed`),
not on create/update.

`GET /api/programs/{id}` and the workspace return `frameworkVersionNumber`,
`latestFrameworkVersionNumber` (highest published version of the program's
framework), and `hasNewerFrameworkVersion` (null/false without a framework).

Checkout (`select-class`, direct Stripe checkout, parent payment request, parent
checkout) on a program that is not `Active` releases the student's pending
checkout (seat hold withdrawn, pending payments cancelled, open payment requests
expired) and fails with 400 `PROGRAM_NOT_AVAILABLE`, so no seat is sold on a
`Draft` program.

## Advisory Chat and Approval

One realtime chat per program plus a single versioned approval by the
responsible advisor. Participants are Manager/Admin, the advisor
(`Program.AdvisorExpertId`), and `ProgramBoard` experts. Only the advisor
approves. Realtime events and notifications: `docs/product/notifications.md`.

### Curriculum version and change log

`Program.CurriculumVersion` (int64, starts at 0) increments once per
`SaveChanges` that mutates curriculum. It is returned on program DTOs and the
workspace. Curriculum = program content fields (`name`, `code`, `seriesName`,
`description`, `level`, `category`, `estimatedDuration`, `thumbnailUrl`, skill
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
  not recorded separately. Seeding runs with recording suppressed; the
  advisory chat seed (`SeedService.AdvisoryChatDemo`) writes its change rows,
  `CurriculumVersion` bumps and system messages explicitly.
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
`version` > current → 400. An item is unseen only when someone other than the
viewer changed it after `seenVersion`; the viewer's own edits never count.

Editing session message (no background job): on each curriculum save by user
U, if U's latest `CurriculumUpdated` message is under 10 minutes old (from its
last update) and U has posted no user message since, its payload (`toVersion`,
`changeCount`) is updated, `editedAt` bumped, and it moves to the end of the
stream (new `sequence`). Otherwise a new one is posted with `fromVersion` = the
version before the save. Saves without a user post nothing. `changeCount` is
the net item count. A Manager/Admin chat message closes that manager's session.

### Approval

`ProgramApproval`: `curriculumVersion` (approved), `fromVersion` (the most
recent previous approval's version, revoked or not, capped at the current
version; 0 when none), `frameworkVersionId`, `frameworkCheckJson`,
`curriculumSnapshotJson` (built by `CurriculumSnapshotBuilder`),
`approvedByExpertId`, `approvedAt`, `comment` (≤ 2000, else 400),
`revokedAt`, `revokedByUserId`, `revokeReason` (`ManagerReopened`,
`CurriculumEdited`, `ExpertRevoked`, `AdvisorChanged`, `FrameworkUpgraded`),
`revokeComment` (≤ 2000). At most one non-revoked approval per program.

Approve checks, in order: caller is the advisor (403); program has a framework
(409 `FRAMEWORK_REQUIRED`); status `Draft`
(409 `INVALID_STATUS`); `curriculumVersion` matches (409
`CURRICULUM_VERSION_STALE`); no `Open` pins (409 `APPROVAL_BLOCKED`, message
includes the count); framework check passes (409 `FRAMEWORK_CHECK_FAILED`,
`FrameworkCheckDto` in `value.data`). On success, in one transaction: create
the approval, resolve every `Addressed` pin, set `Approved`, post `Approved`,
notify managers.

Auto-revoke: any curriculum save while an approval is active, while the program
is `Approved`, or while a program with a framework is `Active`/`Inactive`
revokes the approval (`CurriculumEdited`) in the same save, posts
`ApprovalRevoked` before the session message, and notifies the advisor
(`CurriculumApprovalRevoked`, only when the advisor has a login and is not the
editor). `Approved` returns to `Draft`; a program with a
framework also returns from `Active`/`Inactive` to `Draft`, because every
published version must be approved again. Programs without a framework keep
`Active`/`Inactive`. Edits are only possible when `CurriculumEditGuard` passes,
so only programs whose `Open` classes have no enrolled students or open
checkouts are affected; those classes sell seats again after republish. Later
edits find no active approval and only extend the session message.

### Framework version upgrade

`POST /api/programs/{id}/framework-version` — Manager/Admin. Body
`{ frameworkVersionId }`. Checks, in order: program has a framework (409
`FRAMEWORK_REQUIRED`); the version exists, belongs to the program's framework,
is published, and is newer than the pinned version (400
`FRAMEWORK_VERSION_INVALID`); `CurriculumEditGuard` passes (409
`CURRICULUM_LOCKED_COHORT`). On success, in one transaction: pin the new
version, revoke the active approval (`FrameworkUpgraded`) and return to `Draft`
when the program is not `Draft` or has an active approval (posts
`ApprovalRevoked`), post `FrameworkUpgraded`, and notify the advisor
(`ProgramFrameworkUpgraded`, only when the advisor has a login and is not the
caller). `curriculumVersion` is not bumped. The workspace
`frameworkCheckPassed` reflects the new rules. Returns the workspace DTO.

Publishing a new framework version (`POST
/api/program-frameworks/{id}/versions/{versionId}/publish`) notifies managers
once per non-deleted program of that framework that is pinned to an older
version or has no pinned version (`FrameworkVersionPublished`, payload
`programId`, `fromVersion` (null when unpinned), `toVersion`). Pinned programs
never change by themselves.

Workspace `GET /api/programs/{id}/advisory` returns `programId`, `status`,
`curriculumVersion`, `curriculumLocked` (a live cohort blocks curriculum edits),
`advisorExpertId`, `advisorName`, `participants[]
{ userId, name, role, isAdvisor }` (active managers, then the advisor, then
board experts), `capabilities`, `approval` (active approval or null:
`id`, `curriculumVersion`, `approvedAt`, `approvedByName`, `comment`),
`openPinCount`, `addressedPinCount`, `unreadCount`, `frameworkCheckPassed`
(live), `changesSinceApprovalCount` (net items for `base=lastApproval`),
`unseenChangeCount`, `latestSequence`, `frameworkVersionNumber`,
`latestFrameworkVersionNumber`, `hasNewerFrameworkVersion`. Approval
request/approve/revoke and framework upgrade return the workspace DTO.

| Capability | Rule |
| --- | --- |
| `canPost` | participant |
| `canPin`, `canResolvePin` | caller is the advisor (board experts cannot) |
| `canEditCurriculum` | Manager/Admin and `curriculumLocked` is false (any status) |
| `canRequestApproval` | Manager/Admin, program has a framework, `Draft`, advisor assigned with a login |
| `canApprove` | caller is the advisor, program has a framework, status `Draft` |
| `canRevokeApproval` | `Approved` and caller is Manager/Admin or the advisor |
| `canPublish` | Manager/Admin; with a framework: `Approved` and approval version = `curriculumVersion`; without: `Draft` or `Approved` |
| `canUpgradeFrameworkVersion` | Manager/Admin, `hasNewerFrameworkVersion`, `curriculumLocked` is false |

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
`User`/`System`; `text` ≤ 4000 with `@[Type:uuid]` tokens). Mentions are
stored as `ProgramAdvisoryReference` rows (`Node` anchor) linked through
`ProgramAdvisoryDiscussionMessageReference`. Target types: `Program`,
`Module`, `Course`, `Activity`, `Assignment`, `ResearchMilestone`, `Material`.
Reference DTO: `id`, `programId`, `targetType`, `targetId`, `anchorKind`,
`fieldKey`, `quote`, `quotePrefix`, `quoteSuffix`, `capturedLabel`,
`capturedExcerpt`, `capturedAt`, `isAvailable`, `unavailableReason`,
`quoteMatched`; always resolved against the live curriculum. One read cursor
per program and user (`ProgramAdvisoryStreamRead`).

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
| POST | `/messages` | participant | `{ text, attachmentIds[], clientMessageId }` | Message DTO; `clientMessageId` required (≤ 100); idempotent on author + `clientMessageId` |
| PATCH | `/messages/{messageId}` | author | `{ text }` | Message DTO; mentions re-parsed |
| DELETE | `/messages/{messageId}` | author | | Removal (tombstone; pin removed) |
| POST | `/messages/{messageId}/pin` | advisor | | Pin `Open` (idempotent) |
| DELETE | `/messages/{messageId}/pin` | advisor | | Pin removed (idempotent) |
| POST | `/messages/{messageId}/pin/actions` | see below | `{ action }` | Message DTO |
| GET | `/pins` | participant | `status?` | Message DTO[] by `pinnedAt` |
| GET | `/mention-counts` | participant | | `[{ targetType, targetId, messageCount, openPinCount }]` |
| POST | `/read` | participant | `{ cursor?, lastDisplayedSequence }` | Read cursor |
| POST | `/attachments` | participant | multipart `file` | AttachmentDto |
| GET | `/attachments/{attachmentId}/url` | participant | | `{ url, expiresAt }` (15 minutes) |

Pin actions: `MarkAddressed` (Manager/Admin, `Open` → `Addressed`), `Reopen`
(advisor, `Addressed`/`Resolved` → `Open`), `Resolve` (advisor,
`Open`/`Addressed` → `Resolved`). Board experts get 403 on pin, unpin, reopen,
and resolve; they can still post and read. Unpinned message or wrong
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
| `FrameworkUpgraded` | `{ fromVersion?, toVersion, actorName }` | `POST framework-version` |

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
| `CURRICULUM_LOCKED_COHORT` | 409 | curriculum, program update of curriculum fields, program delete, save as material, framework upgrade while a live cohort runs or a checkout payment is open |
| `FRAMEWORK_LOCKED` | 409 | `PUT /api/programs/{id}` that would change the framework |
| `FRAMEWORK_REQUIRED` | 409 | approval request, approve, framework upgrade on a program without a framework |
| `FRAMEWORK_VERSION_INVALID` | 400 | framework upgrade (other framework, unpublished, not newer) |
| `PROGRAM_NOT_AVAILABLE` | 400 | checkout / parent payment on a program that is not `Active` |
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

An ordered slice of a module (`CourseOrder`) containing activities. A course
has no mentor; the mentor is assigned per class. SelfPaced activities may
have one optional learning material (video, PDF, etc.).

API: `/api/courses`.

## Activity

Individual learning tasks within a course.

| ActivityType | Meaning |
| --- | --- |
| SelfPaced | No fixed schedule; progress tracked individually |
| LiveOnline | Scheduled online session |
| Offline | Physical session; may offer QR check-in |

Allowed types per module: `Theory` modules take SelfPaced and LiveOnline;
`Experiential` and `Research` take all three. `DurationMinutes` is required
(> 0) for LiveOnline and Offline and not allowed for SelfPaced; changing it
moves `EndTime` (= `StartTime` + duration) on linked non-cancelled sessions.
`Activity` has no start/end times; cohort-specific times live on
`ClassSession`.

Flags: `RequireQrCheckin` (Offline only, else 400; switching an activity to
SelfPaced clears it and the duration), `RequireMediaEvidence`.
`RequireQrCheckin` enables student QR/code check-in; it does not require that
path for attendance or mentor-complete. `POST /api/activity-progresses/mentor-complete-bulk`
(Mentor/Manager) completes a LiveOnline or Offline activity for students of a
linked session whose attendance is `Present`, `Late`, or `Excused` (from either
student QR/code check-in, meeting join, or a mentor/manager roster mark);
other students are skipped. When `RequireMediaEvidence` is set, the session
must have at least one image evidence upload, else the request fails with 400.

API: `/api/activities`.

## Class and Sessions

`Class` is a running cohort (đợt học) for a program: date range, capacity,
mentor, `MinHoursBeforeAssignmentJoin` (generate first-session buffer),
`ScheduleSummary`.

Lifecycle (`ClassStatus`): **Draft → ReadyForMentor → Open → InProgress → Completed**.
`Cancelled` is stored but has no public cancel endpoint.

1. `POST /api/classes` (Admin/Manager) always creates **Draft**. The program must be **Active**. `StartDate` must be at least 14 days after today (UTC date). `MaxCapacity` ≥ 1, `MinHoursBeforeAssignmentJoin` ≥ 0 (default 48), duplicate code → 409. Mentor is optional. Reassigning `ProgramId` on `PUT` also requires the target program to be **Active**. `PUT` cannot change `Status` (400); a `StartDate` change while Draft/ReadyForMentor/Open must keep the 14-day lead; the date range must still contain every active session; `MaxCapacity` cannot drop below current enrollment.
2. Generate the timetable (`POST /api/classes/{classId}/sessions/generate`, Admin/Manager, or add sessions manually). Coverage is one active session per LiveOnline/Offline activity plus each assignment.
3. When coverage is complete and `StartDate` is still in the future, the class becomes **ReadyForMentor** automatically after session create/generate/update/delete, or via `POST /api/classes/{id}/ready-for-mentor` (requires complete coverage). Mentors request assignment from the board (`GET /api/class-mentor-requests/board`; the older `GET /api/classes/mentor-board` lists ReadyForMentor classes without a mentor). Students cannot enroll.
4. After a mentor is assigned, `POST /api/classes/{id}/open` moves **ReadyForMentor → Open** (mentor and complete coverage required). The program must still be **Active**. Students may enroll only in this status.
5. `POST /api/classes/{id}/start` (or auto-start) moves **Open → InProgress**. The program must still be **Active**, and the mentor and coverage checks of `open` are repeated. Auto-start runs when the class is Open, active enrollments reach `MaxCapacity`, and `StartDate` has arrived; it is triggered after enrollment and by the hosted `OpenClassAutoStartService` (adaptive delay), and skips the class when the program is not Active or coverage is stale. Enrollment closes.
6. `POST /api/classes/{id}/complete` moves **InProgress → Completed**.

Any other transition returns 400. If sessions are deleted or cancelled so coverage no longer matches the curriculum, **ReadyForMentor** returns to **Draft**.

`DELETE /api/classes/{id}` — Manager only; Draft, ReadyForMentor, or Open
(else 400); an Open class with active students → 409. Soft-deletes the class
sessions and their active co-teach rows, notifying the roster and those experts
(`ClassSessionCancelled`).

Generate preconditions: the class is not Completed (400); an Open or
InProgress class has no enrolled students (409); no active sessions exist yet
(409); every LiveOnline/Offline activity has `DurationMinutes`. Lives and
offlines take the weekly `DaysOfWeek` slots at `SessionStartTime` from
`StartDate` and must fit before `EndDate`; the first session must be at least
`MinHoursBeforeAssignmentJoin` hours away. Mentor calendar overlap is checked
only when a mentor is assigned.

`ClassSession` schedules concrete session instances. `SessionKind` mirrors the
curriculum item: **LiveOnline**, **Offline**, or **AssignmentWindow**. LiveOnline
join links live on `MeetingUrl` (separate from free-text `Location`).
`SessionAttendance` records attendance status per student.

Session routes (`/api/classes/{classId}/sessions`): `GET` list (filters
`sortBy`, `moduleId`, `sessionKind`, `status`, `from`, `to`, `assignmentId`),
`GET {id}`, `GET with-students/{sessionId}` (Student/Mentor/Manager/Admin;
students see only their own attendance row), `POST` and `DELETE {id}`
(Admin/Manager), `POST generate` (Admin/Manager), `PUT {id}`
(Admin/Manager; the class Mentor may change only `StartTime`, `EndTime`, and
`Description` of AssignmentWindow sessions). A session targets exactly one of
`ActivityId` or `AssignmentId`, one active session per curriculum item per
class (duplicate → 409); SelfPaced activities cannot be scheduled. For
activity sessions `EndTime` is derived (`StartTime` + `DurationMinutes`) and
cannot be set by the client. A time change resets `ReminderSentAt` when the
start moves and checks mentor overlap (not for AssignmentWindow).

Session lifecycle (`ClassSessionStatus`): **Scheduled → InProgress → Completed**,
or **Cancelled** from Scheduled or InProgress (setting the same status is a
no-op; other transitions 400). Admin/Manager may move a session manually via
`PUT /api/classes/{classId}/sessions/{id}`. The hosted
`SessionLifecycleService` runs `SessionLifecyclePublisher` every 5 minutes
(completing elapsed sessions first, then starting due ones) and applies the
clock to LiveOnline and Offline sessions:

- `Scheduled` with `StartTime <= now < EndTime` becomes `InProgress` and
  publishes `ClassSessionStarted`. Pending co-teach invitations stay `Invited`.
- `Scheduled` or `InProgress` with `EndTime <= now` becomes `Completed`, with
  the same side effects as the manual move (open participation segments
  closed, `ClassSessionCompleted`, expert feedback requests). An elapsed
  `Scheduled` row goes straight to `Completed`.

AssignmentWindow rows are not moved by the job. Completed sessions reject
QR/code check-in and join.

Student QR/code check-in (used for Offline sessions; the code itself does not
check `SessionKind`). Token generation and check-in require the session to be
Scheduled or InProgress; check-in also requires an Active class enrollment and
an Active module enrollment, and records `Present`.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `POST /api/class-sessions/{id}/checkin-token` | Assigned class Mentor / Manager / Admin | Rotate QR UUID + 6-digit code (60s TTL). Live 6-digit codes are unique across non-expired tokens. |
| `POST /api/class-sessions/{id}/checkin` | Student | Web / session-scoped. Body `{ "token" }` or `{ "code" }`; the exactly-one rule is not enforced here, and a supplied credential that matches the session's live token is accepted. |
| `POST /api/class-sessions/checkin-by-token` | Student | Mobile scan-first. Body: exactly one of `{ "token" }` or `{ "code" }`; resolves live token/code → session (no `sessionId` in path). Code must match exactly one non-expired live token (0 or >1 → fail closed). |

Online meetings (JaaS) for **LiveOnline** sessions only:

- `POST /api/class-sessions/{id}/join` — Student/Mentor/Manager/Admin. Open
  from 15 minutes before `StartTime` until `EndTime`; Cancelled and Completed
  sessions are rejected (400). Students need an Active class enrollment and an
  Active module enrollment (400 otherwise); attendance is
  `Present` when joining by `StartTime` + 10 minutes, else `Late`. Re-joining
  is idempotent and reopens the participation segment. Admin/Manager and the
  class mentor join as moderators; other non-students get 403. Returns
  `{ jwt, roomName (session id), appId, domain, isModerator, attendanceStatus }`.
- `POST /api/class-sessions/{id}/leave` — closes the student's segment
  (`LeftAt`, `ParticipationMinutes`); leaving before joining → 400.
  Non-students leave without changes.

Session evidence photos (`/api/class-sessions/{id}/evidence`): `POST`
(Mentor/Manager/Admin, same permission as attendance updates) uploads a
`.jpg`/`.jpeg`/`.png` image ≤ 10 MB to `session-evidence/{sessionId}/` as a
`MediaAsset`; `GET` (Student/Mentor/Manager/Admin, roster-view access) lists
them; `DELETE {mediaId}` (Mentor/Manager/Admin) removes the S3 object and
soft-deletes the row. These satisfy `RequireMediaEvidence` for mentor-complete.

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
  Offline** session (non-Offline or expert without a login or not on the
  board → 400; not Scheduled → 409; duplicate active invite → 409). A
  calendar overlap does not block the invite; it is returned as
  `scheduleConflictWarning`.
- `GET /mine` — Expert lists own invitations.
- `GET /{id}` — Manager/Admin, or the owning Expert.
- `GET /` — Manager/Admin list (`classId` / `sessionId` / `expertId` / `status`).
  A Student who is actively enrolled may call the same route with `classId`
  (required; missing → 400). Not enrolled → 403. The student list is always
  `Accepted` only, even if another `status` is queried, and omits
  `mentorFeedback`, `mentorFeedbackRating`, `mentorFeedbackAt`, and
  `scheduleConflictWarning`. `expertAvatarUrl` is `Expert.AvatarUrl`.
  Student `pageSize` cannot exceed 100.
- `POST /{id}/accept` and `POST /{id}/decline` — owning Expert, `Invited`
  only (400). Accept also requires the session to still be Scheduled (409) and
  is blocked (`409`) on calendar overlap with another Accepted
  Offline/LiveOnline session.
- `POST /{id}/withdraw` — Manager/Admin, **Invited only** (409). Accepted
  cannot be withdrawn.
- `PUT /{id}/feedback` — owning Accepted expert (else 400), session
  **Completed** (409; `Cancelled` → 409), and `StartTime` is not in the future
  (409). Upserts one class-level overview (`MentorFeedback` comment required +
  rating 1–5). Declined experts cannot submit.
  Feedback is private: Expert (own row),
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
48 hours, clamped to the end of the `Class.EndDate` day (23:59:59 Vietnam
time). Manual create (`POST /api/classes/{classId}/sessions`) and edits by
mentors and managers follow the same rules: start not in the past (create),
end in the future, open at least 48 hours, and within the class dates (through
the end of the class end date day); violations return 400. Blocked new attempts
return 409 with `error.code` `ASSIGNMENT_WINDOW_MISSING`,
`ASSIGNMENT_WINDOW_NOT_OPEN`, or `ASSIGNMENT_WINDOW_CLOSED`; the last two carry
`{ classSessionId, classId, assignmentId, startTime, endTime }` (UTC) in
`value.data`. A timed quiz attempt cannot be saved or submitted more than 60
seconds after `Submission.ExpiresAt` (409 `ASSIGNMENT_ATTEMPT_TIME_EXPIRED`).
An expired quiz attempt is graded from its saved answers by the next quiz start
(409 `QUIZ_ATTEMPT_EXPIRED_GRADED` with the quiz result in `value.data`; start
again for the next attempt) or by the 5-minute window close job, which grades
expired quiz attempts before it decides AcademicFail. That job is the only place
that fails enrollments for an elapsed window; starting an attempt never does.
Retrospective and file-upload work already in progress is not bound by the
window or its timer and may be saved and turned in after `EndTime`.
`GET /api/classes/{classId}/sessions` accepts `assignmentId`.
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

Class API: `/api/classes` — `GET` list, `GET {id}`,
`GET with-students/{classId}` (Student/Mentor/Admin/Manager),
`GET with-sessions/{classId}`, `POST` / `PUT {id}` and the lifecycle actions
(`ready-for-mentor`, `open`, `start`, `complete`; Admin/Manager),
`DELETE {id}` (Manager), and the Mentor routes `mentor-board`,
`{classId}/curriculum-progress`, and the two `student-progress` routes above.

## Materials

Learning assets for **SelfPaced** activities only (video, PDF, doc, etc.). At most
one material per activity. LiveOnline and Offline activities do not have materials.

Types via `MaterialType` enum. API: `/api/materials`:

- `POST /upload?activityId&title` (multipart `file`) — the activity must be
  SelfPaced with no material yet (409); `CurriculumEditGuard` applies. Allowed:
  PDF and DOC/DOCX ≤ 50 MB, video `.mp4`/`.mov`/`.avi`/`.mkv` ≤ 3 GB, image
  `.jpg`/`.jpeg`/`.png`/`.gif`/`.webp` ≤ 10 MB; stored under
  `materials/pdf|doc|video|image/`.
- `POST /from-discussion-attachment` — see
  [Advisory Chat and Approval](#advisory-chat-and-approval).
- `GET /` (filtered list), `GET /activity/{activityId}` (anonymous allowed),
  `PUT /{materialId}` (title only), `DELETE /{materialId}` (removes the S3
  file, then hard-deletes the row). `PUT` and `DELETE` also apply
  `CurriculumEditGuard`.

Material files are not public while a program is not `Active`.
Managers and Admins can receive an authorized preview; Experts only for
programs where they are the advisor or a board member (else 403);
students use enrollment-scoped access (`programEnrollmentId` required, else
400). Once a program is `Active`, the activity material endpoint may be called
without enrollment and returns a time-limited preview URL; anonymous callers
get 403 otherwise. The S3 `materials/*` prefix is excluded from the bucket's
anonymous read policy.

## Experts

Experts associated with programs via `ProgramBoard` and the `Expert` entity.
`RoleType.Expert` is a dedicated login role. Manager/Admin provision it with
`POST /api/experts` (email required and unique, else 409; the user is created
Active with a verified email; temporary password auto-generated and
emailed — create rolls back if email fails); the expert signs in through
`POST /api/auth/login` immediately. Public register does not allow Expert.
Password reset uses forgot-password OTP. `PUT /api/experts/{id}` does not
change credentials. `DELETE /api/experts/{id}` is blocked (409) while the
expert is the responsible advisor of any program; otherwise it locks the
linked user and withdraws the expert's co-teach invitations.
Profile credentials: `Specialization` tags, `ExpertDegree`, and
`ExpertPublication`. Manager/Admin routes:

- `POST /api/experts/{id}/avatar`
- `POST|PUT|DELETE /api/experts/{expertId}/programs/{programId}` — board
  membership. Removing a membership (or a `PUT /api/experts/{id}` whose program
  list drops a program) is blocked (409) for that program's advisor.
- `POST /api/experts/{expertId}/degrees`, `PUT|DELETE
  /api/experts/{expertId}/degrees/{degreeId}`
- `POST /api/experts/{expertId}/publications`, `PUT|DELETE
  /api/experts/{expertId}/publications/{publicationId}`

Expert self-service (`/api/experts/me`, Expert role): `GET` / `PUT me` (cannot
change code, email, user link, or board), `POST me/avatar` (jpg, jpeg, png,
gif ≤ 5 MB; also updates the user avatar), `POST me/degrees`,
`PUT|DELETE me/degrees/{degreeId}`, `POST me/publications`,
`PUT|DELETE me/publications/{publicationId}`.

Public reads: `GET /api/experts`, `GET /api/experts/{id}`, and
`GET /api/experts/{id}/profile`.

### Program framework and curriculum review

`ProgramFramework` is a reusable expert-authored identity. Its immutable
published `ProgramFrameworkVersion` rows hold description, academic guidance,
and optional rules: module count (min/max), courses per non-Research module
(min/max), total hours (min/max), max activity minutes, LiveOnline/Offline
activity duration required, Offline/LiveOnline session minimums and share of activities,
assignment in every module, valid assignment pass score, materials per
SelfPaced activity, category match, description length, skills gained, thumbnail,
and capstone research milestone. Blank or `false` rules are unrestricted;
values are ≥ 0, each min ≤ its max, and the two ratios sum to ≤ 100
(`FRAMEWORK_RULES_INVALID`). The rubric was removed. A program pins one published
version and many programs may pin the same version, even when they have
different responsible advisors. Publishing a newer version never changes an
existing program; managers are notified and explicitly adopt it with
`POST /api/programs/{id}/framework-version` while curriculum is editable (see
[Framework version upgrade](#framework-version-upgrade)). Framework `Category` is guidance only and never an eligibility
gate.

Published versions and referenced history are immutable (editing one → 409). Draft updates are
partial: `null` leaves a rule unchanged and `clear<Field>` turns a numeric rule
(or `requireCapstoneResearchMilestone`) off.

API: `/api/program-frameworks` (Expert/Manager/Admin). Experts list and read
only their own frameworks (another expert's id → 404); Manager/Admin may list
and read all. Every write is limited to the authoring Expert (Manager/Admin →
403): `POST` (create with draft version 1), `PUT {id}` (name, category, and the
draft version's fields; 409 when there is no draft), `DELETE {id}` (same as
archive), `POST {id}/archive`, `POST {id}/versions/draft`, and
`POST {id}/versions/{versionId}/publish`. Reads: `GET`, `GET {id}`,
`GET {id}/versions`, `GET {id}/versions/{versionId}`. Category query is a
hint only. Authoring occurs on one draft version: a new draft copies the
latest published version and requires that no draft exists and at least one
version is published (409 otherwise). Archived frameworks cannot create or
publish versions (409). The old rubric and criteria routes return 410
`ENDPOINT_REMOVED`. Archive prevents new assignment while retaining existing
pins and history: program create rejects an archived framework or an
unpublished version (409), and `frameworkId` alone pins the latest published
version.

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
affectedCurriculumLinks[] }` (the failing components for most checks;
`MinModules` lists all modules, `MinOfflineSessions` / `MinLiveSessions` the
matching activities, and `RequireCapstoneResearchMilestone` the capstone
milestones; empty for program-level checks). `TotalHours` compares minutes (`hours × 60`); null or ≤ 0 durations
count as 0. `ActivityDurationSet` applies only to LiveOnline and Offline
activities (null or ≤ 0 fails); SelfPaced activities have no duration and are
exempt. Ratios are `count / all activities
× 100` (0 with no activities). `CoursesPerModule` applies to non-Research
modules. `AssignmentPerModule` counts any assignment whose `moduleId` is the
module. `AssignmentPassScore` requires `0 < passScore ≤ maxPoints`.
`MaterialsPerActivity` counts SelfPaced activities. `MaxModules` links the
modules beyond the maximum by order. `SkillsGained` counts `ProgramSkill`
links. `CategoryMatch` compares `Program.Category` with the framework category.
Approval runs the live check (409 `FRAMEWORK_CHECK_FAILED`).

Only `Program.AdvisorExpertId` approves (see
[Approval](#approval)); framework authorship does not grant that authority.
Other board experts may advise and may be invited to co-teach. The responsible
advisor cannot be removed from the board until the program is reassigned.
`ProgramApproval` is the only approval record (distinct from student
`ProgramReview` star ratings). The old review-round history
(`CurriculumReview`, `CurriculumReviewRequirement`, `ProgramReviewSubmission`,
`ProgramReviewDraft`, advisory threads, messages, events, reads, and
notification intents) was dropped by migration `DropLegacyAdvisoryHistory`.

## Highlight Videos

Per-student highlight reels for a **class**, processed asynchronously via AWS
MediaConvert. Model: `HighlightVideoStack` (up to 3 per student/class) with
`HighlightVideoItem` outputs (up to 4 per stack). Source clips come from
`MediaAsset` videos for that class (`ClassId` required; optional
`ClassSessionId`) whose tagging is complete. Only videos with a **verified**
`MediaTag` for the student are used (face or mentor tag), ordered by session
start. Videos without face segments (including mentor late tags) are treated
as scene-only participation / project credit: full video when no strength is
set, or activity clips from the media label timeline when
`StrengthDescription` is set. When the student is the only face, the whole
video is used; with several people, the student's face segments are used with
2-second buffers. Optional `StrengthDescription` (≤ 2000) otherwise filters via
Bedrock + label timeline.

API: `/api/highlight-video` (Student/Mentor/Manager/Admin; students only see
their own stacks, mentors only their own classes): `POST stacks` (202),
`GET stacks?classId&studentId`, `GET stacks/{stackId}`,
`POST stacks/{stackId}/regenerate`, `GET stacks/{stackId}/source-media`,
`DELETE stacks/{stackId}`, and per item under
`stacks/{stackId}/items/{itemId}`: `GET progress`, `POST cancel`,
`POST retry`, `POST trim`, `POST add-segment`, `DELETE`.
AWS completion: `/api/webhooks/aws`.
Completed reels are attached to a portfolio Gallery section via
`POST /api/portfolios/me/media/from-highlight-reel` (copies into portfolio-owned
Video media). Sync no longer creates `HighlightReel` project items.
