using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OboxSteam.Application.Notifications;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private static readonly JsonSerializerOptions SeedNotificationPayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
        var module = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01")
                     ?? await _unitOfWork.Modules.FirstOrDefaultAsync(m => !m.IsDeleted);
        var classEntity = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == "CLS-OPEN-001");
        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.StudentId == student.Id && !ce.IsDeleted);
        var programEnrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == student.Id && !pe.IsDeleted);
        var payment = await _unitOfWork.Payments.FirstOrDefaultAsync(
            p => p.StudentId == student.Id && !p.IsDeleted);
        var paymentRequest = await _unitOfWork.PaymentRequests.FirstOrDefaultAsync(
            pr => pr.StudentId == student.Id && !pr.IsDeleted);
        var assignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(a => !a.IsDeleted);
        var classSession = classEntity is null
            ? null
            : await _unitOfWork.ClassSessions.FirstOrDefaultAsync(
                cs => cs.ClassId == classEntity.Id && !cs.IsDeleted);
        var mentorRequest = await _unitOfWork.ClassMentorRequests.FirstOrDefaultAsync(
            r => r.MentorId == mentor.Id && !r.IsDeleted);
        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.StudentId == student.Id && !s.IsDeleted);

        var programId = program?.Id ?? Guid.NewGuid();
        var moduleId = module?.Id ?? Guid.NewGuid();
        var classId = classEntity?.Id ?? Guid.NewGuid();
        var className = classEntity?.Name ?? "Robotics Open Cohort 1";
        var programEnrollmentId = programEnrollment?.Id ?? Guid.NewGuid();
        var classEnrollmentId = classEnrollment?.Id ?? Guid.NewGuid();
        var paymentId = payment?.Id ?? Guid.NewGuid();
        var paymentRequestId = paymentRequest?.Id ?? Guid.NewGuid();
        var assignmentId = assignment?.Id ?? Guid.NewGuid();
        var assignmentTitle = assignment?.Title ?? "Seed assignment";
        var classSessionId = classSession?.Id ?? Guid.NewGuid();
        var mentorRequestId = mentorRequest?.Id ?? Guid.NewGuid();
        var submissionId = submission?.Id ?? Guid.NewGuid();
        var highlightVideoId = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var samples = new List<(NotificationCommand Command, Guid RecipientId, DateTime? ReadAt)>();

        // Manager — ForManagers types
        samples.Add((
            NotificationCatalog.ClassCreated(classId, programId, className),
            manager.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassOpenForEnrollment(classId, programId, className),
            manager.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassMentorRequestSubmitted(
                mentorRequestId, classId, programId, mentor.Id, className),
            manager.Id,
            now.AddHours(-6)));
        samples.Add((
            NotificationCatalog.AssignmentEditedByMentor(
                assignmentId, mentor.Id, programId, assignmentTitle),
            manager.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassQuizSetEditedByMentor(
                assignmentId, classId, mentor.Id, programId, "updated questions", "Added two new MCQ items."),
            manager.Id,
            now.AddDays(-1)));

        // Mentor — assignment / session / research inbox
        samples.Add((
            NotificationCatalog.ClassMentorRequestApproved(
                mentorRequestId, classId, programId, mentor.Id, className),
            mentor.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassSessionScheduled(classId, classSessionId, programId),
            mentor.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassSessionStarted(classId, classSessionId, programId),
            mentor.Id,
            now.AddHours(-3)));
        samples.Add((
            NotificationCatalog.ResearchWorkSubmitted(
                student.Id, submissionId, assignmentId, classId, programId, assignmentTitle),
            mentor.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassUpdated(classId, programId, className),
            mentor.Id,
            now.AddDays(-2)));
        samples.Add((
            NotificationCatalog.ClassSessionCompleted(classId, classSessionId, programId),
            mentor.Id,
            null));

        // Parent — link / payment / student-support events
        samples.Add((
            NotificationCatalog.ParentLinkVerified(parent.Id, student.Id),
            parent.Id,
            now.AddDays(-3)));
        samples.Add((
            NotificationCatalog.ParentPaymentRequested(
                parent.Id, student.Id, paymentRequestId, programId, programEnrollmentId),
            parent.Id,
            null));
        samples.Add((
            NotificationCatalog.ProgramActivated(
                student.Id, programId, programEnrollmentId, program?.Name),
            parent.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassSessionScheduled(classId, classSessionId, programId),
            parent.Id,
            now.AddHours(-12)));
        samples.Add((
            NotificationCatalog.AttendanceMarked(
                AttendanceStatus.Absent, student.Id, classSessionId, classId, mentor.Id),
            parent.Id,
            null));
        samples.Add((
            NotificationCatalog.PaymentSucceeded(
                student.Id, paymentId, programId, programEnrollmentId),
            parent.Id,
            now.AddDays(-1)));
        samples.Add((
            NotificationCatalog.AssignmentPublished(
                classId, assignmentId, programId, assignmentTitle),
            parent.Id,
            null));

        // Student — enrollment / progress / class / grading
        samples.Add((
            NotificationCatalog.ProgramActivated(
                student.Id, programId, programEnrollmentId, program?.Name),
            student.Id,
            now.AddDays(-4)));
        samples.Add((
            NotificationCatalog.PaymentSucceeded(
                student.Id, paymentId, programId, programEnrollmentId),
            student.Id,
            null));
        samples.Add((
            NotificationCatalog.ModuleCompleted(
                student.Id, moduleId, null, programId, module?.Name),
            student.Id,
            null));
        samples.Add((
            NotificationCatalog.ClassEnrolled(
                student.Id, classId, classEnrollmentId, programId, className),
            student.Id,
            now.AddDays(-2)));
        samples.Add((
            NotificationCatalog.QuizGraded(
                student.Id, submissionId, assignmentId, passed: true, programId, assignmentTitle),
            student.Id,
            null));
        samples.Add((
            NotificationCatalog.AttendanceMarked(
                AttendanceStatus.Present, student.Id, classSessionId, classId, mentor.Id),
            student.Id,
            now.AddHours(-8)));
        samples.Add((
            NotificationCatalog.HighlightVideoReady(student.Id, highlightVideoId),
            student.Id,
            null));
        samples.Add((
            NotificationCatalog.ParentLinkApproved(student.Id, parent.Id, parent.Id),
            student.Id,
            now.AddDays(-5)));

        var notifications = new List<Notification>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var (command, recipientId, readAt) = samples[i];
            notifications.Add(ToSeedNotification(
                command,
                recipientId,
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
        DateTime? readAt,
        DateTime createdAt)
    {
        var payloadJson = command.Payload is null
            ? null
            : JsonSerializer.Serialize(command.Payload, SeedNotificationPayloadJsonOptions);

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = command.Type,
            Title = command.Title,
            Body = command.Body,
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
