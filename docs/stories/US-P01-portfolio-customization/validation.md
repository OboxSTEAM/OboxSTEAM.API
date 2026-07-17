# Validation

## Proof Strategy

No test project exists in this repository (see `AGENTS.md` proof gap), so
mechanical proof is `dotnet build` plus EF migration generation succeeding.
Behavioral verification is manual via Swagger until a test project lands.

## Test Plan

| Layer | Cases |
| --- | --- |
| Unit | None (no test project). |
| Integration | None (no test project). |
| E2E | Manual Swagger: theme update, media upload/list/delete, section CRUD/reorder, item galleries/styling, publish/unpublish, by-subdomain snapshot. |
| Platform | Startup migration + built-in section backfill on existing data. |
| Performance | Not required. |
| Logs/Audit | Upload/publish/unpublish information logs. |

## Fixtures

Seeded student `STD-001` portfolio (see `SeedService.Portfolio.cs`) with
certificates and capstone items for augmentation checks.

## Commands

```text
dotnet build OboxSteam.API/OboxSteam.API.csproj
dotnet test
dotnet ef migrations add <Name> --project OboxSteam.Infrastructure/OboxSteam.Infrastructure.csproj --startup-project OboxSteam.API/OboxSteam.API.csproj
```

## Acceptance Evidence

- Stage 1 (entities + migration): `dotnet build` succeeded; migration
  `20260717151124_AddPortfolioSectionsMediaAndPublishing` generated via EF CLI.
- Stage 2–5 (DTOs, sanitizer, media, sections, publication, backfill, seed):
  `dotnet build OboxSteam.API/OboxSteam.API.csproj` succeeded (0 errors).
  `dotnet test OboxSteam.API.slnx` completes with no test project (proof gap).
