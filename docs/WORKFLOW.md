# Repository Workflow

Repository product behavior, architecture, decisions, plans, code, tests, and
runtime signals are the system of record.

## Repository Map

- `AGENTS.md`: entry map and authority boundary.
- `docs/product/`, `docs/ARCHITECTURE.md`, and `docs/decisions/`: current intent.
- `docs/plans/`: durable multi-session work.
- `docs/templates/`: exec-plan, decision, story, validation, runbook.
- Code, tests, CI, and runtime signals: executable and observable truth.

Use `docs/README.md` for the complete map.

## OboxSTEAM Proof Commands

Default verification for this repository:

```powershell
dotnet build OboxSteam.API/OboxSteam.API.csproj
dotnet test OboxSteam.Test/OboxSteam.Test.csproj
# optional coverage:
.\scripts\run-coverage.ps1
```

After EF model changes, generate migrations with the EF CLI (never hand-edit
migration snapshots). See `.cursor/rules/cursor-instructions.mdc`.

## Select The Work Shape

### Does The Work Need Durable Memory?

Use an ephemeral plan for bounded, single-session work. Create one plan in
`docs/plans/active/` when work spans sessions, coordinates contributors, has
meaningful dependencies, needs recovery, or cannot safely resume from its diff.

Use `docs/templates/exec-plan.md`. Keep progress and task-local decisions in the
same file.

### Does The Work Need Human Judgment?

Before editing, identify authority for new externally observable policy. If
materially different choices remain, stop and request the smallest decision.
Configurable defaults are not authority.

Also pause for ambiguous product intent, difficult recovery, weakened
validation, security, or compatibility, and insufficient authority.

In Cursor, the project rule `follow-up-question.mdc` requires stating a
confidence percentage and clarifying before changes when confidence is below
97%.

### What Proves The Behavior?

Use focused unit tests for local rules, broader tests for boundaries, and
runtime checks when the task requires a live app. Plans and checklists do not
prove product behavior by themselves.

### Does The Work Encode An Invariant?

For architecture, reliability, security, or quality boundaries, follow
`docs/patterns/encoding-invariants.md`. Encode only accepted repository rules.

## Task Flows

### Read-Only Request

Inspect only what the answer needs. Change nothing. Discovery never grants
authority to fix what it finds.

### Bounded Change

Restate the outcome, inspect authority and proof, make the smallest coherent
change, run focused and required checks, and report outcome, changes, evidence,
and limits. No parallel lifecycle database is required.

### Durable Planned Change

Create or resume one active plan. Keep outcome, context, approach, risk,
recovery, progress, decisions, and validation current. Implement in verifiable
groups, promote lasting decisions into `docs/decisions/`, then move the plan to
`docs/plans/completed/`.

### Operate The Application

Use a consumer-owned runbook when one exists (`docs/templates/application-runbook.md`
is structure only). Do not invent credentials, cleanup, or product policy.

### Improve The Harness

Report reusable agent friction during ordinary work. Change Harness guidance
only when the user explicitly invokes `$improve-harness` and follow
`docs/templates/harness-improvement.md`.

## Maintain The Installed Core

```powershell
.\scripts\bin\harness.exe status
.\scripts\bin\harness.exe doctor
.\scripts\bin\harness.exe update
```

`.harness-core/` stores the frozen upstream BASE used for safe updates. Agents
do not read it for product rules.

## Completion Standard

A change is complete when the outcome exists or its blocker is explicit,
repository truth remains current, behavior-appropriate proof passed or its gap
is disclosed, any required plan is current, and the report separates facts,
limits, and unattempted work.
