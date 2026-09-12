# Execution Plan: Expert advisory flow backend

Date: 2026-09-12

## Status

Active

## Outcome

Extend the existing curriculum review API into a version-aware advisory workspace backend. Managers, the responsible advisor, and board experts can share historical submissions, review notes, structured discussion, field references, independent read state, and safe review decisions without losing cross-round context.

## Context

- User-provided execution specification: Expert Advisory Workspace coordinated BE and FE execution plan, sections 3 and 4 and BE packages P0-P6.
- FE hardening backlog (2026-09-13): stage-accurate capabilities, `approvalBlockingCount` gate, submit error codes, timeline contract, anchor-field registry, discussion unread separation.
- `docs/WORKFLOW.md`, `docs/ARCHITECTURE.md`, `docs/product/api-conventions.md`, `docs/product/curriculum.md`.
- Existing implementation in `ProgramAdvisoryService`, `CurriculumReviewService`, `ProgramController`, and the review/advisory entities.
- Accepted reliability boundaries are encoded through application validation and EF constraints; database locking remains in Infrastructure.

## Scope

In scope:

- Additive advisory workspace capabilities and blocker/count summaries.
- Versioned note transitions with accepted authorization, verification/waiver metadata, and idempotent operation IDs.
- Immutable persisted references and program Discussion with cursor pagination and idempotent sends.
- Independent note/discussion read cursors.
- Requirement links from formal decisions, notification intents, and EF persistence constraints.
- Server-derived six-stage workflow timeline projection for shared manager/expert UI.
- FE contract harden (P0–P3): capability matrix, Address-only-when-editable, machine submit/approve codes, published anchor-field registry, full thread PATCH/detail responses, carried-round labels.
- Focused unit tests and generated EF migration.

Out of scope:

- Frontend changes, mobile, attachments, presence, typing indicators, private messages, or collaborative editing.
- An Active-to-Draft lifecycle transition.
- Replacing the existing notification delivery channels or migrating old note messages into Discussion.
- P4 nice-to-haves: compact collaboration summary badge, If-Match/ETag, SignalR (FE polls).

## Approach

1. Extend domain entities, enums, DTOs, interfaces, and EF configuration additively.
2. Add reference resolution and Discussion services with shared advisory authorization.
3. Tighten note lifecycle and formal decision rules while retaining legacy endpoint compatibility.
4. Add controller routes and generate the migration with EF CLI.
5. Persist review-round intent and add the advisory workflow timeline projection endpoint.
6. Harden capability / blocking / submit / registry contracts for FE.
7. Add focused positive/negative tests and run repository build/test checks.

## Risks And Recovery

- Existing clients omit concurrency fields; preserve nullable legacy inputs while enforcing versions for new mutations.
- Existing records have no event/reference history; retain them and only create new audit rows for new actions.
- EF schema changes require a generated migration; if migration tooling or PostgreSQL is unavailable, report that limitation without hand-editing generated files.

## Progress

- [x] Read product, architecture, workflow, coding, and invariant guidance.
- [x] Inventory existing review/advisory implementation and proof.
- [x] Add additive contracts and persistence.
- [x] Implement references, Discussion, read streams, and lifecycle corrections.
- [x] Generate migration and update focused tests.
- [x] Harden P0–P3 FE contract (capabilities, blocking count, submit codes, registry, unread).
- [x] Run build and test validation.

## Decisions

- 2026-09-12: Keep legacy advisory endpoints and fields while adding the new contract so rollout remains additive.
- 2026-09-12: Use the existing `IUnitOfWork`/`GenericRepository` boundary for application changes; introduce only the smallest transaction/locking abstraction needed by the database-backed review mutations.
- 2026-09-12: Keep timeline state server-owned and separate from selected UI sections; persist review-round intent so revision verification remains distinguishable after requirements resolve.
- 2026-09-13: `canAddress` only when program is `Draft` (curriculum editable); resolve/waive/reopen remain allowed in `PendingReview`.
- 2026-09-13: `approvalBlockingCount` = Open + Addressed RequiredChanges (not Resolved); matches approve gate and `scope=outstanding`.
- 2026-09-13: Responsible advisor always required on submit-review (with or without framework).
- 2026-09-13: Status PATCH keeps returning full `AdvisoryThreadDto`; detail GET includes ordered `events` + `messages`.
- 2026-09-13: Publish anchor fields via `GET /api/programs/advisory-anchor-fields` from BE allowlist.

## Validation

- Focused proof: advisory service tests for authorization, stale versions, lifecycle, idempotency, references, Discussion, read cursor monotonicity, capability matrix, Address-only-when-editable, submit codes, outstanding scope, anchor registry.
- Integration or end-to-end proof: generated migration model/build proof; PostgreSQL race tests remain a follow-up if no integration harness is available.
- Repository-required checks: `dotnet build OboxSteam.API/OboxSteam.API.csproj`; `dotnet test OboxSteam.Test/OboxSteam.Test.csproj` (or focused `ProgramAdvisoryServiceTests`).

## Result

Backend hardening completed on 2026-09-13 (P0–P3 FE contract). Generated EF
migration includes `AddCurriculumReviewClientOperationId` for request-changes
idempotency. Navigation for this pass used CodeGraph where available, with
`Grep`/`Read` for docs and configs.

Remaining release follow-ups are real PostgreSQL transaction-race/integration
coverage, a background worker that consumes notification intents, and optional
P4 items (compact collaboration summary, If-Match/ETag, SignalR). Frontend,
mobile, and other out-of-scope clients were not changed.
