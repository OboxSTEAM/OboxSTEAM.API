# Notifications

## Delivery Contract

Business services create notification commands through `NotificationCatalog`.
`NotificationPublisher` resolves each command's audience to
`(UserId, Role, ContextStudentId)`, renders the matching role variant with
token interpolation, persists one inbox record per `(recipient, context student)`,
and then attempts real-time delivery through SignalR. Persistence is the source
of truth if real-time delivery fails.

Priority types also send email in parallel with SignalR, using the already-rendered
inbox title and body. Email failure is logged and does not roll back the inbox.

| Email | Types |
| --- | --- |
| Priority inbox email | `ProgramPendingPayment`, `ModuleRetakePendingPayment`, `PendingPaymentExpired`, `PaymentFailed`, `PaymentCancelled`, `ResearchReturnedForRevision`, `ResearchWorkSubmitted` |
| Existing `IEmailService` templates (unchanged) | Parent payment request (checkout link), payment invoice, enrollment confirmation |
| Not emailed | All other catalog types, including `PaymentSucceeded` / `ProgramActivated` / `ParentPaymentRequested` (covered by the templates above) |

Scheduled session reminders, assignment due-soon reminders, and overdue alerts
are not catalog events yet. When they are added, include them in
`NotificationEmailPriority`.

`NotificationService` provides inbox queries and read-state operations; it does
not publish business notifications.

## Audience Rules

| Audience | Recipients |
| --- | --- |
| `ForUser` | One specified user, with optional context student id |
| `ForStudentAndParents` | The student and parents with verified links |
| `ForParentsOfStudent` | Verified parents of one student (student is not a recipient) |
| `ForClassRoster` | Students with active class enrollments |
| `ForClassRosterAndParents` | Active class-roster students and their verified parents |
| `ForClassMentor` | The mentor currently assigned to the class |
| `ForClassRosterAndMentor` | Active class-roster students and the class mentor |
| `ForClassRosterAndParentsAndMentor` | Active class-roster students, their verified parents, and the class mentor |
| `ForManagers` | All active manager accounts |

Recipient rows are distinct by `(UserId, ContextStudentId)`, not by user id
alone. A parent with two actively enrolled children receives one inbox row per
child, with that child's name in the copy. Unverified parent links and inactive
class enrollments do not qualify.

`ForUser` may pass an optional context student id (used for parent-only events
such as payment requests) so `{studentName}` still interpolates.

## Role templates and tokens

Each catalog event supplies a default copy plus optional Student, Parent,
Mentor, and Manager variants. Missing variants fall back to default. `RoleType.Expert`
has no dedicated variant yet and resolves to default. Copy may
include `{token}` placeholders interpolated at publish time:

| Token | Source |
| --- | --- |
| `{studentName}` | `User.FullName` (else email) of `ContextStudentId` |
| `{actorName}` | `User.FullName` (else email) of `ActorUserId` |
| `{className}` | Catalog token |
| `{programName}` | Catalog token |
| `{moduleName}` | Catalog token |
| `{activityName}` | Catalog token |
| `{assignmentTitle}` | Catalog token |
| `{extraAttempts}` | Catalog token |
| `{checkedInAt}` | Catalog token (`HH:mm` Asia/Ho_Chi_Minh) |
| `{frameworkName}` | Catalog token (expert blueprint name) |
| `{comment}` | Catalog token (expert request-changes feedback) |
| `{sessionTitle}` | Catalog token (class session title) |
| `{sessionStartTime}` | Catalog token (`dd/MM/yyyy HH:mm` Asia/Ho_Chi_Minh when set by co-teach / reminder publishers) |

Student copy addresses the learner as "bạn" ("Bạn đã hoàn thành…"). Parent copy
names the child as "con bạn {studentName}" ("Con bạn {studentName} đã hoàn
thành…"). Catalog titles and bodies are Vietnamese.

## Payload display names

`NotificationPayload` includes `studentName`, `actorName`, `className`, and
`programName` in addition to deeplink ids. Catalog factories set class and
program names from values the publishing service already has. At publish time
the publisher also fills missing `className` / `programName` from
`payload.classId` / `payload.programId` (and `class.programId` when only the
class is present). `StudentName` and `ActorName` are filled per recipient from
`ContextStudentId` and `ActorUserId`. Copy for events with a distinct actor
includes `{actorName}`. Class-roster events do not set a single `studentId` in
the catalog; the publisher writes the context student id onto each inbox row.

## Strict Type-to-Audience-to-Publisher Matrix

The matrix is the product contract for who receives each notification and which
service emits it.

| Notification type                | Audience                                                     | Publisher                                   |
| ----------------------------------| --------------------------------------------------------------| ---------------------------------------------|
| `AccountRegistered`              | `ForUser`                                                    | `AuthService`                               |
| `EmailVerified`                  | `ForUser`                                                    | `AuthService`                               |
| `PasswordChanged`                | `ForUser`                                                    | `AuthService`                               |
| `ParentLinkRequested`            | Parent via `ForUser`                                         | `ParentService`                             |
| `ParentLinkVerified`             | Parent via `ForUser`                                         | `ParentService`                             |
| `ParentLinkApproved`             | Student via `ForUser`                                        | `ParentService`                             |
| `ProgramPendingPayment`          | `ForStudentAndParents`                                       | `ProgramEnrollmentService`                  |
| `ProgramActivated`               | `ForStudentAndParents`                                       | `PaymentService`                            |
| `ProgramWithdrawn`               | `ForStudentAndParents`                                       | `ProgramPurchaseLifecycle`                  |
| `ModuleCompleted`                | `ForStudentAndParents`                                       | `ActivityProgressService`                   |
| `ModuleFailed`                   | `ForStudentAndParents`                                       | `ProgramPurchaseLifecycle` (academic fail or attendance close) |
| `ModuleUnlocked`                 | `ForStudentAndParents`                                       | `ActivityProgressService`                   |
| `ModuleRetakePendingPayment`     | `ForStudentAndParents`                                       | `ModuleEnrollmentService`                   |
| `ModuleRetakeInitiated`          | `ForStudentAndParents`                                       | `ModuleEnrollmentService`                   |
| `PendingPaymentExpired`          | `ForStudentAndParents`                                       | `PendingEnrollmentCleanupService`           |
| `ActivityCompleted`              | `ForStudentAndParents`                                       | `ActivityProgressService`                   |
| `PaymentSucceeded`               | `ForStudentAndParents`                                       | `PaymentService`                            |
| `PaymentFailed`                  | `ForStudentAndParents`                                       | `PaymentService`                            |
| `PaymentCancelled`               | `ForStudentAndParents`                                       | `PaymentService`                            |
| `ParentPaymentRequested`         | Parent via `ForUser`                                         | `PaymentService`                            |
| `ParentModuleRetakeRequested`    | Parent via `ForUser`                                         | `PaymentService`                            |
| `ClassCreated`                   | `ForManagers`                                                | `ClassService`                              |
| `ClassUpdated`                   | `ForClassRosterAndParentsAndMentor`                          | `ClassService`                              |
| `ClassOpenForEnrollment`         | `ForManagers`                                                | `ClassService`                              |
| `ClassStarted`                   | `ForClassRosterAndParentsAndMentor`                          | `ClassService`                              |
| `ClassAutoStarted`               | `ForClassRosterAndParentsAndMentor`                          | `ClassService`                              |
| `ClassCompleted`                 | `ForClassRosterAndParentsAndMentor`                          | `ClassService`                              |
| `ClassMentorRequestSubmitted`    | `ForManagers`                                                | `ClassMentorRequestService`                 |
| `ClassMentorRequestApproved`     | Mentor via `ForUser`                                         | `ClassMentorRequestService`, `ClassService` |
| `ClassMentorRequestRejected`     | Mentor via `ForUser`                                         | `ClassMentorRequestService`, `ClassService` |
| `AssessmentRecoveryRequested`    | Class mentor (or managers)                                   | `AssessmentRecoveryRequestService`          |
| `AssessmentRecoveryApproved`     | `ForStudentAndParents`                                       | `AssessmentRecoveryRequestService`          |
| `AssessmentRecoveryRejected`     | `ForStudentAndParents`                                       | `AssessmentRecoveryRequestService`          |
| `ClassRedeliveryPendingManager`  | `ForManagers`                                                | `ClassRedeliveryRequestService`             |
| `ClassRedeliveryMatchedPendingPayment` | `ForStudentAndParents`                                 | `ClassRedeliveryRequestService`             |
| `ClassRedeliveryRejected`        | `ForStudentAndParents`                                       | `ClassRedeliveryRequestService`             |
| `ClassRedeliveryCompleted`       | `ForStudentAndParents`                                       | `ClassRedeliveryRequestService`             |
| `ClassEnrolled`                  | `ForStudentAndParents`                                       | `ClassEnrollmentService`                    |
| `ClassTransferred`               | `ForStudentAndParents`                                       | `ClassEnrollmentService`                    |
| `ClassSessionScheduled`          | `ForClassRosterAndParentsAndMentor`                          | `ClassSessionService`                       |
| `ClassSessionRescheduled`        | `ForClassRosterAndParentsAndMentor`; Invited expert via `ForUser` when the committed window moves | `ClassSessionService`, `ClassSessionExpertService` (approve-reschedule) |
| `ClassSessionStarted`            | `ForClassRosterAndParentsAndMentor`                          | `ClassSessionService`                       |
| `ClassSessionCompleted`          | `ForClassRosterAndParentsAndMentor`                          | `ClassSessionService`                       |
| `ClassSessionCancelled`          | `ForClassRosterAndParentsAndMentor`; Invited/Accepted expert via `ForUser` | `ClassSessionService`                       |
| `AttendanceMarkedPresent`        | `ForStudentAndParents` (staff mark); `ForParentsOfStudent` (first student check-in) | `SessionAttendanceService`                  |
| `AttendanceMarkedLate`           | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `AttendanceMarkedAbsent`         | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `AttendanceMarkedExcused`        | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `QuizPassed`                     | `ForStudentAndParents`                                       | `QuizAttemptService`                        |
| `QuizFailed`                     | `ForStudentAndParents`                                       | `QuizAttemptService`                        |
| `ResearchGradedPassed`           | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchGradedFailed`           | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchReturnedForRevision`    | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchWorkSubmitted`          | `ForClassMentor`; student fallback when no class is resolved | `ResearchSubmissionService`                 |
| `MediaVideoReady`                | Uploader via `ForUser`                                       | `MediaService`                              |
| `MediaProcessingFailed`          | Uploader via `ForUser`                                       | `MediaService`                              |
| `MediaAiTaggingFailed`           | Uploader via `ForUser`                                       | `MediaService`                              |
| `MediaTagsProcessed`             | Uploader via `ForUser`                                       | `MediaService`                              |
| `HighlightVideoGenerationQueued` | Student via `ForUser`                                        | `PersonalVideoService`                      |
| `HighlightVideoReady`            | `ForStudentAndParents`                                       | `PersonalVideoService`                      |
| `HighlightVideoGenerationFailed` | Student via `ForUser`                                        | `PersonalVideoService`                      |
| `AssignmentPublished`            | `ForClassRosterAndParents`                                   | `AssignmentService`                         |
| `MaterialUpdated`                | `ForClassRoster`                                             | `MaterialService`                           |
| `AssignmentEditedByMentor`       | `ForManagers`                                                | `AssignmentService`                         |
| `ClassQuizSetEditedByMentor`     | `ForManagers`                                                | `ClassQuizQuestionSetService`               |
| `CurriculumReviewSubmitted`      | Framework-owning expert via `ForUser`                        | `CurriculumReviewService`                   |
| `CurriculumReviewApproved`       | `ForManagers`                                                | `CurriculumReviewService`                   |
| `CurriculumReviewChangesRequested` | `ForManagers`                                              | `CurriculumReviewService`                   |
| `ClassSessionExpertInvited`        | Expert via `ForUser`                                       | `ClassSessionExpertService`                 |
| `ClassSessionExpertAccepted`       | `ForManagers`                                              | `ClassSessionExpertService`                 |
| `ClassSessionExpertDeclined`       | `ForManagers`                                              | `ClassSessionExpertService`                 |
| `ClassSessionExpertInvitationWithdrawn` | Expert via `ForUser`                                  | `ClassSessionExpertService`                 |
| `ClassSessionExpertRescheduleRequested` | Accepted expert via `ForUser`                         | `ClassSessionService`                       |
| `ClassSessionExpertRescheduleDeclined` | `ForManagers`                                          | `ClassSessionExpertService`                 |
| `ClassSessionExpertFeedbackRequested`  | Accepted expert via `ForUser` when the session first becomes Completed | `ClassSessionService`        |
| `ClassSessionExpertFeedbackSubmitted`  | Class mentor via `ForClassMentor`                      | `ClassSessionExpertService`                 |

## Parent Time-Support Policy

Verified parents receive planning-relevant events that help them support a
middle- or senior-school student:

- class details and lifecycle changes;
- session scheduling, rescheduling, start, completion, and cancellation;
- assignment publication;
- enrollment, payment, progress, attendance, and grading events already sent
  through `ForStudentAndParents`;
- the student's first QR/code check-in for a session (`AttendanceMarkedPresent`
  via `ForParentsOfStudent`, copy includes `{checkedInAt}` in Vietnam local
  time). Staff marking Present after that check-in does not send a second
  Present notification. Material updates remain student-only. Scheduled session
  reminders, assignment due-soon reminders, and overdue alerts are not
  implemented by this contract and require a separate scheduling feature.
