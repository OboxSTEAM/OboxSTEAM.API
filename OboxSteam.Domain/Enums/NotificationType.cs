namespace OboxSteam.Domain.Enums;

/// <summary>
/// In-app notification categories for the centralized notification hub.
/// Stored as strings in PostgreSQL.
/// </summary>
public enum NotificationType
{
    // Account
    AccountRegistered,
    EmailVerified,
    PasswordChanged,

    // Parent link
    ParentLinkRequested,
    ParentLinkVerified,
    ParentLinkApproved,

    // Enrollment
    ProgramPendingPayment,
    ProgramActivated,
    ModuleCompleted,
    ModuleUnlocked,
    ModuleRetakePendingPayment,
    ModuleRetakeInitiated,
    PendingPaymentExpired,
    ActivityCompleted,

    // Payment
    PaymentSucceeded,
    PaymentFailed,
    PaymentCancelled,
    ParentPaymentRequested,
    ParentModuleRetakeRequested,

    // Class lifecycle
    ClassCreated,
    ClassUpdated,
    ClassOpenForEnrollment,
    ClassStarted,
    ClassAutoStarted,
    ClassCompleted,

    // Class mentor assignment
    ClassMentorRequestSubmitted,
    ClassMentorRequestApproved,
    ClassMentorRequestRejected,

    // Class enrollment
    ClassEnrolled,
    ClassTransferred,

    // Class session
    ClassSessionScheduled,
    ClassSessionRescheduled,
    ClassSessionStarted,
    ClassSessionCompleted,
    ClassSessionCancelled,

    // Attendance
    AttendanceMarkedPresent,
    AttendanceMarkedLate,
    AttendanceMarkedAbsent,
    AttendanceMarkedExcused,

    // Grading
    QuizPassed,
    QuizFailed,
    ResearchGradedPassed,
    ResearchGradedFailed,
    ResearchReturnedForRevision,

    // Research workflow
    ResearchSubmissionOpened,
    ResearchWorkSubmitted,

    // Media / AI
    MediaVideoReady,
    MediaProcessingFailed,
    MediaAiTaggingFailed,
    MediaTagsProcessed,

    // Highlight video
    HighlightVideoGenerationQueued,
    HighlightVideoReady,
    HighlightVideoGenerationFailed,

    // Catalog (optional admin publishes)
    AssignmentPublished,
    MaterialUpdated,

    // Mentor curriculum edits
    AssignmentEditedByMentor,
    ClassQuizSetEditedByMentor
}
