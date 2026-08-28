using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
using OboxSteam.Application.Realtime;
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
    private readonly ISyncEventPublisher _syncEventPublisher;

    public AssignmentService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ILogger<AssignmentService> logger,
        INotificationPublisher notificationPublisher,
        ISyncEventPublisher syncEventPublisher)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _notificationPublisher = notificationPublisher;
        _syncEventPublisher = syncEventPublisher;
    }

    private async Task EnsureCurriculumEditableForModuleAsync(Guid moduleId)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
        if (module is null || module.IsDeleted)
        {
            return;
        }

        await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, module.ProgramId);
    }

    private async Task PublishCurriculumStructureChangedForModuleAsync(Guid moduleId)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
        if (module is null || module.IsDeleted)
        {
            return;
        }

        await _syncEventPublisher.PublishAsync(
            SyncScopes.CurriculumStructureChanged,
            NotificationAudience.ForProgramParticipants(module.ProgramId),
            entityType: "Program",
            entityId: module.ProgramId);
    }

    public Task<Pagination<AssignmentListItemDto>> GetAllAssignments(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? moduleId = null,
        Guid? programId = null,
        Guid? courseId = null,
        AssignmentType? assignmentType = null)
    {
        _logger.LogInformation(
            "[GetAllAssignments] Start — page: {Page}, pageSize: {PageSize}, search: '{Search}'",
            page, pageSize, search);

        var query = BuildAssignmentsQuery(
            search, sortBy, isDescending, moduleId, programId, courseId, assignmentType);

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AssignmentListItemDto
            {
                Id = a.Id,
                Code = a.Code,
                Title = a.Title,
                AssignmentType = a.AssignmentType,
                ModuleId = a.ModuleId,
                CourseId = a.CourseId,
                MaxPoints = a.MaxPoints,
                PassScore = a.PassScore,
                DueDate = a.DueDate,
                QuestionBankId = a.QuestionBankId,
                QuestionCount = a.QuestionCount,
                ModuleName = a.Module.Name,
                ProgramId = a.Module.ProgramId,
                ProgramName = a.Module.Program.Name,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            })
            .ToList();

        _logger.LogInformation(
            "[GetAllAssignments] Retrieved {Count}/{Total} assignments.",
            items.Count, totalCount);

        return Task.FromResult(new Pagination<AssignmentListItemDto>(items, totalCount, page, pageSize));
    }

    private IQueryable<Assignment> BuildAssignmentsQuery(
        string? search,
        string? sortBy,
        bool isDescending,
        Guid? moduleId,
        Guid? programId,
        Guid? courseId,
        AssignmentType? assignmentType)
    {
        var query = _unitOfWork.Assignments
            .GetQueryable()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(lowerSearch) ||
                a.Code.ToLower().Contains(lowerSearch) ||
                a.Module.Name.ToLower().Contains(lowerSearch) ||
                a.Module.Program.Name.ToLower().Contains(lowerSearch));
        }

        if (moduleId.HasValue)
            query = query.Where(a => a.ModuleId == moduleId.Value);

        if (programId.HasValue)
            query = query.Where(a => a.Module.ProgramId == programId.Value);

        if (courseId.HasValue)
            query = query.Where(a => a.CourseId == courseId.Value);

        if (assignmentType.HasValue)
            query = query.Where(a => a.AssignmentType == assignmentType.Value);

        return sortBy?.ToLower() switch
        {
            "title" => isDescending
                ? query.OrderByDescending(a => a.Title)
                : query.OrderBy(a => a.Title),
            "code" => isDescending
                ? query.OrderByDescending(a => a.Code)
                : query.OrderBy(a => a.Code),
            "duedate" => isDescending
                ? query.OrderByDescending(a => a.DueDate)
                : query.OrderBy(a => a.DueDate),
            "assignmenttype" => isDescending
                ? query.OrderByDescending(a => a.AssignmentType)
                : query.OrderBy(a => a.AssignmentType),
            "modulename" => isDescending
                ? query.OrderByDescending(a => a.Module.Name)
                : query.OrderBy(a => a.Module.Name),
            "programname" => isDescending
                ? query.OrderByDescending(a => a.Module.Program.Name)
                : query.OrderBy(a => a.Module.Program.Name),
            "createdat" => isDescending
                ? query.OrderByDescending(a => a.CreatedAt)
                : query.OrderBy(a => a.CreatedAt),
            _ => isDescending
                ? query.OrderByDescending(a => a.CreatedAt)
                : query.OrderBy(a => a.CreatedAt),
        };
    }

    public async Task<List<AssignmentSubmissionListItemDto>> GetAssignmentSubmissions(
        Guid assignmentId,
        Guid classId)
    {
        var userId = _claimsService.GetCurrentUserId;
        _logger.LogInformation(
            "GetAssignmentSubmissions started by UserId={UserId} for AssignmentId={AssignmentId}, ClassId={ClassId}",
            userId, assignmentId, classId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
            throw ErrorHelper.NotFound($"Assignment with id '{assignmentId}' not found.");

        var caller = await _unitOfWork.Users.GetByIdAsync(userId);
        if (caller == null || caller.IsDeleted)
            throw ErrorHelper.Unauthorized("Unauthorized access.");

        if (caller.Role == RoleType.Mentor)
        {
            await MentorScopeValidator.EnsureMentorOwnsClassForModuleAsync(
                _unitOfWork, userId, classId, assignment.ModuleId);
        }
        else
        {
            var classEntity = await _unitOfWork.Classes.GetByIdAsync(classId);
            ClassValidator.ValidateClassExists(classEntity, classId);

            var module = await _unitOfWork.Modules.GetByIdAsync(assignment.ModuleId);
            if (module == null || module.IsDeleted)
                throw ErrorHelper.NotFound($"Module with id '{assignment.ModuleId}' not found.");

            if (module.ProgramId != classEntity!.ProgramId)
                throw ErrorHelper.BadRequest(MentorScopeValidator.ClassProgramMismatchMessage);
        }

        var enrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var studentIds = enrollments.Select(ce => ce.StudentId).Distinct().ToList();
        if (studentIds.Count == 0)
            return [];

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && !s.IsDeleted
                 && studentIds.Contains(s.StudentId));

        var students = await _unitOfWork.Users.GetAllAsync(u => studentIds.Contains(u.Id));
        var studentNames = students.ToDictionary(u => u.Id, u => u.FullName);

        var items = submissions
            .Select(s => new AssignmentSubmissionListItemDto
            {
                SubmissionId = s.Id,
                StudentId = s.StudentId,
                StudentName = studentNames.TryGetValue(s.StudentId, out var name) ? name : null,
                AttemptNumber = s.AttemptNumber,
                Status = s.Status,
                AssignedGrade = s.AssignedGrade,
                Passed = s.Status == SubmissionStatus.Graded && s.AssignedGrade.HasValue
                    ? s.AssignedGrade.Value >= assignment.PassScore
                    : null,
                SubmittedAt = s.SubmittedAt,
                GradedAt = s.GradedAt
            })
            .OrderBy(i => i.StudentName)
            .ThenByDescending(i => i.AttemptNumber)
            .ToList();

        _logger.LogInformation(
            "GetAssignmentSubmissions retrieved {Count} submission(s). AssignmentId={AssignmentId}, ClassId={ClassId}",
            items.Count, assignmentId, classId);

        return items;
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

        await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, module!.ProgramId);

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
        await PublishCurriculumStructureChangedForModuleAsync(assignment.ModuleId);

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

        var originalModuleId = assignment.ModuleId;

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
        else
        {
            // Manager edits are structural curriculum changes; mentor edits (due dates etc.)
            // are operational and stay allowed while their class is in progress.
            await EnsureCurriculumEditableForModuleAsync(originalModuleId);
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

            if (moduleId != originalModuleId)
            {
                await CurriculumEditGuard.EnsureProgramCurriculumEditableAsync(_unitOfWork, module!.ProgramId);
            }
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
                    assignment.Title,
                    assignment.ModuleId));
        }

        await PublishCurriculumStructureChangedForModuleAsync(assignment.ModuleId);
        if (originalModuleId != assignment.ModuleId)
        {
            // The tree of the previous module's program changed as well.
            await PublishCurriculumStructureChangedForModuleAsync(originalModuleId);
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

        await EnsureCurriculumEditableForModuleAsync(assignment.ModuleId);

        await _unitOfWork.Assignments.SoftRemove(assignment);
        await _unitOfWork.SaveChangesAsync();

        await PublishCurriculumStructureChangedForModuleAsync(assignment.ModuleId);

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
                assignment.Title,
                module.Id,
                className: c.Name))
            .ToList();

        await _notificationPublisher.PublishManyAsync(commands);
    }
}
