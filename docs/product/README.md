# Product Docs

Current product contract for the OboxSTEAM backend API.

## Files

| File | Scope |
| --- | --- |
| `README.md` | This index and the update rule |
| `overview.md` | Platform summary, roles, surfaces, hierarchy, deployment |
| `api-conventions.md` | Response envelope, JSON, dates, auth, errors, CORS |
| `permissions.md` | Role-based access patterns |
| `curriculum.md` | Program → module → course → activity model |
| `enrollment.md` | Enrollments, progress, gating, re-delivery |
| `assessment.md` | Assignments, quizzes, question banks |
| `notifications.md` | Notification audiences and publishers |
| `student-skills.md` | Skill catalog, program skills, achieved and snapshot student skills |
| `mentor-skills.md` | Mentor skill profiles, evidence, class skill matching |
| `integrations.md` | PostgreSQL, AWS, Bedrock, email, Stripe, JaaS, SignalR, webhooks, telemetry, env |

## Update Rule

When behavior changes:

1. Update the affected product doc.
2. For multi-session work, keep `docs/plans/active/` current (or a story under
   `docs/stories/` when useful).
3. Record a decision in `docs/decisions/` when architecture or a locked product
   rule changes.
4. Prove with `dotnet test` / `dotnet build` (or document the proof gap).

## Proof

```powershell
dotnet test OboxSteam.Test/OboxSteam.Test.csproj
dotnet build OboxSteam.API/OboxSteam.API.csproj
```

See also `docs/TEST_MATRIX.md` for epic-level snapshot status.
