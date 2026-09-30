# Execution Plan: Advisory Chat Overhaul (Backend)

Date: 2026-09-30

## Status

Active

## Outcome

One realtime chat per program (mentions, pins, attachments) and a single
versioned advisor approval replace advisory threads, review rounds, the rubric,
and `PendingReview`. Framework rules are automatic only. Curriculum edits are
versioned and logged so the expert sees one consolidated change list.

Contract: `specs/advisory-chat-contract.md`.

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

1. Contract doc (this plan + `specs/advisory-chat-contract.md`).
2. A1 model + migration: `Program.CurriculumVersion`, `ProgramApproval`,
   discussion message extensions (kind, system event, pin, edit/delete),
   `ProgramAdvisoryDiscussionAttachment`, remove `PendingReview`, migrate the
   `General` thread and RequiredChange/Suggestion roots into discussion.
3. A8 change log: EF `SaveChanges` interceptor (version bump + `CurriculumChange`
   rows), lock rules (`CURRICULUM_LOCKED_ACTIVE`, auto-revoke on Approved),
   `curriculum/changes` with net consolidation, `changes/seen`, lazy
   `CurriculumUpdated` session message.
4. A7 framework rules: drop rubric tables/columns, add rule fields, validation,
   framework-check codes.
5. A3 + A2: mention-targets, discussion messages/pins/attachments/
   mention-counts, material from attachment, attachment purge job.
6. A4: workspace DTO, approval request/approve/revoke, publish, advisor change,
   expert read permissions.
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
- [ ] 2. A1 model + migration
- [ ] 3. A8 change log + lock rules
- [ ] 4. A7 framework rules + rubric drop
- [ ] 5. A3 + A2 discussion endpoints
- [ ] 6. A4 approval lifecycle
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

## Validation

- Focused proof: unit tests per task in `OboxSteam.Test/UnitTests`.
- Repository-required checks: `dotnet build OboxSteam.API/OboxSteam.API.csproj`,
  `dotnet test OboxSteam.Test/OboxSteam.Test.csproj`.

## Result

Pending.
