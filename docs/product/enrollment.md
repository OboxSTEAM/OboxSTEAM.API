# Enrollment

## Enrollment Types

| Entity | Scope | Purpose |
| --- | --- | --- |
| ProgramEnrollment | Program | Student enrolled in a full track |
| ModuleEnrollment | Module | Progress attempt within a program enrollment |
| CourseEnrollment | Course | Mentor-led course instance access |
| ClassEnrollment | Class | Cohort membership with shared schedule |

Status fields use `EnrollmentStatus` (`PendingPayment`, `Active`, `Deferred`,
`Completed`, `Failed`, `Dropped`) or `ClassEnrollmentStatus` (`Active`,
`Transferred`, `Withdrawn`, `Completed`, `Pending` = soft seat hold).
`ClassEnrollment.Kind` is `Primary` or `Retake`.

## Student Flow

1. Browse programs (public catalog): `GET /api/programs?status=Active` returns only
   programs that already have at least one Standard **Open** class whose
   `MaxCapacity` exceeds Active seats plus non-expired Pending holds. Publishing
   a program to Active is not enough by itself — class schedule, mentor assignment,
   and opening enrollment must complete first. Omitting `status` (or using other
   statuses) does not apply this enrollability filter (manager/admin browse).
2. Preview recruiting cohorts via `GET /api/programs/{programId}/open-classes`
   (public; Standard + **Open** + seats remaining > 0, with schedule sessions and seat
   counts; optional `preferredClassId`; expired holds are released first).
   Logged-in students picking a class for checkout use
   `GET /api/programs/{programId}/rebuy-classes` (Student) instead (Open-only for first
   purchase / Completed / fail-drop after the 1-month window; Open + InProgress
   after fail/drop inside the window). It returns 409
   `Student is already enrolled in this program.` while the student has an
   `Active` or `Deferred` enrollment for the program, and 400
   `PROGRAM_NOT_AVAILABLE` when the program is not `Active`. Classes with no seats
   left are omitted. Seat counts include non-expired **Pending** holds from class
   selection.
3. **Select class** via `POST /api/programs/{programId}/select-class` (`classId`,
   Student) — starts the **5-minute** soft seat hold (`ClassEnrollment.Status =
   Pending`, `HoldExpiresAt`) and publishes `seats.changed`. Checks, in order:
   program `Price` > 0; program `Active` (otherwise the pending checkout is
   abandoned and 400 `PROGRAM_NOT_AVAILABLE` is returned); the class belongs to the
   program, is Standard, and is `Open` (or `Open`/`InProgress` for an in-window
   fail/drop rebuy, subject to the rebuy class rules below); 409 when this
   enrollment already has an Active seat; at most **2** Primary class seats per
   student (Active or live Pending); class capacity (409
   `Class has reached maximum capacity.`); late-join cutoff; schedule conflicts.
   Re-selecting the same class refreshes the hold; selecting another class
   withdraws the previous hold.
4. **Leave checkout** via `POST /api/programs/{programId}/release-class-hold` (Student,
   idempotent) when the learner reloads or navigates away — withdraws the hold,
   cancels `Pending` payments, expires open parent payment requests, and
   soft-deletes the `PendingPayment` program enrollment. Direct checkout
   cancel/fail and Stripe session expiry abandon the same checkout state
   automatically. Hold cleanup does **not** drop an expired hold while the
   enrollment is already `Active` (paid, seat not yet activated) or while a
   `Pending` `Payment` exists for that enrollment.
5. Pay program tuition (`POST /api/payments/checkout` or parent-pay with the same
   `classId`). Requires a valid hold from step 3 (otherwise 400
   `Select this class before checkout or your seat hold has expired.`). Opening
   Stripe Checkout **pins** the hold for **24 hours**. On
   `checkout.session.completed`, `ProgramEnrollment` and `ClassEnrollment` become
   **Active** together — no separate post-pay class join step. Stripe may deliver
   that event more than once: payment/enrollment status, invoice, and receipt email
   are recorded only on the first success; seat activation, source supersede, rebuy
   credit copy, and continuity completion run on every delivery until they succeed
   (an expired Pending hold — or the latest Withdrawn seat — still activates after
   pay; a class that filled up after the hold lapsed returns Conflict).
6. Module enrollments are created as part of program progression (not sold as
   separate retail products). `GET .../curriculum` on an Active program enrollment
   provisions module enrollments for modules that are unlocked.

Limits: at most **2** in-progress program enrollments per student
(`Active` + `PendingPayment`, 409 when exceeded) and at most **2** Primary class
seats.

Shared read endpoints (Student, Parent, Admin, Manager) on
`/api/program-enrollments`: `GET /{id}`, `GET /me` (`programId`, `sortBy`,
`isDescending`, `page`, `pageSize`, `includeSuperseded`), `GET /{enrollmentId}/class`,
`GET /{enrollmentId}/module-enrollments`, `GET /{enrollmentId}/curriculum`,
`GET /student/{studentId}`; `GET /api/module-enrollments/{id}`;
`GET /api/class-enrollments/{id}` and
`GET /api/class-enrollments/program-enrollment/{programEnrollmentId}`.
Student-only: `GET /api/program-enrollments/{enrollmentId}/curriculum-mind-map`,
`PATCH .../activities/{activityId}/checkpoint`,
`POST .../activities/{activityId}/complete` (SelfPaced activities only), and
`POST /api/program-enrollments/{id}/withdraw`.

`GET /api/program-enrollments/{programEnrollmentId}/module-enrollments` returns
the latest module enrollment per module (ordered by `ModuleOrder`). Use the
returned `id` as `moduleEnrollmentId` for flows such as research milestone
progress.

Class seat changes on `/api/class-enrollments`:

- `POST /` (Student) — self-enroll into an `Open` class: program enrollment must be
  Active and have no Active seat; load cap, capacity, late-join, and schedule
  conflict checks apply.
- `PUT /{id}` — student moves the existing seat to another `Open` class (capacity
  and late-join checks; the same row is re-pointed).
- `PUT /manager-transfer/{id}` (Manager; `id` is the student id) — target must be
  `Open`; the old seat becomes `Transferred` and a new Active seat is created. No
  late-join check.

## Gating Rules

- Program checkout: the selected class itself must be Open, Standard, and have a
  free seat (see Student Flow step 3). A rebuy may instead join an
  `InProgress` Standard class when stop-module session eligibility holds.
- Module prerequisites: a module with `PrerequisiteModuleId` unlocks when the
  prerequisite module's latest module enrollment has `ProgressPercent` ≥ 100.
- Class late-join: self-enrollment, select-class, student transfer, and the rebuy
  picker block when any non-cancelled, not-yet-ended `AssignmentWindow` is at or
  past two-thirds of (`EndTime` − `StartTime`) from `StartTime` (a window with zero
  or negative duration also blocks). Windows that have not opened yet, and
  LiveOnline/Offline sessions, do not count.
  `Class.MinHoursBeforeAssignmentJoin` is the generate first-session buffer,
  not this cutoff. Message:
  `Cannot join after two-thirds of an assignment work window has elapsed.`
  `GET .../rebuy-classes` marks those classes `IsEligible = false` with the
  same reason `POST .../select-class` returns.
- Quiz and assignment access require an Active module enrollment whose program
  enrollment is Active (enforced in `IQuizAttemptService` and assignment services).

## Enrollment curriculum tree

`GET /api/program-enrollments/{enrollmentId}/curriculum` returns per-student
nav status for activities and assignments (`locked`, `available`, `current`,
`completed`, `submitted`; the mind-map payload also uses `in_progress`).
Reading is blocked only while the program enrollment is `PendingPayment`
(403 `Program enrollment must be active to access curriculum.`); curriculum
mutations require an Active program enrollment. Assignment nodes also expose
`latestSubmissionId` (latest attempt under the student's module enrollment;
filtered by `ResearchMilestoneId` for research deliverables) so clients can
hydrate result UIs without a separate submissions list:

- Quiz → `GET /api/submissions/{latestSubmissionId}/quiz/result`
- Retrospective → `GET /api/submissions/{latestSubmissionId}/retrospective`
- Research FileUpload → milestone progress `submissionId` first, else
  `latestSubmissionId` → `GET /api/research-submissions/{id}`

The mind-map curriculum payload mirrors `latestSubmissionId` on each
assignment's `learning` object.

Assignment locking mirrors activity gating:

- **Module locked** — prerequisite module not complete → `locked`.
- **Course assignment** — locked until every **SelfPaced** activity in that
  course is done. LiveOnline/Offline sessions do not gate unlock (absence is
  attendance, not a homework lock).
- **Module-scoped assignment** — locked until every **SelfPaced** activity in
  the module is done. LiveOnline/Offline do not gate.
- **Research milestone deliverable** — locked until the previous milestone is
  passed (if any) and required **SelfPaced** milestone activities are
  completed. Required live/offline links do not block submit.

Sequential activity unlock in a course or milestone skips incomplete
LiveOnline/Offline sessions, so a missed live does not block later SelfPaced
work or the assignment. Absence still counts toward the 50% attendance fail.

Assignment status order: any passing graded submission → `completed`;
submissions exist but none is `Pending` / `ReturnedForRevision` → `submitted`
(this includes `TurnedIn` work and a graded fail); otherwise prerequisites,
then the class AssignmentWindow. Missing, not-yet-open, or closed windows mark
new work `locked` (parent view uses `overdue` when the window has already
ended). In-progress drafts (`Pending` / `ReturnedForRevision`) stay `available`
after close.

Curriculum, parent progression, mentor submission lists, class assignment
rollups, and class quiz-set lock use submissions whose `ModuleEnrollment`
belongs to the program enrollment in view (or the Active class seat's
enrollment). Copied credit rows on a rebuy enrollment count. Source-purchase
rows never substitute. Student save, submit, and file upload of an existing
attempt must target that enrollment's module enrollment; leftover drafts from
a closed purchase are not resumed.

## Progress

`ActivityProgress` tracks completion per student per activity. Module and
program `ProgressPercent` = (done activities + passed required assignments) /
(all activities + required assignments); reaching 100 sets the row
`Completed`.
Capacity is a class-level seat count (`Class.MaxCapacity`); there is no
per-activity or per-session booking. Seat counts for open-class preview use
**Active** `ClassEnrollment` rows plus non-expired **Pending** holds (5-minute
select-class window, then 24 hours after Stripe Checkout is created). Realtime hints: SignalR `syncEvent` scope `seats.changed` on
hub `/hubs/notifications` — clients call `JoinProgramSync(programId)` then
refetch open-classes when notified.

## Purchase close and rebuy (fail / drop)

A `ProgramEnrollment` closes permanently when the student fails or withdraws:

- **Academic fail** — a **required** assignment (`IsRequiredForModulePass`)
  is not passed and the student has no remaining way to continue it.
  Two triggers set `ProgramEnrollment.Status = Failed`, `EndReason = AcademicFail`:
  - *After a failing grade* (Experiential/Research only; no window check): the
    latest graded attempt failed, nothing is `TurnedIn`, the latest
    `AttemptNumber` reached the effective max, and decided recovery requests
    (`Approved` + `Rejected`) reached the cap (2). Evaluated on grading, quiz
    submit/auto-grade, and recovery rejection.
  - *After the class AssignmentWindow `EndTime`* (any module type, checked by
    the 5-minute window close job): no passing graded submission, nothing
    `TurnedIn`, and no draft (`Pending` / `ReturnedForRevision`) whose
    `ExpiresAt` is set and still in the future.
  Optional assignments never close the purchase. Theory still has unlimited
  attempts while the window is open.
- **Attendance fail** — `Absent` records cover ≥50% of the module's sessions
  (counted as distinct session activities; only `Absent` counts as missed) →
  `Failed` with `EndReason = Attendance`.
- **Withdraw** — student self-withdraws via
  `POST /api/program-enrollments/{id}/withdraw` (Active only; otherwise 400
  `Only an Active enrollment can be withdrawn. Use checkout abandon for PendingPayment enrollments.`)
  → `Dropped` with `EndReason = Withdraw`.

Closing withdraws Active and Pending class seats immediately, terminals every
open `ModuleEnrollment` (the ended module becomes `Failed` on academic/attendance
close; every other Active/Deferred module — including later modules in
`ModuleOrder` — becomes `Dropped`; withdraw drops all of them; `Completed` rows
stay `Completed`), records `EndedAt` / `EndedModuleId`, and sends a
`ProgramWithdrawn` or `ModuleFailed` notification. A closed enrollment keeps
**read-only** curriculum access (`GET .../curriculum` still works); student
mutations (activity completion, quiz/assignment/research submissions) require an
**Active** enrollment and return 403 on closed ones; recovery requests return
400 because the module enrollment is no longer Active.

**Rebuy (chuyen ca).** Continuing requires a new purchase of the same program
and a seat in a **different** Standard class. This is a cohort transfer, not a
module retake. Continuity / in-window rebuy price is always **50% of
`Program.Price`** (rounded away from zero); after the window the student pays
full `Program.Price`. (`Program.RetakeFee` remains on the schema for historical
rows but is **not** used for checkout.) Credit still follows what the **new
class** has already taught — pick a class that has finished modules you already
passed to keep that credit. `GET .../rebuy-classes` module rows include
`creditHint` (`Ahead` = not completed on the source; `Copied` = completed and the
class has no teaching sessions for the module or has taught them all;
`RedoWithClass` = completed but the class has not finished teaching it).

- Checkout detects the latest closed source enrollment (`Failed`, `Dropped`, or
  `Completed`) and links it via `SourceProgramEnrollmentId`.
- When the rebuy payment succeeds and the new enrollment becomes **Active**,
  the source row is marked with `SupersededByEnrollmentId` pointing at the new
  purchase (Failed / Dropped / Completed sources alike). Until pay succeeds, a
  `PendingPayment` rebuy is already treated as the **current** card on list
  APIs (the prior terminal is history-only).
- **My courses list:** `GET /api/program-enrollments/me` and
  `GET /api/program-enrollments/student/{studentId}` default to **one row per
  program** — the current enrollment (`PendingPayment` | `Active` | `Deferred`,
  else the latest non-superseded terminal). Pass `includeSuperseded=true` for
  full purchase history. Response fields for rebuy UX: `isRebuy`,
  `attemptNumber`, `priorStatus`, `priorEndReason`, `isSuperseded`,
  `supersededByEnrollmentId` (plus existing `sourceProgramEnrollmentId` /
  close fields).
- **Price:** within **1 calendar month** of the source `EndedAt` (boundary day
  included) the student pays **50% of `Program.Price`**; after the window they
  pay full `Program.Price`. A `Completed` source anchors the window at
  `CompletedAt` and gets continuity **pricing only**.
- **Class eligibility:** after **Failed** or **Dropped** *inside the 1-month
  rebuy window*, the rebuy must join exactly one `Open` or `InProgress`
  Standard class that has not started the module the student stopped at, nor any
  later module in `ModuleOrder` (no `InProgress`/`Completed` LiveOnline/Offline
  session on those modules; `AssignmentWindow` work periods do not count as
  teaching). The student cannot rejoin a class they already occupied on the
  source purchase (listed with `IsEligible = false`). Class status `InProgress`
  is allowed; the session rule is the gate. For `Failed` sources the stop module
  is `EndedModuleId`; for `Dropped` sources it is the first not-`Completed`
  module. **After the window**, fail/drop is a fresh start: **Open** Standard
  classes only (same rule as `open-classes`); stop-module / InProgress join is
  off, so credit copy (already window-gated) has nothing mid-cohort to attach
  to. **First purchase** and a **Completed (100%)** source only join `Open`
  Standard classes; a Completed source still links for retake pricing and still
  cannot rejoin the old class if it appears.
  `GET /api/programs/{programId}/rebuy-classes` (Student) is the picker for both
  cases: `IsRebuy = false` lists Open classes with seats; `IsRebuy = true`
  lists Open and InProgress classes with per-module session progress
  (`NotStarted` / `InProgress` / `Completed`) and `isEligible`. Public catalog
  browse still uses `GET /api/programs/{programId}/open-classes`.
- **Credit copy (inside the window only; a `Completed` source copies nothing):**
  on payment success (including Stripe webhook retries after `Payment` is already
  `Success`), each `Completed` source module enrollment is copied into a new
  `Active` module enrollment, scoped to what the **new class** has already
  taught. A **teaching** session (LiveOnline/Offline, not cancelled) counts as
  taught when it is `Completed` **or** its `EndTime` is at or before now (the
  class already passed that slot even if status is still `Scheduled`).
  `AssignmentWindow` is a work period after teaching and does not count as
  taught or as “started the stop module”. A module with no teaching sessions on
  that class (self-paced or unscheduled) is copied whole. A module whose every
  teaching session is already taught is copied whole with its `ActivityProgress`
  rows and `Graded` submissions (new `Submission.Code` per copy, source
  `AttemptNumber` kept); `FinalGrade` is copied when the recalculated copy ends
  `Completed`. Future lives are not copied — the student relearns those with the
  new class. A module the new class is part-way through copies only the
  `ActivityProgress`/`Graded` submissions whose activity/assignment the new
  class has already taught; the copied enrollment stays `Active`. Every copy's
  `ProgressPercent` (and status) is recalculated with the live formula (done
  activities + passed required assignments). Each copy uses the next global
  `AttemptNumber`. Program `ProgressPercent` is recalculated after copy. Copy is
  idempotent per module. Outside the window nothing is copied.
- Rebuy starts a **fresh attempt budget** on the new `ModuleEnrollment`
  (quiz, assignment, and recovery counts are per enrollment). Copied graded
  submissions on that enrollment still count toward `MaxAttempts`. Pending
  quiz attempts from the old enrollment are not resumed. Reads and mutates
  for the new class never fall back to source-purchase submissions.
  Assignment open/close is the new class’s `AssignmentWindow` session
  (`StartTime` / `EndTime`), not catalog dates. Quiz, file, retrospective, and
  research start enforce that window. Recovery on the old enrollment does not
  apply; extra attempts on the new enrollment must be used inside the new
  window. After `EndTime` there is no personal-deadline recovery.

**Manager correction.** Attendance stays editable on closed enrollments and
Admin/Manager may re-grade `Graded` submissions. A correction that removes
the closing condition (attendance below 50% for `Attendance` closes; a
corrected pass for `AcademicFail` closes) reopens the purchase **unless**
the student already has an `Active` or `PendingPayment` enrollment for the
same program — that case returns 409 **before** any attendance, grade, or
progress is written, and leaves the closed purchase closed. On a successful
reopen, the program enrollment returns to `Active`, the failed module enrollment
and sibling `Dropped` module enrollments return to `Active`, close fields are
cleared, withdrawn seats reactivate, and progress is recalculated. Attempt counts
and recovery decisions are not reset.

## Assessment recovery vs class continuity

- **Same class:** mentor-granted extra attempts never transfer the student.
- **Theory:** unlimited assignment retries; never pay or transfer only to redo a
  test. Continuity requests and `continuity-classes` return 400 for Theory modules.
- **Active continuity** (Experiential/Research, still on an Active purchase): the
  student opens a **shared class catalog** and picks an Open or eligible
  InProgress Standard class. Dismissing the picker does **not** withdraw the
  program enrollment — the student stays Active and can reopen the list later.
  There is **no** manager waitlist, Remedial intensive class, or intensive
  consent step.
- **Create request:** `POST /api/class-redelivery-requests` (Student, Mentor,
  Manager, Admin; students only for their own module enrollment) requires a
  module enrollment linked to a program enrollment and an Active class seat.
  Allowed when the module enrollment is `Failed` or `Completed` (voluntary
  retake), or `Active` with no `Pending` recovery request and either a graded
  fail on that module enrollment or an assignment with 2 decided recovery
  requests. Otherwise 400 (`A recovery request is still pending...` /
  `You still have assignment attempts or recovery requests available for this module...`).
  An open request for the same student and module returns 409. Status is always
  `AwaitingClassSelection`, even when the list is empty.
- **Catalog contract (shared with rebuy):**
  - `GET /api/module-enrollments/{id}/continuity-classes` (Student; Active
    program enrollment only; includes InProgress classes; stop module = the
    module in focus; excludes classes this program enrollment already occupied)
  - `GET /api/class-redelivery-requests/{id}/candidates` (student, requester, or
    Manager/Admin; 400 unless status is `AwaitingClassSelection`)
  - Same `RebuyClassCatalogDto` shape as `GET /api/programs/{id}/rebuy-classes`
    (`context`, `checkoutAmount`, `isEligible`, `creditHint`, optional
    `moduleSessions` on Active catalogs)
- **Select class:** `POST /api/class-redelivery-requests/{id}/select-class`
  (Student, own request) — the class must be eligible in the catalog and differ
  from the current class; capacity, schedule conflict, and Primary load checks
  apply. Creates a `PendingPayment` retake `ModuleEnrollment`
  (`AttemptNumber` + 1) and moves the request to `MatchedPendingPayment`
  (`ResolutionType = StudentSelectedCohort`).
- **Price:** Active continuity checkout always charges **50% of `Program.Price`**
  (no expiry while the purchase stays Active). Same rate as in-window rebuy.
  Routes: `POST /api/payments/checkout/retake` and parent retake request.
- **After continuity payment:** the old seat becomes `Transferred`, a new Primary
  Active seat is created in the target class, the original module enrollment
  becomes `Failed` if it was Active (`Completed` stays `Completed`), the retake
  module enrollment becomes `Active`, and the request becomes `Completed`.
- **Cancel request:** `POST /api/class-redelivery-requests/{id}/cancel` (only the
  request's student or requester) cancels only the continuity request (status
  `Withdrawn`) and discards the unpaid retake module enrollment; PE stays Active.
  Prefer `/cancel` over the obsolete `/withdraw` alias. Quitting the whole
  program is `POST /api/program-enrollments/{id}/withdraw` → PE `Dropped` /
  `EndReason = Withdraw`.
- `GET /api/class-redelivery-requests/me` lists requests where the user is the
  student or requester.
- **After fail/drop:** use the rebuy lifecycle and `rebuy-classes` above — not
  Active continuity. Do not wire program withdraw into the continuity picker UX.
- Removed — return **410 Gone**: `GET /api/manager/redelivery/waitlist`,
  `POST /api/manager/redelivery/open-remedial-class`, and on
  `/api/class-redelivery-requests`: `GET pending-manager`,
  `POST {id}/accept-intensive`, `POST {id}/decline-intensive`,
  `POST {id}/assign-target`, `POST {id}/reject`. Schema may still store
  `Class.Kind` / `RemedialModuleId` for historical rows.
- **Legacy enum values (do not send / ignore on FE):** request status
  `PendingManager`, `PendingAutoMatch`, `AwaitingIntensiveConsent`, `Approved`;
  resolution `RemedialClass`. Happy path only uses
  `AwaitingClassSelection` → `MatchedPendingPayment` → `Completed` | `Withdrawn`.

API: `/api/class-redelivery-requests`, `/api/module-enrollments/{id}/continuity-classes`.

## Payments

`Payment` entity and `PaymentStatus` (`Pending`, `Success`, `Failed`,
`Cancelled`, `Refunded`) / `PaymentGateway` enums exist in the domain model.
Only Stripe is accepted at checkout; other gateways return 400. Payments use code
`PAY-...` and currency VND. Module-level retail price columns have been dropped.
Continuity / in-window rebuy checkout is **50% of `Program.Price`**; after the
1-month window (or first purchase) it is full `Price`. `Program.RetakeFee` is
unused for these amounts.

Program tuition checkout requires the student to select a class first
(`classId` on checkout / parent request; see Student Flow step 3). A **5-minute**
soft seat hold starts at class selection. Creating a Stripe Checkout session pins
that hold for **24 hours**. Rebuy checkout follows the same class-selection rule,
restricted to eligible classes (see above). Continuity retake checkout needs no
hold.

Endpoints on `/api/payments`:

- `POST checkout` (Student) — program tuition; hold required and pinned.
- `POST checkout/retake` (Student) — continuity retake; the retake module
  enrollment must be `PendingPayment`.
- `POST request-parent` (Student) — needs a verified parent link and a valid
  hold; creates a `PaymentRequest` token valid **5 minutes** and emails/notifies
  the parent. `POST request-parent/retake` issues a token valid **24 hours**.
- `POST parent-checkout` (anonymous, token) — the request must be `Pending` and
  unexpired; for program tuition the hold must still be valid and is pinned. The
  request becomes `Accepted` and the response carries a parent JWT valid
  30 minutes.
- `POST stripe-webhook` (anonymous; `Stripe-Signature` required) — handles
  `checkout.session.completed` and `checkout.session.expired` (treated as failed).
- `GET {id}` — payer, student owner, or Admin/Manager.
- `PATCH {id}/cancel` (anonymous, idempotent, 204).

On cancel or failure, an `Accepted` parent request that has not expired returns to
`Pending`; otherwise the direct checkout is abandoned. On first success the
payment becomes `Success`, the program enrollment `Active` (`EnrolledAt` set),
the module enrollment `Active`, and the parent request `Paid`; an `Invoice`
(`INV-yyyyMMdd-XXXXXX`) is issued to the payer with an invoice email, and the
student gets an enrollment confirmation email when a parent paid.

Invoices: `GET /api/invoices/my`, `GET /api/invoices/{id}`,
`GET /api/invoices/by-payment/{paymentId}` — visible to the user the invoice was
issued to and to Admin/Manager.

Background cleanup:

- `ClassSeatHoldCleanupService` (every minute) releases expired holds under the
  retention rule in Student Flow step 4 and publishes `seats.changed`.
- `PendingEnrollmentCleanupService` (hourly) soft-deletes `PendingPayment`
  program and module enrollments created more than 1 day ago and sends a
  `PendingPaymentExpired` notification for program enrollments. It does not
  release holds or cancel payments.

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles. The parent link must be verified
(otherwise 403 `You can only view enrollments of students linked to your account.`).
Mentors are not allowed on these enrollment reads.
