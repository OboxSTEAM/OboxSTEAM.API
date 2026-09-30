# Advisory Chat Contract (Backend)

Status: Draft for BE/FE review
Date: 2026-09-30
Plan: `docs/plans/active/advisory-chat-overhaul.md`

This contract replaces per-node advisory threads, review rounds, the rubric,
and the "Kết luận thẩm định" flow with **one realtime chat per program** plus a
**single versioned approval** by the responsible advisor.

## 0. Conventions

- Envelope is unchanged: `{ isSuccess, value: { code, message, data }, error: { code, message } }`.
- JSON is camelCase; enums serialize as strings (`JsonStringEnumConverter`).
- Business errors use `ErrorHelper` with a machine `error.code`. HTTP status
  follows the helper (`BadRequest` 400, `Forbidden` 403, `NotFound` 404,
  `Conflict` 409, `Gone` 410).
- All timestamps are UTC ISO-8601.
- "Participants" = Manager/Admin, the responsible advisor
  (`Program.AdvisorExpertId`), and experts on `ProgramBoard` for the program.
  Only the advisor can approve.

## 1. Status machine

```
Draft --(advisor approve @ curriculumVersion)--> Approved
Approved --(manager reopen | curriculum edit | advisor revoke | advisor change)--> Draft
Approved --(manager publish, version must match)--> Active
Active <--> Inactive (manager)
```

- `ProgramStatus` values after migration: `Draft`, `Active`, `Inactive`,
  `Approved`. `PendingReview` is removed; existing rows become `Draft`.
- Curriculum edits are blocked only by live cohorts (`CurriculumEditGuard`):
  any class `InProgress`, or an `Open` class with `Active` enrollments
  (**409**). `PendingReview` stays blocked until it is removed.
- When a curriculum edit lands while an approval is active (or the program
  is `Approved`), the approval is auto-revoked (`CurriculumEdited`) in the
  same save and an `ApprovalRevoked` system message is posted. `Approved`
  returns to `Draft`; `Active`/`Inactive` keep their status (re-approval of
  live programs is phase 2).
- The advisor receives one `CurriculumApprovalRevoked` notification per
  revoke. Later edits find no active approval and only extend the
  editing-session message. The notification is queued during the save and
  published only after commit; a failed save or rolled-back transaction
  drops it.

## 2. Data model

### 2.1 Program

| Field | Type | Notes |
|---|---|---|
| `curriculumVersion` | int64 | Starts at 0. Incremented once per `SaveChanges` that mutates curriculum under the program (see 2.6). Returned on `ProgramsResponseDto`, `ProgramListItemDto`, and the advisory workspace. |

Curriculum = program content fields (`name`, `code`, `description`, `level`,
`category`, `estimatedDuration`, `skillsGained`, `thumbnailUrl`, skills links),
modules, courses, activities, assignments, research milestones,
milestone-activity links, materials.

Not curriculum (no version bump, allowed on Active programs): `price`,
`retakeFee`, `status`, `frameworkId`/`frameworkVersionId`, `advisorExpertId`,
board membership, ratings.

### 2.2 ProgramApproval

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `programId` | uuid | |
| `curriculumVersion` | int64 | Version approved (= `toVersion`). |
| `fromVersion` | int64 | Base version of the change list shown at approval (previous approval's version, or 0). |
| `frameworkVersionId` | uuid? | |
| `frameworkCheckJson` | jsonb | Serialized `FrameworkCheckDto` at approval time. |
| `curriculumSnapshotJson` | jsonb | Existing curriculum snapshot builder output, for audit. |
| `approvedByExpertId` | uuid | |
| `approvedAt` | datetime | |
| `comment` | string? | ≤ 2000 chars. |
| `revokedAt` | datetime? | |
| `revokedByUserId` | uuid? | |
| `revokeReason` | enum? | `ManagerReopened`, `CurriculumEdited`, `ExpertRevoked`, `AdvisorChanged`. |

At most one non-revoked approval per program.

### 2.3 Discussion message

Extends the existing `ProgramAdvisoryDiscussionMessage` (it becomes the only
chat store).

| Field | Type | Notes |
|---|---|---|
| `id`, `programId`, `sequence`, `clientMessageId`, `createdAt` | existing | `sequence` is per program, monotonic. |
| `authorUserId` | uuid? | Null for system messages. |
| `kind` | enum | `User`, `System`. |
| `text` | string | ≤ 4000 chars. Contains mention tokens `@[Type:uuid]`. Empty string allowed only when attachments are present or `kind = System`. |
| `systemEventCode` | enum? | See 2.3.1. |
| `systemEventPayloadJson` | jsonb? | |
| `editedAt` | datetime? | |
| `deletedAt` | datetime? | Author removal (stored as `RemovedAt`/`RemovedByUserId`, separate from the global soft-delete flag so tombstones stay visible): text cleared, attachments hidden, mentions removed from counts, pin removed. |
| pin fields | | `pinStatus` (`Open`, `Addressed`, `Resolved`)?, `pinnedByUserId`?, `pinnedAt`?, `addressedByUserId`?, `addressedAt`?, `resolvedByUserId`?, `resolvedAt`? |

#### 2.3.1 System event codes

| Code | Payload | Posted when |
|---|---|---|
| `CurriculumUpdated` | `{ actorUserId, actorName, fromVersion, toVersion, changeCount }` | Manager editing session (see 7.4). Payload is updated in place while the session is open. |
| `ApprovalRequested` | `{ requestedByName }` | `POST approval/request` |
| `Approved` | `{ approvalId, curriculumVersion, approvedByName, comment? }` | Approval |
| `ApprovalRevoked` | `{ approvalId, reason, actorName, comment? }` | Any revoke path |
| `Published` | `{ publishedByName }` | Publish |
| `AdvisorChanged` | `{ previousAdvisorName?, newAdvisorName? }` | `PUT advisor` |

### 2.4 Discussion attachment

| Field | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `programId` | uuid | |
| `messageId` | uuid? | Null until sent. |
| `uploaderUserId` | uuid | |
| `fileName` | string | ≤ 255 chars. |
| `contentType` | string | |
| `sizeBytes` | int64 | ≤ 20 MB. |
| `kind` | enum | `Image`, `File`. |
| `storageKey` | string | S3 key (`advisory/{programId}/{attachmentId}/{fileName}`). |
| `createdAt` | datetime | |

Unsent attachments older than 24 hours are purged (S3 object + row) by a
background job.

### 2.5 Mentions

- Reuse `ProgramAdvisoryReference` with `anchorKind = Node` and
  `context = WorkingDraft`, linked through
  `ProgramAdvisoryDiscussionMessageReference`.
- `ProgramAdvisoryTargetType`: `Program`, `Module`, `Course`, `Activity`,
  `Assignment`, `ResearchMilestone`, `Material`. `RubricCriterion` is removed.

### 2.6 Curriculum change log

`CurriculumChange` rows are written by an EF Core `SaveChanges` interceptor in
the same transaction that increments `curriculumVersion`.

| Field | Type |
|---|---|
| `id` | uuid |
| `programId` | uuid |
| `version` | int64 (the version produced by this save) |
| `actorUserId` | uuid? |
| `actorName` | string? |
| `at` | datetime |
| `targetType` | `ProgramAdvisoryTargetType` (links are recorded against `ResearchMilestone`) |
| `targetId` | uuid |
| `changeKind` | `Created`, `Updated`, `Deleted`, `Moved`, `Reordered` |
| `fieldsJson` | jsonb: `[{ fieldKey, before, after }]` with typed JSON values |
| `parentBefore`, `parentAfter` | uuid? |
| `parentBeforeLabel`, `parentAfterLabel` | string? (captured for `moved` labels) |
| `orderBefore`, `orderAfter` | int? |
| `labelSnapshot` | string |
| `pathSnapshotJson` | jsonb: `[{ targetType, targetId, label }]` (ancestors, program first) |

`CurriculumChangeSeen`: `programId, userId, seenVersion` (unique on program + user).

Capture rules (one save = one version):

- Only whitelisted fields count (see `CurriculumChangeFieldCatalog`); audit
  columns, price, status, framework, and advisor never bump the version.
- Skill links are recorded on the `Program` item as field `skill:{skillId}`
  (`false` → `true` when added). Milestone-activity links are recorded on the
  `ResearchMilestone` item as `activityLink:{activityId}` and
  `activityLinkRequired:{activityId}`.
- Creating or deleting the program itself records nothing.
- Deleting a component records only the topmost deleted component (cascaded
  child deletes are implied).
- Sibling order shifts caused by an insert, delete, or move under the same
  parent are not recorded separately.
- Seeding runs with recording suppressed.
- No optimistic concurrency token on `curriculumVersion`: two simultaneous
  saves can produce the same version number; consolidation still works because
  rows are grouped by target and ordered by `(version, at)`.

### 2.7 Migration

Schema lands first (`AddAdvisoryChatModel`). Data steps run with the task that
switches the behaviour: steps 2–5 with the new discussion service, step 1 with
the new approval lifecycle, step 7 with the framework-rules change.

1. `Programs.Status = 'PendingReview'` → `'Draft'`.
2. For each program: copy messages of the `General` advisory thread into
   `ProgramAdvisoryDiscussionMessages` (kind `User`), ordered by original
   `StreamSequence`, renumbering `sequence` after any existing discussion
   messages.
3. Root message of each `RequiredChange` thread with status `Open` or
   `Addressed` → a pinned discussion message (same pin status) whose text
   is prefixed with the mention token of the thread's target (when the target
   is not `RubricCriterion`).
4. Root message of each `Suggestion` thread → a plain discussion message with
   the same mention prefix rule.
5. Threads that target `RubricCriterion` → plain messages without a mention.
6. `Programs.CurriculumVersion = 0`; no change-log backfill.
7. Rubric storage is dropped (no archive): `FrameworkRubricCriteria`,
   `ReviewCriterionScores`, `ProgramReviewDrafts`,
   `ProgramReviewSubmissions.RubricSnapshotJson`, and any
   `ProgramAdvisoryReferences` with `TargetType = 'RubricCriterion'`.
8. `CurriculumReview`, `ProgramReviewSubmission`, and advisory thread tables
   stay read-only for audit. No endpoint writes them.

## 3. Mention index

### `GET /api/programs/{id}/mention-targets`

Participants only. Returns every component of the program as a flat list in
curriculum tree order.

```json
[
  {
    "targetType": "activity",
    "targetId": "uuid",
    "label": "Thí nghiệm núi lửa",
    "code": "ACT-01",
    "path": [
      { "targetType": "program", "targetId": "uuid", "label": "STEAM 1" },
      { "targetType": "module", "targetId": "uuid", "label": "Học phần 1" },
      { "targetType": "course", "targetId": "uuid", "label": "Khoa học" }
    ],
    "moduleId": "uuid",
    "courseId": "uuid",
    "activityId": "uuid",
    "order": 12
  }
]
```

`order` is the zero-based index in tree order. `activityId` is set for
activities, assignments, and materials.

## 4. Discussion endpoints

Base: `/api/programs/{id}/advisory-discussion`. Participants only.

### 4.1 Message DTO

```json
{
  "id": "uuid",
  "programId": "uuid",
  "sequence": 42,
  "cursor": "programId:42",
  "kind": "user",
  "authorUserId": "uuid",
  "authorName": "Lan Nguyễn",
  "authorRole": "manager",
  "text": "Xem lại @[Activity:uuid] nhé",
  "clientMessageId": "c-123",
  "systemEvent": { "code": "curriculumUpdated", "payload": { } },
  "references": [ /* AdvisoryReferenceDto, ordered by first token occurrence */ ],
  "attachments": [ /* AttachmentDto */ ],
  "pin": {
    "status": "open",
    "pinnedByName": "TS. Minh",
    "pinnedAt": "2026-09-30T10:00:00Z",
    "addressedByName": null,
    "addressedAt": null,
    "resolvedByName": null,
    "resolvedAt": null
  },
  "createdAt": "2026-09-30T10:00:00Z",
  "editedAt": null,
  "isDeleted": false
}
```

`systemEvent` and `pin` are `null` when not applicable. Deleted messages are
returned as tombstones (`isDeleted: true`, empty text, no references,
attachments, or pin).

### 4.2 Endpoints

| Method | Route | Who | Body / query | Result |
|---|---|---|---|---|
| GET | `/messages` | participant | `before?`, `after?`, `pageSize` (1–100, default 30), `targetType?`, `targetId?` | `{ messages[], before, after, hasMoreBefore, hasMoreAfter }` |
| GET | `/messages/{messageId}` | participant | | Message DTO |
| POST | `/messages` | participant | `{ text, attachmentIds[], clientMessageId }` | Message DTO (idempotent on author + `clientMessageId`) |
| PATCH | `/messages/{messageId}` | author | `{ text }` | Message DTO; mentions are re-parsed |
| DELETE | `/messages/{messageId}` | author | | 204-style success; soft delete |
| POST | `/messages/{messageId}/pin` | advisor or board expert | | Message DTO, pin `Open` |
| DELETE | `/messages/{messageId}/pin` | advisor or board expert | | Message DTO, pin removed |
| POST | `/messages/{messageId}/pin/actions` | see below | `{ action }` | Message DTO |
| GET | `/pins` | participant | `status?` | Message DTO[] ordered by `pinnedAt` |
| GET | `/mention-counts` | participant | | `[{ targetType, targetId, messageCount, openPinCount }]` |
| POST | `/read` | participant | `{ cursor?, lastDisplayedSequence }` | unchanged |
| POST | `/attachments` | participant | multipart `file` | AttachmentDto |
| GET | `/attachments/{attachmentId}/url` | participant | | `{ url, expiresAt }` (15 minutes) |

Pin actions:

| Action | Who | Transition |
|---|---|---|
| `MarkAddressed` | Manager/Admin | `Open` → `Addressed` |
| `Reopen` | advisor or board expert | `Addressed`/`Resolved` → `Open` |
| `Resolve` | advisor or board expert | `Open`/`Addressed` → `Resolved` |

Only `Open` pins block approval. Approval auto-resolves every `Addressed` pin.
System messages cannot be pinned, edited, or deleted.

### 4.3 Posting rules

- Token format: `@[Type:uuid]` where `Type` is a `ProgramAdvisoryTargetType`
  name (case-insensitive). Malformed tokens are left as plain text.
- Each target must belong to the program, otherwise **400 `MENTION_TARGET_INVALID`**.
- Limits: text ≤ 4000 → **400 `MESSAGE_TOO_LONG`**; ≤ 20 distinct mentions →
  **400 `TOO_MANY_MENTIONS`**; ≤ 10 attachments → **400 `TOO_MANY_ATTACHMENTS`**.
- `attachmentIds` must be unsent attachments uploaded by the caller for this
  program, otherwise **400 `ATTACHMENT_INVALID`**.
- Text and attachments both empty → **400 `MESSAGE_EMPTY`**.
- A Manager/Admin message closes that manager's open `CurriculumUpdated`
  session (7.4).

### 4.4 AttachmentDto

```json
{
  "id": "uuid",
  "fileName": "thi-nghiem.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 123456,
  "kind": "file",
  "uploaderUserId": "uuid",
  "createdAt": "2026-09-30T10:00:00Z"
}
```

Upload limits: ≤ 20 MB → **400 `ATTACHMENT_TOO_LARGE`**. Allowed types
(by content type and extension): images (`png`, `jpg`, `jpeg`, `gif`, `webp`),
`pdf`, `doc`, `docx`, `ppt`, `pptx`, `xls`, `xlsx`, `zip` → otherwise
**400 `ATTACHMENT_TYPE_NOT_ALLOWED`**.

### 4.5 Save attachment as material

`POST /api/materials/from-discussion-attachment` with
`{ attachmentId, activityId, title }`.

- Manager/Admin only; program must be `Draft` (or `Approved`, which
  auto-revokes as a curriculum edit).
- Same rules as material upload: the activity must be `SelfPaced`, belong to
  the attachment's program, and have no material yet → otherwise
  **409 `MATERIAL_ACTIVITY_INVALID`**.
- The attachment must be sent (belongs to a message) → otherwise
  **400 `ATTACHMENT_INVALID`**.
- The S3 object is copied to the material key space. Returns the material DTO.

## 5. Advisory workspace and approval

### 5.1 `GET /api/programs/{id}/advisory`

```json
{
  "programId": "uuid",
  "status": "draft",
  "curriculumVersion": 17,
  "advisorExpertId": "uuid",
  "advisorName": "TS. Minh",
  "participants": [
    { "userId": "uuid", "name": "Lan Nguyễn", "role": "manager", "isAdvisor": false }
  ],
  "capabilities": {
    "canPost": true,
    "canPin": false,
    "canResolvePin": false,
    "canEditCurriculum": true,
    "canApprove": false,
    "canRevokeApproval": false,
    "canRequestApproval": true,
    "canPublish": false
  },
  "approval": { "id": "uuid", "curriculumVersion": 15, "approvedAt": "…", "approvedByName": "TS. Minh", "comment": null },
  "openPinCount": 2,
  "addressedPinCount": 1,
  "unreadCount": 4,
  "frameworkCheckPassed": false,
  "changesSinceApprovalCount": 12,
  "unseenChangeCount": 3,
  "latestSequence": 42
}
```

- `approval` is the active (non-revoked) approval, or `null`.
- `frameworkCheckPassed` is computed live from `framework-check`.
- `changesSinceApprovalCount` = number of net change items for
  `base=lastApproval`.

Capabilities:

| Capability | Rule |
|---|---|
| `canPost` | participant |
| `canPin`, `canResolvePin` | advisor or board expert |
| `canEditCurriculum` | Manager/Admin and status `Draft` or `Approved` |
| `canRequestApproval` | Manager/Admin, `Draft`, advisor assigned and has a login |
| `canApprove` | caller is the advisor and status `Draft` |
| `canRevokeApproval` | status `Approved` and caller is Manager/Admin or the advisor |
| `canPublish` | Manager/Admin, `Approved`, approval version == `curriculumVersion` |

### 5.2 Endpoints

| Method | Route | Who | Body | Effect / errors |
|---|---|---|---|---|
| POST | `/api/programs/{id}/approval/request` | Manager/Admin | | `Draft` only (**409 `INVALID_STATUS`**); advisor required (**400 `ADVISOR_REQUIRED`**) with login (**400 `ADVISOR_LOGIN_REQUIRED`**). Posts `ApprovalRequested`, notifies advisor. Status unchanged. |
| POST | `/api/programs/{id}/approval` | advisor | `{ curriculumVersion, comment? }` | See 5.3. Returns workspace DTO. |
| POST | `/api/programs/{id}/approval/revoke` | Manager/Admin or advisor | `{ reason? }` | `Approved` only (**409 `INVALID_STATUS`**). Approved → Draft; reason `ManagerReopened` (manager) or `ExpertRevoked` (advisor). Posts `ApprovalRevoked`. Returns workspace DTO. |
| POST | `/api/programs/{id}/publish` | Manager/Admin | | `Approved` and approval version == `curriculumVersion`, otherwise **409 `CURRICULUM_VERSION_STALE`**. Approved → Active. Posts `Published`. |
| PUT | `/api/programs/{id}/advisor` | Manager/Admin | unchanged | Allowed in `Draft` and `Approved`. In `Approved` it revokes (`AdvisorChanged`). `Active`/`Inactive` → **409 `INVALID_STATUS`**. |

### 5.3 Approve

Checks, in order:

1. Caller is the advisor → otherwise **403**.
2. Status is `Draft` → otherwise **409 `INVALID_STATUS`**.
3. `body.curriculumVersion == program.curriculumVersion` → otherwise
   **409 `CURRICULUM_VERSION_STALE`**.
4. No `Open` pins → otherwise **409 `APPROVAL_BLOCKED`** (`error.message`
   includes the count).
5. `framework-check` passes → otherwise **409 `FRAMEWORK_CHECK_FAILED`**; the
   failing checks are returned in `value.data` as `FrameworkCheckDto`.

On success, in one transaction: create `ProgramApproval`
(`fromVersion` = previous approval version or 0, `toVersion` = current),
resolve every `Addressed` pin, set status `Approved`, post `Approved`,
notify the manager(s) with `CurriculumReviewApproved`.

### 5.4 Auto-revoke

Any curriculum mutation on an `Approved` program revokes the active approval
with `CurriculumEdited`, sets status `Draft`, and posts `ApprovalRevoked`,
inside the same transaction as the edit.

### 5.5 Expert read access

Board experts and the advisor of a program get read access to that program's
modules, courses, activities, assignments, research milestones (and links),
and material signed URLs.

## 6. Framework rules (replaces the rubric)

### 6.1 New framework version fields

All nullable or boolean; `null`/`false` = rule off. Existing published versions
keep current behaviour.

| Field | Type | Check code |
|---|---|---|
| `maxModules` | int? | `MaxModules` |
| `minCoursesPerModule`, `maxCoursesPerModule` | int? | `CoursesPerModule` |
| `minTotalHours`, `maxTotalHours` | int? | `TotalHours` (Σ activity `durationMinutes` / 60) |
| `maxActivityMinutes` | int? | `MaxActivityDuration` |
| `requireActivityDuration` | bool | `ActivityDurationSet` |
| `minOfflineRatioPercent`, `minLiveRatioPercent` | int? (0–100) | `OfflineRatio`, `LiveRatio` (share of activities) |
| `requireAssignmentPerModule` | bool | `AssignmentPerModule` |
| `requireAssignmentPassScore` | bool | `AssignmentPassScore` (`passScore` set and ≤ `maxPoints`) |
| `minMaterialsPerActivity` | int? | `MaterialsPerActivity` (SelfPaced activities) |
| `requireCategoryMatch` | bool | `CategoryMatch` |
| `minDescriptionLength` | int? | `DescriptionLength` |
| `minSkillsGained` | int? | `SkillsGained` |
| `requireThumbnail` | bool | `ThumbnailSet` |

Existing: `minModules` (`MinModules`), `minOfflineSessions`
(`MinOfflineSessions`), `minLiveSessions` (`MinLiveSessions`),
`requireCapstoneResearchMilestone` (`RequireCapstoneResearchMilestone`).

Checks are emitted only for rules that are on. Each check keeps the existing
shape `{ code, label, expected, actual, passed, affectedCurriculumLinks[] }`;
`affectedCurriculumLinks` lists the failing components.

### 6.2 Validation on save (400 `FRAMEWORK_RULES_INVALID`)

- every value ≥ 0
- each min ≤ its max
- ratios in 0–100 and `minOfflineRatioPercent + minLiveRatioPercent ≤ 100`

Published versions stay immutable.

### 6.3 Removed

- `criteria[]` on `ProgramFrameworkVersionResponseDto` and on framework
  create/update bodies.
- `PUT /api/program-frameworks/{id}/versions/{versionId}/rubric` and the
  `/criteria` CRUD routes → **410 `ENDPOINT_REMOVED`**.

## 7. Curriculum change log

### 7.1 `GET /api/programs/{id}/curriculum/changes`

Query `base`: `lastApproval` (default; falls back to `start` if never
approved), `lastSeen`, `start`, or `version:N`. Optional `to` (default current
version).

Access: advisory participants (Manager, Admin, advisor, board experts).
Invalid `base` or `to` → **400**.

```json
{
  "fromVersion": 15,
  "toVersion": 17,
  "currentVersion": 17,
  "seenVersion": 16,
  "summary": { "created": 3, "updated": 7, "deleted": 2, "moved": 1 },
  "items": [
    {
      "targetType": "Activity",
      "targetId": "uuid",
      "label": "Thí nghiệm núi lửa",
      "path": [
        { "targetType": "Program", "targetId": "uuid", "label": "Robotics" },
        { "targetType": "Module", "targetId": "uuid", "label": "Học phần 1" },
        { "targetType": "Course", "targetId": "uuid", "label": "Khóa 1" }
      ],
      "changeKind": "Updated",
      "fields": [
        { "fieldKey": "durationMinutes", "label": "Duration", "valueType": "DurationMinutes", "before": 90, "after": 45 }
      ],
      "moved": null,
      "reorderedChildren": [],
      "changedBy": [ { "userId": "uuid", "name": "Lan Nguyễn" } ],
      "lastChangedAt": "2026-09-30T10:00:00Z",
      "isUnseen": true
    }
  ]
}
```

- Enum values are PascalCase (global string enum converter). Field `label` is
  an English default; clients may localize by `fieldKey`. Dynamic keys get
  `Skill: {name}`, `Linked activity: {name}`, `Required before submission: {name}`.
- `valueType`: `ShortText`, `LongText`, `Number`, `DurationMinutes`, `Enum`,
  `Boolean`, `List`, `Media`.
- `moved`: `{ fromParentLabel, toParentLabel, fromOrder, toOrder }`, set for
  `Moved`, single-item `Reordered`, and `Updated` items whose order also changed.
- `reorderedChildren`: `[{ targetType, targetId, label, fromOrder, toOrder }]`,
  set on a parent item when two or more of its children were reordered.
- `Created` items list their final non-null field values (`before` = null).
- Items are sorted in current tree order; deleted items follow their deepest
  surviving ancestor. `summary.moved` counts `Moved` and `Reordered`.

### 7.2 Net consolidation

- Several updates to one field → first `before`, last `after`.
- Field back to its original value → dropped; an `Updated` item with no fields
  left is dropped.
- Created then updated → `Created` with final values.
- Created then deleted → omitted.
- Updated then deleted → `Deleted`.
- Several reorders under the same parent → one `Reordered` item on the parent
  (merged into the parent's own item when the parent also changed).
- Moved to another parent (course/activity) → `Moved`.

### 7.3 `POST /api/programs/{id}/curriculum/changes/seen`

Body `{ version }`. Stores `max(seenVersion, version)` for the caller; drives
`isUnseen` and `unseenChangeCount`. `version` > current → **400**.

### 7.4 Editing session system message

No background job. On each curriculum save by user U:

- If the latest `CurriculumUpdated` message for U is less than 10 minutes old
  (from its last update) and no user message by U was posted after it, update
  its payload (`toVersion`, `changeCount`), bump its `editedAt`, and move it to
  the end of the stream (new `sequence`), so it reappears as unread.
- Otherwise post a new `CurriculumUpdated` system message with
  `fromVersion` = the version before this save.
- Saves without an authenticated user post no session message.

`changeCount` is the net item count between `fromVersion` and `toVersion`.

When an approval is active (or the program is `Approved`), the same save also
revokes it, posts `ApprovalRevoked` (`reason = CurriculumEdited`) before the
session message, and queues the advisor's `CurriculumApprovalRevoked`
notification.

## 8. Realtime and notifications

Hub `/hubs/notifications`, client event `syncEvent`
`{ scope, entityType, entityId, at, payload? }`. `JoinProgramSync(programId)`
is allowed for participants.

| Scope | entityType / entityId | Payload |
|---|---|---|
| `advisory.discussionChanged` | Program / programId | `{ latestSequence }` |
| `advisory.pinChanged` | Program / programId | `{ messageId }` |
| `advisory.approvalChanged` | Program / programId | `{ status, curriculumVersion }` |
| `curriculum.structureChanged` | existing | adds `{ curriculumVersion }`; now also raised for Module, Material, ResearchMilestone and program content changes |

Notification types:

- Added: `AdvisoryDiscussionMessage` (at most one per recipient per program
  per 5 minutes; skipped when the recipient has a live connection in the
  program group — single-instance in-memory tracker), `AdvisoryMentionPinned`,
  `CurriculumApprovalRequested`, `CurriculumApprovalRevoked` (advisor only,
  once per revoke, published after commit — see section 1).
- Kept: `CurriculumReviewApproved`, `CurriculumReviewPublished`.
- Deprecated (no longer emitted): `CurriculumReviewSubmitted`,
  `CurriculumReviewChangesRequested`, `AdvisoryFeedbackPublished`,
  `AdvisoryReply`, `AdvisoryCorrectionAddressed`.

## 9. Removed endpoints (410 `ENDPOINT_REMOVED`)

Routes stay registered, marked `[Obsolete]`, and return **410** with
`error.code = "ENDPOINT_REMOVED"`. Their service code is deleted.

- `POST {id}/submit-review`, `withdraw-review`, `approve-review`, `request-changes`
- `{id}/review-submissions/*` (including `draft` and `changes`), `{id}/curriculum-reviews`
- `{id}/advisory-threads/*`, `{id}/advisory/board`, `{id}/advisory/timeline`,
  `advisory-anchor-fields`, `{id}/advisory-references/*`, `{id}/advisory-read`
- `PUT /api/program-frameworks/{id}/versions/{versionId}/rubric`,
  `/api/program-frameworks/{id}/criteria*`

`GET /api/programs/review-queue` is removed (410). `GET /api/programs/advisory-mine`
is extended with `unreadCount`, `openPinCount`, `status`, `approvalState`
(`None`, `Approved`, `Revoked`).

## 10. Error code index

| Code                                                                             | HTTP | Where                                      |
| ----------------------------------------------------------------------------------| ------| --------------------------------------------|
| `CURRICULUM_VERSION_STALE`                                                       | 409  | approve, publish                           |
| `APPROVAL_BLOCKED`                                                               | 409  | approve (open pins)                        |
| `FRAMEWORK_CHECK_FAILED`                                                         | 409  | approve (data = `FrameworkCheckDto`)       |
| `INVALID_STATUS`                                                                 | 409  | lifecycle action in wrong status           |
| `ADVISOR_REQUIRED`, `ADVISOR_LOGIN_REQUIRED`                                     | 400  | approval request                           |
| `MENTION_TARGET_INVALID`                                                         | 400  | post/edit message                          |
| `MESSAGE_EMPTY`, `MESSAGE_TOO_LONG`, `TOO_MANY_MENTIONS`, `TOO_MANY_ATTACHMENTS` | 400  | post/edit message                          |
| `ATTACHMENT_INVALID`, `ATTACHMENT_TOO_LARGE`, `ATTACHMENT_TYPE_NOT_ALLOWED`      | 400  | attachments                                |
| `MATERIAL_ACTIVITY_INVALID`                                                      | 409  | save attachment as material                |
| `FRAMEWORK_RULES_INVALID`                                                        | 400  | framework version save                     |
| `ENDPOINT_REMOVED`                                                               | 410  | removed endpoints                          |
