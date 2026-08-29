using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AssessmentRecoveryDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class AssessmentRecoveryRequestService : IAssessmentRecoveryRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ILogger<AssessmentRecoveryRequestService> _logger;
    private readonly ProgramPurchaseLifecycle _programPurchaseLifecycle;

    public AssessmentRecoveryRequestService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        INotificationPublisher notificationPublisher,
        ILogger<AssessmentRecoveryRequestService> logger,
        ProgramPurchaseLifecycle programPurchaseLifecycle)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _notificationPublisher = notificationPublisher;
        _logger = logger;
        _programPurchaseLifecycle = programPurchaseLifecycle;
    }

    public async Task<AssessmentRecoveryRequestResponseDto> CreateAsync(CreateAssessmentRecoveryRequestDto request)
    {
        if (request.ModuleEnrollmentId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("ModuleEnrollmentId and AssignmentId are required.");
        }

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            "Only students can request assessment recovery.");

        var enrollment = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId)
            ?? throw ErrorHelper.NotFound($"Module enrollment '{request.ModuleEnrollmentId}' not found.");

        if (enrollment.IsDeleted || enrollment.StudentId != student.Id)
        {
            throw ErrorHelper.Forbidden("Module enrollment does not belong to the current student.");
        }

        if (enrollment.Status != EnrollmentStatus.Active)
        {
            throw ErrorHelper.BadRequest("Module enrollment must be active to request recovery.");
        }

        if (enrollment.ProgramEnrollmentId.HasValue)
        {
            var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                enrollment.ProgramEnrollmentId.Value);
            if (programEnrollment != null
                && !programEnrollment.IsDeleted
                && programEnrollment.Status != EnrollmentStatus.Active)
            {
                throw ErrorHelper.Forbidden(QuizAttemptValidator.EnrollmentNotActiveMessage);
            }
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(request.AssignmentId)
            ?? throw ErrorHelper.NotFound($"Assignment '{request.AssignmentId}' not found.");

        if (assignment.IsDeleted || assignment.ModuleId != enrollment.ModuleId)
        {
            throw ErrorHelper.BadRequest("Assignment does not belong to this module enrollment.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
        if (AssessmentAttemptPolicy.IsUnlimitedAttempts(module)
            && !assignment.AvailableUntil.HasValue
            && !assignment.DueDate.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Theory modules allow unlimited attempts; request a personal deadline only when the assignment window is closed.");
        }

        var priorRequests = await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.ModuleEnrollmentId == enrollment.Id
                 && r.AssignmentId == assignment.Id
                 && !r.IsDeleted
                 && r.Status != AssessmentRecoveryRequestStatus.Withdrawn);

        if (priorRequests.Any(r => r.Status == AssessmentRecoveryRequestStatus.Pending))
        {
            throw ErrorHelper.Conflict("A pending recovery request already exists for this assignment.");
        }

        var decidedCount = priorRequests.Count(r =>
            r.Status is AssessmentRecoveryRequestStatus.Approved or AssessmentRecoveryRequestStatus.Rejected);
        if (decidedCount >= AssessmentAttemptPolicy.MaxRecoveryRequestsPerAssignment)
        {
            throw ErrorHelper.BadRequest(
                $"Recovery request limit ({AssessmentAttemptPolicy.MaxRecoveryRequestsPerAssignment}) reached. "
                + "Request class re-delivery if hands-on experience is needed again.");
        }

        if (!AssessmentAttemptPolicy.IsUnlimitedAttempts(module))
        {
            var effectiveMax = await AssessmentAttemptPolicy.GetEffectiveMaxAttemptsAsync(
                _unitOfWork,
                assignment,
                student.Id,
                enrollment.Id);
            var completed = await _unitOfWork.Submissions.GetAllAsync(
                s => s.AssignmentId == assignment.Id
                     && s.StudentId == student.Id
                     && !s.IsDeleted
                     && (s.Status == SubmissionStatus.Graded || s.Status == SubmissionStatus.TurnedIn));
            if (completed.Count < effectiveMax)
            {
                throw ErrorHelper.BadRequest(
                    "Attempts remain on this assignment. Use a normal retry before requesting recovery.");
            }
        }

        Guid? classId = null;
        if (enrollment.ProgramEnrollmentId.HasValue)
        {
            var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
                ce => ce.StudentId == student.Id
                      && ce.ProgramEnrollmentId == enrollment.ProgramEnrollmentId.Value
                      && ce.Status == ClassEnrollmentStatus.Active
                      && !ce.IsDeleted);
            classId = classEnrollment?.ClassId;
        }

        var entity = new AssessmentRecoveryRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ModuleEnrollmentId = enrollment.Id,
            AssignmentId = assignment.Id,
            ClassId = classId,
            Status = AssessmentRecoveryRequestStatus.Pending,
            StudentMessage = string.IsNullOrWhiteSpace(request.StudentMessage)
                ? null
                : request.StudentMessage.Trim(),
        };

        await _unitOfWork.AssessmentRecoveryRequests.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.AssessmentRecoveryRequested(
                entity.Id,
                student.Id,
                assignment.Id,
                enrollment.ModuleId,
                classId,
                assignment.Title));

        _logger.LogInformation(
            "[CreateAsync] Assessment recovery {RequestId} created by student {StudentId} for assignment {AssignmentId}.",
            entity.Id,
            student.Id,
            assignment.Id);

        return Map(entity);
    }

    public async Task<AssessmentRecoveryRequestResponseDto> WithdrawAsync(Guid requestId)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            "Only students can withdraw recovery requests.");

        var entity = await GetRequestOrThrow(requestId);
        if (entity.StudentId != student.Id)
        {
            throw ErrorHelper.Forbidden("This recovery request does not belong to you.");
        }

        if (entity.Status != AssessmentRecoveryRequestStatus.Pending)
        {
            throw ErrorHelper.BadRequest("Only pending recovery requests can be withdrawn.");
        }

        entity.Status = AssessmentRecoveryRequestStatus.Withdrawn;
        await _unitOfWork.AssessmentRecoveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<AssessmentRecoveryRequestResponseDto> ApproveAsync(
        Guid requestId,
        DecideAssessmentRecoveryRequestDto dto)
    {
        var mentor = await EnsureMentorOrStaffAsync();
        var entity = await GetRequestOrThrow(requestId);
        if (entity.Status != AssessmentRecoveryRequestStatus.Pending)
        {
            throw ErrorHelper.BadRequest("Only pending recovery requests can be approved.");
        }

        await EnsureCanDecideForRequestAsync(mentor, entity);

        if (dto.ExtraAttemptsGranted < 0)
        {
            throw ErrorHelper.BadRequest("ExtraAttemptsGranted cannot be negative.");
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(entity.AssignmentId);
        var module = assignment != null
            ? await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId)
            : null;

        if (!AssessmentAttemptPolicy.IsUnlimitedAttempts(module) && dto.ExtraAttemptsGranted < 1
            && !dto.PersonalDueDate.HasValue && !dto.PersonalAvailableUntil.HasValue)
        {
            throw ErrorHelper.BadRequest(
                "Approve must grant at least one extra attempt or a personal deadline.");
        }

        entity.Status = AssessmentRecoveryRequestStatus.Approved;
        entity.ExtraAttemptsGranted = dto.ExtraAttemptsGranted;
        entity.PersonalDueDate = dto.PersonalDueDate;
        entity.PersonalAvailableUntil = dto.PersonalAvailableUntil;
        entity.MentorNote = string.IsNullOrWhiteSpace(dto.MentorNote) ? null : dto.MentorNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = mentor.Id;

        await ReopenFailedResearchSubmissionIfNeededAsync(entity);

        await _unitOfWork.AssessmentRecoveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.AssessmentRecoveryApproved(
                entity.Id,
                entity.StudentId,
                entity.AssignmentId,
                entity.ExtraAttemptsGranted,
                assignment?.Title,
                module?.ProgramId));

        return Map(entity);
    }

    public async Task<AssessmentRecoveryRequestResponseDto> RejectAsync(
        Guid requestId,
        DecideAssessmentRecoveryRequestDto? dto)
    {
        var mentor = await EnsureMentorOrStaffAsync();
        var entity = await GetRequestOrThrow(requestId);
        if (entity.Status != AssessmentRecoveryRequestStatus.Pending)
        {
            throw ErrorHelper.BadRequest("Only pending recovery requests can be rejected.");
        }

        await EnsureCanDecideForRequestAsync(mentor, entity);

        entity.Status = AssessmentRecoveryRequestStatus.Rejected;
        entity.MentorNote = string.IsNullOrWhiteSpace(dto?.MentorNote) ? null : dto!.MentorNote.Trim();
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedBy = mentor.Id;

        await _unitOfWork.AssessmentRecoveryRequests.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(entity.AssignmentId);
        var module = assignment != null
            ? await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId)
            : null;

        if (assignment != null)
        {
            await _programPurchaseLifecycle.TryCloseAfterFailedAssignmentAsync(
                entity.StudentId,
                entity.AssignmentId,
                entity.ModuleEnrollmentId);
        }

        await _notificationPublisher.PublishAsync(
            NotificationCatalog.AssessmentRecoveryRejected(
                entity.Id,
                entity.StudentId,
                entity.AssignmentId,
                module?.ProgramId));

        return Map(entity);
    }

    public async Task<List<AssessmentRecoveryRequestResponseDto>> GetMineAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        var items = await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.StudentId == userId && !r.IsDeleted);
        return items.OrderByDescending(r => r.CreatedAt).Select(Map).ToList();
    }

    public async Task<List<AssessmentRecoveryRequestResponseDto>> GetPendingForMentorAsync()
    {
        var mentor = await EnsureMentorOrStaffAsync();
        var pending = await _unitOfWork.AssessmentRecoveryRequests.GetAllAsync(
            r => r.Status == AssessmentRecoveryRequestStatus.Pending && !r.IsDeleted);

        if (mentor.Role is RoleType.Admin or RoleType.Manager)
        {
            return pending.OrderByDescending(r => r.CreatedAt).Select(Map).ToList();
        }

        var mentoredClassIds = (await _unitOfWork.Classes.GetAllAsync(
                c => c.MentorId == mentor.Id && !c.IsDeleted))
            .Select(c => c.Id)
            .ToHashSet();

        return pending
            .Where(r => r.ClassId.HasValue && mentoredClassIds.Contains(r.ClassId.Value))
            .OrderByDescending(r => r.CreatedAt)
            .Select(Map)
            .ToList();
    }

    private async Task ReopenFailedResearchSubmissionIfNeededAsync(AssessmentRecoveryRequest entity)
    {
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(entity.AssignmentId);
        if (assignment == null || assignment.AssignmentType != AssignmentType.FileUpload)
        {
            // Research milestones use FileUpload-linked assignments with ResearchMilestone.
        }

        var submission = await _unitOfWork.Submissions.FirstOrDefaultAsync(
            s => s.AssignmentId == entity.AssignmentId
                 && s.StudentId == entity.StudentId
                 && s.ModuleEnrollmentId == entity.ModuleEnrollmentId
                 && !s.IsDeleted
                 && s.ResearchMilestoneId != null
                 && s.Status == SubmissionStatus.Graded);

        if (submission == null || !submission.AssignedGrade.HasValue || assignment == null)
        {
            return;
        }

        if (submission.AssignedGrade.Value >= assignment.PassScore)
        {
            return;
        }

        submission.Status = SubmissionStatus.ReturnedForRevision;
        submission.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Submissions.Update(submission);
    }

    private async Task EnsureCanDecideForRequestAsync(User mentor, AssessmentRecoveryRequest entity)
    {
        if (mentor.Role is RoleType.Admin or RoleType.Manager)
        {
            return;
        }

        if (!entity.ClassId.HasValue)
        {
            throw ErrorHelper.Forbidden("No class linked to this request; only managers can decide.");
        }

        var cls = await _unitOfWork.Classes.GetByIdAsync(entity.ClassId.Value);
        if (cls == null || cls.MentorId != mentor.Id)
        {
            throw ErrorHelper.Forbidden("You are not the mentor for this student's class.");
        }
    }

    private async Task<User> EnsureMentorOrStaffAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        var user = await _unitOfWork.Users.GetByIdAsync(userId)
            ?? throw ErrorHelper.Unauthorized("User not found.");

        if (user.Role is not (RoleType.Mentor or RoleType.Manager or RoleType.Admin))
        {
            throw ErrorHelper.Forbidden("Only mentors, managers, or admins can decide recovery requests.");
        }

        return user;
    }

    private async Task<AssessmentRecoveryRequest> GetRequestOrThrow(Guid requestId)
    {
        var entity = await _unitOfWork.AssessmentRecoveryRequests.GetByIdAsync(requestId);
        if (entity == null || entity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assessment recovery request '{requestId}' not found.");
        }

        return entity;
    }

    private static AssessmentRecoveryRequestResponseDto Map(AssessmentRecoveryRequest entity)
        => new()
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            ModuleEnrollmentId = entity.ModuleEnrollmentId,
            AssignmentId = entity.AssignmentId,
            ClassId = entity.ClassId,
            Status = entity.Status,
            StudentMessage = entity.StudentMessage,
            MentorNote = entity.MentorNote,
            ExtraAttemptsGranted = entity.ExtraAttemptsGranted,
            PersonalDueDate = entity.PersonalDueDate,
            PersonalAvailableUntil = entity.PersonalAvailableUntil,
            DecidedAt = entity.DecidedAt,
            DecidedBy = entity.DecidedBy,
            CreatedAt = entity.CreatedAt,
        };
}
