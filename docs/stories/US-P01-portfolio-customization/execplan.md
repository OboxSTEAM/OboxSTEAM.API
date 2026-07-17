# Exec Plan

## Goal

Deliver deep portfolio customization (theme, media, sections, per-item
styling), server-side HTML sanitization, and draft/publish snapshots without
breaking existing auto-synced certificate/capstone items.

## Scope

In scope:

- Domain entities: `PortfolioSection`, `PortfolioMediaAsset`,
  `PortfolioMediaPlacement`; new fields on `Portfolio` and
  `PortfolioCustomItem`.
- EF Core migration (CLI-generated) plus idempotent startup backfill of
  built-in group sections from legacy `theme.sectionOrder`.
- Extended theme DTO validation, portfolio media endpoints (image only),
  section endpoints, item gallery/styling fields.
- HTML sanitization service and publication snapshot (JSONB on `Portfolio`).

Out of scope:

- Video uploads for portfolio media.
- Changes to the activity media pipeline.
- Test project scaffolding (no test project exists; build is the proof).

## Risk Classification

Risk flags:

- Data model (new tables, new columns, backfill).
- Public contracts (new/changed portfolio API shapes).
- External systems (AWS S3 uploads).
- Audit/security (HTML sanitization of user content).
- Existing behavior (publication and by-subdomain semantics change).

Hard gates:

- Data migration → high-risk lane; migration generated only via EF CLI.
- External provider behavior (S3) → reuse existing `IBlobService` only.

## Work Phases

1. Discovery — completed (portfolio service/controller, blob service, EF
   config surveyed via CodeGraph).
2. Design — `design.md`.
3. Validation planning — `validation.md`.
4. Implementation — stage 1: entities + migration; stage 2: DTOs/sanitizer;
   stage 3: media + sections; stage 4: publication snapshot; stage 5:
   backfill + seed + verification.
5. Verification — `dotnet build`, `dotnet test` (no test project yet).
6. Harness update — record trace when the Harness CLI binary is available
   (the documented `scripts/bin/harness-cli.exe` is absent on this checkout).

## Stop Conditions

Pause for human confirmation if:

- Snapshot shape needs to diverge from the public response DTO.
- Backfill cannot deterministically map legacy `sectionOrder` values.
- Validation requirements need to be weakened.
