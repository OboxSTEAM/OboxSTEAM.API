# US-E03-001 Student weekly schedule

## Status

implemented

## Lane

normal

## Product Contract

An authenticated student can load one Monday–Sunday week of class sessions they
must attend, grouped by local date in Asia/Ho_Chi_Minh. A verified parent can
load the same timetable for a linked child by passing `studentId`. The response
is a seven-day timetable (empty days included). Cancelled sessions and inactive
class enrollments are omitted. Occupied-interval conflict data stays on
`GET /api/me/schedule`.

## Relevant Product Docs

- `docs/product/enrollment.md`
- `docs/product/curriculum.md`
- `docs/product/api-conventions.md`

## Acceptance Criteria

- `GET /api/schedules/weekly` allows Student and Parent.
- Students see their own schedule (`studentId` optional; another student's id is 403).
- Parents must pass `studentId` of a verified linked child; omit → 400; unverified → 403.
- Omit `weekStart` to use the current VN Monday; a non-Monday `weekStart` returns 400.
- Response always has seven days Monday through Sunday in Asia/Ho_Chi_Minh.
- Sessions come from active class enrollments only; cancelled sessions are omitted.
- Each session includes class code/name, location, times, attendance status, and
  `materialId` when a SelfPaced material exists.
- `GET /api/me/schedule` is unchanged (flat occupied intervals for conflict UX).
- The API does not return FAP-style Slot 1–8 rows; the client grids by time.

## Design Notes

- Commands: none (read-only).
- Queries: active `ClassEnrollment` → non-cancelled `ClassSession` in the UTC
  window for the VN week; join `SessionAttendance` and `Material`.
- API: `GET /api/schedules/weekly?weekStart=yyyy-MM-dd&studentId={guid}`.
- Tables: no schema changes.
- Domain rules: week bounds and grouping use Asia/Ho_Chi_Minh (Windows fallback
  `SE Asia Standard Time`). Filter and group by session start. Attendance is
  the roster status when a row exists. Materials exist only for SelfPaced activities.
  Parent access uses a verified `ParentStudent` link.
- UI surfaces: student and parent weekly timetable; client places cells using start/end.

## Validation

When updating durable proof status, use numeric booleans:
`scripts/bin/harness-cli story update --id US-E03-001 --unit 1 --integration 0 --e2e 0 --platform 0`.

| Layer | Expected proof |
| --- | --- |
| Unit | `ScheduleServiceTests` |
| Integration | none |
| E2E | none |
| Platform | none |
| Release | `dotnet test` filtered to `ScheduleServiceTests` |

## Harness Delta

Weekly timetable is a distinct read from occupied intervals. Product docs now
name both endpoints so later agents do not replace `GET /api/me/schedule`.
The Harness CLI binary is not in this checkout, so durable proof records were
not updated.

## Evidence

- `dotnet test --filter FullyQualifiedName~ScheduleServiceTests` — 12 passed.
- `dotnet build OboxSteam.API/OboxSteam.API.csproj` succeeded with 0 errors.
