using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.NotificationDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Publishes one catalog command per <see cref="NotificationType"/> using seeded users/classes.
/// Intended for Development smoke testing only.
/// </summary>
public sealed class NotificationSmokeTestService : INotificationSmokeTestService
{
    private static readonly string[] TargetEmails =
    [
        "student1@oboxsteam.com",
        "student2@oboxsteam.com",
        "student3@oboxsteam.com",
        "student4@oboxsteam.com",
        "student5@oboxsteam.com",
        "superadmin@oboxsteam.com",
        "manager@oboxsteam.com",
        "parent@oboxsteam.com",
        "mentor@oboxsteam.com"
    ];

    private const string OpenClassCode = "CLS-OPEN-001";

    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _publisher;
    private readonly INotificationRecipientResolver _recipientResolver;
    private readonly ILogger<NotificationSmokeTestService> _logger;

    public NotificationSmokeTestService(
        IUnitOfWork unitOfWork,
        INotificationPublisher publisher,
        INotificationRecipientResolver recipientResolver,
        ILogger<NotificationSmokeTestService> logger)
    {
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public async Task<NotificationSmokeTestResultDto> PublishAllCatalogTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var users = await _unitOfWork.Users.GetAllAsync(u => TargetEmails.Contains(u.Email) && !u.IsDeleted);
        var byEmail = users.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);

        foreach (var email in TargetEmails)
        {
            if (!byEmail.ContainsKey(email))
            {
                throw ErrorHelper.NotFound($"Seed user not found for smoke test: {email}");
            }
        }

        var student1 = byEmail["student1@oboxsteam.com"];
        var student2 = byEmail["student2@oboxsteam.com"];
        var student3 = byEmail["student3@oboxsteam.com"];
        var student4 = byEmail["student4@oboxsteam.com"];
        var student5 = byEmail["student5@oboxsteam.com"];
        var admin = byEmail["superadmin@oboxsteam.com"];
        var manager = byEmail["manager@oboxsteam.com"];
        var parent = byEmail["parent@oboxsteam.com"];
        var mentor = byEmail["mentor@oboxsteam.com"];

        var clazz = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == OpenClassCode && !c.IsDeleted);
        if (clazz is null)
        {
            throw ErrorHelper.NotFound($"Seed class {OpenClassCode} not found for smoke test.");
        }

        var programId = clazz.ProgramId;
        var classId = clazz.Id;
        var dummy = Guid.NewGuid();

        var commands = BuildCatalogCommands(
            student1.Id,
            student2.Id,
            student3.Id,
            student4.Id,
            student5.Id,
            admin.Id,
            manager.Id,
            parent.Id,
            mentor.Id,
            classId,
            programId,
            dummy);

        var emailById = users.ToDictionary(u => u.Id, u => u.Email);
        var typeResults = new List<NotificationTypePublishResultDto>();
        var rowsCreated = 0;

        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new NotificationTypePublishResultDto
            {
                Type = command.Type.ToString()
            };

            try
            {
                var recipientIds = await _recipientResolver.ResolveAsync(command.Audience, cancellationToken);
                result.RecipientCount = recipientIds.Count;
                result.RecipientEmails = recipientIds
                    .Select(id => emailById.TryGetValue(id, out var email) ? email : id.ToString())
                    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (recipientIds.Count == 0)
                {
                    result.Status = "SkippedZeroRecipients";
                    _logger.LogWarning(
                        "Notification smoke test: {Type} resolved to zero recipients.",
                        command.Type);
                }
                else
                {
                    await _publisher.PublishAsync(command, cancellationToken);
                    rowsCreated += recipientIds.Distinct().Count();
                    result.Status = "Ok";
                    _logger.LogInformation(
                        "Notification smoke test: published {Type} to {Count} recipient(s): {Recipients}",
                        command.Type,
                        result.RecipientCount,
                        string.Join(", ", result.RecipientEmails));
                }
            }
            catch (Exception ex)
            {
                result.Status = "Failed";
                result.Error = ex.Message;
                _logger.LogError(ex, "Notification smoke test failed for {Type}.", command.Type);
            }

            typeResults.Add(result);
        }

        var finishedAt = DateTime.UtcNow;
        return new NotificationSmokeTestResultDto
        {
            StartedAtUtc = startedAt,
            FinishedAtUtc = finishedAt,
            ClassCodeUsed = OpenClassCode,
            ClassIdUsed = classId,
            ResolvedSeedEmails = TargetEmails,
            Types = typeResults,
            TotalTypesAttempted = typeResults.Count,
            TotalTypesWithRecipients = typeResults.Count(t => t.Status == "Ok"),
            TotalTypesSkippedZeroRecipients = typeResults.Count(t => t.Status == "SkippedZeroRecipients"),
            TotalTypesFailed = typeResults.Count(t => t.Status == "Failed"),
            TotalNotificationRowsCreated = rowsCreated
        };
    }

    private static IReadOnlyList<NotificationCommand> BuildCatalogCommands(
        Guid student1Id,
        Guid student2Id,
        Guid student3Id,
        Guid student4Id,
        Guid student5Id,
        Guid adminId,
        Guid managerId,
        Guid parentId,
        Guid mentorId,
        Guid classId,
        Guid programId,
        Guid dummy)
    {
        // Cover every NotificationType once (or more for multi-user ForUser cases that target seed roles).
        return new List<NotificationCommand>
        {
            // Account — publish to each role so every smoke target gets an account-type event
            NotificationCatalog.AccountRegistered(student1Id),
            NotificationCatalog.EmailVerified(student2Id),
            NotificationCatalog.PasswordChanged(student3Id),
            NotificationCatalog.AccountRegistered(adminId),
            NotificationCatalog.EmailVerified(managerId),
            NotificationCatalog.PasswordChanged(mentorId),
            NotificationCatalog.AccountRegistered(parentId),
            NotificationCatalog.EmailVerified(student4Id),
            NotificationCatalog.PasswordChanged(student5Id),

            // Parent link
            NotificationCatalog.ParentLinkRequested(parentId, student1Id, actorUserId: student1Id),
            NotificationCatalog.ParentLinkVerified(parentId, student2Id),
            NotificationCatalog.ParentLinkApproved(student1Id, parentId, actorUserId: parentId),

            // Enrollment
            NotificationCatalog.ProgramPendingPayment(student1Id, programId, dummy, "Robotics"),
            NotificationCatalog.ProgramActivated(student1Id, programId, dummy, "Robotics"),
            NotificationCatalog.ModuleCompleted(student1Id, dummy, dummy, programId, "Module A"),
            NotificationCatalog.ModuleUnlocked(student1Id, dummy, programId, "Module B"),
            NotificationCatalog.ModuleRetakePendingPayment(student1Id, dummy, dummy, "Module A"),
            NotificationCatalog.ModuleRetakeInitiated(student1Id, dummy, dummy, "Module A"),
            NotificationCatalog.PendingPaymentExpired(student1Id, dummy, programId),
            NotificationCatalog.ActivityCompleted(student1Id, dummy, dummy, programId, "Activity 1"),

            // Payment
            NotificationCatalog.PaymentSucceeded(student1Id, dummy, programId, dummy),
            NotificationCatalog.PaymentFailed(student1Id, dummy, programId),
            NotificationCatalog.PaymentCancelled(student1Id, dummy, programId),
            NotificationCatalog.ParentPaymentRequested(parentId, student1Id, dummy, programId, dummy),
            NotificationCatalog.ParentModuleRetakeRequested(parentId, student1Id, dummy, dummy),

            // Class lifecycle
            NotificationCatalog.ClassCreated(classId, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassUpdated(classId, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassOpenForEnrollment(classId, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassStarted(classId, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassAutoStarted(classId, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassCompleted(classId, programId, "Robotics Open Cohort 1"),

            // Class enrollment
            NotificationCatalog.ClassEnrolled(student1Id, classId, dummy, programId, "Robotics Open Cohort 1"),
            NotificationCatalog.ClassTransferred(student1Id, classId, dummy, programId, "Robotics Open Cohort 1"),

            // Class session
            NotificationCatalog.ClassSessionScheduled(classId, dummy, programId),
            NotificationCatalog.ClassSessionRescheduled(classId, dummy, programId),
            NotificationCatalog.ClassSessionStarted(classId, dummy, programId),
            NotificationCatalog.ClassSessionCompleted(classId, dummy, programId),
            NotificationCatalog.ClassSessionCancelled(classId, dummy, programId),

            // Attendance
            NotificationCatalog.AttendanceMarked(AttendanceStatus.Present, student1Id, dummy, classId, mentorId),
            NotificationCatalog.AttendanceMarked(AttendanceStatus.Late, student2Id, dummy, classId, mentorId),
            NotificationCatalog.AttendanceMarked(AttendanceStatus.Absent, student3Id, dummy, classId, mentorId),
            NotificationCatalog.AttendanceMarked(AttendanceStatus.Excused, student4Id, dummy, classId, mentorId),

            // Grading
            NotificationCatalog.QuizGraded(student1Id, dummy, dummy, passed: true, programId, "Quiz 1"),
            NotificationCatalog.QuizGraded(student2Id, dummy, dummy, passed: false, programId, "Quiz 1"),
            NotificationCatalog.ResearchGraded(student1Id, dummy, dummy, passed: true, programId, "Research 1"),
            NotificationCatalog.ResearchGraded(student2Id, dummy, dummy, passed: false, programId, "Research 1"),
            NotificationCatalog.ResearchReturnedForRevision(student1Id, dummy, dummy, programId, "Research 1", mentorId),
            NotificationCatalog.ResearchSubmissionOpened(student1Id, dummy, dummy, programId),
            NotificationCatalog.ResearchWorkSubmitted(student1Id, dummy, dummy, classId, programId, "Research 1"),

            // Media
            NotificationCatalog.MediaVideoReady(mentorId, dummy),
            NotificationCatalog.MediaProcessingFailed(adminId, dummy),
            NotificationCatalog.MediaAiTaggingFailed(managerId, dummy),
            NotificationCatalog.MediaTagsProcessed(student5Id, dummy),

            // Highlight video
            NotificationCatalog.HighlightVideoGenerationQueued(student1Id, dummy),
            NotificationCatalog.HighlightVideoReady(student1Id, dummy),
            NotificationCatalog.HighlightVideoGenerationFailed(student1Id, dummy),

            // Catalog
            NotificationCatalog.AssignmentPublished(classId, dummy, programId, "Assignment 1"),
            NotificationCatalog.MaterialUpdated(classId, dummy, dummy, programId, "Material 1")
        };
    }
}
