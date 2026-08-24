# Context Rules

Put the smallest useful context in the model. Prefer `docs/WORKFLOW.md` over
re-reading historical process docs.

## By Task Shape

### Read-only (explain, review, diagnose)

| Source | When |
| --- | --- |
| `AGENTS.md` | Always |
| Relevant `docs/product/*` | When answering product or API behavior |
| `docs/ARCHITECTURE.md` | When layering or controller map matters |
| Code / tests under question | As needed |

Do not edit files. Discovery does not authorize fixes.

### Bounded change (one session)

| Source | When |
| --- | --- |
| `AGENTS.md`, `docs/WORKFLOW.md` | Always |
| Affected `docs/product/*` | Behavior or API contract changes |
| `docs/ARCHITECTURE.md` | Structural / layer changes |
| Files to edit + nearby patterns | Always before editing |
| `docs/decisions/*` | When touching a locked choice |

Proof: `dotnet build` and focused `dotnet test` (or disclose the gap).

### Durable planned change (multi-session)

Same as bounded, plus:

| Source | When |
| --- | --- |
| `docs/plans/active/<plan>.md` | Must create or resume |
| `docs/templates/exec-plan.md` | When starting a new plan |

Keep the plan current; move to `docs/plans/completed/` after validation.

### High judgment (payments, auth, migrations, new policy)

Stop before edits when authority is missing or two material policies fit.
Read the product doc and any matching decision. Prefer one clarifying question
over guessing (see `.cursor/rules/follow-up-question.mdc`).

### Improve Harness

Only when the user invokes `$improve-harness`. Use
`docs/templates/harness-improvement.md` and `docs/WORKFLOW.md` (Improve The
Harness). Do not treat ordinary product work as a harness rewrite.

## CodeGraph Integration

Optional local index for cheaper structure navigation. Additive to Harness —
product authority and proof still come from docs, code, and tests.

**Setup (optional, per developer):**

```bash
codegraph init
```

Cursor MCP: `.cursor/mcp.json` → `codegraph serve --mcp`. Restart Cursor after
changing MCP config.

**When available:** prefer graph explore/callers for structure questions; still
**Read** files before editing and still read `docs/product/*` when contracts
change.

**When unavailable:** use `Grep` / `Read` / search. Do not block work waiting
for CodeGraph.

Policy detail: `.cursor/rules/codegraph.mdc`.

## Stop Reading When

- The question is answered from authority already in context.
- The edit surface is identified and proof command is clear.
- Further docs only repeat the same rule without changing the decision.
