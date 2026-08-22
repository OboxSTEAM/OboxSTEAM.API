using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private const string SeedRoboticsQuizAssignmentCode = "ASG-ROBOTICS-Q01";
    private const string SeedRoboticsResearchAssignmentCode = "ASG-ROBOTICS-03-01";

    private async Task SeedNotificationsAsync()
    {
        _loggerService.LogInformation("Starting seed notifications");

        var existing = await _unitOfWork.Notifications.GetAllAsync();
        if (existing.Count > 0)
        {
            _loggerService.LogInformation("Notifications already exist, skipping seeding");
            return;
        }

        var manager = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNG-001");
        var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "MNT-001");
        var parent = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "PRT-001");
        var student = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");

        if (manager is null || mentor is null || parent is null || student is null)
        {
            _loggerService.LogWarning(
                "Primary role users (MNG-001 / MNT-001 / PRT-001 / STD-001) not found. Skipping notification seeding.");
            return;
        }

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(
            p => p.Code == "PRG-ROBOTICS" && !p.IsDeleted);
        if (program is null)
        {
            _loggerService.LogWarning("PRG-ROBOTICS not found. Skipping notification seeding.");
            return;
        }

        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(
            m => m.Code == "MOD-ROBOTICS-01" && m.ProgramId == program.Id && !m.IsDeleted);
        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(
            c => c.Code == RoboticsCurrentClassCode && c.ProgramId == program.Id && !c.IsDeleted);
        var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id && pe.ProgramId == program.Id && !pe.IsDeleted);
        var classEnrollment = classEntity is null || programEnrollment is null
            ? null
            : await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.StudentId == student.Id
                      && ce.ClassId == classEntity.Id
                      && ce.ProgramEnrollmentId == programEnrollment.Id
                      && !ce.IsDeleted);
        var payment = programEnrollment is null
            ? null
            : await _unitOfWork.Payments.FirstOrDefaultAsync(
                p => p.StudentId == student.Id
                     && p.ProgramEnrollmentId == programEnrollment.Id
                     && !p.IsDeleted);
        var paymentRequest = programEnrollment is null
            ? null
            : await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
                pr => pr.StudentId == student.Id
                      && (pr.ProgramId == program.Id || pr.ProgramEnrollmentId == programEnrollment.Id)
                      && !pr.IsDeleted);

        // Prefer robotics quiz for student/manager assignment deep-links (not an arbitrary assignment).
        Assignment? quizAssignment = null;
        if (module is not null)
        {
            quizAssignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
                a => a.Code == SeedRoboticsQuizAssignmentCode
                     && a.ModuleId == module.Id
                     && !a.IsDeleted)
                ?? await _unitOfWork.Assignments.FirstOrDefaultAsync(
                    a => a.ModuleId == module.Id && a.AssignmentType == AssignmentType.Quiz && !a.IsDeleted);
        }

        var researchAssignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code == SeedRoboticsResearchAssignmentCode && !a.IsDeleted);

        var classSession = classEntity is null
            ? null
            : await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.ClassId == classEntity.Id
                      && cs.ActivityId != null
                      && !cs.IsDeleted)
              ?? await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                  cs => cs.ClassId == classEntity.Id && !cs.IsDeleted);

        var mentorRequest = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
            r => r.MentorId == mentor.Id
                 && classEntity != null
                 && r.ClassId == classEntity.Id
                 && !r.IsDeleted);

        var quizSubmission = quizAssignment is null
            ? null
            : await _unitOfWork.Submissions.FirstOrDefaultAsync(
                s => s.StudentId == student.Id
                     && s.AssignmentId == quizAssignment.Id
                     && !s.IsDeleted);

        var researchSubmission = researchAssignment is null
            ? null
            : await _unitOfWork.Submissions.FirstOrDefaultAsync(
                s => s.StudentId == student.Id
                     && s.AssignmentId == researchAssignment.Id
                     && !s.IsDeleted);

        var moduleEnrollment = module is null || programEnrollment is null
            ? null
            : await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.StudentId == student.Id
                      && me.ModuleId == module.Id
                      && me.ProgramEnrollmentId == programEnrollment.Id
                      && !me.IsDeleted);

        if (program is null
            || module is null
            || classEntity is null
            || classEnrollment is null
            || programEnrollment is null
            || payment is null
            || quizAssignment is null
            || classSession is null)
        {
            _loggerService.LogWarning(
                "Linked robotics entities missing for notification seed "
                + "(program/module/class/session/payment/quiz). Skipping.");
            return;
        }

        var programId = program.Id;
        var moduleId = module.Id;
        var classId = classEntity.Id;
        var className = classEntity.Name;
        var programEnrollmentId = programEnrollment.Id;
        var classEnrollmentId = classEnrollment.Id;
        var paymentId = payment.Id;
        var quizAssignmentId = quizAssignment.Id;
        var quizAssignmentTitle = quizAssignment.Title;
        var classSessionId = classSession.Id;
        var sessionActivityId = classSession.ActivityId;
        var mentorRequestId = mentorRequest?.Id;
        var paymentRequestId = paymentRequest?.Id;
        var studentName = !string.IsNullOrWhiteSpace(student.FullName) ? student.FullName! : student.Email;

        var nextActivityId = await NotificationDeeplinkResolver.ResolveCurrentActivityIdAsync(
            _unitOfWork,
            programId,
            programEnrollmentId);
        if (!nextActivityId.HasValue && sessionActivityId.HasValue)
        {
            nextActivityId = sessionActivityId;
        }

        var firstModuleActivityId = await NotificationDeeplinkResolver.ResolveFirstActivityInModuleAsync(
            _unitOfWork,
            programId,
            moduleId);

        var now = _seedNow;
        var samples = new List<(NotificationCommand Command, Guid RecipientId, RoleType Role, DateTime? ReadAt)>();

        // Manager — ForManagers types
        samples.Add((
            NotificationCatalog.ClassCreated(classId, programId, className),
            manager.Id,
            RoleType.Manager,
            null));
        samples.Add((
            NotificationCatalog.ClassOpenForEnrollment(classId, programId, className),
            manager.Id,
            RoleType.Manager,
            null));
        if (mentorRequestId.HasValue)
        {
            samples.Add((
                NotificationCatalog.ClassMentorRequestSubmitted(
                    mentorRequestId.Value, classId, programId, mentor.Id, className),
                manager.Id,
                RoleType.Manager,
                now.AddHours(-6)));
        }

        samples.Add((
            NotificationCatalog.AssignmentEditedByMentor(
                quizAssignmentId, mentor.Id, programId, quizAssignmentTitle, moduleId),
            manager.Id,
            RoleType.Manager,
            null));
        samples.Add((
            NotificationCatalog.ClassQuizSetEditedByMentor(
                quizAssignmentId,
                classId,
                mentor.Id,
                programId,
                "updated questions",
                "Added two new MCQ items.",
                moduleId),
            manager.Id,
            RoleType.Manager,
            now.AddDays(-1)));

        // Mentor — assignment / session / research inbox
        if (mentorRequestId.HasValue)
        {
            samples.Add((
                NotificationCatalog.ClassMentorRequestApproved(
                    mentorRequestId.Value, classId, programId, mentor.Id, className),
                mentor.Id,
                RoleType.Mentor,
                null));
        }

        samples.Add((
            NotificationCatalog.ClassSessionScheduled(classId, classSessionId, programId),
            mentor.Id,
            RoleType.Mentor,
            null));
        samples.Add((
            NotificationCatalog.ClassSessionStarted(classId, classSessionId, programId),
            mentor.Id,
            RoleType.Mentor,
            now.AddHours(-3)));

        if (researchSubmission is not null && researchAssignment is not null)
        {
            samples.Add((
                NotificationCatalog.ResearchWorkSubmitted(
                    student.Id,
                    researchSubmission.Id,
                    researchAssignment.Id,
                    classId,
                    programId,
                    researchAssignment.Title),
                mentor.Id,
                RoleType.Mentor,
                null));
        }

        samples.Add((
            NotificationCatalog.ClassUpdated(classId, programId, className),
            mentor.Id,
            RoleType.Mentor,
            now.AddDays(-2)));
        samples.Add((
            NotificationCatalog.ClassSessionCompleted(classId, classSessionId, programId),
            mentor.Id,
            RoleType.Mentor,
            null));

        // Parent — link / payment / student-support events
        samples.Add((
            NotificationCatalog.ParentLinkVerified(parent.Id, student.Id),
            parent.Id,
            RoleType.Parent,
            now.AddDays(-3)));
        if (paymentRequestId.HasValue)
        {
            samples.Add((
                NotificationCatalog.ParentPaymentRequested(
                    parent.Id, student.Id, paymentRequestId.Value, programId, programEnrollmentId),
                parent.Id,
                RoleType.Parent,
                null));
        }

        samples.Add((
            NotificationCatalog.ProgramActivated(
                student.Id, programId, programEnrollmentId, program.Name, nextActivityId),
            parent.Id,
            RoleType.Parent,
            null));
        samples.Add((
            NotificationCatalog.ClassSessionScheduled(classId, classSessionId, programId),
            parent.Id,
            RoleType.Parent,
            now.AddHours(-12)));
        samples.Add((
            NotificationCatalog.AttendanceMarked(
                AttendanceStatus.Absent,
                student.Id,
                classSessionId,
                classId,
                mentor.Id,
                programId,
                programEnrollmentId,
                sessionActivityId),
            parent.Id,
            RoleType.Parent,
            null));
        samples.Add((
            NotificationCatalog.PaymentSucceeded(
                student.Id, paymentId, programId, programEnrollmentId, nextActivityId),
            parent.Id,
            RoleType.Parent,
            now.AddDays(-1)));
        samples.Add((
            NotificationCatalog.AssignmentPublished(
                classId, quizAssignmentId, programId, quizAssignmentTitle, moduleId),
            parent.Id,
            RoleType.Parent,
            null));

        // Student — enrollment / progress / class / grading
        samples.Add((
            NotificationCatalog.ProgramActivated(
                student.Id, programId, programEnrollmentId, program.Name, nextActivityId),
            student.Id,
            RoleType.Student,
            now.AddDays(-4)));
        samples.Add((
            NotificationCatalog.PaymentSucceeded(
                student.Id, paymentId, programId, programEnrollmentId, nextActivityId),
            student.Id,
            RoleType.Student,
            null));
        samples.Add((
            NotificationCatalog.ModuleCompleted(
                student.Id,
                moduleId,
                moduleEnrollment?.Id,
                programId,
                module.Name,
                programEnrollmentId,
                nextActivityId ?? firstModuleActivityId),
            student.Id,
            RoleType.Student,
            null));
        samples.Add((
            NotificationCatalog.ClassEnrolled(
                student.Id,
                classId,
                classEnrollmentId,
                programId,
                className,
                programEnrollmentId,
                nextActivityId),
            student.Id,
            RoleType.Student,
            now.AddDays(-2)));

        if (quizSubmission is not null)
        {
            samples.Add((
                NotificationCatalog.QuizGraded(
                    student.Id,
                    quizSubmission.Id,
                    quizAssignmentId,
                    passed: true,
                    programId,
                    quizAssignmentTitle,
                    programEnrollmentId),
                student.Id,
                RoleType.Student,
                null));
        }

        samples.Add((
            NotificationCatalog.AttendanceMarked(
                AttendanceStatus.Present,
                student.Id,
                classSessionId,
                classId,
                mentor.Id,
                programId,
                programEnrollmentId,
                sessionActivityId),
            student.Id,
            RoleType.Student,
            now.AddHours(-8)));
        samples.Add((
            NotificationCatalog.ParentLinkApproved(student.Id, parent.Id, parent.Id),
            student.Id,
            RoleType.Student,
            now.AddDays(-5)));

        var notifications = new List<Notification>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var (command, recipientId, role, readAt) = samples[i];
            notifications.Add(ToSeedNotification(
                command,
                recipientId,
                role,
                studentName,
                readAt,
                now.AddMinutes(-(samples.Count - i) * 17)));
        }

        await _unitOfWork.Notifications.AddRangeAsync(notifications);

        // Restore staggered CreatedAt after repository overwrite so inbox order varies for FE.
        for (var i = 0; i < notifications.Count; i++)
        {
            notifications[i].CreatedAt = now.AddMinutes(-(notifications.Count - i) * 17);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed notifications — {Count} inbox row(s) for MNG-001, MNT-001, PRT-001, STD-001.",
            notifications.Count);
    }

    private static Notification ToSeedNotification(
        NotificationCommand command,
        Guid recipientUserId,
        RoleType recipientRole,
        string studentName,
        DateTime? readAt,
        DateTime createdAt)
    {
        var tokens = new Dictionary<string, string>(command.Tokens, StringComparer.Ordinal)
        {
            [NotificationTokenKeys.StudentName] = studentName
        };

        var copy = NotificationTemplateRenderer.Interpolate(
            command.Templates.Resolve(recipientRole),
            tokens);

        var payloadJson = NotificationDtoMapper.SerializePayload(command.Payload?.Clone());

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = command.Type,
            Title = copy.Title,
            Body = copy.Body,
            PayloadJson = payloadJson,
            ReadAt = readAt,
            ActorUserId = command.ActorUserId,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            CreatedAt = createdAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false
        };
    }
}
