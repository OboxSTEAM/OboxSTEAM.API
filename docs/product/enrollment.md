# Enrollment

## Enrollment Types

| Entity | Scope | Purpose |
| --- | --- | --- |
| ProgramEnrollment | Program | Student enrolled in a full track |
| ModuleEnrollment | Module | Access to a module (may be à la carte) |
| CourseEnrollment | Course | Mentor-led course instance access |
| ClassEnrollment | Class | Cohort membership with shared schedule |

Status fields use `EnrollmentStatus` or `ClassEnrollmentStatus` enums.

## Student Flow

1. Browse programs (public catalog).
2. Enroll at program level (`POST /api/program-enrollments`) — Student role.
3. Enroll in modules (`POST /api/module-enrollments`) when module access is
   required separately.
4. Join a class cohort when class-based delivery applies.

Parents and managers can view enrollment state on shared read endpoints
(Student, Parent, Admin, Manager).

`GET /api/program-enrollments/{programEnrollmentId}/module-enrollments` returns
the latest module enrollment per module (ordered by `ModuleOrder`). Use the
returned `id` as `moduleEnrollmentId` for flows such as research milestone
progress.

## Gating Rules

- Module prerequisites: `PrerequisiteModuleId` must be satisfied before access.
- Class late-join: `Class.MinHoursBeforeAssignmentJoin` blocks self-enrollment
  near assignment windows; managers may bypass in service logic.
- Quiz and assignment access require active module enrollment (enforced in
  `IQuizAttemptService` and assignment services).

## Enrollment curriculum tree

`GET /api/program-enrollments/{enrollmentId}/curriculum` returns per-student
nav status for activities and assignments (`locked`, `available`, `current`,
`completed`, `submitted`).

Assignment locking mirrors activity gating:

- **Module locked** — prerequisite module not complete → `locked`.
- **Course assignment** — locked until all activities in that course are done.
- **Module-scoped assignment** — locked until all activities in the module are done.
- **Research milestone deliverable** — locked until the previous milestone is
  passed (if any) and required milestone activities are completed.

Assignments with an in-progress draft (`Pending` / `ReturnedForRevision`) stay
`available` when prerequisites are met. Turned-in work shows `submitted`; a
passing grade shows `completed`.

## Progress

`ActivityProgress` tracks completion per student per activity.
`ActivityBooking` handles booking for capacity-limited activities.

## Payments

`Payment` entity and `PaymentStatus` / `PaymentGateway` enums exist in the
domain model. Payment integration endpoints should be verified against current
controllers before documenting public contract details.

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles.
