# Notifications

## Delivery Contract

Business services create notification commands through `NotificationCatalog`.
`NotificationPublisher` resolves each command's audience, persists one inbox
record per distinct recipient, and then attempts real-time delivery through
SignalR. Persistence is the source of truth if real-time delivery fails.

`NotificationService` provides inbox queries and read-state operations; it does
not publish business notifications.

## Audience Rules

| Audience | Recipients |
| --- | --- |
| `ForUser` | One specified user |
| `ForStudentAndParents` | The student and parents with verified links |
| `ForClassRoster` | Students with active class enrollments |
| `ForClassRosterAndParents` | Active class-roster students and their verified parents |
| `ForClassMentor` | The mentor currently assigned to the class |
| `ForClassRosterAndMentor` | Active class-roster students and the class mentor |
| `ForClassRosterAndParentsAndMentor` | Active class-roster students, their verified parents, and the class mentor |
| `ForManagers` | All active manager accounts |

Recipient IDs are distinct. Unverified parent links and inactive class
enrollments do not qualify.

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
| `ModuleCompleted`                | `ForStudentAndParents`                                       | `ActivityProgressService`                   |
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
| `ClassSessionRescheduled`        | `ForClassRosterAndParentsAndMentor`                          | `ClassSessionService`                       |
| `ClassSessionStarted`            | `ForClassRosterAndMentor`                                    | `ClassSessionService`                       |
| `ClassSessionCompleted`          | `ForClassRosterAndMentor`                                    | `ClassSessionService`                       |
| `ClassSessionCancelled`          | `ForClassRosterAndParentsAndMentor`                          | `ClassSessionService`                       |
| `AttendanceMarkedPresent`        | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `AttendanceMarkedLate`           | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `AttendanceMarkedAbsent`         | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `AttendanceMarkedExcused`        | `ForStudentAndParents`                                       | `SessionAttendanceService`                  |
| `QuizPassed`                     | `ForStudentAndParents`                                       | `QuizAttemptService`                        |
| `QuizFailed`                     | `ForStudentAndParents`                                       | `QuizAttemptService`                        |
| `ResearchGradedPassed`           | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchGradedFailed`           | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchReturnedForRevision`    | `ForStudentAndParents`                                       | `ResearchSubmissionService`                 |
| `ResearchSubmissionOpened`       | Student via `ForUser`                                        | `ResearchSubmissionService`                 |
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

## Parent Time-Support Policy

Verified parents receive planning-relevant events that help them support a
middle- or senior-school student:

- class details and lifecycle changes;
- session scheduling, rescheduling, and cancellation;
- assignment publication;
- enrollment, payment, progress, attendance, and grading events already sent
  through `ForStudentAndParents`.

Session started/completed events remain operational signals for students and
mentors. Material updates remain student-only. Scheduled session reminders,
assignment due-soon reminders, and overdue alerts are not implemented by this
contract and require a separate scheduling feature.
