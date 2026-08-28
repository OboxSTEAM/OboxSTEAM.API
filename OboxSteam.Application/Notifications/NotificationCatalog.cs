using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Static factories for consistent notification type, audience, role copy, tokens, and payload.
/// Business services should call these methods instead of building raw <see cref="NotificationCommand"/>s.
/// Student copy addresses the learner as "bạn". Parent copy names the child as "con bạn {studentName}".
/// </summary>
public static class NotificationCatalog
{
    // ── Account ──────────────────────────────────────────────────────────────

    public static NotificationCommand AccountRegistered(Guid userId)
        => new(
            NotificationType.AccountRegistered,
            NotificationAudience.ForUser(userId),
            "Chào mừng đến với OboxSTEAM",
            "Tài khoản của bạn đã được tạo. Hãy xác minh email để bắt đầu.",
            entityType: "User",
            entityId: userId);

    public static NotificationCommand EmailVerified(Guid userId)
        => new(
            NotificationType.EmailVerified,
            NotificationAudience.ForUser(userId),
            "Đã xác minh email",
            "Địa chỉ email của bạn đã được xác minh thành công.",
            entityType: "User",
            entityId: userId);

    public static NotificationCommand PasswordChanged(Guid userId)
        => new(
            NotificationType.PasswordChanged,
            NotificationAudience.ForUser(userId),
            "Đã đổi mật khẩu",
            "Mật khẩu của bạn đã được thay đổi. Nếu không phải bạn thực hiện, hãy liên hệ hỗ trợ.",
            entityType: "User",
            entityId: userId);

    // ── Parent link ───────────────────────────────────────────────────────────

    public static NotificationCommand ParentLinkRequested(
        Guid parentId,
        Guid studentId,
        Guid? actorUserId = null,
        string? studentName = null,
        string? actorName = null)
        => new(
            NotificationType.ParentLinkRequested,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu liên kết phụ huynh",
                "Yêu cầu liên kết phụ huynh cho con bạn {studentName} đang chờ xác minh."),
            payload: new NotificationPayload { StudentId = studentId }
                .WithNames(studentName: studentName, actorName: actorName),
            actorUserId: actorUserId,
            entityType: "ParentStudent",
            entityId: studentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, actorName: actorName));

    public static NotificationCommand ParentLinkVerified(Guid parentId, Guid studentId, string? studentName = null)
        => new(
            NotificationType.ParentLinkVerified,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Đã xác minh liên kết phụ huynh",
                "Liên kết với con bạn {studentName} đã được xác minh."),
            payload: new NotificationPayload { StudentId = studentId }.WithNames(studentName: studentName),
            entityType: "ParentStudent",
            entityId: studentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName));

    public static NotificationCommand ParentLinkApproved(
        Guid studentId,
        Guid parentId,
        Guid? actorUserId = null,
        string? studentName = null,
        string? actorName = null)
        => new(
            NotificationType.ParentLinkApproved,
            NotificationAudience.ForUser(studentId),
            "Đã duyệt liên kết phụ huynh",
            "Một phụ huynh đã được liên kết với tài khoản của bạn.",
            payload: new NotificationPayload { StudentId = studentId }
                .WithNames(studentName: studentName, actorName: actorName),
            actorUserId: actorUserId,
            entityType: "ParentStudent",
            entityId: parentId);

    // ── Enrollment ────────────────────────────────────────────────────────────

    public static NotificationCommand ProgramPendingPayment(
        Guid studentId,
        Guid programId,
        Guid programEnrollmentId,
        string? programName = null,
        string? studentName = null)
        => StudentAndParent(
            NotificationType.ProgramPendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Cần thanh toán",
            string.IsNullOrWhiteSpace(programName)
                ? "Hoàn tất thanh toán để kích hoạt ghi danh chương trình của bạn."
                : "Hoàn tất thanh toán để kích hoạt ghi danh chương trình \"{programName}\".",
            string.IsNullOrWhiteSpace(programName)
                ? "Hoàn tất thanh toán để kích hoạt ghi danh chương trình của con bạn {studentName}."
                : "Hoàn tất thanh toán để kích hoạt ghi danh chương trình \"{programName}\" của con bạn {studentName}.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand ProgramActivated(
        Guid studentId,
        Guid programId,
        Guid programEnrollmentId,
        string? programName = null,
        Guid? nextActivityId = null,
        string? studentName = null)
        => StudentAndParent(
            NotificationType.ProgramActivated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã kích hoạt ghi danh chương trình",
            string.IsNullOrWhiteSpace(programName)
                ? "Ghi danh chương trình của bạn đã được kích hoạt."
                : "Ghi danh chương trình \"{programName}\" của bạn đã được kích hoạt.",
            string.IsNullOrWhiteSpace(programName)
                ? "Ghi danh chương trình của con bạn {studentName} đã được kích hoạt."
                : "Ghi danh chương trình \"{programName}\" của con bạn {studentName} đã được kích hoạt.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand ModuleCompleted(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ModuleCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Hoàn thành học phần",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Bạn đã hoàn thành một học phần."
                : "Bạn đã hoàn thành \"{moduleName}\".",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Con bạn {studentName} đã hoàn thành một học phần."
                : "Con bạn {studentName} đã hoàn thành \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ModuleFailed(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? assignmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ModuleFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            "Học phần không đạt",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Một lần học phần bị đánh dấu không đạt do vắng quá số buổi cho phép. Bạn cần học lại."
                : "Học phần \"{moduleName}\" bị đánh dấu không đạt do vắng quá số buổi cho phép. Bạn cần học lại.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Lần học phần của con bạn {studentName} bị đánh dấu không đạt do vắng quá số buổi cho phép. Cần học lại."
                : "Học phần \"{moduleName}\" của con bạn {studentName} bị đánh dấu không đạt do vắng quá số buổi cho phép. Cần học lại.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                AssignmentId = assignmentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ModuleUnlocked(
        Guid studentId,
        Guid moduleId,
        Guid? programId = null,
        string? moduleName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ModuleUnlocked,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã mở học phần",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Một học phần mới đã sẵn sàng."
                : "Học phần \"{moduleName}\" đã sẵn sàng.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Một học phần mới đã sẵn sàng cho con bạn {studentName}."
                : "Học phần \"{moduleName}\" đã sẵn sàng cho con bạn {studentName}.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Module",
            entityId: moduleId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ModuleRetakePendingPayment(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ModuleRetakePendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Cần thanh toán để học lại",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Hoàn tất thanh toán để học lại học phần này."
                : "Hoàn tất thanh toán để học lại \"{moduleName}\".",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Hoàn tất thanh toán để con bạn {studentName} học lại học phần này."
                : "Hoàn tất thanh toán để con bạn {studentName} học lại \"{moduleName}\".",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ModuleRetakeInitiated(
        Guid studentId,
        Guid moduleId,
        Guid? moduleEnrollmentId = null,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        Guid? assignmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ModuleRetakeInitiated,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã bắt đầu học lại học phần",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Lần học lại học phần của bạn đã được khởi tạo."
                : "Lần học lại \"{moduleName}\" đã được khởi tạo.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Lần học lại học phần của con bạn {studentName} đã được khởi tạo."
                : "Lần học lại \"{moduleName}\" của con bạn {studentName} đã được khởi tạo.",
            payload: new NotificationPayload
            {
                ModuleId = moduleId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId,
                AssignmentId = assignmentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ModuleEnrollment",
            entityId: moduleEnrollmentId ?? moduleId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand PendingPaymentExpired(
        Guid studentId,
        Guid programEnrollmentId,
        Guid? programId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.PendingPaymentExpired,
            NotificationAudience.ForStudentAndParents(studentId),
            "Ghi danh chờ thanh toán đã hết hạn",
            "Ghi danh chương trình đang chờ của bạn đã hết hạn vì chưa thanh toán kịp thời.",
            "Ghi danh chương trình đang chờ của con bạn {studentName} đã hết hạn vì chưa thanh toán kịp thời.",
            payload: new NotificationPayload
            {
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ProgramEnrollment",
            entityId: programEnrollmentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand ActivityCompleted(
        Guid studentId,
        Guid activityId,
        Guid? moduleId = null,
        Guid? programId = null,
        string? activityName = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        Guid? courseId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ActivityCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Hoàn thành hoạt động",
            string.IsNullOrWhiteSpace(activityName)
                ? "Bạn đã hoàn thành một hoạt động."
                : "Bạn đã hoàn thành \"{activityName}\".",
            string.IsNullOrWhiteSpace(activityName)
                ? "Con bạn {studentName} đã hoàn thành một hoạt động."
                : "Con bạn {studentName} đã hoàn thành \"{activityName}\".",
            payload: new NotificationPayload
            {
                ActivityId = activityId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                CourseId = courseId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Activity",
            entityId: activityId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                activityName: activityName));

    // ── Payment ───────────────────────────────────────────────────────────────

    public static NotificationCommand PaymentSucceeded(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.PaymentSucceeded,
            NotificationAudience.ForStudentAndParents(studentId),
            "Thanh toán thành công",
            "Thanh toán của bạn đã thành công.",
            "Thanh toán của con bạn {studentName} đã thành công.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Payment",
            entityId: paymentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand PaymentFailed(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.PaymentFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            "Thanh toán thất bại",
            "Thanh toán của bạn không thể hoàn tất. Vui lòng thử lại.",
            "Thanh toán của con bạn {studentName} không thể hoàn tất. Vui lòng thử lại.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Payment",
            entityId: paymentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand PaymentCancelled(
        Guid studentId,
        Guid paymentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.PaymentCancelled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã hủy thanh toán",
            "Thanh toán của bạn đã bị hủy.",
            "Thanh toán của con bạn {studentName} đã bị hủy.",
            payload: new NotificationPayload
            {
                PaymentId = paymentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Payment",
            entityId: paymentId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand ParentPaymentRequested(
        Guid parentId,
        Guid studentId,
        Guid paymentRequestId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => new(
            NotificationType.ParentPaymentRequested,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu thanh toán từ học viên",
                "Con bạn {studentName} đề nghị bạn hoàn tất thanh toán chương trình."),
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    public static NotificationCommand ParentModuleRetakeRequested(
        Guid parentId,
        Guid studentId,
        Guid paymentRequestId,
        Guid? moduleId = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => new(
            NotificationType.ParentModuleRetakeRequested,
            NotificationAudience.ForUser(parentId, studentId),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu thanh toán học lại học phần",
                "Con bạn {studentName} đề nghị bạn hoàn tất thanh toán để học lại học phần."),
            payload: new NotificationPayload
            {
                PaymentRequestId = paymentRequestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            actorUserId: studentId,
            entityType: "PaymentRequest",
            entityId: paymentRequestId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    // ── Class lifecycle ───────────────────────────────────────────────────────

    public static NotificationCommand ClassCreated(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassCreated,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Đã tạo lớp",
                string.IsNullOrWhiteSpace(className)
                    ? "Một lớp mới đã được tạo."
                    : "Lớp \"{className}\" đã được tạo."),
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassUpdated(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassUpdated,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Đã cập nhật lớp",
            string.IsNullOrWhiteSpace(className)
                ? "Thông tin lớp đã được cập nhật."
                : "Lớp \"{className}\" đã được cập nhật.",
            string.IsNullOrWhiteSpace(className)
                ? "Thông tin lớp của con bạn {studentName} đã được cập nhật."
                : "Lớp \"{className}\" của con bạn {studentName} đã được cập nhật.",
            string.IsNullOrWhiteSpace(className)
                ? "Thông tin lớp đã được cập nhật."
                : "Lớp \"{className}\" đã được cập nhật.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassOpenForEnrollment(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassOpenForEnrollment,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Lớp đã mở ghi danh",
                string.IsNullOrWhiteSpace(className)
                    ? "Một lớp hiện đã mở ghi danh."
                    : "Lớp \"{className}\" hiện đã mở ghi danh."),
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassStarted(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassStarted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Lớp đã bắt đầu",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã bắt đầu."
                : "Lớp \"{className}\" đã bắt đầu.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của con bạn {studentName} đã bắt đầu."
                : "Lớp \"{className}\" của con bạn {studentName} đã bắt đầu.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã bắt đầu."
                : "Lớp \"{className}\" đã bắt đầu.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassAutoStarted(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassAutoStarted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Lớp được tự động bắt đầu",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã được tự động bắt đầu."
                : "Lớp \"{className}\" đã được tự động bắt đầu.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của con bạn {studentName} đã được tự động bắt đầu."
                : "Lớp \"{className}\" của con bạn {studentName} đã được tự động bắt đầu.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã được tự động bắt đầu."
                : "Lớp \"{className}\" đã được tự động bắt đầu.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassCompleted(
        Guid classId,
        Guid programId,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassCompleted,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Lớp đã hoàn thành",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã hoàn thành."
                : "Lớp \"{className}\" đã hoàn thành.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của con bạn {studentName} đã hoàn thành."
                : "Lớp \"{className}\" của con bạn {studentName} đã hoàn thành.",
            string.IsNullOrWhiteSpace(className)
                ? "Lớp của bạn đã hoàn thành."
                : "Lớp \"{className}\" đã hoàn thành.",
            payload: new NotificationPayload { ClassId = classId, ProgramId = programId }
                .WithNames(className: className, programName: programName),
            entityType: "Class",
            entityId: classId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    // ── Class mentor assignment ───────────────────────────────────────────────

    public static NotificationCommand ClassMentorRequestSubmitted(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null,
        string? programName = null,
        string? actorName = null)
        => new(
            NotificationType.ClassMentorRequestSubmitted,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Mentor gửi yêu cầu nhận lớp",
                string.IsNullOrWhiteSpace(className)
                    ? "Một mentor đã yêu cầu được phân công lớp."
                    : "Một mentor đã yêu cầu được phân công lớp \"{className}\"."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            }.WithNames(actorName: actorName, className: className, programName: programName),
            actorUserId: mentorId,
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(actorName: actorName, className: className, programName: programName));

    public static NotificationCommand ClassMentorRequestApproved(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassMentorRequestApproved,
            NotificationAudience.ForUser(mentorId),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu nhận lớp đã được duyệt",
                string.IsNullOrWhiteSpace(className)
                    ? "Yêu cầu nhận lớp của bạn đã được duyệt."
                    : "Yêu cầu nhận lớp \"{className}\" của bạn đã được duyệt."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            }.WithNames(className: className, programName: programName),
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassMentorRequestRejected(
        Guid requestId,
        Guid classId,
        Guid programId,
        Guid mentorId,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassMentorRequestRejected,
            NotificationAudience.ForUser(mentorId),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu nhận lớp bị từ chối",
                string.IsNullOrWhiteSpace(className)
                    ? "Yêu cầu nhận lớp của bạn đã bị từ chối."
                    : "Yêu cầu nhận lớp \"{className}\" của bạn đã bị từ chối."),
            payload: new NotificationPayload
            {
                ClassMentorRequestId = requestId,
                ClassId = classId,
                ProgramId = programId,
            }.WithNames(className: className, programName: programName),
            entityType: "ClassMentorRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    // ── Assessment recovery ───────────────────────────────────────────────────

    public static NotificationCommand AssessmentRecoveryRequested(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        Guid moduleId,
        Guid? classId,
        string? assignmentTitle = null,
        string? studentName = null,
        string? actorName = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.AssessmentRecoveryRequested,
            classId.HasValue
                ? NotificationAudience.ForClassMentor(classId.Value)
                : NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu làm lại bài đánh giá",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "{actorName} đã yêu cầu thêm lượt làm bài tập."
                    : "{actorName} đã yêu cầu thêm lượt làm \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                StudentId = studentId,
                AssignmentId = assignmentId,
                ModuleId = moduleId,
                ClassId = classId,
            }.WithNames(studentName: studentName, actorName: actorName, className: className, programName: programName),
            actorUserId: studentId,
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                actorName: actorName,
                className: className,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand AssessmentRecoveryApproved(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        int extraAttempts,
        string? assignmentTitle = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.AssessmentRecoveryApproved,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã duyệt làm lại bài đánh giá",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Yêu cầu làm lại của bạn đã được duyệt (+{extraAttempts} lượt)."
                : "Yêu cầu làm lại \"{assignmentTitle}\" đã được duyệt (+{extraAttempts} lượt).",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Yêu cầu làm lại của con bạn {studentName} đã được duyệt (+{extraAttempts} lượt)."
                : "Yêu cầu làm lại \"{assignmentTitle}\" của con bạn {studentName} đã được duyệt (+{extraAttempts} lượt).",
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                assignmentTitle: assignmentTitle,
                extraAttempts: extraAttempts.ToString()));

    public static NotificationCommand AssessmentRecoveryRejected(
        Guid requestId,
        Guid studentId,
        Guid assignmentId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.AssessmentRecoveryRejected,
            NotificationAudience.ForStudentAndParents(studentId),
            "Yêu cầu làm lại bài đánh giá bị từ chối",
            "Yêu cầu làm lại bài đánh giá của bạn đã bị từ chối.",
            "Yêu cầu làm lại bài đánh giá của con bạn {studentName} đã bị từ chối.",
            payload: new NotificationPayload
            {
                AssessmentRecoveryRequestId = requestId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "AssessmentRecoveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(studentName: studentName, programName: programName));

    // ── Class re-delivery ─────────────────────────────────────────────────────

    public static NotificationCommand ClassRedeliveryPendingManager(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid programId,
        string? moduleName = null,
        string? studentName = null,
        string? programName = null)
        => new(
            NotificationType.ClassRedeliveryPendingManager,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Yêu cầu chuyển lớp học lại cần quyết định",
                string.IsNullOrWhiteSpace(moduleName)
                    ? "Không tìm thấy lớp phù hợp; yêu cầu chuyển lớp học lại cần quản lý xử lý."
                    : "Không tìm thấy lớp phù hợp để học lại \"{moduleName}\"; cần quản lý xử lý."),
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ProgramId = programId,
            }.WithNames(studentName: studentName, programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryMatchedPendingPayment(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid targetClassId,
        Guid retakeModuleEnrollmentId,
        string? moduleName = null,
        string? className = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryMatchedPendingPayment,
            NotificationAudience.ForStudentAndParents(studentId),
            "Thanh toán phí học lại để vào lớp khác",
            string.IsNullOrWhiteSpace(className)
                ? "Đã ghép một lớp để học lại. Hoàn tất thanh toán để chuyển lớp."
                : "Đã ghép lớp \"{className}\" để học lại \"{moduleName}\". Hoàn tất thanh toán để chuyển lớp.",
            string.IsNullOrWhiteSpace(className)
                ? "Đã ghép một lớp để con bạn {studentName} học lại. Hoàn tất thanh toán để chuyển lớp."
                : "Đã ghép lớp \"{className}\" để con bạn {studentName} học lại \"{moduleName}\". Hoàn tất thanh toán để chuyển lớp.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                StudentId = studentId,
                ModuleId = moduleId,
                ClassId = targetClassId,
                ModuleEnrollmentId = retakeModuleEnrollmentId,
                ProgramId = programId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryRejected(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryRejected,
            NotificationAudience.ForStudentAndParents(studentId),
            "Yêu cầu chuyển lớp học lại bị từ chối",
            "Yêu cầu chuyển lớp học lại của bạn đã bị từ chối.",
            "Yêu cầu chuyển lớp học lại của con bạn {studentName} đã bị từ chối.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId);

    public static NotificationCommand ClassRedeliveryCompleted(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid targetClassId,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null,
        string? className = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryCompleted,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã hoàn tất chuyển lớp học lại",
            "Bạn đã được chuyển lớp để học lại học phần.",
            "Con bạn {studentName} đã được chuyển lớp để học lại học phần.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ClassId = targetClassId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName));

    public static NotificationCommand ClassRedeliveryWithdrawn(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryWithdrawn,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã rút yêu cầu chuyển lớp học lại",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Yêu cầu chuyển lớp học lại của bạn đã được rút. Tiến độ được giữ và bạn có thể gửi lại sau."
                : "Yêu cầu học lại \"{moduleName}\" của bạn đã được rút. Tiến độ được giữ và bạn có thể gửi lại sau.",
            string.IsNullOrWhiteSpace(moduleName)
                ? "Yêu cầu chuyển lớp học lại của con bạn {studentName} đã được rút. Tiến độ được giữ và có thể gửi lại sau."
                : "Yêu cầu học lại \"{moduleName}\" của con bạn {studentName} đã được rút. Tiến độ được giữ và có thể gửi lại sau.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryAwaitingSelection(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        int candidateCount,
        string? moduleName = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryAwaitingSelection,
            NotificationAudience.ForStudentAndParents(studentId),
            "Chọn lớp để học lại học phần",
            string.IsNullOrWhiteSpace(moduleName)
                ? $"{candidateCount} lớp đủ điều kiện để học lại. Hãy chọn lớp phù hợp lịch của bạn."
                : $"{candidateCount} lớp đủ điều kiện để học lại \"{{moduleName}}\". Hãy chọn lớp phù hợp lịch của bạn.",
            string.IsNullOrWhiteSpace(moduleName)
                ? $"{candidateCount} lớp đủ điều kiện để con bạn {{studentName}} học lại."
                : $"{candidateCount} lớp đủ điều kiện để con bạn {{studentName}} học lại \"{{moduleName}}\".",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryIntensiveOffered(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid remedialClassId,
        string? moduleName = null,
        string? className = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryIntensiveOffered,
            NotificationAudience.ForStudentAndParents(studentId),
            "Có lớp bổ trợ — xác nhận lịch tăng cường",
            string.IsNullOrWhiteSpace(className)
                ? "Một lớp bổ trợ đã được mở cho học phần của bạn. Xác nhận bạn theo được lịch tăng cường, hoặc từ chối."
                : "Lớp bổ trợ \"{className}\" đã được mở để học lại \"{moduleName}\". Xác nhận bạn theo được lịch tăng cường, hoặc từ chối.",
            string.IsNullOrWhiteSpace(className)
                ? "Một lớp bổ trợ đã được mở cho học phần của con bạn {studentName}. Cần xác nhận lịch tăng cường."
                : "Lớp bổ trợ \"{className}\" đã được mở để con bạn {studentName} học lại \"{moduleName}\". Cần xác nhận lịch tăng cường.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ClassId = remedialClassId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName,
                moduleName: moduleName));

    public static NotificationCommand ClassRedeliveryCandidatesAvailable(
        Guid requestId,
        Guid studentId,
        Guid moduleId,
        Guid classId,
        string? moduleName = null,
        string? className = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassRedeliveryCandidatesAvailable,
            NotificationAudience.ForStudentAndParents(studentId),
            "Có lớp mới phù hợp yêu cầu học lại",
            string.IsNullOrWhiteSpace(className)
                ? "Một lớp mới hiện phù hợp yêu cầu học lại của bạn. Mở yêu cầu để chọn lớp."
                : "Lớp mới \"{className}\" hiện phù hợp yêu cầu học lại \"{moduleName}\" của bạn. Mở yêu cầu để chọn lớp.",
            string.IsNullOrWhiteSpace(className)
                ? "Một lớp mới hiện phù hợp yêu cầu học lại của con bạn {studentName}."
                : "Lớp mới \"{className}\" hiện phù hợp yêu cầu học lại \"{moduleName}\" của con bạn {studentName}.",
            payload: new NotificationPayload
            {
                ClassRedeliveryRequestId = requestId,
                ModuleId = moduleId,
                ClassId = classId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassRedeliveryRequest",
            entityId: requestId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName,
                moduleName: moduleName));

    // ── Class enrollment ──────────────────────────────────────────────────────

    public static NotificationCommand ClassEnrolled(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassEnrolled,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã ghi danh vào lớp",
            string.IsNullOrWhiteSpace(className)
                ? "Bạn đã được ghi danh vào một lớp."
                : "Bạn đã được ghi danh vào lớp \"{className}\".",
            string.IsNullOrWhiteSpace(className)
                ? "Con bạn {studentName} đã được ghi danh vào một lớp."
                : "Con bạn {studentName} đã được ghi danh vào lớp \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName));

    public static NotificationCommand ClassTransferred(
        Guid studentId,
        Guid classId,
        Guid classEnrollmentId,
        Guid? programId = null,
        string? className = null,
        Guid? programEnrollmentId = null,
        Guid? nextActivityId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ClassTransferred,
            NotificationAudience.ForStudentAndParents(studentId),
            "Đã chuyển sang lớp khác",
            string.IsNullOrWhiteSpace(className)
                ? "Bạn đã được chuyển sang lớp khác."
                : "Bạn đã được chuyển sang lớp \"{className}\".",
            string.IsNullOrWhiteSpace(className)
                ? "Con bạn {studentName} đã được chuyển sang lớp khác."
                : "Con bạn {studentName} đã được chuyển sang lớp \"{className}\".",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassEnrollmentId = classEnrollmentId,
                ProgramId = programId,
                StudentId = studentId,
                NextActivityId = nextActivityId,
                ActivityId = nextActivityId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                className: className,
                programName: programName),
            entityType: "ClassEnrollment",
            entityId: classEnrollmentId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                className: className,
                programName: programName));

    // ── Class session ─────────────────────────────────────────────────────────

    public static NotificationCommand ClassSessionScheduled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassSessionScheduled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Đã lên lịch buổi học",
            "Một buổi học mới đã được lên lịch.",
            "Một buổi học mới đã được lên lịch cho con bạn {studentName}.",
            "Một buổi học mới đã được lên lịch.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            }.WithNames(className: className, programName: programName),
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassSessionRescheduled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassSessionRescheduled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Đã đổi lịch buổi học",
            "Một buổi học đã được đổi lịch.",
            "Một buổi học của con bạn {studentName} đã được đổi lịch.",
            "Một buổi học đã được đổi lịch.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            }.WithNames(className: className, programName: programName),
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassSessionStarted(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassSessionStarted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            NotificationRoleTemplates.FromDefault(
                "Buổi học đã bắt đầu",
                "Một buổi học đã bắt đầu."),
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            }.WithNames(className: className, programName: programName),
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassSessionCompleted(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassSessionCompleted,
            NotificationAudience.ForClassRosterAndMentor(classId),
            NotificationRoleTemplates.FromDefault(
                "Buổi học đã kết thúc",
                "Một buổi học đã kết thúc."),
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            }.WithNames(className: className, programName: programName),
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    public static NotificationCommand ClassSessionCancelled(
        Guid classId,
        Guid classSessionId,
        Guid? programId = null,
        string? className = null,
        string? programName = null)
        => StudentParentMentor(
            NotificationType.ClassSessionCancelled,
            NotificationAudience.ForClassRosterAndParentsAndMentor(classId),
            "Buổi học đã bị hủy",
            "Một buổi học đã bị hủy.",
            "Một buổi học của con bạn {studentName} đã bị hủy.",
            "Một buổi học đã bị hủy.",
            payload: new NotificationPayload
            {
                ClassId = classId,
                ClassSessionId = classSessionId,
                ProgramId = programId
            }.WithNames(className: className, programName: programName),
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(className: className, programName: programName));

    // ── Attendance ────────────────────────────────────────────────────────────

    public static NotificationCommand AttendanceMarked(
        AttendanceStatus status,
        Guid studentId,
        Guid classSessionId,
        Guid? classId = null,
        Guid? actorUserId = null,
        Guid? programId = null,
        Guid? programEnrollmentId = null,
        Guid? activityId = null,
        string? studentName = null,
        string? actorName = null,
        string? className = null,
        string? programName = null)
    {
        var (type, title, studentBody, parentBody) = status switch
        {
            AttendanceStatus.Present => (
                NotificationType.AttendanceMarkedPresent,
                "Được điểm danh có mặt",
                "Bạn được điểm danh có mặt cho một buổi học.",
                "Con bạn {studentName} được điểm danh có mặt cho một buổi học."),
            AttendanceStatus.Late => (
                NotificationType.AttendanceMarkedLate,
                "Được điểm danh đi muộn",
                "Bạn được điểm danh đi muộn cho một buổi học.",
                "Con bạn {studentName} được điểm danh đi muộn cho một buổi học."),
            AttendanceStatus.Absent => (
                NotificationType.AttendanceMarkedAbsent,
                "Được điểm danh vắng",
                "Bạn được điểm danh vắng cho một buổi học.",
                "Con bạn {studentName} được điểm danh vắng cho một buổi học."),
            AttendanceStatus.Excused => (
                NotificationType.AttendanceMarkedExcused,
                "Được điểm danh vắng có phép",
                "Bạn được điểm danh vắng có phép cho một buổi học.",
                "Con bạn {studentName} được điểm danh vắng có phép cho một buổi học."),
            _ => (
                NotificationType.AttendanceMarkedPresent,
                "Đã cập nhật điểm danh",
                "Điểm danh của bạn đã được cập nhật cho một buổi học.",
                "Điểm danh của con bạn {studentName} đã được cập nhật cho một buổi học.")
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
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                actorName: actorName,
                className: className,
                programName: programName),
            actorUserId: actorUserId,
            entityType: "ClassSession",
            entityId: classSessionId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                actorName: actorName,
                className: className,
                programName: programName));
    }

    // ── Grading / Quiz ────────────────────────────────────────────────────────

    public static NotificationCommand QuizGraded(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        bool passed,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            passed ? NotificationType.QuizPassed : NotificationType.QuizFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Đạt bài kiểm tra" : "Bài kiểm tra cần chú ý",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed ? "Bạn đã đạt một bài kiểm tra." : "Bạn chưa đạt một bài kiểm tra.")
                : (passed
                    ? "Bạn đã đạt \"{assignmentTitle}\"."
                    : "Bạn chưa đạt \"{assignmentTitle}\"."),
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed
                    ? "Con bạn {studentName} đã đạt một bài kiểm tra."
                    : "Con bạn {studentName} chưa đạt một bài kiểm tra.")
                : (passed
                    ? "Con bạn {studentName} đã đạt \"{assignmentTitle}\"."
                    : "Con bạn {studentName} chưa đạt \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand ResearchGraded(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        bool passed,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? programName = null)
        => StudentAndParent(
            passed ? NotificationType.ResearchGradedPassed : NotificationType.ResearchGradedFailed,
            NotificationAudience.ForStudentAndParents(studentId),
            passed ? "Bài tập đã đạt" : "Bài tập cần chú ý",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed
                    ? "Bài nghiên cứu của bạn đã được chấm đạt."
                    : "Bài nghiên cứu của bạn đã được chấm và cần chú ý.")
                : "Bài nộp \"{assignmentTitle}\" của bạn đã được chấm.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? (passed
                    ? "Bài nghiên cứu của con bạn {studentName} đã được chấm đạt."
                    : "Bài nghiên cứu của con bạn {studentName} đã được chấm và cần chú ý.")
                : "Bài nộp \"{assignmentTitle}\" của con bạn {studentName} đã được chấm.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(studentName: studentName, programName: programName),
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand ResearchReturnedForRevision(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? actorUserId = null,
        Guid? programEnrollmentId = null,
        string? studentName = null,
        string? actorName = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.ResearchReturnedForRevision,
            NotificationAudience.ForStudentAndParents(studentId),
            "Bài nộp được trả lại để chỉnh sửa",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Bài nghiên cứu của bạn đã được trả lại để chỉnh sửa."
                : "Bài nộp \"{assignmentTitle}\" của bạn đã được trả lại để chỉnh sửa.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Bài nghiên cứu của con bạn {studentName} đã được trả lại để chỉnh sửa."
                : "Bài nộp \"{assignmentTitle}\" của con bạn {studentName} đã được trả lại để chỉnh sửa.",
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ProgramId = programId,
                StudentId = studentId
            }.SetEnrollment(programEnrollmentId).WithNames(
                studentName: studentName,
                actorName: actorName,
                programName: programName),
            actorUserId: actorUserId,
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                actorName: actorName,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand ResearchSubmissionOpened(
        Guid studentId,
        Guid submissionId,
        Guid assignmentId,
        Guid? programId = null)
        => new(
            NotificationType.ResearchSubmissionOpened,
            NotificationAudience.ForUser(studentId),
            "Đã mở bài nghiên cứu",
            "Bạn có thể bắt đầu làm bài nghiên cứu.",
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
        string? assignmentTitle = null,
        string? studentName = null,
        string? actorName = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ResearchWorkSubmitted,
            classId.HasValue
                ? NotificationAudience.ForClassMentor(classId.Value)
                : NotificationAudience.ForUser(studentId),
            NotificationRoleTemplates.FromDefault(
                "Đã nộp bài nghiên cứu",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "{actorName} đã nộp bài nghiên cứu để chấm."
                    : "Bài nghiên cứu cho \"{assignmentTitle}\" đã được nộp."),
            payload: new NotificationPayload
            {
                SubmissionId = submissionId,
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                StudentId = studentId
            }.WithNames(
                studentName: studentName,
                actorName: actorName,
                className: className,
                programName: programName),
            actorUserId: studentId,
            entityType: "Submission",
            entityId: submissionId,
            tokens: NotificationTokenKeys.Create(
                studentName: studentName,
                actorName: actorName,
                className: className,
                programName: programName,
                assignmentTitle: assignmentTitle));

    // ── Media ─────────────────────────────────────────────────────────────────

    public static NotificationCommand MediaVideoReady(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null,
        string? className = null)
        => new(
            NotificationType.MediaVideoReady,
            NotificationAudience.ForUser(uploaderUserId),
            "Video đã sẵn sàng",
            "Video của bạn đã xử lý xong và sẵn sàng.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId }
                .WithNames(className: className),
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaProcessingFailed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null,
        string? className = null)
        => new(
            NotificationType.MediaProcessingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "Xử lý video thất bại",
            "Xử lý video thất bại. Vui lòng tải lên lại.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId }
                .WithNames(className: className),
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaAiTaggingFailed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null,
        string? className = null)
        => new(
            NotificationType.MediaAiTaggingFailed,
            NotificationAudience.ForUser(uploaderUserId),
            "Gắn thẻ AI thất bại",
            "Gắn thẻ tự động cho video của bạn đã thất bại.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId }
                .WithNames(className: className),
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    public static NotificationCommand MediaTagsProcessed(
        Guid uploaderUserId,
        Guid mediaAssetId,
        Guid? classId = null,
        string? className = null)
        => new(
            NotificationType.MediaTagsProcessed,
            NotificationAudience.ForUser(uploaderUserId),
            "Thẻ video đã sẵn sàng",
            "Thẻ AI cho video của bạn đã sẵn sàng.",
            payload: new NotificationPayload { MediaAssetId = mediaAssetId, ClassId = classId }
                .WithNames(className: className),
            entityType: "MediaAsset",
            entityId: mediaAssetId);

    // ── Highlight video ───────────────────────────────────────────────────────

    public static NotificationCommand HighlightVideoGenerationQueued(
        Guid studentId,
        Guid highlightVideoId,
        string? studentName = null)
        => new(
            NotificationType.HighlightVideoGenerationQueued,
            NotificationAudience.ForUser(studentId),
            NotificationRoleTemplates.FromDefault(
                "Video nổi bật đang chờ xử lý",
                "Video nổi bật cá nhân của bạn đã được đưa vào hàng đợi."),
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            }.WithNames(studentName: studentName),
            entityType: "HighlightVideo",
            entityId: highlightVideoId,
            tokens: NotificationTokenKeys.Create(studentName: studentName));

    public static NotificationCommand HighlightVideoReady(
        Guid studentId,
        Guid highlightVideoId,
        string? studentName = null)
        => StudentAndParent(
            NotificationType.HighlightVideoReady,
            NotificationAudience.ForStudentAndParents(studentId),
            "Video nổi bật đã sẵn sàng",
            "Video nổi bật cá nhân của bạn đã sẵn sàng.",
            "Video nổi bật cá nhân của con bạn {studentName} đã sẵn sàng.",
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            }.WithNames(studentName: studentName),
            entityType: "HighlightVideo",
            entityId: highlightVideoId,
            tokens: NotificationTokenKeys.Create(studentName: studentName));

    public static NotificationCommand HighlightVideoGenerationFailed(
        Guid studentId,
        Guid highlightVideoId,
        string? studentName = null)
        => new(
            NotificationType.HighlightVideoGenerationFailed,
            NotificationAudience.ForUser(studentId),
            NotificationRoleTemplates.FromDefault(
                "Tạo video nổi bật thất bại",
                "Không tạo được video nổi bật cá nhân của bạn."),
            payload: new NotificationPayload
            {
                HighlightVideoId = highlightVideoId,
                StudentId = studentId
            }.WithNames(studentName: studentName),
            entityType: "HighlightVideo",
            entityId: highlightVideoId,
            tokens: NotificationTokenKeys.Create(studentName: studentName));

    // ── Catalog ───────────────────────────────────────────────────────────────

    public static NotificationCommand AssignmentPublished(
        Guid classId,
        Guid assignmentId,
        Guid? programId = null,
        string? assignmentTitle = null,
        Guid? moduleId = null,
        string? className = null,
        string? programName = null)
        => StudentAndParent(
            NotificationType.AssignmentPublished,
            NotificationAudience.ForClassRosterAndParents(classId),
            "Bài tập mới đã được đăng",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Một bài tập mới đã sẵn sàng."
                : "Bài tập \"{assignmentTitle}\" hiện đã sẵn sàng.",
            string.IsNullOrWhiteSpace(assignmentTitle)
                ? "Một bài tập mới đã sẵn sàng cho con bạn {studentName}."
                : "Bài tập \"{assignmentTitle}\" hiện đã sẵn sàng cho con bạn {studentName}.",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                ModuleId = moduleId
            }.WithNames(className: className, programName: programName),
            entityType: "Assignment",
            entityId: assignmentId,
            tokens: NotificationTokenKeys.Create(
                className: className,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand MaterialUpdated(
        Guid classId,
        Guid materialId,
        Guid? activityId = null,
        Guid? programId = null,
        string? materialTitle = null,
        Guid? courseId = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.MaterialUpdated,
            NotificationAudience.ForClassRoster(classId),
            "Tài liệu đã được cập nhật",
            string.IsNullOrWhiteSpace(materialTitle)
                ? "Tài liệu khóa học đã được cập nhật."
                : $"Tài liệu \"{materialTitle}\" đã được cập nhật.",
            payload: new NotificationPayload
            {
                MaterialId = materialId,
                ActivityId = activityId,
                ClassId = classId,
                ProgramId = programId,
                CourseId = courseId
            }.WithNames(className: className, programName: programName),
            entityType: "Material",
            entityId: materialId);

    // ── Mentor curriculum edits ───────────────────────────────────────────────

    public static NotificationCommand AssignmentEditedByMentor(
        Guid assignmentId,
        Guid mentorId,
        Guid programId,
        string assignmentTitle,
        Guid? moduleId = null,
        string? actorName = null,
        string? programName = null)
        => new(
            NotificationType.AssignmentEditedByMentor,
            NotificationAudience.ForManagers(),
            NotificationRoleTemplates.FromDefault(
                "Mentor đã chỉnh sửa bài tập",
                string.IsNullOrWhiteSpace(assignmentTitle)
                    ? "Một mentor đã cập nhật thông tin bài tập."
                    : "Mentor đã cập nhật bài tập \"{assignmentTitle}\"."),
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ProgramId = programId,
                ModuleId = moduleId
            }.WithNames(actorName: actorName, programName: programName),
            actorUserId: mentorId,
            entityType: "Assignment",
            entityId: assignmentId,
            tokens: NotificationTokenKeys.Create(
                actorName: actorName,
                programName: programName,
                assignmentTitle: assignmentTitle));

    public static NotificationCommand ClassQuizSetEditedByMentor(
        Guid assignmentId,
        Guid classId,
        Guid mentorId,
        Guid programId,
        string action,
        string? detail = null,
        Guid? moduleId = null,
        string? actorName = null,
        string? className = null,
        string? programName = null)
        => new(
            NotificationType.ClassQuizSetEditedByMentor,
            NotificationAudience.ForManagers(),
            "Mentor đã chỉnh sửa bộ câu hỏi lớp",
            string.IsNullOrWhiteSpace(detail)
                ? $"Mentor {action} cho bài kiểm tra của lớp."
                : $"Mentor {action}: {detail}",
            payload: new NotificationPayload
            {
                AssignmentId = assignmentId,
                ClassId = classId,
                ProgramId = programId,
                ModuleId = moduleId,
                Extra = action
            }.WithNames(actorName: actorName, className: className, programName: programName),
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
