# Context Engineering Rules

Context rules help agents decide what to read, when to read it, and when to
stop reading. They are additive to the stable `AGENTS.md` reading list.

The goal is not to maximize context. The goal is to put the right information
in the model for the current task phase and risk lane.

## Context Phases

### Intake Phase

Read to classify the request, find the affected surface, and choose a lane.

| Document Or Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| `AGENTS.md` | Must | Must | Must |
| `docs/FEATURE_INTAKE.md` | Must | Must | Must |
| `scripts/bin/harness-cli query matrix` | Must | Must | Must |
| `README.md` | Should | Must | Must |
| `docs/HARNESS.md` | Should | Must | Must |
| `docs/ARCHITECTURE.md` | Skip | Should | Must |
| Relevant `docs/product/*` | Skip if unrelated | Must if product behavior changes | Must |
| Relevant `docs/stories/*` | Skip if unrelated | Must if a story exists | Must |
| `docs/decisions/*` | Skip | Should if architecture or durable rules are touched | Must |
| `docs/HARNESS_COMPONENTS.md` | Skip | Should for Harness improvements | Must for observability or benchmark work |

### Planning Phase

Read to decide the smallest safe approach and expected proof.

| Document Or Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Current files to edit | Must | Must | Must |
| `docs/templates/story.md` | Skip | Must when creating/updating a story | Should |
| `docs/templates/high-risk-story/*` | Skip | Skip unless risk escalates | Must |
| `docs/ARCHITECTURE.md` | Skip | Should for code or boundary changes | Must |
| `docs/TEST_MATRIX.md` or `scripts/bin/harness-cli query matrix` | Should | Must | Must |
| Relevant decisions | Skip | Should | Must |
| `docs/HARNESS_MATURITY.md` | Skip | Should for Harness improvements | Must for maturity or process changes |
| `docs/HARNESS_BACKLOG.md` and `scripts/bin/harness-cli query backlog` | Skip | Should if friction repeats | Must if changing Harness behavior |

### Implementation Phase

Read while making the change. Keep this phase scoped to files that directly
affect the selected story.

| Document Or Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Files being changed | Must | Must | Must |
| Adjacent files with same pattern | Should | Must | Must |
| Relevant product docs | Skip if copy-only | Must if behavior changes | Must |
| Relevant story packet | Skip if no story needed | Must | Must |
| Relevant templates | Skip | Should when adding docs | Must |
| `docs/ARCHITECTURE.md` | Skip | Should for structural changes | Must |
| Provider/API/security docs | Skip | Should if touched | Must |
| Unrelated docs and historical traces | Skip | Skip | Should only if they affect decisions |

### Validation Phase

Read to prove the change and avoid claiming unsupported completion.

| Document Or Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Story acceptance criteria | Should | Must | Must |
| `docs/TEST_MATRIX.md` or `scripts/bin/harness-cli query matrix` | Should | Must | Must |
| Validation section of story packet | Skip if no story | Must | Must |
| `docs/templates/validation-report.md` | Skip | Should for notable proof | Must for high-risk proof |
| Relevant commands from README/package docs | Should | Must | Must |
| Benchmark protocol or external benchmark repo | Skip | Skip unless requested | Must if the story depends on benchmark proof |
| `docs/HARNESS_MATURITY.md` | Skip | Should for Harness improvements | Must for maturity claims |

### Trace Phase

Read to leave useful evidence for the next agent and for benchmark scoring.

| Document Or Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| `docs/TRACE_SPEC.md` | Should | Must | Must |
| `scripts/bin/harness-cli query matrix` | Should | Must | Must |
| `scripts/bin/harness-cli query backlog` | Skip | Should if friction occurred | Must |
| Changed-file list from `git status --short` | Must | Must | Must |
| Validation command output | Should | Must | Must |
| Story packet or progress log | Skip if no story | Must | Must |
| `docs/HARNESS_COMPONENTS.md` | Skip | Should if attributing friction | Must if failure attribution is needed |

## Retrieval Triggers

| Trigger Condition | Action |
| --- | --- |
| Task touches database schema, durable records, or migrations | Read `docs/decisions/0004-sqlite-durable-layer.md`, `scripts/schema/`, and relevant CLI code before planning. |
| Task touches CLI command behavior or installer distribution | Read `docs/decisions/0005-prebuilt-rust-harness-cli.md`, `scripts/README.md`, relevant `crates/harness-cli/*` code, CLI help output, and installer docs. |
| Task touches auth, authorization, audit/security, data loss, or external providers | Treat as high-risk, read `docs/templates/high-risk-story/*`, and check prior decisions before implementation. |
| Task changes public API shape, product behavior, or user-visible workflow | Read relevant `docs/product/*`, story packets, and validation expectations before editing. |
| Task changes Harness policy, source hierarchy, risk classification, or validation requirements | Read `docs/HARNESS.md`, `docs/FEATURE_INTAKE.md`, `docs/ARCHITECTURE.md`, and `docs/decisions/*`; pause if direction is ambiguous. |
| Task discovers repeated confusion, stale docs, or missing proof | Read `docs/HARNESS_BACKLOG.md`, record `harness_friction`, and add a backlog item when the fix is out of scope. |
| Task makes a maturity, observability, trace quality, or benchmark claim | Read `docs/HARNESS_COMPONENTS.md`, `docs/HARNESS_MATURITY.md`, and `docs/TRACE_SPEC.md`. |
| Task is normal or high-risk and spans multiple iterations | Create or update a story/progress file under `docs/stories/` and keep it current. |
| Final response is being prepared | Re-read the validation evidence, `git status --short`, and `docs/TRACE_SPEC.md` before recording the final trace. |

## Token Budget Guidance

| Lane | Target Context Budget | Read Shape | Reasoning |
| --- | --- | --- | --- |
| Tiny | About 2K tokens of Harness context | `AGENTS.md`, `docs/FEATURE_INTAKE.md`, matrix query, and the exact file being changed. | Tiny work should not spend more context on policy than on the edit. |
| Normal | About 5K tokens of Harness context | Intake docs, relevant product/story docs, architecture when structural, validation expectations, and trace spec at the end. | Normal work needs enough context to preserve contracts and record proof without reading every historical file. |
| High-risk | About 10K tokens of Harness context | Full intake, architecture, relevant decisions, high-risk templates, product docs, validation docs, trace spec, and component/maturity docs when Harness behavior changes. | High-risk work needs source hierarchy, prior decisions, and proof expectations in context before implementation. |

Budget rules:

- Prefer CodeGraph or targeted `rg` over bulk file reading for **code structure**.
- Read the smallest section that answers the current phase question.
- Escalate context when a retrieval trigger fires.
- Do not keep reading unrelated history after the lane, affected files, and
  validation path are clear.

Harness context budgets above cover **policy and product docs only**. CodeGraph
savings apply to **source-code navigation** and do not reduce Harness intake,
story, or trace obligations.

## CodeGraph Integration

CodeGraph is an optional inbound tool registered in the Harness tool registry.
It indexes C# source and ASP.NET routes locally (`.codegraph/`). Cursor loads it
via `.cursor/mcp.json`. Harness still owns intake, lanes, product contract,
validation proof, and traces.

Run at intake start when CodeGraph is expected:

```bash
scripts/bin/harness-cli tool check
scripts/bin/harness-cli query tools --capability impact-analysis --status present
```

If `.codegraph/` is missing, run `codegraph init` once per machine checkout.
If MCP is not connected, restart Cursor after editing `.cursor/mcp.json`.

### Role Split

| Question type | Use Harness | Use CodeGraph |
| --- | --- | --- |
| What should this feature do? | `docs/product/*`, stories | Skip |
| How risky is the change? | `docs/FEATURE_INTAKE.md`, matrix | Skip |
| What proof is required? | matrix, story validation | Skip |
| Where is symbol X defined? | Skip after graph query | `codegraph_explore` / `codegraph_search` |
| Who calls this service? | Skip after graph query | `codegraph_callers` |
| What breaks if I change Y? | product docs for contracts | `codegraph_explore` (blast radius) |
| What file do I edit? | story packet, architecture | `codegraph_node` then **Read** for edits |

CodeGraph answers structure; Harness answers intent, risk, and done definition.
Never skip Harness-mandatory reads because CodeGraph returned source snippets.

### MCP Tools (Cursor)

| Tool | Use when |
| --- | --- |
| `codegraph_explore` | Primary — flows, "how does X work", survey an area |
| `codegraph_search` | Locate a symbol by name |
| `codegraph_callers` | Every call site or registration of a method |
| `codegraph_node` | One symbol's full source, or read a file with dependents |

After your own edits, honor staleness banners — **Read** the live file before
claiming the final content is correct.

### Lane-By-Lane: Harness vs CodeGraph

#### Intake Phase

| Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Harness intake + matrix (see table above) | Must | Must | Must |
| `harness-cli tool check` | Should | Must | Must |
| CodeGraph | Skip | Skip | Skip |

Intake classifies work using Harness only. Do not explore code before the lane
is chosen.

#### Planning Phase

| Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Harness planning docs (see table above) | Per lane | Per lane | Per lane |
| `codegraph_explore` for touch-point discovery | Skip if file known | Should when span unclear | Must before multi-file plan |
| `codegraph_callers` / blast radius | Skip | Should for behavior changes | Must for auth, API, data model |
| `rg` / Read for code | Should if one file | Fallback if graph absent | Fallback if graph absent |

#### Implementation Phase

| Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Files being changed (**Read** before edit) | Must | Must | Must |
| Relevant product docs + story | Per lane above | Must | Must |
| `codegraph_explore` to find adjacent patterns | Skip | Should | Must |
| `codegraph_node` instead of blind grep | Optional | Should | Should |
| Re-grep to "verify" graph output | Skip | Skip | Skip |

#### Validation Phase

| Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Harness validation (see table above) | Per lane | Per lane | Per lane |
| `dotnet build` / story `verify` | Should | Must | Must |
| `codegraph affected` (when tests exist) | Skip | Should after US-E07 | Should after US-E07 |
| CodeGraph | Does not replace build or test proof | Does not replace build or test proof | Does not replace build or test proof |

#### Trace Phase

| Source | Tiny | Normal | High-Risk |
| --- | --- | --- | --- |
| Harness trace fields (see table above) | Per lane | Per lane | Per lane |
| Record CodeGraph usage in `actions_taken` | Optional | Should | Must |
| Note `capability impact-analysis: full/degraded/inactive` | Skip | Should | Must |

### Degrade Ladder (from `docs/TOOL_REGISTRY.md`)

| Providers present for `impact-analysis` | Agent behavior |
| --- | --- |
| none registered | Inactive — use `rg` and Read; note in trace |
| registered, graph missing | Degraded — run `codegraph init`; use `rg` meanwhile |
| registered, graph present, MCP down | Degraded — use `codegraph` CLI; note MCP gap |
| all present | Full — prefer graph over grep loops for structure |

### CodeGraph Retrieval Triggers

| Trigger | Action |
| --- | --- |
| Task spans controllers, services, repositories, or DI wiring | `codegraph_explore` from the entry controller or service name |
| Task changes a public method or DTO used across layers | `codegraph_callers` before editing |
| Task adds or changes an ASP.NET route | explore handler + attribute route together |
| Task touches `OpenClassAutoStartService` or background workers | explore registration in `Program.cs` / `DependencyInjection` |
| Graph returns stale-file banner | Read the flagged file directly |
| CodeGraph absent after `tool check` | Fall back to `rg`; set Weak proof if impact was unclear |

### OboxSTEAM.API Entry Points (quick explore seeds)

| Area | Seed query or symbol |
| --- | --- |
| HTTP surface | `OboxSteam.API.Controllers` namespace or specific `*Controller` |
| Application services | `OboxSteam.Application` service under change |
| Infrastructure / EF | `OboxSteamDbContext`, `GenericRepository`, or target `*Repository` |
| Background jobs | `OpenClassAutoStartService`, `IHostedService` registrations |
| Auth | `AuthController`, JWT middleware, authorization handlers |

## Additive Behavior

These rules do not replace `AGENTS.md`. Agents should still read the stable
entrypoint documents listed there before work. This document explains what to
retrieve after that initial context, based on lane, phase, and trigger.

## Review Checklist

Before implementation:

- Lane is chosen from `docs/FEATURE_INTAKE.md`.
- Relevant product docs or story packets are identified.
- Any high-risk trigger has been handled.
- `scripts/bin/harness-cli tool check` run when CodeGraph is registered.

Before final response:

- Validation evidence has been read.
- `docs/TRACE_SPEC.md` has been read for normal/high-risk tasks.
- The final trace includes files read, files changed, outcome, and friction
  when applicable.
