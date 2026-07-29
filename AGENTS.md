# Agent Instructions

## OboxSTEAM.API

.NET 8 clean-architecture backend for the OboxSTEAM STEAM education platform.

**Read first for product work:**

- `docs/product/overview.md` — roles, hierarchy, capability map
- `docs/ARCHITECTURE.md` — layers, controllers, stack
- `docs/product/api-conventions.md` — `ApiResult`, auth, errors

**Build:**

```bash
dotnet build OboxSteam.API/OboxSteam.API.csproj
```

**Unit tests + coverage** (Application only; excludes Seed / Media / PersonalVideo
and all Infrastructure / Background / AWS adapters via `coverage.runsettings`):

```powershell
.\scripts\run-coverage.ps1
# optional filter:
.\scripts\run-coverage.ps1 -Filter "FullyQualifiedName~ClassServiceTests"
```

**Conventions:** follow `.cursor/rules/coding-style-csharp.mdc` — manual DTO
mapping, `GenericRepository`, no `Async` suffix on controller actions,
`ErrorHelper` for business errors.

<!-- HARNESS:BEGIN -->
## Harness

This repo uses Harness. Before work, read:

- `README.md`
- `docs/HARNESS.md`
- `docs/FEATURE_INTAKE.md`
- `docs/ARCHITECTURE.md`
- `docs/CONTEXT_RULES.md`
- `docs/TOOL_REGISTRY.md` (CodeGraph + inbound tool registry)
- `scripts/bin/harness-cli query matrix` on macOS/Linux, or `.\scripts\bin\harness-cli.exe query matrix` on Windows

Use the Rust Harness CLI at `scripts/bin/harness-cli` on macOS/Linux or
`scripts/bin/harness-cli.exe` on Windows as the main operational tool.
<!-- HARNESS:END -->

<!-- CODEGRAPH:BEGIN -->
## CodeGraph

Local code knowledge graph for cheaper structure navigation. **Additive to
Harness** — see `docs/CONTEXT_RULES.md` (CodeGraph Integration) for lane-by-lane
rules. Harness still owns intake, product contract, validation, and traces.

**Setup (once per checkout):**

```bash
codegraph init
scripts/bin/harness-cli tool check
```

**Cursor MCP:** `.cursor/mcp.json` runs `codegraph serve --mcp`. Restart Cursor
after changing MCP config.

**Prefer graph over grep loops** for structure questions (`explore`, `callers`).
Still **Read** files before editing and still read `docs/product/*` per lane.
Record graph usage in harness traces (`actions_taken`).
<!-- CODEGRAPH:END -->
