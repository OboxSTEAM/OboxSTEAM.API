# Exec Plan

## Goal

Replace free-text `StudentSkill` with catalog `Skill` + snapshot `StudentSkill`
+ evidence join `StudentSkillEvidence`, including EF migration.

## Scope

In scope:

- Domain enums and entities
- `OboxSteamDbContext` / `IUnitOfWork` wiring
- EF Core migration via CLI

Out of scope:

- Seed STEAM skill list
- Application services / controllers
- LLM assessment pipeline

## Risk Classification

Risk flags:

- Data model
- Weak proof (no test project)

Hard gates:

- Data loss or migration (drops legacy SkillName/SkillType columns)

## Work Phases

1. Discovery — confirmed with product owner.
2. Design — Skill / StudentSkill / StudentSkillEvidence.
3. Validation planning — `dotnet build`.
4. Implementation — entities + DbContext + migration.
5. Verification — build succeeds.
6. Harness update — decision + story + trace.

## Stop Conditions

Pause for human confirmation if:

- Existing `StudentSkills` rows must be preserved with data transform.
- Validation requirements need to be weakened.
- Architecture direction changes.
