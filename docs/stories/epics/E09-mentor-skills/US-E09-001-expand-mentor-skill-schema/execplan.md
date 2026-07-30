# Exec Plan

## Goal

Expand mentor skill schema so expertise can be stored transparently, without
manager verification, as the foundation for later profile APIs.

## Scope

In scope:

- Domain entities (`MentorSkill` enrichment, `MentorSkillEvidence`)
- `OboxSteamDbContext` / `IUnitOfWork` wiring
- Product docs, decision 0008, high-risk story packet
- EF Core migration via CLI

Out of scope:

- Application services / controllers / DTOs
- Seed enrichment
- Unit / integration tests for API behavior

## Risk Classification

Risk flags:

- Authorization (ownership / visibility rules documented now; enforced later)
- Data model
- Public contracts (product contract change)
- Existing behavior (mentor skill shape)

Hard gates:

- Data model / migration

Lane: high-risk

## Work Phases

1. Discovery — product choices locked (self-managed, evidence entries, public flag).
2. Design — schema + decision 0008.
3. Validation planning — `dotnet build` after migration.
4. Implementation — entities + DbContext + UoW + migration.
5. Verification — build succeeds.
6. Stop — APIs and tests deferred.

## Stop Conditions

Pause for human confirmation if:

- Existing mentor skill rows need destructive transforms.
- Product asks to reintroduce manager verification.
- Architecture direction changes.
