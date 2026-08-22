using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Static factories for consistent notification type, audience, role copy, tokens, and payload.
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
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Parent link requested",
                "A parent–student link request for {studentName} is waiting for verification."),
            payload: new NotificationPayload { StudentId = studentId },
            actorUserId: actorUserId,
            entityType: "ParentStudent",
            entityId: studentId);

    public static NotificationCommand ParentLinkVerified(Guid parentId, Guid studentId)
        => new(
            NotificationType.ParentLinkVerified,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Parent link verified",
                "Your link with {studentName} has been verified."),
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
        => StudentAndParent(
            NotificationType.ProgramPendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment required",
            string.IsNullOrWhiteSpace(programName)
                ? "Complete payment to activate your program enrollment."
                : "Complete payment to activate enrollment in \"{programName}\".",
            string.IsNullOrWhiteSpace(programName)
                ? "Complete payment to activate {studentName}'s program enrollment."
                : "Complete payment to activate {studentName}'s enrollment in \"{programName}\".",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId,
            tokens: NotificationTokenKeys.Create(programName: programName));

    public static NotificationCommand ProgramActivated(
        Guid studentId,
        Guid programId,
        Guid programEnrollmentId,
        string? programName = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ProgramActivated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Program enrollment activated",
            string.IsNullOrWhiteSpace(programName)
                ? "Your program enrollment is now active."
                : "Your enrollment in \"{programName}\" is now active.",
            string.IsNullOrWhiteSpace(programName)
                ? "{studentName}'s program enrollment is now active."
                : "{studentName}'s enrollment in \"{programName}\" is now active.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId,
            tokens: NotificationTokenKeys.Create(programName: programName));

    public static NotificationCommand ModuleCompleted(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ModuleCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module completed",
            string.IsNullOrWhiteSpace(moduleName)
                ? "You completed a module."
                : "You completed \"{moduleName}\".",
            string.IsNullOrWhiteSpace(moduleName)
                ? "{studentName} completed a module."
                : "{studentName} completed \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand ModuleFailed(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? assignmentId = null)
        => StudentAndParent(
            NotificationType.ModuleFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module failed",
            string.IsNullOrWhiteSpace(moduleName)
                ? "A module attempt was marked as failed due to excess absences. A retake is required."
                : "Module \"{moduleName}\" was marked as failed due to excess absences. A retake is required.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "{studentName}'s module attempt was marked as failed due to excess absences. A retake is required."
                : "{studentName}'s module \"{moduleName}\" was marked as failed due to excess absences. A retake is required.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                AssignmentId = assignmentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand ModuleUnlocked(
        Guid studentId,
        Guid moduleId,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ModuleUnlocked,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module unlocked",
            string.IsNullOrWhiteSpace(moduleName)
                ? "A new module is now available."
                : "Module \"{moduleName}\" is now available.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "A new module is now available for {studentName}."
                : "Module \"{moduleName}\" is now available for {studentName}.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand ModuleRetakePendingPayment(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.ModuleRetakePendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Retake payment required",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Complete payment to retake this module."
                : "Complete payment to retake \"{moduleName}\".",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Complete payment so {studentName} can retake this module."
                : "Complete payment so {studentName} can retake \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand ModuleRetakeInitiated(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        Guid? assignmentId = null)
        => StudentAndParent(
            NotificationType.ModuleRetakeInitiated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Module retake started",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Your module retake has been initiated."
                : "Retake of \"{moduleName}\" has been initiated.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "{studentName}'s module retake has been initiated."
                : "Retake of \"{moduleName}\" has been initiated for {studentName}.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId,
                AssignmentId = assignmentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand PendingPaymentExpired(
        Guid studentId,
        Guid programEnrollmentId,
        Guid? programId = null)
        => StudentAndParent(
            NotificationType.PendingPaymentExpired,
            NotificationAudience.ForStudentAndParents(studentId),
            "Pending enrollment expired",
            "Your pending program enrollment expired because payment was not completed in time.",
            "{studentName}'s pending program enrollment expired because payment was not completed in time.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId);

    public static NotificationCommand ActivityCompleted(
        Guid studentId,
        Guid activityId,
        Guid? moduleId = null,
        Guid? programId = null,
        string? activityName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        Guid? courseId = null)
        => StudentAndParent(
            NotificationType.ActivityCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Activity completed",
            string.IsNullOrWhiteSpace(activityName)
                ? "You completed an activity."
                : "You completed \"{activityName}\".",
            string.IsNullOrWhiteSpace(activityName)
                ? "{studentName} completed an activity."
                : "{studentName} completed \"{activityName}\".",
            payload: new NotificationPayload
            {
                ActivityId = activityId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                CourseId = courseId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Activity",
            entityId: activityId,
            tokens: NotificationTokenKeys.Create(activityName: activityName));

    // ── Payment ───────────────────────────────────────────────────────────────

    public static NotificationCommand PaymentSucceeded(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.PaymentSucceeded,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment succeeded",
            "Your payment was successful.",
            "{studentName}'s payment was successful.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Payment",
            entityId: paymentId);

    public static NotificationCommand PaymentFailed(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.PaymentFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment failed",
            "Your payment could not be completed. Please try again.",
            "{studentName}'s payment could not be completed. Please try again.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Payment",
            entityId: paymentId);

    public static NotificationCommand PaymentCancelled(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.PaymentCancelled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Payment cancelled",
            "Your payment was cancelled.",
            "{studentName}'s payment was cancelled.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
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
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Payment request from student",
                "{studentName} requested that you complete a program payment."),
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId),
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId);

    public static NotificationCommand ParentModuleRetakeRequested(
        Guid parentId,
        Guid studentId,
        Guid paymentRequestId,
        Guid? moduleId = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => new(
            NotificationType.ParentModuleRetakeRequested,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Module retake payment request",
                "{studentName} requested that you complete a module retake payment."),
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId),
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId);

    // ── Class lifecycle ───────────────────────────────────────────────────────

    public static NotificationCommand ClassCreated(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassCreated,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Class created",
                string.IsNullOrWhiteSpace(className)
                    ? "A new class was created."
                    : "Class \"{className}\" was created."),
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassUpdated(Guid classId, Guid programId, string? className = null)
        => StudentParentMentor(
            NotificationType.ClassUpdated,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Class updated",
            string.IsNullOrWhiteSpace(className)
                ? "Class details were updated."
                : "Class \"{className}\" was updated.",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName}'s class details were updated."
                : "{studentName}'s class \"{className}\" was updated.",
            string.IsNullOrWhiteSpace(className)
                ? "Class details were updated."
                : "Class \"{className}\" was updated.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassOpenForEnrollment(Guid classId, Guid programId, string? className = null)
        => new(
            NotificationType.ClassOpenForEnrollment,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Class open for enrollment",
                string.IsNullOrWhiteSpace(className)
                    ? "A class is now open for enrollment."
                    : "Class \"{className}\" is now open for enrollment."),
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassStarted(Guid classId, Guid programId, string? className = null)
        => StudentParentMentor(
            NotificationType.ClassStarted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Class started",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has started."
                : "Class \"{className}\" has started.",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName}'s class has started."
                : "{studentName}'s class \"{className}\" has started.",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has started."
                : "Class \"{className}\" has started.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassAutoStarted(Guid classId, Guid programId, string? className = null)
        => StudentParentMentor(
            NotificationType.ClassAutoStarted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Class auto-started",
            string.IsNullOrWhiteSpace(className)
                ? "Your class was automatically started."
                : "Class \"{className}\" was automatically started.",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName}'s class was automatically started."
                : "{studentName}'s class \"{className}\" was automatically started.",
            string.IsNullOrWhiteSpace(className)
                ? "Your class was automatically started."
                : "Class \"{className}\" was automatically started.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassCompleted(Guid classId, Guid programId, string? className = null)
        => StudentParentMentor(
            NotificationType.ClassCompleted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Class completed",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has been completed."
                : "Class \"{className}\" has been completed.",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName}'s class has been completed."
                : "{studentName}'s class \"{className}\" has been completed.",
            string.IsNullOrWhiteSpace(className)
                ? "Your class has been completed."
                : "Class \"{className}\" has been completed.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId },
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className));

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
            NotificationRoleTemplates.FromDefault(
                "Mentor request submitted",
                string.IsNullOrWhiteSpace(className)
                    ? "A mentor requested assignment to a class."
                    : "A mentor requested assignment to class \"{className}\"."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            actorUserId: mentorId,
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassMentorRequestApproved(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null)
        => new(
            NotificationType.ClassMentorRequestApproved,
            NotificationAudience.ForUser(mentorId),
            NotificationRoleTemplates.FromDefault(
                "Mentor request approved",
                string.IsNullOrWhiteSpace(className)
                    ? "Your class assignment request was approved."
                    : "Your request for class \"{className}\" was approved."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassMentorRequestRejected(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null)
        => new(
            NotificationType.ClassMentorRequestRejected,
            NotificationAudience.ForUser(mentorId),
            NotificationRoleTemplates.FromDefault(
                "Mentor request rejected",
                string.IsNullOrWhiteSpace(className)
                    ? "Your class assignment request was rejected."
                    : "Your request for class \"{className}\" was rejected."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            },
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(className: className));

    // ── Assessment recovery ───────────────────────────────────────────────────

    public static NotificationCommand AssessmentRecoveryRequested(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        Guid moduleId,
        Guid? classId,
        string? assignmentTitle = null)
        => new(
            NotificationType.AssessmentRecoveryRequested,
            classId.HasValue
                ? NotificationAudience.ForClassMentor(classId.Value)
                : NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Assessment recovery requested",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "{actorName} requested another attempt on an assignment."
                    : "{actorName} requested another attempt on \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                StudentId = studentId,
                AssignmentId = assignmentId,
                ModuleId = moduleId,
                ClassId = classId,
            },
            actorUserId: studentId,
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    public static NotificationCommand AssessmentRecoveryApproved(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        int extraAttempts,
        string? assignmentTitle = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.AssessmentRecoveryApproved,
            NotificationAudience.ForStudentAndParents(studentId),
            "Assessment recovery approved",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Your recovery request was approved (+{extraAttempts} attempt(s))."
                : "Recovery for \"{assignmentTitle}\" was approved (+{extraAttempts} attempt(s)).",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "{studentName}'s recovery request was approved (+{extraAttempts} attempt(s))."
                : "Recovery for \"{assignmentTitle}\" was approved for {studentName} (+{extraAttempts} attempt(s)).",
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                assignmentTitle: assignmentTitle,
                extraAttempts: extraAttempts.ToString()));

    public static NotificationCommand AssessmentRecoveryRejected(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.AssessmentRecoveryRejected,
            NotificationAudience.ForStudentAndParents(studentId),
            "Assessment recovery rejected",
            "Your assessment recovery request was rejected.",
            "{studentName}'s assessment recovery request was rejected.",
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId);

    // ── Class re-delivery ─────────────────────────────────────────────────────

    public static NotificationCommand ClassRedeliveryPendingManager(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid programId,
        string? moduleName = null)
        => new(
            NotificationType.ClassRedeliveryPendingManager,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Class re-delivery needs decision",
                string.IsNullOrWhiteSpace(moduleName)
                    ? "No eligible class found; a re-delivery request needs manager action."
                    : "No eligible class for re-delivery of \"{moduleName}\"; manager action needed."),
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ProgramId = programId,
            },
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryMatchedPendingPayment(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid targetClassId,
        Guid retakeModuleEnrollmentId,
        string? moduleName = null,
        string? className = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryMatchedPendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Pay retake fee to join another class",
            string.IsNullOrWhiteSpace(className)
                ? "A class was matched for re-delivery. Complete retake payment to transfer."
                : "Matched class \"{className}\" for re-delivery of \"{moduleName}\". Complete payment to transfer.",
            string.IsNullOrWhiteSpace(className)
                ? "A class was matched for {studentName}'s re-delivery. Complete retake payment to transfer."
                : "Matched class \"{className}\" for re-delivery of \"{moduleName}\" for {studentName}. Complete payment to transfer.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ClassId = targetClassId,
                ModuleEnrollmentId = retakeModuleEnrollmentId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(moduleName: moduleName, className: className));

    public static NotificationCommand ClassRedeliveryRejected(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid? programId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryRejected,
            NotificationAudience.ForStudentAndParents(studentId),
            "Class re-delivery rejected",
            "Your class re-delivery request was rejected.",
            "{studentName}'s class re-delivery request was rejected.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId);

    public static NotificationCommand ClassRedeliveryCompleted(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid targetClassId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Class re-delivery completed",
            "You have been transferred for module re-delivery.",
            "{studentName} has been transferred for module re-delivery.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ClassId = targetClassId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId);

    // ── Class enrollment ──────────────────────────────────────────────────────

    public static NotificationCommand ClassEnrolled(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ClassEnrolled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Enrolled in class",
            string.IsNullOrWhiteSpace(className)
                ? "You have been enrolled in a class."
                : "You have been enrolled in \"{className}\".",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName} has been enrolled in a class."
                : "{studentName} has been enrolled in \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId,
            tokens: NotificationTokenKeys.Create(className: className));

    public static NotificationCommand ClassTransferred(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null)
        => StudentAndParent(
            NotificationType.ClassTransferred,
            NotificationAudience.ForStudentAndParents(studentId),
            "Transferred to another class",
            string.IsNullOrWhiteSpace(className)
                ? "You have been transferred to another class."
                : "You have been transferred to \"{className}\".",
            string.IsNullOrWhiteSpace(className)
                ? "{studentName} has been transferred to another class."
                : "{studentName} has been transferred to \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId),
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId,
            tokens: NotificationTokenKeys.Create(className: className));

    // ── Class session ─────────────────────────────────────────────────────────

    public static NotificationCommand ClassSessionScheduled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null)
        => StudentParentMentor(
            NotificationType.ClassSessionScheduled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Session scheduled",
            "A new class session has been scheduled.",
            "A new class session has been scheduled for {studentName}.",
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
        => StudentParentMentor(
            NotificationType.ClassSessionRescheduled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Session rescheduled",
            "A class session has been rescheduled.",
            "A class session has been rescheduled for {studentName}.",
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
        => StudentParentMentor(
            NotificationType.ClassSessionCancelled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Session cancelled",
            "A class session has been cancelled.",
            "A class session has been cancelled for {studentName}.",
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
        Guid? actorUserId = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? activityId = null)
    {
        var (type, title, studentBody, parentBody) = status switch
        {
            AttendanceStatus.Present => (
                NotificationType.AttendanceMarkedPresent,
                "Marked present",
                "You were marked present for a class session.",
                "{studentName} was marked present for a class session."),
            AttendanceStatus.Late => (
                NotificationType.AttendanceMarkedLate,
                "Marked late",
                "You were marked late for a class session.",
                "{studentName} was marked late for a class session."),
            AttendanceStatus.Absent => (
                NotificationType.AttendanceMarkedAbsent,
                "Marked absent",
                "You were marked absent for a class session.",
                "{studentName} was marked absent for a class session."),
            AttendanceStatus.Excused => (
                NotificationType.AttendanceMarkedExcused,
                "Marked excused",
                "You were marked excused for a class session.",
                "{studentName} was marked excused for a class session."),
            _ => (
                NotificationType.AttendanceMarkedPresent,
                "Attendance updated",
                "Your attendance was updated for a class session.",
                "{studentName}'s attendance was updated for a class session.")
        };

        return StudentAndParent(
            type,
            NotificationAudience.ForStudentAndParents(studentId),
            title,
            studentBody,
            parentBody,
            payload: new NotificationPayload
            {
                ClassSessionId = classSessionId,
                ClassId = classId,
                StudentId = studentId,
                ProgramId = programId,
                ActivityId = activityId
            }.SetEnrollment(programEnrollmentId),
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
        string? assignmentTitle = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            passed ? NotificationType.QuizPassed : NotificationType.QuizFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Quiz passed" : "Quiz needs attention",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed ? "You passed a quiz." : "A quiz was not passed.")
                : (passed
                    ? "You passed \"{assignmentTitle}\"."
                    : "\"{assignmentTitle}\" was not passed."),
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed ? "{studentName} passed a quiz." : "{studentName} did not pass a quiz.")
                : (passed
                    ? "{studentName} passed \"{assignmentTitle}\"."
                    : "{studentName} did not pass \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    public static NotificationCommand ResearchGraded(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        bool passed,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            passed ? NotificationType.ResearchGradedPassed : NotificationType.ResearchGradedFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Assignment passed" : "Assignment needs attention",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed
                    ? "Your research submission was graded as passed."
                    : "Your research submission was graded and needs attention.")
                : "Your submission for \"{assignmentTitle}\" was graded.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed
                    ? "{studentName}'s research submission was graded as passed."
                    : "{studentName}'s research submission was graded and needs attention.")
                : "{studentName}'s submission for \"{assignmentTitle}\" was graded.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    public static NotificationCommand ResearchReturnedForRevision(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? actorUserId = null,
        Guid? programEnrollmentId = null)
        => StudentAndParent(
            NotificationType.ResearchReturnedForRevision,
            NotificationAudience.ForStudentAndParents(studentId),
            "Submission returned for revision",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Your research submission was returned for revision."
                : "Your submission for \"{assignmentTitle}\" was returned for revision.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "{studentName}'s research submission was returned for revision."
                : "{studentName}'s submission for \"{assignmentTitle}\" was returned for revision.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId),
            actorUserId: actorUserId,
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

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
            NotificationRoleTemplates.FromDefault(
                "Research work submitted",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "{actorName} submitted research work for review."
                    : "Research work was submitted for \"{assignmentTitle}\"."),
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
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    // ── Media ─────────────────────────────────────────────────────────────────

    public static NotificationCommand MediaVideoReady(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null)
        => new(
            NotificationType.MediaVideoReady,
            NotificationAudience.ForUser(uploaderUserId),
            "Video ready",
            "Your video has finished processing and is ready.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaProcessingFailed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null)
        => new(
            NotificationType.MediaProcessingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "Video processing failed",
            "Video processing failed. Please try uploading again.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaAiTaggingFailed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null)
        => new(
            NotificationType.MediaAiTaggingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "AI tagging failed",
            "Automatic tagging for your video failed.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId },
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaTagsProcessed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null)
        => new(
            NotificationType.MediaTagsProcessed,
            NotificationAudience.ForUser(uploaderUserId),
            "Video tags ready",
            "AI tags for your video are ready.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId },
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
        => StudentAndParent(
            NotificationType.HighlightVideoReady,
            NotificationAudience.ForStudentAndParents(studentId),
            "Highlight video ready",
            "Your personal highlight video is ready.",
            "{studentName}'s personal highlight video is ready.",
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
        string? assignmentTitle = null,
        Guid? moduleId = null)
        => StudentAndParent(
            NotificationType.AssignmentPublished,
            NotificationAudience.ForClassRosterAndParents(classId),
            "New assignment published",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "A new assignment is available."
                : "Assignment \"{assignmentTitle}\" is now available.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "A new assignment is available for {studentName}."
                : "Assignment \"{assignmentTitle}\" is now available for {studentName}.",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                ModuleId = moduleId
            },
            entityType: "Assignment",
            entityId: assignmentId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    public static NotificationCommand MaterialUpdated(
        Guid classId,
        Guid materialId,
        Guid? activityId = null,
        Guid? programId = null,
        string? materialTitle = null,
        Guid? courseId = null)
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
                ProgramId = programId,
                CourseId = courseId
            },
            entityType: "Material",
            entityId: materialId);

    // ── Mentor curriculum edits ───────────────────────────────────────────────

    public static NotificationCommand AssignmentEditedByMentor(
        Guid assignmentId,
        Guid mentorId,
        Guid programId,
        string assignmentTitle,
        Guid? moduleId = null)
        => new(
            NotificationType.AssignmentEditedByMentor,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Assignment edited by mentor",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "A mentor updated assignment details."
                    : "Mentor updated assignment \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ProgramId = programId,
                ModuleId = moduleId
            },
            actorUserId: mentorId,
            entityType: "Assignment",
            entityId: assignmentId,
            tokens: NotificationTokenKeys.Create(assignmentTitle: assignmentTitle));

    public static NotificationCommand ClassQuizSetEditedByMentor(
        Guid assignmentId,
        Guid classId,
        Guid mentorId,
        Guid programId,
        string action,
        string? detail = null,
        Guid? moduleId = null)
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
                ModuleId = moduleId,
                Extra = action
            },
            actorUserId: mentorId,
            entityType: "ClassQuizQuestionSet",
            entityId: assignmentId);

    private static NotificationCommand StudentAndParent(
        NotificationType type,
        NotificationAudience audience,
        string title,
        string studentBody,
        string parentBody,
        NotificationPayload? payload = null,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        IReadOnlyDictionary<string, string>? tokens = null)
        => new(
            type,
            audience,
            NotificationRoleTemplates.ForStudentAndParent(title, studentBody, parentBody),
            payload,
            actorUserId,
            entityType,
            entityId,
            tokens);

    private static NotificationCommand StudentParentMentor(
        NotificationType type,
        NotificationAudience audience,
        string title,
        string studentBody,
        string parentBody,
        string mentorBody,
        NotificationPayload? payload = null,
        Guid? actorUserId = null,
        string? entityType = null,
        Guid? entityId = null,
        IReadOnlyDictionary<string, string>? tokens = null)
        => new(
            type,
            audience,
            NotificationRoleTemplates.ForStudentParentAndMentor(title, studentBody, parentBody, mentorBody),
            payload,
            actorUserId,
            entityType,
            entityId,
            tokens);
}
