# Decisions

Decision records explain why important product, architecture, or harness choices
were made.

Use `docs/templates/decision.md` when adding a new decision. Keep the markdown
file under `docs/decisions/` — there is no separate Harness CLI decision DB.

Add a decision when:

- A locked technical choice changes.
- A product rule changes meaningfully.
- A validation requirement is added, removed, or weakened.
- A high-risk feature chooses one design over another.
- Auth, authorization, data ownership, security, or API behavior changes.
- The agent operating protocol or source-of-truth hierarchy changes.

Related: `0009-adopt-harness-repository-protocol.md` (current Harness core).
Historical: `0004` / `0005` describe the retired SQLite `harness-cli` layer.
