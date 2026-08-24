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

Start with the requested outcome and use the repository as the system of record.
Read `docs/WORKFLOW.md` and only relevant product, design, plan, code, and
validation material.

- Answers, explanations, reviews, diagnoses, plans, and status reports are
  read-only. Inspect only what is needed; change nothing.
- For a bounded change, inspect affected behavior and proof, implement, and
  validate. No control-plane operation is required.
- Use one `docs/plans/active/` file when work spans sessions, coordinates
  contributors, has dependencies, or needs recovery. Move it to
  `docs/plans/completed/` only after validation.
- Before editing, identify repository authority for each new externally
  observable policy. If materially different choices remain open, stop before
  edits; configurable defaults are not authority.
- For architecture, reliability, security, or quality invariant work, read
  `docs/patterns/encoding-invariants.md` and enforce only accepted rules.
- Report reusable agent friction. Change guidance, tools, runbooks, or validation
  for that purpose only when explicitly asked to use `$improve-harness`.
- Also pause when product intent remains ambiguous, recovery is difficult,
  validation is weakened, or authority is insufficient.
- Claim completion only with executable or observable evidence. Report outcome,
  changes, validation, and unresolved risks.

Maintain the installed core with `scripts/bin/harness` (Windows:
`.\scripts\bin\harness.exe`): `status`, `doctor`, `update`. Protocol v1
`harness-cli` / SQLite `harness.db` is end of life; do not record intake or
story rows there.

Harness has no task database or orchestration lifecycle. Use repository plans
and behavior-level proof; do not create parallel control-plane state.
<!-- HARNESS:END -->

<!-- CODEGRAPH:BEGIN -->
## CodeGraph

Local code knowledge graph for cheaper structure navigation. **Additive to
Harness** — see `docs/CONTEXT_RULES.md` (CodeGraph Integration) for lane-by-lane
rules. Harness still owns product contract, plans, and validation proof.

**Setup (once per checkout, optional):**

```bash
codegraph init
.\scripts\bin\harness.exe doctor
```

**Cursor MCP:** `.cursor/mcp.json` runs `codegraph serve --mcp`. Restart Cursor
after changing MCP config.

**Prefer graph over grep loops** for structure questions (`explore`, `callers`).
Still **Read** files before editing and still read `docs/product/*` per lane.
When CodeGraph is unavailable, use `Grep` / `Read` and note the fallback in any
plan or completion report.
<!-- CODEGRAPH:END -->
