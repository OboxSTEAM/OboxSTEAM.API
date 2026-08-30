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
   (public; Standard + **Open** + seats remaining > 0, with schedule sessions and seat
   counts). Logged-in students picking a class for checkout use
   `GET /api/programs/{programId}/rebuy-classes` instead (Open-only for first
   purchase / Completed; Open + InProgress after fail/drop). Seat counts include
   non-expired **Pending** holds from class selection.
3. **Select class** via `POST /api/programs/{programId}/select-class` (`classId`) — starts
   the **5-minute** soft seat hold and publishes `seats.changed`.
4. **Leave checkout** via `POST /api/programs/{programId}/release-class-hold` when the
   learner reloads or navigates away — releases the seat and soft-deletes the
   `PendingPayment` program enrollment. Direct checkout cancel/fail and Stripe session
   expiry abandon the same checkout state automatically. Cleanup does **not** drop a
   hold while the enrollment is already `Active` (paid, seat not yet activated) or
   while a `Pending` `Payment` exists for that enrollment.
5. Pay program tuition (`POST /api/payments/checkout` or parent-pay with the same
   `classId`). Requires the hold from step 3. Opening Stripe Checkout **pins** the
   hold for **24 hours** (Stripe session default). On `checkout.session.completed`,
   `ProgramEnrollment` and `ClassEnrollment` become **Active** together — no separate
   post-pay class join step. Stripe may deliver that event more than once: invoice and
   receipt email are recorded only on the first success; seat activation and rebuy
   credit copy run on every delivery until they succeed (expired Pending holds still
   activate after pay; a class that filled up after the hold lapsed returns Conflict).
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
  (server-enforced on first-time select-class). A rebuy may instead join an
  `InProgress` Standard class when stop-module session eligibility holds.
- Module prerequisites: `PrerequisiteModuleId` must be satisfied before access.
- Class late-join: `Class.MinHoursBeforeAssignmentJoin` blocks self-enrollment
  when a future `AssignmentWindow` session on that class starts sooner than
  the buffer (default 48 hours). LiveOnline/Offline sessions do not count.
  `GET .../rebuy-classes` marks those classes `IsEligible = false` with the
  same reason `POST .../select-class` returns.
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
select-class window, then 24 hours after Stripe Checkout is created). Realtime hints: SignalR `syncEvent` scope `seats.changed` on
hub `/hubs/notifications` — clients call `JoinProgramSync(programId)` then
refetch open-classes when notified.

## Purchase close and rebuy (fail / drop)

A `ProgramEnrollment` closes permanently when the student fails or withdraws:

- **Academic fail** — latest non-theory assignment submission graded fail,
  effective attempts exhausted, and the recovery cap (2) reached →
  `ProgramEnrollment.Status = Failed`, `EndReason = AcademicFail`.
  `IsRequiredForModulePass` does not gate this close. Theory modules never
  close this way (unlimited attempts).
- **Attendance fail** — ≥20% missed sessions on a module → `Failed` with
  `EndReason = Attendance`.
- **Withdraw** — student self-withdraws via
  `POST /api/program-enrollments/{id}/withdraw` → `Dropped` with
  `EndReason = Withdraw`.

Closing withdraws class seats immediately, terminals every open
`ModuleEnrollment` (the ended module becomes `Failed` on academic/attendance
close; every other Active/Deferred module — including later modules in
`ModuleOrder` — becomes `Dropped`; `Completed` rows stay `Completed`), and
records `EndedAt` / `EndedModuleId`. A closed enrollment keeps **read-only** curriculum access
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
- **Class eligibility:** after **Failed** or **Dropped**, the rebuy must join
  exactly one `Open` or `InProgress` Standard class that has not started the
  module the student stopped at, nor any later module in `ModuleOrder` (no
  `InProgress`/`Completed` `ClassSession` on those modules). The student cannot
  rejoin a class they already occupied on the source purchase. Class status
  `InProgress` is allowed; the session rule is the gate. For `Failed` sources
  the stop module is `EndedModuleId`; for `Dropped` sources it is the first
  not-`Completed` module. **First purchase** and a **Completed (100%)** source
  only join `Open` Standard classes (same rule as `open-classes`); a Completed
  source still links for retake pricing/credit and still cannot rejoin the old
  class if it appears.
  `GET /api/programs/{programId}/rebuy-classes` (Student) is the picker for both
  cases: `IsRebuy = false` lists Open classes with seats; `IsRebuy = true`
  lists Open and InProgress classes with per-module session progress
  (`NotStarted` / `InProgress` / `Completed`) and `isEligible`. Public catalog
  browse still uses `GET /api/programs/{programId}/open-classes`.
- **Credit copy (inside the window only):** on payment success (including Stripe
  webhook retries after `Payment` is already `Success`), credit is
  copied scoped to what the **new class** has already taught. A session counts
  as taught when it is `Completed` **or** its `EndTime` is at or before now
  (the class already passed that slot even if status is still `Scheduled`).
  A module with no non-cancelled sessions on that class (self-paced or
  unscheduled) is copied whole. A module whose every non-cancelled session is
  already taught is copied whole as `Completed` with its `ActivityProgress`
  rows and `Graded` submissions (new `Submission.Code` per copy). Future
  sessions are not copied — the student relearns those with the new class.
  A module the new class is part-way through copies only the
  `ActivityProgress`/`Graded` submissions whose activity/assignment the new
  class has already taught; the copied enrollment stays `Active` and
  `ProgressPercent` is recalculated with the live formula (done activities +
  passed required assignments). Each copy uses the next global
  `AttemptNumber`. Program `ProgressPercent` is recalculated after copy.
  Outside the window nothing is copied.
- Rebuy starts a **fresh attempt budget** on the new `ModuleEnrollment`
  (quiz, assignment, and recovery counts are per enrollment). Copied graded
  submissions on that enrollment still count toward `MaxAttempts`. Pending
  quiz attempts from the old enrollment are not resumed.
  `Assignment.DueDate` and `Assignment.AvailableUntil` are catalog fields on
  the module assignment; rebuy does not copy or extend them. Quiz start
  enforces both; file/research submit enforces `AvailableUntil`. A recovery
  personal window on the old enrollment does not apply to the new one.

**Manager correction.** Attendance stays editable on closed enrollments and
Admin/Manager may re-grade `Graded` submissions. A correction that removes
the closing condition (attendance below 20% for `Attendance` closes; a
corrected pass for `AcademicFail` closes) reopens the purchase **unless**
the student already has an `Active` or `PendingPayment` enrollment for the
same program — that case returns 409 and leaves the closed purchase closed
(the attendance or grade correction is still saved). On a successful reopen,
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
payment token start at class selection / parent request. Creating a Stripe Checkout
session pins that hold for **24 hours**. Rebuy checkout follows the same
class-selection rule, restricted to eligible classes (see above).

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles.
