# Permissions and Authorization

## Role Model

`RoleType` enum (`OboxSteam.Domain.Enums`):

| Role | Description |
| --- | --- |
| Admin | Platform-wide administration |
| Manager | Curriculum and operational management |
| Mentor | Delivers courses and mentors class cohorts |
| Parent | Views and acts on behalf of linked students |
| Student | Learns, enrolls, submits work |

JWT role claims must match enum names exactly (e.g. `"Student"`, `"Admin"`).

## Authorization Patterns

### Public (no auth)

- `POST /api/auth/register`, `login`, password reset, OTP, refresh-token.
- Read-only catalog endpoints on programs, modules, courses, activities (where
  not decorated with `[Authorize]`).

### Authenticated (any role)

- `POST /api/auth/logout`
- `/api/account/*`
- `/api/media/*` (base controller requires auth; list endpoints are role-scoped in
  `MediaService`)

### Manager / Admin

Create, update, delete for:

- Programs, modules, courses, activities (mutations).
- Assignments and question banks.
- Curriculum structure and admin seed data.
- Media tag management (`POST/PATCH/DELETE /api/media/{id}/tags...`).
- `GET /api/media` without filters returns all ready media.

### Student

- Program and module enrollment.
- Quiz attempts and submissions.
- Program reviews (own reviews).
- Parent link requests (student-initiated).
- Media: own face-tagged ready media only (`GET /api/media`, class-session media filtered).

### Parent

- View linked student enrollments and progress (shared endpoints with Student,
  Admin, Manager).
- Parent-specific endpoints under `/api/parent/*`.
- Media: ready media tagged for verified linked students (`studentId` required on
  `GET /api/media`).

### Mentor

- Assigned via `Course.MentorId` and `Class.MentorId`; mentor-scoped behavior
  is enforced in services, not only at controller level.
- Class curriculum progress rollup: `GET /api/classes/{classId}/curriculum-progress`
  (assigned mentor only). Returns activity Done/InProgress and assignment
  submitted/graded aggregates over active enrollments — no roster PII.
- Media review: list media for mentored class activities; add/remove/verify
  student tags on that media (`Mentor,Manager,Admin` on tag mutation routes).
  AI tags start with `IsVerified = false` until mentor approval.
- Mentor skill profile: freely create, update, delete, and set `IsPublic` on
  own `MentorSkill` rows and evidence (no manager verification). See
  `docs/product/mentor-skills.md`.

### Mentor skill visibility

- Mentor (own): all skills.
- Manager / Admin: all skills on a mentor (staffing).
- Student (mentor profile by id): only `IsPublic` skills.

## Parent–Student Linking

`ParentStudent` entity models the relationship. Parent and Student roles have
dedicated endpoints in `ParentController` for link requests and approvals.

## Claims Access

`IClaimsService` reads the current user from `HttpContext` for service-layer
authorization and audit context.

## Security Notes

- Auth, OTP, and refresh-token flows are high-risk areas; changes require
  decision records and stronger validation proof.
- `SeedController` (`/api/seed`) should be disabled or protected in production
  deployments (verify deployment configuration outside this repo).
