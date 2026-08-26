# US-E06-002 Notification Template Engine

## Status

implemented

## Lane

normal

## Product Contract

In-app notifications render role-specific copy with token interpolation.
Recipient resolution returns `(UserId, Role, ContextStudentId)`. Parents of
multiple students in the same audience receive one inbox row per child, using
that child's name.

## Relevant Product Docs

- `docs/product/notifications.md`

## Acceptance Criteria

- Catalog events that reach students and parents expose Student and Parent
  variants; parent copy uses `{studentName}` instead of second-person "you".
- `INotificationRecipientResolver` returns role-tagged recipients with optional
  context student id.
- Class-roster parent audiences emit one parent row per enrolled child.
- `NotificationPublisher` interpolates `{studentName}`, `{actorName}`,
  `{className}`, `{programName}`, `{moduleName}`, `{activityName}`,
  `{assignmentTitle}`, and `{extraAttempts}` per recipient.
- Duplicate suppression is by `(UserId, ContextStudentId)`, not user id alone.
- Inbox API shape is unchanged (Title/Body are already-rendered strings).

## Design Notes

- Commands: `NotificationCommand` carries `Templates` and `Tokens`; Title/Body
  remain the default variant with shared tokens already applied (seed-friendly).
- Queries: resolver loads active class enrollments, verified parent links, and
  user roles; publisher batch-loads display names.
- API: no route or response shape changes.
- Tables: no schema changes.
- Domain rules: unverified parent links and inactive enrollments still excluded.
- UI surfaces: existing in-app inbox and SignalR stream receive rendered copy.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | `NotificationTemplateEngineTests` — renderer, resolver parent-per-child, publisher role copy |
| Integration | Not required for this slice |
| E2E | Not required |
| Platform | Not required |
| Release | `dotnet test` filtered to notification template engine tests |

## Harness Delta

Product notification contract now documents role templates, tokens, and
per-child parent rows.

## Evidence

- `dotnet test OboxSteam.Test/OboxSteam.Test.csproj --filter FullyQualifiedName~NotificationTemplateEngineTests` passed (9 tests).
- Full `OboxSteam.Test` suite passed: 1056 tests, 0 failures.
- Proof lives in this Evidence section and `docs/TEST_MATRIX.md` (no Harness CLI DB).
