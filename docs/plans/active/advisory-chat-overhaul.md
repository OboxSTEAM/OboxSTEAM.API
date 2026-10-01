# Execution Plan: Advisory Chat Overhaul (Backend)

Date: 2026-09-30

## Status

Active

## Outcome

One realtime chat per program (mentions, pins, attachments) and a single
versioned advisor approval replace advisory threads, review rounds, the rubric,
and `PendingReview`. Framework rules are automatic only. Curriculum edits are
versioned and logged so the expert sees one consolidated change list.

Contract: current behaviour lives in `docs/product/curriculum.md` (Advisory
Chat and Approval, framework rules) and `docs/product/notifications.md`
(Realtime Sync Events). Work not yet shipped is in "Remaining Contract" below.
The original `specs/advisory-chat-contract.md` was folded into those files and
removed on 2026-10-01.

## Context

- Source plan (FE + BE): `advisory_chat_overhaul_2dc40f14.plan.md` (Cursor plan).
- Current behaviour: `docs/product/curriculum.md`, `docs/product/permissions.md`,
  `docs/product/notifications.md`.
- Superseded plan areas: `docs/plans/active/expert-advisory-flow.md`
  (threads, rounds, rubric).
- Core code: `ProgramAdvisoryService`, `ProgramAdvisoryDiscussionService`,
  `CurriculumReviewService`, `CurriculumReviewSnapshotBuilder`,
  `ProgramFrameworkService`, `CurriculumEditGuard`, `NotificationHub`,
  `SyncEventPublisher`, `OboxSteamDbContext`.

## Scope

In scope: Part A of the source plan (A1–A8) and seed-data cleanup.

Out of scope: frontend (Part B); re-approval of Active programs (phase 2);
typing/presence indicators; replies/quotes.

## Approach

One task per review/commit cycle, in this order:

1. Contract doc (this plan + a contract spec, since folded into
   `docs/product`).
2. A1 model (schema only): `Program.CurriculumVersion`, `ProgramApproval`,
   discussion message extensions (kind, system event, pin, edit, removal),
   `ProgramAdvisoryDiscussionAttachment`. Migration `AddAdvisoryChatModel`.
3. A8 change log: EF `SaveChanges` interceptor (version bump + `CurriculumChange`
   rows), cohort-only lock, auto-revoke + advisor notification on edit,
   `curriculum/changes` with net consolidation, `changes/seen`, lazy
   `CurriculumUpdated` session message.
4. A7 framework rules: drop rubric tables/columns, add rule fields, validation,
   framework-check codes.
5. A3 + A2: mention-targets, discussion messages/pins/attachments/
   mention-counts, material from attachment, attachment purge job. Data
   migration: copy `General` thread messages and RequiredChange/Suggestion
   roots into discussion messages.
6. A4: workspace DTO, approval request/approve/revoke, publish, advisor change,
   expert read permissions. Remove `PendingReview` (data migration
   `PendingReview` -> `Draft`).
7. A5 + A6: sync scopes, notification types + batching + in-memory presence,
   410 `ENDPOINT_REMOVED` on old routes, delete old service code.
8. Seed cleanup: remove rubric, review rounds, submissions, drafts, threads;
   seed programs/chat/approvals that fit the new flow.

## Risks And Recovery

- Destructive migration (rubric tables dropped, `PendingReview` rewritten).
  Recovery: restore a DB backup taken before applying; migrations have `Down`
  methods for schema but not for dropped data.
- Interceptor touching every save: must only act on curriculum entities and
  must not recurse. Covered by unit tests per entity type.
- Presence tracker is in-memory and single-instance only; scale-out would need
  a backplane.

## Progress

- [x] 1. Contract doc
- [x] 2. A1 model (schema) — migration `20260930165911_AddAdvisoryChatModel`
- [x] 3. A8 change log + lock rules — migration `20260930172444_AddCurriculumChangeLog`
- [x] 4. A7 framework rules + rubric drop — migration `20260930181132_DropRubricAddFrameworkRules`
- [x] 5. A3 + A2 discussion endpoints — data migration
  `20260930183025_MigrateAdvisoryThreadsToDiscussion` (not applied locally)
- [x] 6. A4 approval lifecycle — data migration
  `20260930210212_ApprovalLifecycleRemovePendingReview` (not applied locally)
- [ ] 7. A5 + A6 realtime + deprecation
- [ ] 8. Seed cleanup

## Decisions

- 2026-09-30: Delivered in phases; each task stops for review and commit.
- 2026-09-30: `ProgramAdvisoryDiscussionMessage` is the canonical chat store;
  `General` thread history and RequiredChange/Suggestion roots are migrated in.
- 2026-09-30: Rubric storage is dropped without archive.
- 2026-09-30: Old routes stay `[Obsolete]` and return 410 `ENDPOINT_REMOVED`;
  their service code is deleted.
- 2026-09-30: Save-as-material follows upload rules (SelfPaced, one material
  per activity), else 409.
- 2026-09-30: Chat notification presence uses an in-memory tracker
  (single instance).
- 2026-09-30: Change capture uses an EF Core `SaveChanges` interceptor.
- 2026-09-30: `CurriculumUpdated` session message is maintained lazily on
  each save (no background job).
- 2026-09-30: Data migrations run in the task that switches the behaviour, not
  in A1: the chat copy lands with the new discussion service (task 5) so no
  General-thread messages written in between are lost, and `PendingReview`
  removal lands with the new approval lifecycle (task 6) because the old
  submit/withdraw code still depends on it.
- 2026-09-30: Author retraction uses `RemovedAt`/`RemovedByUserId`, not
  `BaseEntity.IsDeleted`, so removed messages stay in the stream as tombstones
  despite the global soft-delete query filter.
- 2026-09-30: The interceptor (Infrastructure) only extracts tracked entries;
  all rules live in `CurriculumChangeRecorder` (Application) so they are unit
  tested with the in-memory unit of work. The recorder is resolved lazily from
  the request scope to avoid a DbContext → UnitOfWork → DbContext cycle.
- 2026-10-01: `CurriculumEditGuard` keeps the original cohort-only lock (class
  `InProgress`, or `Open` with `Active` enrollments); there is no
  Active/Inactive lock. Approved no longer blocks edits. `PendingReview`
  still blocks until task 6. An edit while an approval is active revokes it
  (`CurriculumEdited`); `Approved` returns to `Draft`, `Active`/`Inactive`
  keep their status (re-approval of live programs is phase 2).
- 2026-10-01: The advisor gets one `CurriculumApprovalRevoked` notification
  per revoke. The recorder queues it; `UnitOfWork` publishes after a save with
  no open transaction or after `ExecuteAdvisoryTransactionAsync` commits, and
  the interceptor/rollback path discards it on failure.
- 2026-10-01: Task 4 strips scores from the old review flow instead of
  removing it: approve/request-changes/draft keep working with a comment only
  (`RUBRIC_SCORE_BELOW_HALF` removed). `ProgramReviewDrafts` stays (comment
  only; `ScoresJson` dropped) until task 7 removes the old endpoints.
- 2026-10-01: `ProgramAdvisoryTargetType.RubricCriterion` stays as an
  `[Obsolete]` value until the task 5 thread migration; new threads and
  references reject it and the migration deletes rubric references.
- 2026-10-01: Framework rules: every value ≥ 0 (zero allowed), min ≤ max,
  ratios ≤ 100 with sum ≤ 100 (`FRAMEWORK_RULES_INVALID`). Update uses
  `null` = unchanged and `Clear<Field>` for numeric rules. `SkillsGained`
  counts `ProgramSkill` links. The new checks run in `framework-check` and the
  old submit-review pre-check; the frozen-snapshot board highlights keep the
  original four. Evaluation semantics are recorded in
  `docs/product/curriculum.md` (Program framework and curriculum review).
- 2026-10-01: Task 5 makes thread write routes 410; thread reads and the old
  thread service stay until task 7.
- 2026-10-01: Thread migration interleaves General messages and
  RequiredChange/Suggestion roots by `CreatedAt` after existing chat messages;
  a `Resolved` RequiredChange root becomes a plain message.
- 2026-10-01: Chat posts keep the existing `AdvisoryReply` notification
  intent; no pin notifications or realtime events until task 7.
- 2026-10-01: Message filter and mention counts use the exact target only.
- 2026-10-01: Save-as-material uses `CurriculumEditGuard` (no status check)
  plus material type/size rules (400 `ATTACHMENT_TYPE_NOT_ALLOWED`).
- 2026-09-30: `MaterialService` upload/update/delete now go through
  `CurriculumEditGuard` (previously unguarded).
- 2026-09-30: The session message moves to the end of the stream when
  extended; cascaded deletes and insert-driven sibling shifts are not recorded
  separately; no concurrency token on `CurriculumVersion` (accepted race).
- 2026-10-01: Task 6 replaces the advisory workspace with the new workspace
  shape (`ProgramApprovalService`). Review write routes and `review-queue`
  return 410; review reads and the old services stay until task 7.
  `PendingReview` is `[Obsolete]` (rows rewritten to `Draft`) and is deleted
  in task 7.
- 2026-10-01: Existing `Approved` programs get one backfilled active
  `ProgramApproval` at their current curriculum version; without an
  identifiable expert they return to `Draft`.
- 2026-10-01: Revoke notifies the counterpart: manager reopen → advisor,
  advisor revoke → managers. An advisor change posts `AdvisorChanged` and,
  when `Approved`, revokes (`AdvisorChanged`) without a notification.
- 2026-10-01: Workspace participants list active managers, then the advisor,
  then board experts. Experts only get material signed URLs for programs where
  they are the advisor or a board member.

- 2026-10-01: Advisory realtime uses a separate authorized group
  `advisory:{programId}` (`JoinAdvisorySync` / `LeaveAdvisorySync`, participants
  only); the public `program:{programId}` group stays unchanged.
- 2026-10-01: `advisory.discussionChanged` payload is
  `{ latestSequence, messageId }` (`messageId` set on edit/remove). Approval
  auto-resolve sends one `advisory.pinChanged` per resolved pin. Existing
  student-side `curriculum.structureChanged` publishes stay; the recorder adds
  the advisory group publish with `{ curriculumVersion }`.
- 2026-10-01: Task 7b chat notifications: the discussion service no longer
  writes `ProgramAdvisoryNotificationIntents` (table kept) and publishes after
  commit. `AdvisoryDiscussionMessage` goes to every workspace participant
  except the author, user messages only, skipped when present in
  `advisory:{programId}` or notified for the program in the last 5 minutes
  (checked against the `Notifications` table, so it survives restarts).
  `AdvisoryMentionPinned` goes to managers on every new pin. Deprecated
  catalog factories stay until 7e.

## Remaining Contract

Target behaviour for the unfinished steps of tasks 7 and 8. Move each item
into `docs/product` when it ships.

Notifications: deprecated catalog factories (`CurriculumReviewSubmitted`,
`CurriculumReviewChangesRequested`, `AdvisoryFeedbackPublished`,
`AdvisoryReply`, `AdvisoryCorrectionAddressed`) are deleted in 7e together
with their last callers (old services, seed); enum values stay for old inbox
rows.

Live endpoints (task 7c): move `GET {id}/framework-check` and
`GET advisory-mine` out of the services deleted in 7e. `advisory-mine` keeps
`page`, `pageSize`, `status`, `unreadOnly` and adds `unreadCount`,
`openPinCount`, `status`, `approvalState` (`None`, `Approved`, `Revoked`).

Removed endpoints (task 7d): routes stay registered, `[Obsolete]`, and return
410 `ENDPOINT_REMOVED`; their service code is deleted. Already 410: advisory
thread writes (task 5), review writes and `review-queue` (task 6), framework
`rubric` / `criteria*`. Still to switch: `{id}/review-submissions/*` (list,
detail, `changes`, draft GET), `{id}/curriculum-reviews`,
`{id}/advisory-threads/*` (including `advisory-threads/pins` and
`advisory-threads/{threadId}/read`), `{id}/advisory/board`,
`{id}/advisory/timeline`, `advisory-anchor-fields`,
`{id}/advisory-references/*`, `{id}/advisory-read`. The 400
`FRAMEWORK_CHECK_FAILED` submit-review pre-check goes with them.

Cleanup (task 7e): remove `ProgramStatus.PendingReview`,
`ProgramAdvisoryTargetType.RubricCriterion`, `AdvisoryStreamType.Thread`; drop
all history tables (`CurriculumReview`, `ProgramReviewSubmission`, review
drafts, advisory threads/messages/events/reads) in one generated migration;
keep and clean `ProgramAdvisoryReferences` and `ProgramAdvisoryStreamReads`.

Migration history (tasks 2–6, for recovery):

1. `ApprovalLifecycleRemovePendingReview`: `PendingReview` → `Draft`; one
   active `ProgramApproval` backfilled per `Approved` program (expert and time
   from its latest `Approved` curriculum review, else current advisor and now;
   version = current; snapshot and framework check `{}`); without an
   identifiable expert the program returns to `Draft`.
2. `MigrateAdvisoryThreadsToDiscussion`: `General` thread messages and
   `RequiredChange` / `Suggestion` root messages copied as `User` messages,
   interleaved by `CreatedAt` after existing messages,
   `clientMessageId = "migrated:{sourceMessageId}"` (re-run safe). Replies are
   not copied. `Open` / `Addressed` RequiredChange roots become pins with the
   same status (pinned by the thread author at thread creation); `Resolved`
   ones become plain messages. Text is prefixed with the target's mention
   token (plus a `WorkingDraft` / `Node` reference) or `[TargetLabel] ` when
   the target no longer exists. `RubricCriterion` threads get no prefix.
3. `CurriculumVersion` starts at 0; no change-log backfill.
4. `DropRubricAddFrameworkRules`: `FrameworkRubricCriteria`,
   `ReviewCriterionScores`, `ProgramReviewDrafts.ScoresJson`,
   `ProgramReviewSubmissions.RubricSnapshotJson`, and rubric
   `ProgramAdvisoryReferences` (plus message-reference rows) dropped without
   archive.

## Validation

- Focused proof: unit tests per task in `OboxSteam.Test/UnitTests`.
- Repository-required checks: `dotnet build OboxSteam.API/OboxSteam.API.csproj`,
  `dotnet test OboxSteam.Test/OboxSteam.Test.csproj`.

## Result

Pending.
