# Documentation Map

Harness operating docs plus the OboxSTEAM product contract.

## Main Files

- `HARNESS.md`: how humans and agents collaborate.
- `FEATURE_INTAKE.md`: how prompts become tiny, normal, or high-risk work.
- `ARCHITECTURE.md`: OboxSTEAM stack, layers, and API map.
- `CONTEXT_RULES.md`: what to read per phase and risk lane.
- `TEST_MATRIX.md`: behavior-to-proof map (mirror of durable matrix).
- `spec-intake.md`: reverse-engineered spec intake from codebase scan.
- `GLOSSARY.md`: Harness and product terms.
- `HARNESS_BACKLOG.md`: legacy improvement list; use CLI for current backlog.

## Folders

- `product/`: OboxSTEAM product contract (overview, curriculum, assessment, …).
- `stories/`: epic backlog and future story packets.
- `decisions/`: durable decisions including `0006-dotnet-clean-architecture-stack.md`.
- `templates/`: spec, story, decision, and validation formats.

## Current State

- **Application**: .NET 8 API with PostgreSQL and AWS integrations; builds
  successfully.
- **Tests**: no automated test project yet (`US-E07` planned).
- **Harness**: durable layer initialized at `harness.db`; epic rows seeded with
  `dotnet build` verification.

## Quick Commands

```powershell
.\scripts\bin\harness-cli.exe query matrix
.\scripts\bin\harness-cli.exe query stats
dotnet build OboxSteam.API/OboxSteam.API.csproj
```
