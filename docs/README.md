# Documentation Map

OboxSTEAM product contract plus the current Harness repository protocol.

## Start Here

| Doc | Role |
| --- | --- |
| `AGENTS.md` | Entry map and agent boundaries |
| `WORKFLOW.md` | How to shape work, when to stop, how to prove completion |
| `ARCHITECTURE.md` | .NET layers, controllers, stack |
| `CONTEXT_RULES.md` | What to read per task shape; CodeGraph guidance |
| `product/` | Product behavior contract |
| `plans/` | Multi-session execution memory |
| `decisions/` | Lasting product or architecture choices |
| `TEST_MATRIX.md` | Epic-level behavior-to-proof snapshot |

## Folders

- `product/` — enrollment, curriculum, assessment, notifications, …
- `plans/active/` — durable plans while work is in flight
- `plans/completed/` — finished plans with recorded validation
- `stories/` — optional story packets for large features (markdown only)
- `templates/` — exec-plan, decision, story, validation, runbook
- `patterns/` — invariant encoding and similar methods

## Current State

- **Application**: .NET 8 API (PostgreSQL, AWS integrations).
- **Tests**: `OboxSteam.Test` unit suite; run `dotnet test` or `.\scripts\run-coverage.ps1`.
- **Harness**: core **0.1.10** via `.\scripts\bin\harness.exe` (`status`, `doctor`, `update`).
  Protocol v1 (`harness-cli`, SQLite `harness.db`, intake/story/trace tables) is
  retired in this repo — see `docs/decisions/0009-adopt-harness-repository-protocol.md`.

## Quick Commands

```powershell
.\scripts\bin\harness.exe status
.\scripts\bin\harness.exe doctor
dotnet build OboxSteam.API/OboxSteam.API.csproj
dotnet test OboxSteam.Test/OboxSteam.Test.csproj
```
