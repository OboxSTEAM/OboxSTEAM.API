# Assessment

## Assignment Types

| AssignmentType | Behavior |
| --- | --- |
| Quiz | Auto-graded question flow via quiz endpoints |
| FileUpload | Student submits `ContentText` and/or `FileUrl` (file uploaded first); research milestone deliverables use the research submission endpoints |
| Retrospective | Plain-text reflective submission via retrospective endpoints |

Hands-on session evidence is captured through activities and media upload (see
`curriculum.md`); it is not an assignment type.

Assignments belong to a `Module` and optionally a `Course`. Catalog fields
include `MaxPoints`, `PassScore`, `IsRequiredForModulePass`,
`TimeLimitMinutes`, and `MaxAttempts`. Catalog assignment create and update
require `MaxPoints` > 0, 0 ≤ `PassScore` ≤ `MaxPoints`, `MaxAttempts` ≥ 1, and
`TimeLimitMinutes` greater than 0 for every type (application validation; the
column stays nullable). Research milestone create
(`POST /api/modules/{moduleId}/research-milestones`) requires an integer of at
least 1 only when `assignmentType` is `Quiz`. FileUpload and Retrospective
milestones accept a missing or null `timeLimitMinutes`; a present value must
still be at least 1.

`Submission.ExpiresAt` is set from `TimeLimitMinutes` when an attempt starts:
quiz and retrospective start always set it (start returns 400 when the
assignment has no time limit); a research draft sets it only when the milestone
has a time limit. Plain FileUpload submit has no draft row and no timer. Returning
work for revision resets `ExpiresAt` the same way. Calendar open/close is **not**
on the assignment: each class has one `ClassSession` with
`SessionKind = AssignmentWindow` (`StartTime` opens new attempts, `EndTime`
hard-closes new attempts). An attempt already in progress (`Pending` or
`ReturnedForRevision`) may continue after `EndTime` until the student submits.
AcademicFail holds only while `ExpiresAt` is set and still in the future;
a draft with no timer or an elapsed timer does not block close. Rebuy uses the
**new** class window.
`IsRequiredForModulePass` is a progress unit **and** the AcademicFail gate:
optional assignments never close the program purchase.

Mentors of the class may update AssignmentWindow `StartTime` / `EndTime` via
`PUT /api/classes/{classId}/sessions/{id}`. Manager/Admin may also set them
(including via session generate). Missing window: new attempts are blocked. The
student’s class is the Active (then Deferred) program enrollment’s Active class
seat (Primary first) — another class on the same program is used only when that
enrollment has no Active seat.

### Attempt limits by module type

| ModuleType | `MaxAttempts` | Recovery |
| --- | --- | --- |
| Theory | Not enforced — unlimited free retries on the same class while the class window is open | No extra-attempt grant (recovery create returns 400). Required work not passed after `EndTime` (no in-progress draft, nothing `TurnedIn`) → AcademicFail so the student can chuyen ca |
| Experiential / Research | Enforced. Effective max = `MaxAttempts` + `ExtraAttemptsGranted` of `Approved` recovery requests on the same module enrollment | After exhaustion, student submits `AssessmentRecoveryRequest`; mentor grants extra attempts **only** (same class, same open window). Cap: 2 decided (`Approved` + `Rejected`) requests per assignment per module enrollment; `Withdrawn` does not count. Window already ended → no recovery; required work AcademicFails. Latest `TurnedIn` never closes. Passing a research milestone does not change the next milestone’s window; mentors extend it with the session PUT |

Exceeding the effective max returns 409 `ASSIGNMENT_MAX_ATTEMPTS`
(`Maximum number of attempts ({n}) has been reached for this assignment.`).

API: `/api/assignments`:

- `GET /` and `GET /{id}` — no role attribute; a student reading by id needs a
  module enrollment for the assignment's module.
- `GET /{id}/submissions?classId=` — Mentor, Manager, Admin.
- `POST` — Admin, Manager.
- `PUT /{id}` — Admin, Manager, Mentor (mentors may change only `Title` and
  `Description`).
- `DELETE /{id}` — Admin, Manager; 409 when submissions exist.

Student submission flows use the quiz, retrospective, assignment-submission,
and research-submission endpoints below.

### Assessment recovery

Endpoints on `/api/assessment-recovery-requests`:

| Step | Endpoint | Roles |
| --- | --- | --- |
| Request extra attempts | `POST /` | Student |
| List mine | `GET /me` | Student |
| List pending | `GET /pending` | Mentor (own classes only), Manager, Admin (all) |
| Withdraw pending | `POST /{id}/withdraw` | Student (own) |
| Approve | `POST /{id}/approve` | Mentor of the request's class, Manager, Admin |
| Reject | `POST /{id}/reject` | Mentor of the request's class, Manager, Admin |

Create rules: the module enrollment must belong to the student and be `Active`
(400 otherwise), and its program enrollment Active (403); the assignment must
belong to that module; Theory → 400; the class AssignmentWindow must exist and
not have ended (409 `ASSIGNMENT_WINDOW_MISSING` / `ASSIGNMENT_WINDOW_CLOSED`);
one `Pending` request at a time (409); decided cap 2 (400
`Recovery request limit (2) reached...`); completed attempts (`Graded` +
`TurnedIn` rows) must have reached the effective max (400
`Attempts remain on this assignment...`). The request stores the student's
Active class.

Approve requires `ExtraAttemptsGranted` ≥ 1 and a window that has not ended
(409 `ASSIGNMENT_WINDOW_CLOSED`). Approving a failed `Graded` research submission
reopens it as `ReturnedForRevision`. Requests with no class can be decided only
by Manager/Admin. Reject re-runs the AcademicFail check.

## Question Banks

Reusable question pools attached to a `Course`. Supports CSV import via
`ICsvQuestionParserService`.

- `BankQuestion` (`QuestionType` `SingleChoice` / `MultipleChoice`, `Points`,
  integer `DifficultyLevel` 1–5, default 3) and `BankQuestionOption`
  (`IsCorrect`) entities.
- Draw tiers: 1–2 easy, 3 medium, 4–5 hard.

API: `/api/question-banks` — `GET /` (paged; search, sort, `courseId`,
`programId`, `moduleId` filters) and `GET /{questionBankId}` have no role
attribute; `POST /`, `DELETE /{questionBankId}`,
`DELETE /{questionBankId}/questions/{questionId}`, and
`POST /{questionBankId}/import` (CSV, 5 MB limit; invalid rows are skipped and
reported) require Admin or Manager.

## Quiz Modes

A quiz must link a question bank (`Assignment.QuestionBankId`). Start returns
400 `Quiz assignment requires a linked question bank (Mode A).` when it is
null. Assignment create/update accepts a bank only on Quiz assignments, and only
when the bank's course belongs to the assignment's module (and matches its
`CourseId` when set); difficulty percents must sum to 100 and
0 < `QuestionCount` ≤ bank size.

Every start snapshots the served questions into `QuizQuestion` rows tied to that
`SubmissionId` (with `BankQuestionId` and `AttemptNumber`); grading uses the
snapshot.

### Bank-drawn quiz

When the student's class has no class quiz set:

- Questions are randomly drawn at start from the linked bank.
- `QuestionCount` (defaults to the whole bank) and `EasyPercent` /
  `MediumPercent` / `HardPercent` control the draw; short tiers are filled
  randomly from the rest of the bank.
- `AllowShuffle` shuffles question order; `ShuffleOptions` shuffles option
  order. `TimeLimitMinutes` and `MaxAttempts` configure attempt behavior.

### Class quiz set

`ClassQuizQuestionSetController` on
`/api/assignments/{assignmentId}/classes/{classId}/quiz-set`:

- `POST pull` (Mentor of the class) — draws a fixed set from the bank with the
  difficulty percents, replacing any previous set, and notifies.
- `GET` (class mentor, Manager, Admin) — includes `IsLocked`.
- `PUT questions/{questionId}` (Mentor of the class) — edit a question:
  `SingleChoice` / `MultipleChoice`, `Points` > 0, `DifficultyLevel` 1–5, at
  least 2 options.

When a set exists, students of that class get its questions (order shuffled
when `AllowShuffle`). The set locks once any student with an Active seat in the
class has a submission for the assignment (409
`... Ask a Manager to update the question bank instead.`).

### Direct quiz

Not supported: quizzes without `QuestionBankId` cannot be started.

## Quiz Attempt Lifecycle (Student)

Endpoints on `QuizController` (`/api`):

| Step | Endpoint |
| --- | --- |
| Start or resume | `POST /api/assignments/{assignmentId}/quiz/start` |
| Get in-progress | `GET /api/submissions/{submissionId}/quiz` |
| Save drafts | `PUT /api/submissions/{submissionId}/quiz/answers` |
| Submit | `POST /api/submissions/{submissionId}/quiz/submit` |
| View result | `GET /api/submissions/{submissionId}/quiz/result` |

Flow:

1. Student starts attempt → resumes the `Pending` attempt, or (after window and
   max-attempt checks) creates a new `Submission` in `Pending` with
   `AttemptNumber` = completed attempts + 1 and `ExpiresAt` = now +
   `TimeLimitMinutes`. Requires an Active module enrollment whose program
   enrollment is Active (403
   `Your program enrollment has ended. Repurchase the program to continue learning.`).
2. Draft answers stored in `QuizAnswer`. Save and submit require the attempt to
   be `Pending` (409 `This submission is no longer in progress.`) and not past
   `ExpiresAt` + 60 s.
3. Submit merges drafts, requires every question answered, auto-grades, sets
   `Graded`, recalculates progress, runs the AcademicFail check on a fail, and
   sends the quiz-graded notification.
4. Score = correct questions × (`MaxPoints` / question count), rounded to 2
   decimals; a question is correct only when the selected options exactly match
   the correct ones. Pass = score ≥ `PassScore`. `GET .../quiz` requires
   `Pending`; `GET .../quiz/result` requires `Graded` (409 otherwise).

409 `error.code` values on these endpoints (and the matching retrospective,
file-upload, research, and recovery endpoints where they apply):

| Code | When | `value.data` |
| --- | --- | --- |
| `ASSIGNMENT_WINDOW_MISSING` | Student's class has no AssignmentWindow for the assignment | — |
| `ASSIGNMENT_WINDOW_NOT_OPEN` | New attempt before window `StartTime` | window id, class, assignment, `startTime`, `endTime` (UTC) |
| `ASSIGNMENT_WINDOW_CLOSED` | New attempt after window `EndTime` | same as above |
| `ASSIGNMENT_MAX_ATTEMPTS` | Effective attempt budget used | — |
| `ASSIGNMENT_ATTEMPT_TIME_EXPIRED` | Quiz save/submit more than 60 s after `ExpiresAt` | — |
| `QUIZ_ATTEMPT_EXPIRED_GRADED` | Start found an expired Pending quiz attempt; it was graded from saved answers | `QuizResultResponseDto` |

Expired Pending quiz attempts (more than 60 s past `ExpiresAt`) are also graded
by `AssignmentWindowCloseService` — every 5 minutes, skipped while seeding — before
it runs the window-elapsed AcademicFail check, and the student receives the
quiz-graded notification. Retrospective and file drafts are not auto-closed by
the timer.

### Mentor / staff access

`GET .../quiz` and `GET .../quiz/result` also allow **Mentor**, **Manager**, and
**Admin**:

- Mentor may only view submissions of students enrolled in a class they mentor
  (same program as the assignment module).
- Manager / Admin may view any submission.
- Responses include `StudentId` and `StudentName`.

## Retrospective Lifecycle (Student)

Endpoints on `RetrospectiveController` (`/api`):

| Step | Endpoint |
| --- | --- |
| Start or resume draft | `POST /api/assignments/{assignmentId}/retrospective/start` |
| Get submission | `GET /api/submissions/{submissionId}/retrospective` |
| Save draft | `PUT /api/submissions/{submissionId}/retrospective/draft` |
| Submit | `POST /api/submissions/{submissionId}/retrospective/submit` |

Get allows Student, Parent, Mentor, Manager, and Admin; the other steps are
Student only.

Flow:

1. Student starts → resumes the existing `Submission` for that module
   enrollment when it is `Pending` or `ReturnedForRevision` (409 when it is
   `TurnedIn` or `Graded`); otherwise a new `Pending` draft with plain-text
   `ContentText` is created, which needs an open window.
2. Draft saves update `ContentText` while `Pending` / `ReturnedForRevision`
   (not blocked by the window or the timer).
3. Submit requires non-empty text and sets `TurnedIn` for mentor grading. A
   resubmit after `ReturnedForRevision` increments `AttemptNumber` and is
   subject to `ASSIGNMENT_MAX_ATTEMPTS`.
4. Grading uses `POST /api/assignment-submissions/{submissionId}/grade`.

## Submissions and Evidence

`Submission` tracks student work. `SubmissionEvidence` links a research
submission to uploaded `MediaAsset` rows (`MediaId`). The grader is recorded in
`Submission.VerifiedBy` (`VerifiedSubmissions` on User).

FileUpload (non-research) on `AssignmentSubmissionController` (`/api`):

- `POST assignment-submissions/submit` (Student) — at least one of `ContentText`
  or `FileUrl`. One `Submission` per module enrollment: the first submit creates
  it as `TurnedIn`; later submits update it, increment `AttemptNumber`, and set
  `TurnedIn`. A new attempt (first submit, or after a graded fail) needs an open
  window; 409 when already passed or pending review. Quiz, Retrospective, and
  research milestone assignments are rejected (400).
- `POST assignment-submissions/{id}/upload` (Student) — uploads the file to S3 and
  returns its URL; nothing is saved on the submission.
- `GET assignment-submissions/{id}` (Student, Parent, Mentor, Manager, Admin).
- `POST assignment-submissions/{id}/grade` (Mentor, Manager, Admin) — grade
  0..`MaxPoints`; `ReturnForRevision` sets `ReturnedForRevision`, otherwise
  `Graded`. Also used for retrospectives.

Research on `ResearchSubmissionController` (`/api`):

- `POST research-submissions/upload` (Student) — creates a `Pending` draft when
  the milestone is unlocked. Primary files: PDF/DOC/DOCX/ZIP (50 MB), images
  (10 MB), video (3 GB). Evidence (`isEvidence=true`): .jpg/.jpeg/.png or
  .mp4/.mov, sent through the class media pipeline and linked as
  `SubmissionEvidence`.
- `POST research-submissions/submit` (Student) — at least one of `ContentText`,
  `FileUrl`, or `EvidenceMediaAssetIds`; the first submit requires the previous
  milestone passed, required SelfPaced activities done, and an open window.
  Resubmission is allowed only from `Pending` / `ReturnedForRevision`.
- `GET research-submissions/{submissionId}` (Student, Parent, Mentor, Manager,
  Admin).
- `POST research-submissions/{submissionId}/grade` (Mentor, Manager, Admin) — a
  failing grade becomes `ReturnedForRevision` automatically while attempts remain,
  otherwise `Graded`.

Milestones on `ResearchMilestoneController` (`/api`): create/update/delete
(Admin, Manager; delete only without submissions), activity link/update/unlink
(Admin, Manager, Mentor), `GET research-milestones/{id}` and
`GET modules/{moduleId}/research-milestones`, and
`GET module-enrollments/{moduleEnrollmentId}/research-milestones/progress`
(Student, Parent, Admin, Manager).

Grading rules (both grade endpoints): only `TurnedIn` work can be graded, except
that Admin/Manager may re-grade `Graded` work as a correction; mentors must
mentor the student in the program. A passing correction may reopen a closed
purchase (see `enrollment.md`); a failing `Graded` result runs the AcademicFail
check.

## Certificates

Program certificates are issued automatically when every activity in the
program (including research-milestone activities) is `Done` on the latest module
enrollment per module. The check runs after each progress recalculation
(activity completion, grading, quiz submit). Required assignments,
`ProgressPercent == 100`, and the program enrollment status are **not** checked
for issuance. Separately, `ProgramEnrollment` becomes `Completed` only when
progress reaches 100% (activities + required assignments).

Issuance is program-only in v1 (`ModuleId` is null), one certificate per student
and program, code `OBOX-CERT-` + 10 hex characters. The API generates a PDF
(QuestPDF, A4 landscape), uploads it to S3 at
`certificates/{programId}/{studentId}/{code}.pdf`, and stores `PdfUrl` plus a
public `VerificationUrl` = `{APP_FRONTEND_URL | APP_BASE_URL}/certificates/verify/{code}`.
A PDF or S3 failure is logged and the certificate is still saved.

Endpoints under `/api/certificates`:

- `GET /me` (Student, Parent, Admin, Manager) — students see their own, parents
  see linked students, Admin/Manager see all
- `GET /{id}` (Student, Parent, Admin, Manager) — show-page detail
- `GET /by-enrollment/{programEnrollmentId}` (Student, Parent, Admin, Manager) —
  resolve cert for a learning enrollment; `null` data when not issued
- `GET /verify/{code}` — public verify payload for the FE share page
- `POST /program-enrollments/{programEnrollmentId}/ensure` (Student for own
  enrollment, Admin, Manager) — idempotent issue/retry PDF (same code); 400
  when activities are incomplete. On a `Completed` enrollment it also sends the
  program review request once.

The FE owns share UI and PDF download UX using `pdfUrl` and `verificationUrl`.
Skills on the certificate are the catalog names of that program's `ProgramSkill`
links. Learning outcomes come from module `LearningOutcomes` text arrays.

## Validation Expectations

Quiz grading logic lives in `IQuizAttemptService` / application services.
Changes to grading rules, attempt limits, or bank draw algorithms are high-risk
and need integration tests before proof claims.
