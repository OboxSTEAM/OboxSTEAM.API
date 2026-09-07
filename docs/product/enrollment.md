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
   purchase / Completed / fail-drop after the 1-month window; Open + InProgress
   after fail/drop inside the window). Seat counts include
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
- Class late-join: self-enrollment and the rebuy picker block when any
  not-yet-ended `AssignmentWindow` is at or past two-thirds of
  (`EndTime` − `StartTime`) from `StartTime`. Windows that have not opened
  yet, and LiveOnline/Offline sessions, do not count.
  `Class.MinHoursBeforeAssignmentJoin` is the generate first-session buffer,
  not this cutoff. Message:
  `Cannot join after two-thirds of an assignment work window has elapsed.`
  `GET .../rebuy-classes` marks those classes `IsEligible = false` with the
  same reason `POST .../select-class` returns.
- Quiz and assignment access require active module enrollment (enforced in
  `IQuizAttemptService` and assignment services).

## Enrollment curriculum tree

`GET /api/program-enrollments/{enrollmentId}/curriculum` returns per-student
nav status for activities and assignments (`locked`, `available`, `current`,
`completed`, `submitted`). Assignment nodes also expose `latestSubmissionId`
(latest attempt under the student's module enrollment) so clients can hydrate
result UIs without a separate submissions list:

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

After prerequisites, the class AssignmentWindow also gates nav status. Missing,
not-yet-open, or closed windows mark new work `locked` (parent view uses
`overdue` when the window has already ended). In-progress drafts
(`Pending` / `ReturnedForRevision`) stay `available` after close. Turned-in
work shows `submitted`; a passing grade shows `completed`.

Curriculum, parent progression, mentor submission lists, class assignment
rollups, and class quiz-set lock use submissions whose `ModuleEnrollment`
belongs to the program enrollment in view (or the Active class seat's
enrollment). Copied credit rows on a rebuy enrollment count. Source-purchase
rows never substitute. Student save, submit, and file upload of an existing
attempt must target that enrollment's module enrollment; leftover drafts from
a closed purchase are not resumed.

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

- **Academic fail** — a **required** assignment (`IsRequiredForModulePass`)
  is not passed and the student has no remaining way to continue it:
  Experiential/Research while the window is open: latest graded fail, attempts
  exhausted, and the recovery cap (2) reached; any module type after the
  class AssignmentWindow `EndTime` (no in-progress draft; latest row not
  `TurnedIn`) → `ProgramEnrollment.Status = Failed`, `EndReason = AcademicFail`.
  Optional assignments never close the purchase. Theory still has unlimited
  attempts while the window is open.
- **Attendance fail** — ≥50% missed sessions on a module → `Failed` with
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

**Rebuy (chuyen ca).** Continuing requires a new purchase of the same program
and a seat in a **different** Standard class. This is a cohort transfer, not a
module retake. Continuity / in-window rebuy price is always **50% of
`Program.Price`**; after the window the student pays full `Program.Price`.
(`Program.RetakeFee` remains on the schema for historical rows but is **not**
used for checkout.) Credit still follows what the **new class** has already
taught — pick a class that has finished modules you already passed to keep
that credit. `GET .../rebuy-classes` module rows include `creditHint`
(`Copied` / `RedoWithClass` / `Ahead`).

- Checkout detects the latest closed source enrollment and links it via
  `SourceProgramEnrollmentId`.
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
  source purchase. Class status `InProgress` is allowed; the session rule is the
  gate. For `Failed` sources the stop module is `EndedModuleId`; for `Dropped`
  sources it is the first not-`Completed` module. **After the window**, fail/drop
  is a fresh start: **Open** Standard classes only (same rule as `open-classes`);
  stop-module / InProgress join is off, so credit copy (already window-gated)
  has nothing mid-cohort to attach to. **First purchase** and a **Completed
  (100%)** source only join `Open` Standard classes; a Completed source still
  links for retake pricing/credit and still cannot rejoin the old class if it
  appears.
  `GET /api/programs/{programId}/rebuy-classes` (Student) is the picker for both
  cases: `IsRebuy = false` lists Open classes with seats; `IsRebuy = true`
  lists Open and InProgress classes with per-module session progress
  (`NotStarted` / `InProgress` / `Completed`) and `isEligible`. Public catalog
  browse still uses `GET /api/programs/{programId}/open-classes`.
- **Credit copy (inside the window only):** on payment success (including Stripe
  webhook retries after `Payment` is already `Success`), credit is
  copied scoped to what the **new class** has already taught. A **teaching**
  session (LiveOnline/Offline) counts as taught when it is `Completed` **or**
  its `EndTime` is at or before now (the class already passed that slot even if
  status is still `Scheduled`). `AssignmentWindow` is a work period after
  teaching and does not count as taught or as “started the stop module”.
  A module with no teaching sessions on that class (self-paced or unscheduled)
  is copied whole. A module whose every teaching session is already taught is
  copied whole as `Completed` with its `ActivityProgress` rows and `Graded`
  submissions (new `Submission.Code` per copy). Future lives are not copied —
  the student relearns those with the new class.
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
progress is written, and leaves the closed purchase closed. On a successful reopen,
PE/module enrollments return to `Active`, close fields are cleared, withdrawn
seats reactivate, and progress is recalculated. Attempt counts and recovery
decisions are not reset.

## Assessment recovery vs class continuity

- **Same class:** mentor-granted extra attempts never transfer the student.
- **Theory:** unlimited assignment retries; never pay or transfer only to redo a test.
- **Active continuity** (Experiential/Research, still on an Active purchase): after
  recovery is exhausted (or for a voluntary retake of a Completed module), the
  student opens a **shared class catalog** and picks an Open or eligible
  InProgress Standard class. Dismissing the picker does **not** withdraw the
  program enrollment — the student stays Active and can reopen the list later.
  There is **no** manager waitlist, Remedial intensive class, or intensive
  consent step.
- **Catalog contract (shared with rebuy):**
  - `GET /api/module-enrollments/{id}/continuity-classes` (Active PE only)
  - `GET /api/class-redelivery-requests/{id}/candidates` after
    `POST /api/class-redelivery-requests` (status is always
    `AwaitingClassSelection`, even when the list is empty)
  - Same `RebuyClassCatalogDto` shape as `GET /api/programs/{id}/rebuy-classes`
    (`context`, `checkoutAmount`, `isEligible`, `creditHint`, optional
    `moduleSessions` on Active catalogs)
- **Price:** Active continuity checkout always charges **50% of `Program.Price`**
  (no expiry while the purchase stays Active). Same rate as in-window rebuy.
  Routes: `POST /api/payments/checkout/retake` and parent retake request.
- **Cancel request:** `POST /api/class-redelivery-requests/{id}/cancel` cancels
  only the continuity request (status `Withdrawn`); PE stays Active. Prefer
  `/cancel` over the obsolete `/withdraw` alias. Quitting the whole program is
  `POST /api/program-enrollments/{id}/withdraw` → PE `Dropped` /
  `EndReason = Withdraw`.
- **After fail/drop:** use the rebuy lifecycle and `rebuy-classes` above — not
  Active continuity. Do not wire program withdraw into the continuity picker UX.
- Manager waitlist / open-remedial / intensive / assign-target / reject endpoints
  return **410 Gone**. Schema may still store `Class.Kind` / `RemedialModuleId`
  for historical rows.
- **Legacy enum values (do not send / ignore on FE):** request status
  `PendingManager`, `PendingAutoMatch`, `AwaitingIntensiveConsent`, `Approved`;
  resolution `RemedialClass`. Happy path only uses
  `AwaitingClassSelection` → `MatchedPendingPayment` → `Completed` | `Withdrawn`.

API: `/api/class-redelivery-requests`, `/api/module-enrollments/{id}/continuity-classes`.

## Payments

`Payment` entity and `PaymentStatus` / `PaymentGateway` enums exist in the
domain model. Program tuition and rebuy / Active continuity retakes both create
`Invoice` rows visible via invoice endpoints. Module-level retail price columns
have been dropped. Continuity / in-window rebuy checkout is **50% of
`Program.Price`**; after the 1-month window (or first purchase) it is full
`Price`. `Program.RetakeFee` is unused for these amounts.

Program tuition checkout requires the student to select an Open Standard class
(`classId` on checkout / parent request). A **5-minute** soft seat hold and parent
payment token start at class selection / parent request. Creating a Stripe Checkout
session pins that hold for **24 hours**. Rebuy checkout follows the same
class-selection rule, restricted to eligible classes (see above).

## Parent Visibility

Parents access linked student enrollment data through endpoints that accept
Parent role alongside Student and admin roles.
