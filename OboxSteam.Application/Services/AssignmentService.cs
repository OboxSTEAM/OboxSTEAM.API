using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class AssignmentService : IAssignmentService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignmentService> _logger;
    private readonly INotificationPublisher _notificationPublisher;

    public AssignmentService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ILogger<AssignmentService> logger,
        INotificationPublisher notificationPublisher)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<AssignmentResponseDto> CreateAssignment(CreateAssignmentRequestDto request)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "CreateAssignment started by UserId={UserId} for ModuleId={ModuleId}",
            userId, request.ModuleId);

        AssignmentValidator.ValidateRequiredFields(request.Code, request.Title);
        AssignmentValidator.ValidateCommonFields(
            request.MaxPoints,
            request.PassScore,
            request.MaxAttempts,
            request.TimeLimitMinutes);

        var module = await _unitOfWork.Modules.GetByIdAsync(request.ModuleId);
        AssignmentValidator.ValidateModuleExists(module);

        await AssignmentValidator.ValidateCourseBelongsToModuleAsync(
            _unitOfWork, request.CourseId, request.ModuleId);

        var duplicate = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code.ToLower() == request.Code.Trim().ToLower() && !a.IsDeleted);

        if (duplicate != null)
            throw ErrorHelper.Conflict($"Assignment with code '{request.Code}' already exists.");

        await AssignmentValidator.ValidateQuizConfigAsync(
            _unitOfWork,
            request.AssignmentType,
            request.QuestionBankId,
            request.CourseId,
            request.ModuleId,
            request.EasyPercent,
            request.MediumPercent,
            request.HardPercent,
            request.QuestionCount);

        var now = DateTime.UtcNow;
        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            ModuleId = request.ModuleId,
            CourseId = request.CourseId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            AssignmentType = request.AssignmentType,
            MaxPoints = request.MaxPoints,
            PassScore = request.PassScore,
            IsRequiredForModulePass = request.IsRequiredForModulePass,
            DueDate = request.DueDate,
            AvailableFrom = request.AvailableFrom,
            AvailableUntil = request.AvailableUntil,
            AllowShuffle = request.AllowShuffle,
            QuestionBankId = request.QuestionBankId,
            QuestionCount = request.QuestionCount,
            ShuffleOptions = request.ShuffleOptions,
            EasyPercent = request.EasyPercent,
            MediumPercent = request.MediumPercent,
            HardPercent = request.HardPercent,
            TimeLimitMinutes = request.TimeLimitMinutes,
            MaxAttempts = request.MaxAttempts,
            CreatedAt = now,
            CreatedBy = userId,
            IsDeleted = false
        };

        await _unitOfWork.Assignments.AddAsync(assignment);
        await _unitOfWork.SaveChangesAsync();

        await PublishAssignmentPublishedAsync(assignment, module!);

        _logger.LogInformation(
            "CreateAssignment completed. AssignmentId={AssignmentId}",
            assignment.Id);

        return new AssignmentResponseDto
        {
            Id = assignment.Id,
            Code = assignment.Code,
            ModuleId = assignment.ModuleId,
            CourseId = assignment.CourseId,
            Title = assignment.Title,
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            DueDate = assignment.DueDate,
            AvailableFrom = assignment.AvailableFrom,
            AvailableUntil = assignment.AvailableUntil,
            AllowShuffle = assignment.AllowShuffle,
            QuestionBankId = assignment.QuestionBankId,
            QuestionCount = assignment.QuestionCount,
            ShuffleOptions = assignment.ShuffleOptions,
            EasyPercent = assignment.EasyPercent,
            MediumPercent = assignment.MediumPercent,
            HardPercent = assignment.HardPercent,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            MaxAttempts = assignment.MaxAttempts,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };
    }

    public async Task<AssignmentResponseDto?> GetAssignmentById(Guid assignmentId)
    {
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
            return null;

        await QuizAttemptValidator.ValidateStudentModuleAccessAsync(
            _unitOfWork,
            _claimsService,
            assignment);

        return new AssignmentResponseDto
        {
            Id = assignment.Id,
            Code = assignment.Code,
            ModuleId = assignment.ModuleId,
            CourseId = assignment.CourseId,
            Title = assignment.Title,
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            DueDate = assignment.DueDate,
            AvailableFrom = assignment.AvailableFrom,
            AvailableUntil = assignment.AvailableUntil,
            AllowShuffle = assignment.AllowShuffle,
            QuestionBankId = assignment.QuestionBankId,
            QuestionCount = assignment.QuestionCount,
            ShuffleOptions = assignment.ShuffleOptions,
            EasyPercent = assignment.EasyPercent,
            MediumPercent = assignment.MediumPercent,
            HardPercent = assignment.HardPercent,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            MaxAttempts = assignment.MaxAttempts,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };
    }

    public async Task<AssignmentResponseDto?> UpdateAssignment(
        Guid assignmentId,
        UpdateAssignmentRequestDto request)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "UpdateAssignment started by UserId={UserId} for AssignmentId={AssignmentId}",
            userId, assignmentId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
            return null;

        var caller = await _unitOfWork.Users.GetByIdAsync(userId);
        if (caller == null || caller.IsDeleted)
            throw ErrorHelper.Unauthorized("Unauthorized access.");

        var isMentor = caller.Role == RoleType.Mentor;
        if (isMentor)
        {
            await MentorScopeValidator.EnsureMentorOwnsAssignmentAsync(_unitOfWork, userId, assignment);
            if (HasMentorRestrictedFields(request))
            {
                throw ErrorHelper.Forbidden(
                    "Mentors may only update Title, Description, DueDate, AvailableFrom, and AvailableUntil.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Code)
            && !string.Equals(request.Code.Trim(), assignment.Code, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _unitOfWork.Assignments.FirstOrDefaultAsync(
                a => a.Id != assignmentId
                     && a.Code.ToLower() == request.Code.Trim().ToLower()
                     && !a.IsDeleted);

            if (duplicate != null)
                throw ErrorHelper.Conflict($"Assignment with code '{request.Code}' already exists.");
        }

        var moduleId = request.ModuleId ?? assignment.ModuleId;
        if (request.ModuleId.HasValue)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
            AssignmentValidator.ValidateModuleExists(module);
        }

        var courseId = request.CourseId ?? assignment.CourseId;
        if (request.ModuleId.HasValue || request.CourseId.HasValue)
        {
            await AssignmentValidator.ValidateCourseBelongsToModuleAsync(
                _unitOfWork, courseId, moduleId);
        }

        var assignmentType = request.AssignmentType ?? assignment.AssignmentType;
        var questionBankId = request.QuestionBankId ?? assignment.QuestionBankId;
        var easyPercent = request.EasyPercent ?? assignment.EasyPercent;
        var mediumPercent = request.MediumPercent ?? assignment.MediumPercent;
        var hardPercent = request.HardPercent ?? assignment.HardPercent;
        var questionCount = request.QuestionCount ?? assignment.QuestionCount;
        var maxPoints = request.MaxPoints ?? assignment.MaxPoints;
        var passScore = request.PassScore ?? assignment.PassScore;
        var maxAttempts = request.MaxAttempts ?? assignment.MaxAttempts;
        var timeLimitMinutes = request.TimeLimitMinutes ?? assignment.TimeLimitMinutes;

        AssignmentValidator.ValidateCommonFields(maxPoints, passScore, maxAttempts, timeLimitMinutes);

        await AssignmentValidator.ValidateQuizConfigAsync(
            _unitOfWork,
            assignmentType,
            questionBankId,
            courseId,
            moduleId,
            easyPercent,
            mediumPercent,
            hardPercent,
            questionCount);

        if (!string.IsNullOrWhiteSpace(request.Code))
            assignment.Code = request.Code.Trim();

        if (request.ModuleId.HasValue)
            assignment.ModuleId = request.ModuleId.Value;

        if (request.CourseId.HasValue || request.ModuleId.HasValue)
            assignment.CourseId = courseId;

        if (!string.IsNullOrWhiteSpace(request.Title))
            assignment.Title = request.Title.Trim();

        if (request.Description != null)
            assignment.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

        if (request.AssignmentType.HasValue)
            assignment.AssignmentType = request.AssignmentType.Value;

        if (request.MaxPoints.HasValue)
            assignment.MaxPoints = request.MaxPoints.Value;

        if (request.PassScore.HasValue)
            assignment.PassScore = request.PassScore.Value;

        if (request.IsRequiredForModulePass.HasValue)
            assignment.IsRequiredForModulePass = request.IsRequiredForModulePass.Value;

        if (request.DueDate.HasValue)
            assignment.DueDate = request.DueDate;

        if (request.AvailableFrom.HasValue)
            assignment.AvailableFrom = request.AvailableFrom;

        if (request.AvailableUntil.HasValue)
            assignment.AvailableUntil = request.AvailableUntil;

        if (request.AllowShuffle.HasValue)
            assignment.AllowShuffle = request.AllowShuffle.Value;

        if (request.QuestionBankId.HasValue)
            assignment.QuestionBankId = request.QuestionBankId;

        if (request.QuestionCount.HasValue)
            assignment.QuestionCount = request.QuestionCount;

        if (request.ShuffleOptions.HasValue)
            assignment.ShuffleOptions = request.ShuffleOptions.Value;

        if (request.EasyPercent.HasValue)
            assignment.EasyPercent = request.EasyPercent.Value;

        if (request.MediumPercent.HasValue)
            assignment.MediumPercent = request.MediumPercent.Value;

        if (request.HardPercent.HasValue)
            assignment.HardPercent = request.HardPercent.Value;

        if (request.TimeLimitMinutes.HasValue)
            assignment.TimeLimitMinutes = request.TimeLimitMinutes;

        if (request.MaxAttempts.HasValue)
            assignment.MaxAttempts = request.MaxAttempts.Value;

        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = userId;

        await _unitOfWork.Assignments.Update(assignment);
        await _unitOfWork.SaveChangesAsync();

        if (isMentor)
        {
            var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
            await _notificationPublisher.PublishAsync(
                NotificationCatalog.AssignmentEditedByMentor(
                    assignment.Id,
                    userId,
                    module?.ProgramId ?? Guid.Empty,
                    assignment.Title));
        }

        _logger.LogInformation(
            "UpdateAssignment completed. AssignmentId={AssignmentId}",
            assignmentId);

        return new AssignmentResponseDto
        {
            Id = assignment.Id,
            Code = assignment.Code,
            ModuleId = assignment.ModuleId,
            CourseId = assignment.CourseId,
            Title = assignment.Title,
            Description = assignment.Description,
            AssignmentType = assignment.AssignmentType,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            DueDate = assignment.DueDate,
            AvailableFrom = assignment.AvailableFrom,
            AvailableUntil = assignment.AvailableUntil,
            AllowShuffle = assignment.AllowShuffle,
            QuestionBankId = assignment.QuestionBankId,
            QuestionCount = assignment.QuestionCount,
            ShuffleOptions = assignment.ShuffleOptions,
            EasyPercent = assignment.EasyPercent,
            MediumPercent = assignment.MediumPercent,
            HardPercent = assignment.HardPercent,
            TimeLimitMinutes = assignment.TimeLimitMinutes,
            MaxAttempts = assignment.MaxAttempts,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };
    }

    private static bool HasMentorRestrictedFields(UpdateAssignmentRequestDto request)
        => request.Code != null
           || request.ModuleId.HasValue
           || request.CourseId.HasValue
           || request.AssignmentType.HasValue
           || request.MaxPoints.HasValue
           || request.PassScore.HasValue
           || request.IsRequiredForModulePass.HasValue
           || request.AllowShuffle.HasValue
           || request.QuestionBankId.HasValue
           || request.QuestionCount.HasValue
           || request.ShuffleOptions.HasValue
           || request.EasyPercent.HasValue
           || request.MediumPercent.HasValue
           || request.HardPercent.HasValue
           || request.TimeLimitMinutes.HasValue
           || request.MaxAttempts.HasValue;

    public async Task<bool> DeleteAssignment(Guid assignmentId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "DeleteAssignment started by UserId={UserId} for AssignmentId={AssignmentId}",
            userId, assignmentId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
            return false;

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId && !s.IsDeleted);

        AssignmentValidator.ValidateCanDelete(submissions.Count);

        await _unitOfWork.Assignments.SoftRemove(assignment);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "DeleteAssignment completed. AssignmentId={AssignmentId}",
            assignmentId);

        return true;
    }

    /// <summary>
    /// Notifies rosters of Open/InProgress classes in the assignment's program.
    /// Skips when no active cohort exists yet.
    /// </summary>
    private async Task PublishAssignmentPublishedAsync(Assignment assignment, Module module)
    {
        var activeClasses = await _unitOfWork.Classes.GetAllAsync(
            c => c.ProgramId == module.ProgramId
                 && (c.Status == ClassStatus.Open || c.Status == ClassStatus.InProgress));

        if (activeClasses.Count == 0)
        {
            return;
        }

        var commands = activeClasses
            .Select(c => NotificationCatalog.AssignmentPublished(
                c.Id,
                assignment.Id,
                module.ProgramId,
                assignment.Title))
            .ToList();

        await _notificationPublisher.PublishManyAsync(commands);
    }
}
