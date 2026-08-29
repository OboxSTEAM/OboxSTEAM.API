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
   counts). Seat counts include non-expired **Pending** holds from class selection.
3. **Select class** via `POST /api/programs/{programId}/select-class` (`classId`) — starts
   the **5-minute** soft seat hold and publishes `seats.changed`.
4. **Leave checkout** via `POST /api/programs/{programId}/release-class-hold` when the
   learner reloads or navigates away — releases the seat and soft-deletes the
   `PendingPayment` program enrollment. Direct checkout cancel/fail and Stripe session
   expiry abandon the same checkout state automatically.
5. Pay program tuition (`POST /api/payments/checkout` or parent-pay with the same
   `classId`). Requires the hold from step 3. On success, `ProgramEnrollment` and
   `ClassEnrollment` become **Active** together — no separate post-pay class join step.
6. Module enrollments are created as part of program progression (not sold as
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
**Active** `ClassEnrollment` rows plus non-expired **Pending** holds (5-minute
checkout window). Realtime hints: SignalR `syncEvent` scope `seats.changed` on
hub `/hubs/notifications` — clients call `JoinProgramSync(programId)` then
refetch open-classes when notified.

## Purchase close and rebuy (fail / drop)

A `ProgramEnrollment` closes permanently when the student fails or withdraws:

- **Academic fail** — latest required-assignment submission graded fail,
  effective attempts exhausted, and the recovery cap (2) reached →
  `ProgramEnrollment.Status = Failed`, `EndReason = AcademicFail`.
- **Attendance fail** — ≥20% missed sessions on a module → `Failed` with
  `EndReason = Attendance`.
- **Withdraw** — student self-withdraws via
  `POST /api/program-enrollments/{id}/withdraw` → `Dropped` with
  `EndReason = Withdraw`.

Closing withdraws class seats immediately and records `EndedAt` /
`EndedModuleId`. A closed enrollment keeps **read-only** curriculum access
(`GET .../curriculum` still works); all student mutations (activity
completion, quiz/assignment/research submissions, recovery requests) require
an **Active** enrollment and return 403 on closed ones.

**Rebuy.** Continuing requires a new purchase of the same program:

- Checkout detects the latest closed source enrollment and links it via
  `SourceProgramEnrollmentId`.
- **Price:** within 3 calendar months of the source `EndedAt` (boundary day
  included) the student pays `Program.RetakeFee ?? Program.Price`; after the
  window they pay full `Program.Price`. A `Completed` source anchors the
  window at `CompletedAt` and gets retake **pricing only**.
- **Class eligibility:** the rebuy must join exactly one `Open` Standard class
  that has not started the module the student stopped at, nor any later
  module in `ModuleOrder` (no `InProgress`/`Completed` `ClassSession` on
  those modules). For `Failed` sources the stop module is `EndedModuleId`;
  for `Dropped` sources it is the first not-`Completed` module. `Completed`
  sources are unconstrained.
- **Credit copy (inside the window only):** on payment success, modules
  completed on the source are copied onto the new enrollment as `Completed`
  with their `ActivityProgress` rows and `Graded` submissions (new
  `Submission.Code` per copy). The failed/in-progress modules are redone from
  scratch on the next global `AttemptNumber`. Outside the window nothing is
  copied.
- Rebuy does **not** reset assignment `MaxAttempts` or the recovery cap.

**Manager correction.** Attendance stays editable on closed enrollments and
Admin/Manager may re-grade `Graded` submissions. A correction that removes
the closing condition (attendance below 20% for `Attendance` closes; a
corrected pass for `AcademicFail` closes) automatically reopens the purchase:
PE/module enrollments return to `Active`, close fields are cleared, withdrawn
seats reactivate, and progress is recalculated. Attempt counts and recovery
decisions are not reset.

## Assessment recovery vs class re-delivery (legacy)

- **Same class:** mentor-granted extra attempts never transfer the student.
- **Theory:** unlimited assignment retries; never pay or transfer only to redo a test.
- **Class re-delivery** (`ClassRedeliveryRequest`) is the **legacy** transfer
  path for Experiential/Research retakes. It is superseded by the
  fail/drop → rebuy lifecycle above; the new flow does not call into it.
  Schema still stores `Class.Kind` / `Class.RemedialModuleId`,
  `ClassEnrollment.Kind`, and request `IntensivePaceAcceptedAt` /
  `ResolutionType` for the two-tier retake ladder (endpoints in later stories).
- Module retake invoices appear in `GET /api/invoices/me` alongside program tuition.

API: `/api/class-redelivery-requests` (legacy).

## Payments

`Payment` entity and `PaymentStatus` / `PaymentGateway` enums exist in the
domain model. Program tuition and rebuy retakes both create `Invoice` rows
visible via invoice endpoints. Module-level retail price columns have been
dropped; the retake price lives on `Program.RetakeFee` (nullable, falls back
to `Program.Price`).

Program tuition checkout requires the student to select an Open Standard class
(`classId` on checkout / parent request). A **5-minute** soft seat hold and parent
payment token start when checkout is initiated. Rebuy checkout follows the same
class-selection rule, restricted to eligible classes (see above).

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles.
