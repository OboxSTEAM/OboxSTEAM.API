# Product Docs

Current product contract for the OboxSTEAM backend API, derived from the
implemented codebase and domain model.

## Files

| File | Scope |
| --- | --- |
| `overview.md` | Platform summary, roles, hierarchy |
| `api-conventions.md` | Response envelope, auth, CORS, errors |
| `permissions.md` | Role-based access patterns |
| `curriculum.md` | Program → module → course → activity model |
| `enrollment.md` | Enrollments, progress, gating |
| `assessment.md` | Assignments, quizzes, question banks |
| `integrations.md` | PostgreSQL, AWS, email, webhooks |

## Update Rule

When behavior changes:

1. Update the affected product doc.
2. Update or create the story packet under `docs/stories/`.
3. Update durable proof status with `scripts/bin/harness-cli story add` or
   `scripts/bin/harness-cli story update`.
4. Record a decision if the change affects architecture, scope, risk, or a
   previously settled product rule.

## Proof Status

Query the behavior-to-proof matrix:

```bash
scripts/bin/harness-cli query matrix
```

On Windows:

```powershell
.\scripts\bin\harness-cli.exe query matrix
```
