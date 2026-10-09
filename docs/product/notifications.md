# Notifications

## Delivery Contract

Business services create notification commands through `NotificationCatalog`
and pass them to `INotificationPublisher` (`NotificationPublisher`). The
publisher resolves each command's audience (`NotificationRecipientResolver`) to
`(UserId, Role, ContextStudentId)` rows, renders the matching role variant with
token interpolation, and persists one inbox record per
`(recipient, context student)`. After the save it pushes each row over SignalR
(`SignalRNotificationDispatcher`, client event `notificationReceived` on group
`user:{recipientUserId}`). Persistence is the source of truth: a SignalR failure
is logged and does not roll back the inbox. A command that resolves to zero
recipients is skipped.

Priority types also send email (`NotificationEmailDispatcher`) after the
SignalR push, using the already-rendered inbox title and body
(`IEmailService.SendInboxNotificationEmailAsync`). Only recipients with an
`Active` account and an email are mailed. Email failure is logged per message
and does not roll back the inbox.

| Email | Types |
| --- | --- |
| Priority inbox email (`NotificationEmailPriority`) | `ProgramPendingPayment`, `ModuleRetakePendingPayment`, `PendingPaymentExpired`, `PaymentFailed`, `PaymentCancelled`, `ResearchReturnedForRevision`, `ResearchWorkSubmitted` |
| Existing `IEmailService` templates (Vietnamese) | Parent payment request (checkout link), payment invoice, enrollment confirmation, staff Mentor/Expert credentials, parent magic-link / approve-link |
| Not emailed | All other catalog types, including `PaymentSucceeded` / `ProgramActivated` / `ParentPaymentRequested` (covered by the templates above) |

`ModuleRetakePendingPayment` is in the priority set but no service currently
publishes it (see the matrix).

`SessionStartingSoon` is a catalog event (inbox + SignalR only; not emailed).
Assignment due-soon reminders and overdue alerts are not catalog events.

`NotificationService` provides inbox queries and read-state operations for the
current user; it does not publish business notifications.

### Inbox endpoints (`NotificationController`)

All routes are under `api/notifications` and require authentication
(`[Authorize]`, any role). Every query is scoped to
`RecipientUserId == current user`.

| Method and route | Behavior |
| --- | --- |
| `GET /api/notifications?page&pageSize&unreadOnly` | Paginated inbox, newest first. `page < 1` becomes 1, `pageSize < 1` becomes 10, `pageSize > 50` is capped at 50. `unreadOnly=true` returns rows with no `ReadAt`. |
| `GET /api/notifications/unread-count` | `{ count }` of unread rows. |
| `PATCH /api/notifications/{id}/read` | Marks one row read. 404 when the row does not exist or belongs to another user. Already-read rows are a no-op. |
| `PATCH /api/notifications/read-all` | Marks every unread row of the current user read. |

## Audience Rules

| Audience | Recipients |
| --- | --- |
| `ForUser` | One specified user (role read from the user row; defaults to Student when the user is missing), with optional context student id |
| `ForStudentAndParents` | The student and parents with verified links |
| `ForParentsOfStudent` | Verified parents of one student (student is not a recipient) |
| `ForClassRoster` | Students with `Active` class enrollments |
| `ForClassRosterAndParents` | Active class-roster students and their verified parents |
| `ForClassMentor` | The mentor currently assigned to the class (`Class.MentorId`) |
| `ForClassRosterAndMentor` | Active class-roster students and the class mentor (no catalog event uses it) |
| `ForClassRosterAndParentsAndMentor` | Active class-roster students, their verified parents, and the class mentor |
| `ForManagers` | All users with role `Manager` and status `Active` (Admin accounts are not included) |
| `ForProgramParticipants` | Sync events only. Students with `Active` program enrollments, their verified parents, and mentors of the program's non-deleted classes |
| `ForProgramBrowsers` | Sync events only. Connections in the hub group `program:{programId}` |
| `ForAdvisoryParticipants` | Sync events only. Connections in the hub group `advisory:{programId}` |

Recipient rows are distinct by `(UserId, ContextStudentId)`, not by user id
alone. A parent with two actively enrolled children receives one inbox row per
child, with that child's name in the copy. Unverified parent links and inactive
class enrollments do not qualify. Manager and mentor recipients carry no
context student.

`ForUser` may pass an optional context student id (used for parent-only events
such as link and payment requests) so `{studentName}` still interpolates.

## Role templates and tokens

Each catalog event supplies a default copy plus optional Student, Parent,
Mentor, Manager, and Expert variants (`NotificationRoleTemplates`). Missing
variants fall back to default. Admin recipients use the Manager variant.
Copy may include `{token}` placeholders interpolated at publish time
(`NotificationTokenKeys`, `NotificationTemplateRenderer`):

| Token | Source |
| --- | --- |
| `{studentName}` | `User.FullName` (else email) of `ContextStudentId` |
| `{actorName}` | `User.FullName` (else email) of `ActorUserId` |
| `{className}` | Catalog token; else resolved from `payload.classId` |
| `{programName}` | Catalog token; else resolved from `payload.programId` (or the class's program) |
| `{moduleName}` | Catalog token |
| `{activityName}` | Catalog token |
| `{assignmentTitle}` | Catalog token |
| `{extraAttempts}` | Catalog token |
| `{checkedInAt}` | Catalog token (`HH:mm` Asia/Ho_Chi_Minh) |
| `{frameworkName}` | Catalog token (expert blueprint name) |
| `{comment}` | Catalog token |
| `{sessionTitle}` | Catalog token (class session title) |
| `{sessionStartTime}` | Catalog token, Asia/Ho_Chi_Minh. Co-teach publishers use `dd/MM/yyyy HH:mm`; the `SessionStartingSoon` reminder uses `HH:mm dd/MM/yyyy` |

Student copy addresses the learner as "bạn" ("Bạn đã hoàn thành…"). Parent copy
names the child as "con bạn {studentName}" ("Con bạn {studentName} đã hoàn
thành…"). Catalog titles and bodies are Vietnamese.

## Payload display names

`NotificationPayload` includes `studentName`, `actorName`, `className`, and
`programName` in addition to deeplink ids, plus `fromVersion` / `toVersion` for
framework-version events and a free-form `extra` (check-in time, session start
label). Catalog factories set class and program names from values the
publishing service already has. At publish time the publisher also fills missing
`className` / `programName` from `payload.classId` / `payload.programId` (and
`class.programId` when only the class is present). `StudentName` and
`ActorName` are filled per recipient from `ContextStudentId` and `ActorUserId`.
Copy for events with a distinct actor includes `{actorName}`. Class-roster
events do not set a single `studentId` in the catalog; the publisher writes the
context student id onto each inbox row.

## Strict Type-to-Audience-to-Publisher Matrix

The matrix lists every `NotificationType` value, the audience its catalog
factory uses, and which service emits it. "None" means a catalog factory exists
but no service calls it. `SeedService` also publishes sample rows for demo data
and is not listed.

| Notification type | Audience | Publisher |
| --- | --- | --- |
| `AccountRegistered` | `ForUser` | `AuthService` |
| `EmailVerified` | `ForUser` | `AuthService` |
| `PasswordChanged` | `ForUser` | `AuthService` |
| `ParentLinkRequested` | Parent via `ForUser` (context student) when a student requests a link | `ParentService` |
| `ParentLinkVerified` | Parent via `ForUser` (context student) when the link is verified | `ParentService` |
| `ParentLinkApproved` | Student via `ForUser` when the link is verified | `ParentService` |
| `ProgramPendingPayment` | `ForStudentAndParents` | `ProgramEnrollmentService` |
| `ProgramActivated` | `ForStudentAndParents` | `PaymentService` |
| `ProgramWithdrawn` | `ForStudentAndParents` | `ProgramPurchaseLifecycle` (close reason `Withdraw`) |
| `ModuleCompleted` | `ForStudentAndParents` | `ActivityProgressService` |
| `ModuleFailed` | `ForStudentAndParents` | `ProgramPurchaseLifecycle` (close reason `AcademicFail` or `Attendance`) |
| `ModuleUnlocked` | `ForStudentAndParents` | `ActivityProgressService` |
| `ModuleRetakePendingPayment` | `ForStudentAndParents` | None |
| `ModuleRetakeInitiated` | `ForStudentAndParents` | None |
| `PendingPaymentExpired` | `ForStudentAndParents` | `PendingEnrollmentCleanupService` |
| `ActivityCompleted` | `ForStudentAndParents` | `ActivityProgressService` |
| `ProgramReviewRequested` | Student via `ForUser`, once per completed program enrollment (deleted inbox rows still count) when a certificate is ensured | `CertificateService` |
| `PaymentSucceeded` | `ForStudentAndParents` | `PaymentService` |
| `PaymentFailed` | `ForStudentAndParents` | `PaymentService` |
| `PaymentCancelled` | `ForStudentAndParents` | `PaymentService` |
| `ParentPaymentRequested` | Parent via `ForUser` | `PaymentService` |
| `ParentModuleRetakeRequested` | Parent via `ForUser` | `PaymentService` |
| `ClassCreated` | `ForManagers` | `ClassService` |
| `ClassUpdated` | `ForClassRosterAndParentsAndMentor` | `ClassService` |
| `ClassOpenForEnrollment` | `ForManagers` | `ClassService` |
| `ClassStarted` | `ForClassRosterAndParentsAndMentor` | `ClassService` |
| `ClassAutoStarted` | `ForClassRosterAndParentsAndMentor` | `ClassService` |
| `ClassCompleted` | `ForClassRosterAndParentsAndMentor` | `ClassService` |
| `ClassMentorRequestSubmitted` | `ForManagers` | `ClassMentorRequestService` |
| `ClassMentorRequestApproved` | Mentor via `ForUser` | `ClassMentorRequestService`, `ClassService` |
| `ClassMentorRequestRejected` | Mentor via `ForUser` | `ClassMentorRequestService`, `ClassService` |
| `AssessmentRecoveryRequested` | `ForClassMentor` when the request has a class, else `ForManagers` | `AssessmentRecoveryRequestService` |
| `AssessmentRecoveryApproved` | `ForStudentAndParents` | `AssessmentRecoveryRequestService` |
| `AssessmentRecoveryRejected` | `ForStudentAndParents` | `AssessmentRecoveryRequestService` |
| `ClassRedeliveryPendingManager` | `ForManagers` | None (manager tier removed; its endpoints return 410) |
| `ClassRedeliveryMatchedPendingPayment` | `ForStudentAndParents` when the student selects a continuity class | `ClassRedeliveryRequestService` |
| `ClassRedeliveryRejected` | `ForStudentAndParents` | None (manager reject returns 410) |
| `ClassRedeliveryCompleted` | `ForStudentAndParents` after the retake payment completes | `ClassRedeliveryRequestService` |
| `ClassRedeliveryWithdrawn` | `ForStudentAndParents` when the request is cancelled or withdrawn | `ClassRedeliveryRequestService` |
| `ClassRedeliveryAwaitingSelection` | `ForStudentAndParents` when a request is created (copy includes the eligible class count) | `ClassRedeliveryRequestService` |
| `ClassRedeliveryIntensiveOffered` | `ForStudentAndParents` | None (intensive consent endpoints return 410) |
| `ClassRedeliveryCandidatesAvailable` | `ForStudentAndParents` | None |
| `ClassEnrolled` | `ForStudentAndParents` | `ClassEnrollmentService` |
| `ClassTransferred` | `ForStudentAndParents` | `ClassEnrollmentService`, `ClassRedeliveryRequestService` (on redelivery completion) |
| `ClassSessionScheduled` | `ForClassRosterAndParentsAndMentor` | `ClassSessionService` |
| `ClassSessionRescheduled` | `ForClassRosterAndParentsAndMentor`; start/end time change, or a status change to a status other than InProgress / Completed / Cancelled | `ClassSessionService` |
| `ClassSessionStarted` | `ForClassRosterAndParentsAndMentor`; manual status change to InProgress or hosted auto-start at `StartTime` (LiveOnline / Offline) | `ClassSessionService`, `SessionLifecyclePublisher` |
| `ClassSessionCompleted` | `ForClassRosterAndParentsAndMentor`; manual status change or hosted auto-complete after `EndTime` (LiveOnline / Offline), built by `ClassSessionCompletionHelper` | `ClassSessionService`, `SessionLifecyclePublisher` |
| `ClassSessionCancelled` | `ForClassRosterAndParentsAndMentor`; plus Invited/Accepted co-teach experts via `ForUser` (`ClassSessionCancelledForExpert`). Those co-teach rows are soft-deleted (same on session delete and class delete) | `ClassSessionService`, `ClassService` |
| `SessionStartingSoon` | `ForClassRosterAndParentsAndMentor`; hosted publisher for Scheduled / InProgress sessions whose `StartTime` is within the next 30 minutes, once per slot (`ReminderSentAt`). Changing `StartTime` clears `ReminderSentAt` so the new slot can remind again; EndTime-only and description-only edits do not | `SessionReminderPublisher` |
| `AttendanceMarkedPresent` | `ForStudentAndParents` (staff mark, unless the student already self-checked in); `ForParentsOfStudent` (first student QR/code check-in or first LiveOnline self-join, via `AttendanceCheckedIn`) | `SessionAttendanceService`, `SessionMeetingService` |
| `AttendanceMarkedLate` | `ForStudentAndParents` | `SessionAttendanceService` |
| `AttendanceMarkedAbsent` | `ForStudentAndParents` | `SessionAttendanceService` |
| `AttendanceMarkedExcused` | `ForStudentAndParents` | `SessionAttendanceService` |
| `QuizPassed` | `ForStudentAndParents` | `QuizAttemptService` |
| `QuizFailed` | `ForStudentAndParents` | `QuizAttemptService` |
| `ResearchGradedPassed` | `ForStudentAndParents` | `ResearchSubmissionService` |
| `ResearchGradedFailed` | `ForStudentAndParents` | `ResearchSubmissionService` |
| `ResearchReturnedForRevision` | `ForStudentAndParents` | `ResearchSubmissionService` |
| `ResearchSubmissionOpened` | Student via `ForUser` | None |
| `ResearchWorkSubmitted` | `ForClassMentor`; student via `ForUser` when no class is resolved | `ResearchSubmissionService` |
| `MediaVideoReady` | Uploader via `ForUser` | `MediaService` |
| `MediaProcessingFailed` | Uploader via `ForUser` | `MediaService` |
| `MediaAiTaggingFailed` | Uploader via `ForUser` | `MediaService` |
| `MediaTagsProcessed` | Uploader via `ForUser` | `MediaService` |
| `HighlightVideoGenerationQueued` | Student via `ForUser` | `PersonalVideoService` |
| `HighlightVideoReady` | `ForStudentAndParents` | `PersonalVideoService` |
| `HighlightVideoGenerationFailed` | Student via `ForUser` | `PersonalVideoService` |
| `AssignmentPublished` | `ForClassRosterAndParents`, one command per active class | `AssignmentService` |
| `MaterialUpdated` | `ForClassRoster`, one command per active class | `MaterialService` |
| `AssignmentEditedByMentor` | `ForManagers` | `AssignmentService` |
| `ClassQuizSetEditedByMentor` | `ForManagers` | `ClassQuizQuestionSetService` |
| `CurriculumReviewSubmitted` | Legacy enum value only (no catalog factory); kept so old inbox rows still read | — |
| `CurriculumReviewApproved` | `ForManagers` when the advisor approves | `ProgramApprovalService` |
| `CurriculumReviewChangesRequested` | Legacy enum value only (no catalog factory); kept so old inbox rows still read | — |
| `ClassSessionExpertInvited` | Expert via `ForUser` | `ClassSessionExpertService` |
| `ClassSessionExpertAccepted` | `ForManagers` | `ClassSessionExpertService` |
| `ClassSessionExpertDeclined` | `ForManagers` | `ClassSessionExpertService` |
| `ClassSessionExpertInvitationWithdrawn` | Expert via `ForUser` (manager withdraw, or board removal / expert delete) | `ClassSessionExpertService`, `ExpertService` |
| `ClassSessionExpertFeedbackRequested` | Accepted expert via `ForUser` when the session first becomes Completed, built by `ClassSessionCompletionHelper` | `ClassSessionService`, `SessionLifecyclePublisher` |
| `ClassSessionExpertFeedbackSubmitted` | Class mentor via `ForClassMentor` | `ClassSessionExpertService` |
| `CurriculumReviewPublished` | `ForManagers` when a manager publishes the program | `ProgramApprovalService` |
| `ClassSessionExpertClearedOnReschedule` | Invited and Accepted experts via `ForUser` when the session time window moves | `ClassSessionService` |
| `AdvisoryFeedbackPublished`, `AdvisoryReply`, `AdvisoryCorrectionAddressed` | Legacy enum values only (no catalog factories); kept so old inbox rows still read | — |
| `CurriculumApprovalRevoked` | Program advisor via `ForUser` after a curriculum edit (`CurriculumChangeRecorder`, published after commit) or a manager reopen (`CurriculumApprovalReopened`); `ForManagers` when the advisor revokes (`CurriculumApprovalRevokedByAdvisor`). None when the advisor is the actor, on advisor change, or on framework upgrade | `CurriculumChangeRecorder`, `ProgramApprovalService` |
| `CurriculumApprovalRequested` | Program advisor (linked login) via `ForUser` | `ProgramApprovalService` |
| `AdvisoryDiscussionMessage` | Each advisory participant except the author via `ForUser` (active managers, advisor, board experts with a login), after commit, user messages only, one per message. Skipped only while the recipient has a live connection in `advisory:{programId}` | `ProgramAdvisoryDiscussionService` |
| `AdvisoryMentionPinned` | `ForManagers` when the advisor pins a message that was not pinned (re-pin, unpin, and pin actions send nothing) | `ProgramAdvisoryDiscussionService` |
| `ProgramFrameworkUpgraded` | Program advisor via `ForUser` after a manager upgrades the pinned framework version (payload `fromVersion`, `toVersion`); none when the advisor is the actor | `ProgramApprovalService` |
| `FrameworkVersionPublished` | `ForManagers`, once per program of the framework pinned to an older version (or none), when the expert publishes a new version (payload `programId`, `fromVersion`, `toVersion`) | `ProgramFrameworkService` |

## Realtime Sync Events

Hub `NotificationHub` is mapped at `/hubs/notifications` and requires
authentication (`[Authorize]`). JavaScript clients may pass the JWT as the
`access_token` query parameter on that path. Besides `notificationReceived`
(inbox rows), clients receive `syncEvent`
`{ scope, entityType, entityId, at, payload? }` from `SyncEventPublisher`: an
ephemeral hint, never persisted, telling the client to refetch the REST
resource. `payload` is null unless the scope defines one; enum values in
payloads are strings. Dispatch failures are logged and swallowed.

Groups: every connection joins `user:{userId}` and `role:{role}`.
`JoinProgramSync(programId)` / `LeaveProgramSync` join the public
`program:{programId}` group (catalog seat counts). `JoinAdvisorySync(programId)`
/ `LeaveAdvisorySync` join `advisory:{programId}`; only advisory participants
(Manager/Admin, the advisor, board experts) may join, otherwise the hub throws
`HubException`.

Audience routing in `SyncEventPublisher`: `ForManagers` goes to the
`role:Manager` group, `ForProgramBrowsers` to `program:{id}`,
`ForAdvisoryParticipants` to `advisory:{id}`; every other audience is resolved
per user and sent to `user:{userId}`.

| Scope | Audience | Payload | Raised on |
| --- | --- | --- | --- |
| `seats.changed` | `program:{id}` (`ForProgramBrowsers`) and `role:Manager` (`ForManagers`) | none | Seat holds and releases, hold activation after payment, enrollments, transfers. `entityType` is `Class`, `entityId` is the class |
| `curriculum.structureChanged` | `ForProgramParticipants` (students, parents, class mentors) | none | Module, course, activity, and assignment changes. `entityType` is `Program` |
| `curriculum.structureChanged` | `advisory:{id}` | `{ curriculumVersion }` | Every curriculum save that bumps the version (after commit) |
| `advisory.discussionChanged` | `advisory:{id}` | `{ latestSequence, messageId }` | Post, edit, remove, and every system message; `messageId` is set on edit/remove, null when messages were appended |
| `advisory.pinChanged` | `advisory:{id}` | `{ messageId }` | Pin, unpin, pin actions (mark addressed, reopen, resolve), removing a pinned message; one per pin auto-resolved by approval |
| `advisory.approvalChanged` | `advisory:{id}` | `{ status, curriculumVersion }` | Approval request, approve, revoke (manual, curriculum edit, advisor change while Approved), framework-version upgrade, publish |
| `attendance.changed` | the marked student via `ForUser` | `{ studentId, status }` | Staff records session attendance (not raised on student check-in). `entityType` is `ClassSession`, `entityId` is the session. `status` is the `AttendanceStatus` name |
| `activityProgress.changed` | the student and verified parents via `ForStudentAndParents` | `{ studentId, programId, programEnrollmentId, activityId, nextActivityId, status }` | An activity is completed (student self-complete, mentor bulk complete, force complete), one event per student. `entityType` is `Activity`, `entityId` is the activity. `status` is `Done`. Skipped when the module has no program |
| `submission.graded` | the student and verified parents via `ForStudentAndParents` | `{ studentId, assignmentId, programId, programEnrollmentId, researchMilestoneId, status, assignedGrade, maxPoints, passed }` | A mentor or manager grades or returns a FileUpload or research submission. `entityType` is `Submission`, `entityId` is the submission. `status` is `Graded` or `ReturnedForRevision`; `passed` is null unless `Graded`; `researchMilestoneId` is null for FileUpload. Skipped when the assignment's module is missing |
| `submission.turnedIn` | class mentor via `ForClassMentor` | `{ assignmentId, studentId, classId, status }` | Student turns in a FileUpload assignment or research milestone. `entityType` is `Submission`, `entityId` is the submission. Skipped when the student has no active class. `status` is `TurnedIn` |

Advisory events are all `entityType = "Program"`, `entityId = programId`.
They are collected per program in an `AdvisorySyncBatch` during the transaction
(repeated discussion, approval, and structure events collapse into one) and
published only after commit; a rolled-back transaction publishes nothing.

Joining `advisory:{programId}` also registers presence for chat notification
suppression. Presence is an in-memory, single-instance tracker
(`AdvisoryPresenceTracker`, singleton) cleared on leave and disconnect;
scale-out would need a backplane.

## Parent Time-Support Policy

Verified parents receive planning-relevant events that help them support a
middle- or senior-school student:

- class details and lifecycle changes;
- session scheduling, rescheduling, start, completion, cancellation, and the
  30-minute `SessionStartingSoon` reminder (re-armed when `StartTime` changes);
- assignment publication;
- enrollment, payment, progress, attendance, and grading events already sent
  through `ForStudentAndParents`;
- the student's first QR/code check-in or first LiveOnline self-join for a
  session (`AttendanceMarkedPresent` via `ForParentsOfStudent`, copy includes
  `{checkedInAt}` in Vietnam local time). Staff marking Present after a student
  self check-in does not send a second Present notification.

Material updates, `ProgramReviewRequested`, and highlight-video queue/failure
events remain student-only. Assignment due-soon reminders and overdue alerts
are not implemented by this contract and require a separate scheduling feature.
