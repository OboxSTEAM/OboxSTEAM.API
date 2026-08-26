# Product Docs

Current product contract for the OboxSTEAM backend API.

## Files

| File | Scope |
| --- | --- |
| `overview.md` | Platform summary, roles, hierarchy |
| `api-conventions.md` | Response envelope, auth, CORS, errors |
| `permissions.md` | Role-based access patterns |
| `curriculum.md` | Program → module → course → activity model |
| `enrollment.md` | Enrollments, progress, gating, re-delivery |
| `assessment.md` | Assignments, quizzes, question banks |
| `notifications.md` | Notification audiences and publishers |
| `student-skills.md` | Student skill snapshots and evidence |
| `mentor-skills.md` | Mentor skill profiles and evidence |
| `integrations.md` | PostgreSQL, AWS, email, webhooks |

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
