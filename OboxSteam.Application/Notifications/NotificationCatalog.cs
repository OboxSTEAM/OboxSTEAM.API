using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Static factories for consistent notification type, audience, copy, and payload.
/// Business services should call these methods instead of building raw <see cref="NotificationCommand"/>s.
/// </summary>
public static class NotificationCatalog
{
    // ── Account ──────────────────────────────────────────────────────────────

    public static NotificationCommand AccountRegistered(Guid userId)
        => new(
            NotificationType.AccountRegistered,
            NotificationAudience.ForUser(userId),
            "Welcome to OboxSTEAM",
            "Your account has been created. Verify your email to get started.",
            entityType: "User",
            entityId: userId);

    public static NotificationCommand EmailVerified(Guid userId)
        => new(
            NotificationType.EmailVerified,
            NotificationAudience.ForUser(userId),
            "Email verified",
            "Your email address has been verified successfully.",
            entityType: "User",
            entityId: userId);

    public static NotificationCommand PasswordChanged(Guid userId)
        => new(
            NotificationType.PasswordChanged,
            NotificationAudience.ForUser(userId),
            "Password changed",
            "Your password was changed. If this wasn't you, contact support.",
            entityType: "User",
            entityId: userId);

    // ── Parent link ───────────────────────────────────────────────────────────

    public static NotificationCommand ParentLinkRequested(Guid parentId, Guid studentId, Guid? actorUserId = null)
        => new(
            NotificationType.ParentLinkRequested,
            NotificationAudience.ForUser(parentId),
            "Parent link requested",
            "A parent–student link request is waiting for verification.",
            payload: new NotificationPayload { StudentId = studentId },
            actorUserId: actorUserId,
            entityType: "ParentStudent",
            entityId: studentId);

    public static NotificationCommand ParentLinkVerified(Guid parentId, Guid studentId)
        => new(
            NotificationType.ParentLinkVerified,
            NotificationAudience.ForUser(parentId),
            "Parent link verified",
            "Your link with the student has been verified.",
            payload: new NotificationPayload { StudentId = studentId },
            entityType: "ParentStudent",
            entityId: studentId);

    public static NotificationCommand ParentLinkApproved(Guid studentId, Guid parentId, Guid? actorUserId = null)
        => new(
            NotificationType.ParentLinkApproved,
            NotificationAudience.ForUser(studentId),
            "Parent link approved",
            "A parent has been linked to your account.",
            payload: new NotificationPayload { StudentId = studentId },
            actorUserId: actorUserId,
            entityType: "ParentStudent",
            entityId: parentId);

    // ── Enrollment ────────────────────────────────────────────────────────────

    public static NotificationCommand ProgramPendingPayment(
        Guid studentId,
        Guid programId,
        Guid programEnrollmentId,
        string? programName = null)
        => new(
            NotificationType.ProgramPendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment required",
            string.IsNullOrWhiteSpace(programName)
                ? "Complete payment to activate your program enrollment."
                : $"Complete payment to activate enrollment in \"{programName}\".",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                ProgramEnrollmentId = programEnrollmentId,
                StudentId = studentId
            },
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId);

    public static NotificationCommand ProgramActivated(
        Guid studentId,
        Guid programId,
        Guid programEnrollmentId,
        string? programName = null)
        => new(
            NotificationType.ProgramActivated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Program enrollment activated",
            string.IsNullOrWhiteSpace(programName)
                ? "Your program enrollment is now active."
                : $"Your enrollment in \"{programName}\" is now active.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                ProgramEnrollmentId = programEnrollmentId,
                StudentId = studentId
            },
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId);

    public static NotificationCommand ModuleCompleted(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        Guid? programId = null,
        string? moduleName = null)
        => new(
            NotificationType.ModuleCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module completed",
            string.IsNullOrWhiteSpace(moduleName)
                ? "You completed a module."
                : $"You completed \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Module",
            entityId: moduleId);

    public static NotificationCommand ModuleUnlocked(
        Guid studentId,
        Guid moduleId,
        Guid? programId = null,
        string? moduleName = null)
        => new(
            NotificationType.ModuleUnlocked,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module unlocked",
            string.IsNullOrWhiteSpace(moduleName)
                ? "A new module is now available."
                : $"Module \"{moduleName}\" is now available.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Module",
            entityId: moduleId);

    public static NotificationCommand ModuleRetakePendingPayment(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null)
        => new(
            NotificationType.ModuleRetakePendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Retake payment required",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Complete payment to retake this module."
                : $"Complete payment to retake \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                StudentId = studentId
            },
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId);

    public static NotificationCommand ModuleRetakeInitiated(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null)
        => new(
            NotificationType.ModuleRetakeInitiated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module retake started",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Your module retake has been initiated."
                : $"Retake of \"{moduleName}\" has been initiated.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                StudentId = studentId
            },
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId);

    public static NotificationCommand PendingPaymentExpired(
        Guid studentId,
        Guid programEnrollmentId,
        Guid? programId = null)
        => new(
            NotificationType.PendingPaymentExpired,
            NotificationAudience.ForStudentAndParents(studentId),
            "Pending enrollment expired",
            "Your pending program enrollment expired because payment was not completed in time.",
            payload: new NotificationPayload
            {
                ProgramEnrollmentId = programEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId);

    public static NotificationCommand ActivityCompleted(
        Guid studentId,
        Guid activityId,
        Guid? moduleId = null,
        Guid? programId = null,
        string? activityName = null)
        => new(
            NotificationType.ActivityCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Activity completed",
            string.IsNullOrWhiteSpace(activityName)
                ? "You completed an activity."
                : $"You completed \"{activityName}\".",
            payload: new NotificationPayload
            {
                ActivityId = activityId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Activity",
            entityId: activityId);

    // ── Payment ───────────────────────────────────────────────────────────────

    public static NotificationCommand PaymentSucceeded(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => new(
            NotificationType.PaymentSucceeded,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment succeeded",
            "Your payment was successful.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                ProgramEnrollmentId = programEnrollmentId,
                StudentId = studentId
            },
            entityType: "Payment",
            entityId: paymentId);

    public static NotificationCommand PaymentFailed(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null)
        => new(
            NotificationType.PaymentFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment failed",
            "Your payment could not be completed. Please try again.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Payment",
            entityId: paymentId);

    public static NotificationCommand PaymentCancelled(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null)
        => new(
            NotificationType.PaymentCancelled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment cancelled",
            "Your payment was cancelled.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Payment",
            entityId: paymentId);

    public static NotificationCommand ParentPaymentRequested(
        Guid parentId,
        Guid studentId,
        Guid paymentRequestId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => new(
            NotificationType.ParentPaymentRequested,
            NotificationAudience.ForUser(parentId),
            "Payment request from student",
            "Your student requested that you complete a program payment.",
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ProgramId = programId,
                ProgramEnrollmentId = programEnrollmentId
            },
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId);

    public static NotificationCommand ParentModuleRetakeRequested(
        Guid parentId,
        Guid studentId,
        Guid paymentRequestId,
        Guid? moduleId = null)
        => new(
            NotificationType.ParentModuleRetakeRequested,
            NotificationAudience.ForUser(parentId),
            "Module retake payment request",
            "Your student requested that you complete a module retake payment.",
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ModuleId = moduleId
            },
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId);

    // ── Class lifecycle ───────────────────────────────────────────────────────

    public static NotificationCommand ClassCreated(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassCreated,
            NotificationAudience.ForManagers(),
            "Class created",
            string.IsNullOrWhiteSpace(className)
                ? "A new class was created."
                : $"Class \"{className}\" was created.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    public static NotificationCommand ClassUpdated(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassUpdated,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Class updated",
            string.IsNullOrWhiteSpace(className)
                ? "Class details were updated."
                : $"Class \"{className}\" was updated.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    public static NotificationCommand ClassOpenForEnrollment(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassOpenForEnrollment,
            NotificationAudience.ForManagers(),
            "Class open for enrollment",
            string.IsNullOrWhiteSpace(className)
                ? "A class is now open for enrollment."
                : $"Class \"{className}\" is now open for enrollment.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    public static NotificationCommand ClassStarted(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassStarted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Class started",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has started."
                : $"Class \"{className}\" has started.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    public static NotificationCommand ClassAutoStarted(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassAutoStarted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Class auto-started",
            string.IsNullOrWhiteSpace(className)
                ? "Your class was automatically started."
                : $"Class \"{className}\" was automatically started.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    public static NotificationCommand ClassCompleted(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassCompleted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Class completed",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has been completed."
                : $"Class \"{className}\" has been completed.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId);

    // ── Class mentor assignment ───────────────────────────────────────────────

    public static NotificationCommand ClassMentorRequestSubmitted(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null)
        => new(
            NotificationType.ClassMentorRequestSubmitted,
            NotificationAudience.ForManagers(),
            "Mentor request submitted",
            string.IsNullOrWhiteSpace(className)
                ? "A mentor requested assignment to a class."
                : $"A mentor requested assignment to class \"{className}\".",
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            actorUserId: mentorId,
            entityType: "ClassMentorRequest",
            entityId: requestId);

    public static NotificationCommand ClassMentorRequestApproved(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null)
        => new(
            NotificationType.ClassMentorRequestApproved,
            NotificationAudience.ForUser(mentorId),
            "Mentor request approved",
            string.IsNullOrWhiteSpace(className)
                ? "Your class assignment request was approved."
                : $"Your request for class \"{className}\" was approved.",
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            entityType: "ClassMentorRequest",
            entityId: requestId);

    public static NotificationCommand ClassMentorRequestRejected(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null)
        => new(
            NotificationType.ClassMentorRequestRejected,
            NotificationAudience.ForUser(mentorId),
            "Mentor request rejected",
            string.IsNullOrWhiteSpace(className)
                ? "Your class assignment request was rejected."
                : $"Your request for class \"{className}\" was rejected.",
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            entityType: "ClassMentorRequest",
            entityId: requestId);

    // ── Class enrollment ──────────────────────────────────────────────────────

    public static NotificationCommand ClassEnrolled(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null)
        => new(
            NotificationType.ClassEnrolled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Enrolled in class",
            string.IsNullOrWhiteSpace(className)
                ? "You have been enrolled in a class."
                : $"You have been enrolled in \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId);

    public static NotificationCommand ClassTransferred(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null)
        => new(
            NotificationType.ClassTransferred,
            NotificationAudience.ForStudentAndParents(studentId),
            "Transferred to another class",
            string.IsNullOrWhiteSpace(className)
                ? "You have been transferred to another class."
                : $"You have been transferred to \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId);

    // ── Class session ─────────────────────────────────────────────────────────

    public static NotificationCommand ClassSessionScheduled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => new(
            NotificationType.ClassSessionScheduled,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Session scheduled",
            "A new class session has been scheduled.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            },
            entityType: "ClassSession",
            entityId: classSessionId);

    public static NotificationCommand ClassSessionRescheduled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => new(
            NotificationType.ClassSessionRescheduled,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Session rescheduled",
            "A class session has been rescheduled.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            },
            entityType: "ClassSession",
            entityId: classSessionId);

    public static NotificationCommand ClassSessionStarted(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => new(
            NotificationType.ClassSessionStarted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Session started",
            "A class session has started.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            },
            entityType: "ClassSession",
            entityId: classSessionId);

    public static NotificationCommand ClassSessionCompleted(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => new(
            NotificationType.ClassSessionCompleted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Session completed",
            "A class session has been completed.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            },
            entityType: "ClassSession",
            entityId: classSessionId);

    public static NotificationCommand ClassSessionCancelled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => new(
            NotificationType.ClassSessionCancelled,
            NotificationAudience.ForClassRosterAndMentor(classId),
            "Session cancelled",
            "A class session has been cancelled.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            },
            entityType: "ClassSession",
            entityId: classSessionId);

    // ── Attendance ────────────────────────────────────────────────────────────

    public static NotificationCommand AttendanceMarked(
        AttendanceStatus status,
        Guid studentId,
        Guid classSessionId,
        Guid? classId = null,
        Guid? actorUserId = null)
    {
        var (type, title, body) = status switch
        {
            AttendanceStatus.Present => (
                NotificationType.AttendanceMarkedPresent,
                "Marked present",
                "You were marked present for a class session."),
            AttendanceStatus.Late => (
                NotificationType.AttendanceMarkedLate,
                "Marked late",
                "You were marked late for a class session."),
            AttendanceStatus.Absent => (
                NotificationType.AttendanceMarkedAbsent,
                "Marked absent",
                "You were marked absent for a class session."),
            AttendanceStatus.Excused => (
                NotificationType.AttendanceMarkedExcused,
                "Marked excused",
                "You were marked excused for a class session."),
            _ => (
                NotificationType.AttendanceMarkedPresent,
                "Attendance updated",
                "Your attendance was updated for a class session.")
        };

        return new NotificationCommand(
            type,
            NotificationAudience.ForStudentAndParents(studentId),
            title,
            body,
            payload: new NotificationPayload
            {
                ClassSessionId = classSessionId,
                ClassId = classId,
                StudentId = studentId
            },
            actorUserId: actorUserId,
            entityType: "ClassSession",
            entityId: classSessionId);
    }

    // ── Grading / Quiz ────────────────────────────────────────────────────────

    public static NotificationCommand QuizGraded(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        bool passed,
        Guid? programId = null,
        string? assignmentTitle = null)
        => new(
            passed ? NotificationType.QuizPassed : NotificationType.QuizFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Quiz passed" : "Quiz needs attention",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed ? "You passed a quiz." : "A quiz was not passed.")
                : (passed
                    ? $"You passed \"{assignmentTitle}\"."
                    : $"\"{assignmentTitle}\" was not passed."),
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Submission",
            entityId: submissionId);

    public static NotificationCommand ResearchGraded(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        bool passed,
        Guid? programId = null,
        string? assignmentTitle = null)
        => new(
            passed ? NotificationType.ResearchGradedPassed : NotificationType.ResearchGradedFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Assignment passed" : "Assignment needs attention",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed ? "Your research submission was graded as passed." : "Your research submission was graded and needs attention.")
                : $"Your submission for \"{assignmentTitle}\" was graded.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Submission",
            entityId: submissionId);

    public static NotificationCommand ResearchReturnedForRevision(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? actorUserId = null)
        => new(
            NotificationType.ResearchReturnedForRevision,
            NotificationAudience.ForStudentAndParents(studentId),
            "Submission returned for revision",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Your research submission was returned for revision."
                : $"Your submission for \"{assignmentTitle}\" was returned for revision.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            actorUserId: actorUserId,
            entityType: "Submission",
            entityId: submissionId);

    public static NotificationCommand ResearchSubmissionOpened(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? programId = null)
        => new(
            NotificationType.ResearchSubmissionOpened,
            NotificationAudience.ForUser(studentId),
            "Research submission opened",
            "You can now work on your research submission.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            },
            entityType: "Submission",
            entityId: submissionId);

    public static NotificationCommand ResearchWorkSubmitted(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? classId = null,
        Guid? programId = null,
        string? assignmentTitle = null)
        => new(
            NotificationType.ResearchWorkSubmitted,
            classId.HasValue
                ? NotificationAudience.ForClassMentor(classId.Value)
                : NotificationAudience.ForUser(studentId),
            "Research work submitted",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "A student submitted research work for review."
                : $"Research work was submitted for \"{assignmentTitle}\".",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                StudentId = studentId
            },
            actorUserId: studentId,
            entityType: "Submission",
            entityId: submissionId);

    // ── Media ─────────────────────────────────────────────────────────────────

    public static NotificationCommand MediaVideoReady(Guid uploaderUserId, Guid mediaAssetId)
        => new(
            NotificationType.MediaVideoReady,
            NotificationAudience.ForUser(uploaderUserId),
            "Video ready",
            "Your video has finished processing and is ready.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaProcessingFailed(Guid uploaderUserId, Guid mediaAssetId)
        => new(
            NotificationType.MediaProcessingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "Video processing failed",
            "Video processing failed. Please try uploading again.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaAiTaggingFailed(Guid uploaderUserId, Guid mediaAssetId)
        => new(
            NotificationType.MediaAiTaggingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "AI tagging failed",
            "Automatic tagging for your video failed.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaTagsProcessed(Guid uploaderUserId, Guid mediaAssetId)
        => new(
            NotificationType.MediaTagsProcessed,
            NotificationAudience.ForUser(uploaderUserId),
            "Video tags ready",
            "AI tags for your video are ready.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    // ── Highlight video ───────────────────────────────────────────────────────

    public static NotificationCommand HighlightVideoGenerationQueued(Guid studentId, Guid highlightVideoId)
        => new(
            NotificationType.HighlightVideoGenerationQueued,
            NotificationAudience.ForUser(studentId),
            "Highlight video queued",
            "Your personal highlight video generation has been queued.",
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            },
            entityType: "HighlightVideo",
            entityId: highlightVideoId);

    public static NotificationCommand HighlightVideoReady(Guid studentId, Guid highlightVideoId)
        => new(
            NotificationType.HighlightVideoReady,
            NotificationAudience.ForStudentAndParents(studentId),
            "Highlight video ready",
            "Your personal highlight video is ready.",
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            },
            entityType: "HighlightVideo",
            entityId: highlightVideoId);

    public static NotificationCommand HighlightVideoGenerationFailed(Guid studentId, Guid highlightVideoId)
        => new(
            NotificationType.HighlightVideoGenerationFailed,
            NotificationAudience.ForUser(studentId),
            "Highlight video failed",
            "Generating your personal highlight video failed.",
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            },
            entityType: "HighlightVideo",
            entityId: highlightVideoId);

    // ── Catalog ───────────────────────────────────────────────────────────────

    public static NotificationCommand AssignmentPublished(
        Guid classId,
        Guid assignmentId,
        Guid? programId = null,
        string? assignmentTitle = null)
        => new(
            NotificationType.AssignmentPublished,
            NotificationAudience.ForClassRoster(classId),
            "New assignment published",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "A new assignment is available."
                : $"Assignment \"{assignmentTitle}\" is now available.",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId
            },
            entityType: "Assignment",
            entityId: assignmentId);

    public static NotificationCommand MaterialUpdated(
        Guid classId,
        Guid materialId,
        Guid? activityId = null,
        Guid? programId = null,
        string? materialTitle = null)
        => new(
            NotificationType.MaterialUpdated,
            NotificationAudience.ForClassRoster(classId),
            "Material updated",
            string.IsNullOrWhiteSpace(materialTitle)
                ? "Course material was updated."
                : $"Material \"{materialTitle}\" was updated.",
            payload: new NotificationPayload
            {
                MaterialId = materialId,
                ActivityId = activityId,
                ClassId = classId,
                ProgramId = programId
            },
            entityType: "Material",
            entityId: materialId);

    // ── Mentor curriculum edits ───────────────────────────────────────────────

    public static NotificationCommand AssignmentEditedByMentor(
        Guid assignmentId,
        Guid mentorId,
        Guid programId,
        string assignmentTitle)
        => new(
            NotificationType.AssignmentEditedByMentor,
            NotificationAudience.ForManagers(),
            "Assignment edited by mentor",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "A mentor updated assignment details."
                : $"Mentor updated assignment \"{assignmentTitle}\".",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ProgramId = programId
            },
            actorUserId: mentorId,
            entityType: "Assignment",
            entityId: assignmentId);

    public static NotificationCommand ClassQuizSetEditedByMentor(
        Guid assignmentId,
        Guid classId,
        Guid mentorId,
        Guid programId,
        string action,
        string? detail = null)
        => new(
            NotificationType.ClassQuizSetEditedByMentor,
            NotificationAudience.ForManagers(),
            "Class quiz set edited by mentor",
            string.IsNullOrWhiteSpace(detail)
                ? $"A mentor {action} for a class quiz."
                : $"A mentor {action}: {detail}",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                Extra = action
            },
            actorUserId: mentorId,
            entityType: "ClassQuizQuestionSet",
            entityId: assignmentId);
}
