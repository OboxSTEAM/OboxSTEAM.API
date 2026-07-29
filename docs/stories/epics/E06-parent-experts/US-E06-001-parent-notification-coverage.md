# US-E06-001 Parent Notification Coverage

## Status

implemented

## Lane

normal

## Product Contract

Verified parents receive class planning, class lifecycle, session schedule, and
assignment publication notifications for each actively enrolled linked student.
Immediate session start/completion events and material updates remain limited to
their existing audiences.

## Relevant Product Docs

- `docs/product/notifications.md`
- `docs/product/overview.md`

## Acceptance Criteria

- Class updated, started, auto-started, and completed notifications include
  verified parents of active class-roster students.
- Session scheduled, rescheduled, and cancelled notifications include verified
  parents of active class-roster students.
- Assignment published notifications include verified parents of active
  class-roster students.
- Session started/completed and material updated notifications preserve their
  current audiences.
- The complete notification type-to-audience-to-publisher matrix is documented.
- Duplicate recipient IDs are removed when audiences overlap.

## Design Notes

- Commands: Existing notification catalog commands select explicit audience
  kinds.
- Queries: Recipient resolution loads active class enrollments and verified
  parent-student links.
- API: No route or response shape changes.
- Tables: No schema changes.
- Domain rules: Only verified parent links qualify; only active class
  enrollments define the roster.
- UI surfaces: Existing in-app inbox and SignalR notification stream.

## Validation

| Layer | Expected proof |
| --- | --- |
| Unit | Not available; no test project exists |
| Integration | Not available; no test project exists |
| E2E | Not available |
| Platform | Not available |
| Release | `dotnet build OboxSteam.API/OboxSteam.API.csproj` |

## Harness Delta

The product notification matrix becomes explicit in
`docs/product/notifications.md`. The Harness CLI is absent from this checkout,
so durable intake and proof records cannot be updated.

## Evidence

- `dotnet build OboxSteam.API/OboxSteam.API.csproj` passed on 2026-07-29
  with 0 errors.
- Ten pre-existing nullable warnings remain outside the changed files.
- IDE diagnostics report no errors in the changed C# files.
- Automated behavioral proof is unavailable because the repository has no test
  project.
