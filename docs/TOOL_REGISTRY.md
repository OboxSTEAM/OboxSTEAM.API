# Tool Registry

The harness deals with two distinct kinds of "tool". Keep them separate.

| | Capability manifest (outbound) | Inbound tool registry |
| --- | --- | --- |
| Direction | harness offers it to the agent | a project equips it for the harness to use |
| Examples | the `harness-cli` subcommands below | CodeGraph, linters, deploy checks |
| Presence | always compiled in | optional; may be absent on any machine |
| If missing | n/a (it is the harness) | clean skip; never blocks the main process |

This document describes both. The **inbound registry** is where optional tools
like CodeGraph are registered so `tool check` and `query tools` can report what
is equipped on the current machine.

Lane-by-lane usage of CodeGraph with Harness lives in
`docs/CONTEXT_RULES.md` (CodeGraph Integration).

## OboxSTEAM.API: Registered Tools

These rows are seeded via `harness-cli tool register` and reconciled with
`harness-cli tool check`:

```bash
scripts/bin/harness-cli tool register --name codegraph --kind mcp \
  --capability impact-analysis --scan ".codegraph" --command "mcp:codegraph" \
  --description "Local code knowledge graph for symbol search and blast radius" \
  --responsibility "Context selection"

scripts/bin/harness-cli tool register --name codegraph-cli --kind binary \
  --capability code-navigation --command codegraph \
  --description "CodeGraph CLI for explore, callers, and affected-file analysis" \
  --responsibility "Context selection"
```

| Name | Kind | Capability | Scan | Cursor wiring |
| --- | --- | --- | --- | --- |
| `codegraph` | `mcp` | `impact-analysis` | `.codegraph` | `.cursor/mcp.json` → `codegraph serve --mcp` |
| `codegraph-cli` | `binary` | `code-navigation` | `codegraph` on `PATH` | CLI fallback when MCP is disconnected |

Check presence at intake:

```bash
scripts/bin/harness-cli tool check
scripts/bin/harness-cli query tools --capability impact-analysis --status present
```

## Inbound Registry: Register A Tool

```bash
scripts/bin/harness-cli tool register \
  --name deploy-check \
  --kind cli \
  --capability deploy-verification \
  --command ./scripts/deploy-check.sh \
  --description "Verify deploy health before release" \
  --responsibility Verification \
  --args "env:enum:required:staging,production"
```

Fields specific to inbound tools:

- `--kind` — `cli`, `binary`, `mcp`, `skill`, or `http`.
- `--capability` — workflow purpose a step looks up (kebab-case).
- `--scan` — for `mcp`/`skill`/`http`, path or URL that `tool check` resolves.

Remove a tool:

```bash
scripts/bin/harness-cli tool remove --name deploy-check
```

## Inbound Registry: Check Presence

```bash
scripts/bin/harness-cli tool check
scripts/bin/harness-cli tool check --name codegraph
scripts/bin/harness-cli tool check --json
```

| Kind | Probe | `present` means |
| --- | --- | --- |
| `cli`, `binary` | command on `PATH` or as path | installed and runnable |
| `mcp`, `skill` | `scan_target` path exists | equipped on disk |
| `http` | TCP reachability or path | endpoint answers |

`tool check` always exits `0`. An `mcp` tool marked `present` means the index
exists — the agent still confirms the MCP server is live in the current session.

## Inbound Registry: Look Up By Capability

```bash
scripts/bin/harness-cli query tools --capability impact-analysis
scripts/bin/harness-cli query tools --capability impact-analysis --status present
```

### Degrade Ladder

| Providers present | Posture | Agent behavior |
| --- | --- | --- |
| none registered | Inactive | clean skip; note `capability X: inactive` in trace |
| registered but not present | Degraded | use `rg`/Read; note the gap |
| all present | Full | prefer CodeGraph over grep loops for structure |

### Recommended Capability Vocabulary

```
impact-analysis · code-navigation · deploy-verification · coverage
security-scan · performance-benchmark · documentation-lookup
```

## Inspecting The Registry

```bash
scripts/bin/harness-cli query tools --summary
scripts/bin/harness-cli query tools --json
scripts/bin/harness-cli query tools --responsibility "Context selection"
```

## Compiled Harness Commands (Outbound Manifest)

See `docs/HARNESS.md` for the full command list (`init`, `intake`, `story`,
`trace`, `query matrix`, `tool register`, `tool check`, and others).

Upstream reference: [repository-harness TOOL_REGISTRY](https://github.com/hoangnb24/repository-harness/blob/main/docs/TOOL_REGISTRY.md).
