# Enrollment

## Enrollment Types

| Entity | Scope | Purpose |
| --- | --- | --- |
| ProgramEnrollment | Program | Student enrolled in a full track |
| ModuleEnrollment | Module | Progress attempt within a program enrollment |
| CourseEnrollment | Course | Mentor-led course instance access |
| ClassEnrollment | Class | Cohort membership with shared schedule |

Status fields use `EnrollmentStatus` or `ClassEnrollmentStatus` enums.

## Student Flow

1. Browse programs (public catalog).
2. Preview recruiting cohorts via `GET /api/programs/{programId}/open-classes`
   (Standard + **Open** + seats remaining > 0, with schedule sessions and seat
   counts). Soft preference: optional `preferredClassId` sorts that class first
   after payment — **no seat hold** before or after pay.
3. Pay program tuition (`POST /api/payments/checkout` or parent-pay flow).
   Checkout and parent payment requests are **blocked** when the open-classes
   list would be empty (no Open Standard class with capacity).
4. After payment activates the program enrollment, join a class cohort from the
   same open-classes list (pass `preferredClassId` if the learner viewed one
   before pay). Students may enroll (and self-transfer) only when the class is
   **Open** — not Draft, ReadyForMentor, or InProgress. Manager transfer targets
   must also be Open. Enroll and transfer are blocked when any non-cancelled
   ClassSession of the target class overlaps another active class
   (start1 < end2 && start2 < end1). Students can read occupied intervals at
   `GET /api/me/schedule`. The Monday–Sunday timetable is
   `GET /api/schedules/weekly` (weekStart optional; studentId required for
   parents of a verified linked child; Asia/Ho_Chi_Minh; cancelled sessions
   omitted).
5. Module enrollments are created as part of program progression (not sold as
   separate retail products).

Parents and managers can view enrollment state on shared read endpoints
(Student, Parent, Admin, Manager).

`GET /api/program-enrollments/{programEnrollmentId}/module-enrollments` returns
the latest module enrollment per module (ordered by `ModuleOrder`). Use the
returned `id` as `moduleEnrollmentId` for flows such as research milestone
progress.

## Gating Rules

- Program checkout requires ≥1 Open Standard class with remaining seats
  (server-enforced on direct checkout, parent payment request, and parent
  checkout for program tuition).
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
Capacity is a class-level seat count (`Class.MaxCapacity`); there is no
per-activity or per-session booking. Seat counts for open-class preview use
**Active** `ClassEnrollment` rows only.

## Assessment recovery vs class re-delivery

- **Same class:** mentor-granted extra attempts never transfer the student.
- **Theory:** unlimited assignment retries; never pay or transfer only to redo a test.
- **Class re-delivery** (`ClassRedeliveryRequest`): when Experiential/Research needs
  hands-on again (or recovery request cap is hit). System auto-matches another
  `Open`/`InProgress` class in the same program that has not reached that module
  yet (no `InProgress`/`Completed` `ClassSession` for the module). On match,
  student pays `Program.Price` (full program tuition, progression kept) then
  transfers. If no eligible class, request goes to the manager queue.
  Schema also stores `Class.Kind` / `Class.RemedialModuleId`,
  `ClassEnrollment.Kind`, and request `IntensivePaceAcceptedAt` /
  `ResolutionType` for the two-tier retake ladder (endpoints in later stories).
- Module retake invoices appear in `GET /api/invoices/me` alongside program tuition.

API: `/api/class-redelivery-requests`.

## Payments

`Payment` entity and `PaymentStatus` / `PaymentGateway` enums exist in the
domain model. Program tuition and class re-delivery (amount = `Program.Price`)
both create `Invoice` rows visible via invoice endpoints. Module-level retail
price and retake fee columns have been dropped.

Program tuition checkout is gated on open-class capacity (see Student Flow).
Module retake checkout is **not** subject to that gate.

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles.
