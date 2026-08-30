# Assessment

## Assignment Types

| AssignmentType | Behavior |
| --- | --- |
| Quiz | Auto-graded question flow via quiz endpoints |
| FileUpload | Student uploads evidence files |
| Retrospective | Plain-text reflective submission via retrospective endpoints |

Hands-on session evidence is captured through activities and media upload (see
`curriculum.md`); it is not an assignment type.

Assignments belong to a `Module` and optionally a `Course`. Catalog fields
include `MaxPoints`, `PassScore`, `IsRequiredForModulePass`,
`TimeLimitMinutes`, and `MaxAttempts`. Every assignment type must have
`TimeLimitMinutes` greater than 0 (application validation; the column stays
nullable). Starting an attempt sets `Submission.ExpiresAt`. Calendar open/close
is **not** on the assignment: each class has one `ClassSession` with
`SessionKind = AssignmentWindow` (`StartTime` opens new attempts, `EndTime`
hard-closes new attempts). An attempt already in progress (`Pending` or
`ReturnedForRevision`) may continue after `EndTime` until the student submits.
AcademicFail holds only while `ExpiresAt` is set and still in the future;
a draft with no timer or an elapsed timer does not block close. Rebuy uses the
**new** class window.
`IsRequiredForModulePass` is a progress unit **and** the AcademicFail gate:
optional assignments never close the program purchase.

Mentors of the class may update AssignmentWindow `StartTime` / `EndTime`.
Manager/Admin may also set them (including via session generate). Missing window:
new attempts are blocked. The student’s class is the Active (then Deferred)
program enrollment’s class seat — another class on the same program is not used
as a fallback when that seat exists.

### Attempt limits by module type

| ModuleType | `MaxAttempts` | Recovery |
| --- | --- | --- |
| Theory | Not enforced — unlimited free retries on the same class while the class window is open | No extra-attempt grant. Required work not passed after `EndTime` (no in-progress draft, nothing `TurnedIn`) → AcademicFail so the student can chuyen ca |
| Experiential / Research | Enforced | After exhaustion, student submits `AssessmentRecoveryRequest`; mentor grants extra attempts **only** (same class, same open window). Cap: 2 requests per assignment per module enrollment. Window already ended → no recovery; required work AcademicFails. Latest `TurnedIn` never closes. Passing research milestone N extends milestone N+1’s window to at least `now+48h` if that window is closed or has less than 48 hours left |

API: `/api/assignments` — CRUD for Manager/Admin; student submission
flows via assignment, quiz, and retrospective services.
Assessment recovery: `/api/assessment-recovery-requests`.

## Question Banks

Reusable question pools attached to courses. Supports CSV import via
`ICsvQuestionParserService`.

- `BankQuestion` and `BankQuestionOption` entities.
- Difficulty levels via `DifficultyLevel` enum.

API: `/api/question-banks`.

## Quiz Modes

### Bank-drawn quiz

When `Assignment.QuestionBankId` is set:

- Questions are randomly drawn at serve-time from the linked bank.
- `QuestionCount`, `EasyPercent` / `MediumPercent` / `HardPercent` control draw.
- `ShuffleOptions`, `AllowShuffle`, `TimeLimitMinutes`, `MaxAttempts` configure
  attempt behavior.

### Direct quiz

When `QuestionBankId` is null, questions are attached via `QuizQuestion` on the
assignment.

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

1. Student starts attempt → creates or resumes `Submission` in Pending state.
2. Draft answers stored in `QuizAnswer`.
3. Submit merges drafts, validates completeness, auto-grades, sets Graded status.
4. Result returns score against `PassScore` and `MaxPoints`.

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

Flow:

1. Student starts → creates or resumes `Submission` in Pending (or
   `ReturnedForRevision`) with plain-text `ContentText`.
2. Draft saves update `ContentText` while in progress.
3. Submit requires non-empty text, sets `TurnedIn` for mentor grading.
4. Grading uses `POST /api/assignment-submissions/{submissionId}/grade`.

## Submissions and Evidence

`Submission` tracks student work. `SubmissionEvidence` holds file references.
Mentors or staff may verify submissions (`VerifiedSubmissions` on User).

## Certificates

Program certificates are issued automatically when every activity in the
program is `Done` for the enrollment (including research-milestone activities).
Required assignments and `ProgressPercent == 100` are **not** required for
issuance. Separately, `ProgramEnrollment` becomes `Completed` only when
progress reaches 100% (activities + required assignments).

Issuance is program-only in v1 (`ModuleId` is null). The API generates a PDF
(QuestPDF), uploads it to S3 at
`certificates/{programId}/{studentId}/{code}.pdf`, and stores `PdfUrl` plus a
public `VerificationUrl` built from `APP_FRONTEND_URL` / `APP_BASE_URL`.

Endpoints under `/api/certificates`:

- `GET /me` — list certificates for the current user scope
- `GET /{id}` — show-page detail (auth)
- `GET /by-enrollment/{programEnrollmentId}` — resolve cert for a learning enrollment
- `GET /verify/{code}` — public verify payload for the FE share page
- `POST /program-enrollments/{programEnrollmentId}/ensure` — idempotent issue/retry PDF

The FE owns share UI and PDF download UX using `pdfUrl` and `verificationUrl`.
Skills and learning outcomes come from existing `Program.SkillsGained` and
module `LearningOutcomes` text arrays.

## Validation Expectations

Quiz grading logic lives in `IQuizAttemptService` / application services.
Changes to grading rules, attempt limits, or bank draw algorithms are high-risk
and need integration tests before proof claims.
