using Microsoft.Extensions.Logging;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.DTOs.ResearchMilestoneDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ResearchMilestoneService : IResearchMilestoneService
{
    private readonly IClaimsService _claimsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResearchMilestoneService> _logger;

    public ResearchMilestoneService(
        IClaimsService claimsService,
        IUnitOfWork unitOfWork,
        ILogger<ResearchMilestoneService> logger)
    {
        _claimsService = claimsService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ResearchMilestoneResponseDto> CreateMilestone(
        Guid moduleId,
        CreateResearchMilestoneRequestDto request)
    {
        var user = await ResearchMilestoneValidator.EnsureCanMutateMilestoneAsync(_unitOfWork, _claimsService);
        _logger.LogInformation(
            "CreateMilestone started by UserId={UserId} for ModuleId={ModuleId}",
            user.Id,
            moduleId);

        var moduleEntity = await _unitOfWork.Modules.GetByIdAsync(moduleId);
        ResearchMilestoneValidator.ValidateResearchModule(moduleEntity, moduleId);

        AssignmentValidator.ValidateRequiredFields(request.Code, request.Title);
        AssignmentValidator.ValidateRequiredFields(request.AssignmentCode, request.AssignmentTitle);
        AssignmentValidator.ValidateCommonFields(
            request.MaxPoints,
            request.PassScore,
            request.MaxAttempts,
            timeLimitMinutes: null);

        var duplicateMilestone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
            rm => rm.Code.ToLower() == request.Code.Trim().ToLower() && !rm.IsDeleted);

        if (duplicateMilestone != null)
        {
            throw ErrorHelper.Conflict($"Research milestone with code '{request.Code}' already exists.");
        }

        var duplicateAssignment = await _unitOfWork.Assignments.FirstOrDefaultAsync(
            a => a.Code.ToLower() == request.AssignmentCode.Trim().ToLower() && !a.IsDeleted);

        if (duplicateAssignment != null)
        {
            throw ErrorHelper.Conflict($"Assignment with code '{request.AssignmentCode}' already exists.");
        }

        var moduleMilestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == moduleId && !rm.IsDeleted);
        var currentMaxOrder = moduleMilestones.Count == 0 ? 0 : moduleMilestones.Max(rm => rm.MilestoneOrder);

        SequentialOrderValidator.ValidateMustExceedMax(
            request.MilestoneOrder,
            currentMaxOrder,
            orderPropertyName: "MilestoneOrder",
            scopeDescription: $"module '{moduleId}'");

        if (request.IsCapstone)
        {
            var existingCapstone = moduleMilestones.FirstOrDefault(rm => rm.IsCapstone);
            ResearchMilestoneValidator.ValidateCapstoneUniqueness(true, existingCapstone, currentMilestoneId: null);
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            Code = request.AssignmentCode.Trim(),
            ModuleId = moduleId,
            CourseId = null,
            Title = request.AssignmentTitle.Trim(),
            Description = request.AssignmentDescription?.Trim(),
            AssignmentType = request.AssignmentType,
            MaxPoints = request.MaxPoints,
            PassScore = request.PassScore,
            IsRequiredForModulePass = true,
            DueDate = request.DueDate,
            AvailableFrom = request.AvailableFrom,
            AvailableUntil = request.AvailableUntil,
            MaxAttempts = request.MaxAttempts,
            IsDeleted = false
        };

        var milestone = new ResearchMilestone
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            ModuleId = moduleId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            MilestoneOrder = request.MilestoneOrder,
            IsCapstone = request.IsCapstone,
            AssignmentId = assignment.Id,
            IsDeleted = false
        };

        await _unitOfWork.Assignments.AddAsync(assignment);
        await _unitOfWork.ResearchMilestones.AddAsync(milestone);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "CreateMilestone completed. MilestoneId={MilestoneId}, AssignmentId={AssignmentId}",
            milestone.Id,
            assignment.Id);

        return new ResearchMilestoneResponseDto
        {
            Id = milestone.Id,
            Code = milestone.Code,
            ModuleId = milestone.ModuleId,
            Title = milestone.Title,
            Description = milestone.Description,
            MilestoneOrder = milestone.MilestoneOrder,
            IsCapstone = milestone.IsCapstone,
            AssignmentId = milestone.AssignmentId,
            Assignment = new AssignmentResponseDto
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
            },
            Activities = [],
            CreatedAt = milestone.CreatedAt,
            UpdatedAt = milestone.UpdatedAt
        };
    }

    public async Task<ResearchMilestoneResponseDto?> GetMilestoneById(Guid milestoneId)
    {
        var milestone = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        if (milestone == null || milestone.IsDeleted)
        {
            return null;
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            return null;
        }

        var activityLinks = await ResearchMilestoneValidator.LoadActivityLinksAsync(_unitOfWork, milestoneId);
        return new ResearchMilestoneResponseDto
        {
            Id = milestone.Id,
            Code = milestone.Code,
            ModuleId = milestone.ModuleId,
            Title = milestone.Title,
            Description = milestone.Description,
            MilestoneOrder = milestone.MilestoneOrder,
            IsCapstone = milestone.IsCapstone,
            AssignmentId = milestone.AssignmentId,
            Assignment = new AssignmentResponseDto
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
            },
            Activities = activityLinks
                .Where(link => link.Activity != null && !link.Activity.IsDeleted)
                .OrderBy(link => link.DisplayOrder)
                .Select(link => new ResearchMilestoneActivityResponseDto
                {
                    Id = link.Id,
                    ActivityId = link.ActivityId,
                    ActivityCode = link.Activity!.Code,
                    ActivityTitle = link.Activity.Name,
                    ActivityType = link.Activity.ActivityType,
                    IsRequiredForSubmission = link.IsRequiredForSubmission,
                    DisplayOrder = link.DisplayOrder
                })
                .ToList(),
            CreatedAt = milestone.CreatedAt,
            UpdatedAt = milestone.UpdatedAt
        };
    }

    public async Task<List<ResearchMilestoneResponseDto>> GetMilestonesByModule(Guid moduleId)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(moduleId);
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{moduleId}' not found.");
        }

        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == moduleId && !rm.IsDeleted);

        if (milestones.Count == 0)
        {
            return [];
        }

        milestones = milestones.OrderBy(rm => rm.MilestoneOrder).ToList();

        var assignmentIds = milestones.Select(rm => rm.AssignmentId).Distinct().ToList();
        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => assignmentIds.Contains(a.Id) && !a.IsDeleted);
        var assignmentsById = assignments.ToDictionary(a => a.Id);

        var milestoneIds = milestones.Select(rm => rm.Id).ToList();
        var activityLinks = await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            link => milestoneIds.Contains(link.ResearchMilestoneId) && !link.IsDeleted,
            link => link.Activity);
        var linksByMilestoneId = activityLinks
            .GroupBy(link => link.ResearchMilestoneId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var results = new List<ResearchMilestoneResponseDto>();

        foreach (var milestone in milestones)
        {
            if (!assignmentsById.TryGetValue(milestone.AssignmentId, out var assignment))
            {
                continue;
            }

            linksByMilestoneId.TryGetValue(milestone.Id, out var links);
            links ??= [];

            results.Add(new ResearchMilestoneResponseDto
            {
                Id = milestone.Id,
                Code = milestone.Code,
                ModuleId = milestone.ModuleId,
                Title = milestone.Title,
                Description = milestone.Description,
                MilestoneOrder = milestone.MilestoneOrder,
                IsCapstone = milestone.IsCapstone,
                AssignmentId = milestone.AssignmentId,
                Assignment = new AssignmentResponseDto
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
                },
                Activities = links
                    .Where(link => link.Activity != null && !link.Activity.IsDeleted)
                    .OrderBy(link => link.DisplayOrder)
                    .Select(link => new ResearchMilestoneActivityResponseDto
                    {
                        Id = link.Id,
                        ActivityId = link.ActivityId,
                        ActivityCode = link.Activity!.Code,
                        ActivityTitle = link.Activity.Name,
                        ActivityType = link.Activity.ActivityType,
                        IsRequiredForSubmission = link.IsRequiredForSubmission,
                        DisplayOrder = link.DisplayOrder
                    })
                    .ToList(),
                CreatedAt = milestone.CreatedAt,
                UpdatedAt = milestone.UpdatedAt
            });
        }

        return results;
    }

    public async Task<ResearchMilestoneResponseDto?> UpdateMilestone(
        Guid milestoneId,
        UpdateResearchMilestoneRequestDto request)
    {
        var user = await ResearchMilestoneValidator.EnsureCanMutateMilestoneAsync(_unitOfWork, _claimsService);
        _logger.LogInformation(
            "UpdateMilestone started by UserId={UserId} for MilestoneId={MilestoneId}",
            user.Id,
            milestoneId);

        var milestoneEntity = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        var milestone = ResearchMilestoneValidator.ValidateMilestoneExists(milestoneEntity, milestoneId);

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment for milestone '{milestoneId}' not found.");
        }

        if (request.MilestoneOrder.HasValue && request.MilestoneOrder.Value != milestone.MilestoneOrder)
        {
            await ResearchMilestoneValidator.ValidateMilestoneOrderUniqueAsync(
                _unitOfWork,
                milestone.ModuleId,
                request.MilestoneOrder.Value,
                milestoneId);
            milestone.MilestoneOrder = request.MilestoneOrder.Value;
        }

        if (request.IsCapstone.HasValue)
        {
            if (request.IsCapstone.Value)
            {
                var existingCapstone = await _unitOfWork.ResearchMilestones.FirstOrDefaultAsync(
                    rm => rm.ModuleId == milestone.ModuleId && rm.IsCapstone && !rm.IsDeleted);
                ResearchMilestoneValidator.ValidateCapstoneUniqueness(
                    true,
                    existingCapstone,
                    milestoneId);
            }

            milestone.IsCapstone = request.IsCapstone.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            milestone.Title = request.Title.Trim();
        }

        if (request.Description != null)
        {
            milestone.Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
        }

        var maxPoints = request.MaxPoints ?? assignment.MaxPoints;
        var passScore = request.PassScore ?? assignment.PassScore;
        AssignmentValidator.ValidateCommonFields(maxPoints, passScore, assignment.MaxAttempts, assignment.TimeLimitMinutes);

        if (!string.IsNullOrWhiteSpace(request.AssignmentTitle))
        {
            assignment.Title = request.AssignmentTitle.Trim();
        }

        if (request.AssignmentDescription != null)
        {
            assignment.Description = string.IsNullOrWhiteSpace(request.AssignmentDescription)
                ? null
                : request.AssignmentDescription.Trim();
        }

        if (request.MaxPoints.HasValue)
        {
            assignment.MaxPoints = request.MaxPoints.Value;
        }

        if (request.PassScore.HasValue)
        {
            assignment.PassScore = request.PassScore.Value;
        }

        if (request.DueDate.HasValue)
        {
            assignment.DueDate = request.DueDate;
        }

        if (request.AvailableFrom.HasValue)
        {
            assignment.AvailableFrom = request.AvailableFrom;
        }

        if (request.AvailableUntil.HasValue)
        {
            assignment.AvailableUntil = request.AvailableUntil;
        }

        await _unitOfWork.ResearchMilestones.Update(milestone);
        await _unitOfWork.Assignments.Update(assignment);
        await _unitOfWork.SaveChangesAsync();

        var activityLinks = await ResearchMilestoneValidator.LoadActivityLinksAsync(_unitOfWork, milestoneId);
        return new ResearchMilestoneResponseDto
        {
            Id = milestone.Id,
            Code = milestone.Code,
            ModuleId = milestone.ModuleId,
            Title = milestone.Title,
            Description = milestone.Description,
            MilestoneOrder = milestone.MilestoneOrder,
            IsCapstone = milestone.IsCapstone,
            AssignmentId = milestone.AssignmentId,
            Assignment = new AssignmentResponseDto
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
            },
            Activities = activityLinks
                .Where(link => link.Activity != null && !link.Activity.IsDeleted)
                .OrderBy(link => link.DisplayOrder)
                .Select(link => new ResearchMilestoneActivityResponseDto
                {
                    Id = link.Id,
                    ActivityId = link.ActivityId,
                    ActivityCode = link.Activity!.Code,
                    ActivityTitle = link.Activity.Name,
                    ActivityType = link.Activity.ActivityType,
                    IsRequiredForSubmission = link.IsRequiredForSubmission,
                    DisplayOrder = link.DisplayOrder
                })
                .ToList(),
            CreatedAt = milestone.CreatedAt,
            UpdatedAt = milestone.UpdatedAt
        };
    }

    public async Task<bool> DeleteMilestone(Guid milestoneId)
    {
        var user = await ResearchMilestoneValidator.EnsureCanMutateMilestoneAsync(_unitOfWork, _claimsService);
        _logger.LogInformation(
            "DeleteMilestone started by UserId={UserId} for MilestoneId={MilestoneId}",
            user.Id,
            milestoneId);

        var milestoneEntity = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        var milestone = ResearchMilestoneValidator.ValidateMilestoneExists(milestoneEntity, milestoneId);

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.ResearchMilestoneId == milestoneId && !s.IsDeleted);
        ResearchMilestoneValidator.ValidateCanDeleteMilestone(submissions.Count);

        var activityLinks = await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            link => link.ResearchMilestoneId == milestoneId && !link.IsDeleted);

        if (activityLinks.Count > 0)
        {
            await _unitOfWork.ResearchMilestoneActivities.SoftRemoveRange(activityLinks);
        }

        var assignment = await _unitOfWork.Assignments.GetByIdAsync(milestone.AssignmentId);
        if (assignment != null && !assignment.IsDeleted)
        {
            await _unitOfWork.Assignments.SoftRemove(assignment);
        }

        await _unitOfWork.ResearchMilestones.SoftRemove(milestone);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("DeleteMilestone completed. MilestoneId={MilestoneId}", milestoneId);
        return true;
    }

    public async Task<ResearchMilestoneActivityResponseDto> LinkActivity(
        Guid milestoneId,
        LinkMilestoneActivityRequestDto request)
    {
        var milestoneEntity = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        var milestone = ResearchMilestoneValidator.ValidateMilestoneExists(milestoneEntity, milestoneId);

        await ResearchMilestoneValidator.EnsureCanMutateActivityLinkAsync(
            _unitOfWork,
            _claimsService,
            milestone.ModuleId);

        await ResearchMilestoneValidator.ValidateActivityBelongsToModuleAsync(
            _unitOfWork,
            request.ActivityId,
            milestone.ModuleId);

        var duplicateLink = await _unitOfWork.ResearchMilestoneActivities.FirstOrDefaultAsync(
            link => link.ResearchMilestoneId == milestoneId
                    && link.ActivityId == request.ActivityId
                    && !link.IsDeleted);

        if (duplicateLink != null)
        {
            throw ErrorHelper.Conflict("This activity is already linked to the milestone.");
        }

        var activity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId);
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{request.ActivityId}' not found.");
        }

        var link = new ResearchMilestoneActivity
        {
            ResearchMilestoneId = milestoneId,
            ActivityId = request.ActivityId,
            IsRequiredForSubmission = request.IsRequiredForSubmission,
            DisplayOrder = request.DisplayOrder
        };

        await _unitOfWork.ResearchMilestoneActivities.AddAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return new ResearchMilestoneActivityResponseDto
        {
            Id = link.Id,
            ActivityId = link.ActivityId,
            ActivityCode = activity.Code,
            ActivityTitle = activity.Name,
            ActivityType = activity.ActivityType,
            IsRequiredForSubmission = link.IsRequiredForSubmission,
            DisplayOrder = link.DisplayOrder
        };
    }

    public async Task<ResearchMilestoneActivityResponseDto?> UpdateActivityLink(
        Guid milestoneId,
        Guid activityId,
        UpdateMilestoneActivityLinkRequestDto request)
    {
        var milestoneEntity = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        var milestone = ResearchMilestoneValidator.ValidateMilestoneExists(milestoneEntity, milestoneId);

        await ResearchMilestoneValidator.EnsureCanMutateActivityLinkAsync(
            _unitOfWork,
            _claimsService,
            milestone.ModuleId);

        var link = await _unitOfWork.ResearchMilestoneActivities.FirstOrDefaultAsync(
            l => l.ResearchMilestoneId == milestoneId
                 && l.ActivityId == activityId
                 && !l.IsDeleted);

        if (link == null)
        {
            return null;
        }

        if (request.IsRequiredForSubmission.HasValue)
        {
            link.IsRequiredForSubmission = request.IsRequiredForSubmission.Value;
        }

        if (request.DisplayOrder.HasValue)
        {
            link.DisplayOrder = request.DisplayOrder.Value;
        }

        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        await _unitOfWork.ResearchMilestoneActivities.Update(link);
        await _unitOfWork.SaveChangesAsync();

        return new ResearchMilestoneActivityResponseDto
        {
            Id = link.Id,
            ActivityId = link.ActivityId,
            ActivityCode = activity.Code,
            ActivityTitle = activity.Name,
            ActivityType = activity.ActivityType,
            IsRequiredForSubmission = link.IsRequiredForSubmission,
            DisplayOrder = link.DisplayOrder
        };
    }

    public async Task<bool> UnlinkActivity(Guid milestoneId, Guid activityId)
    {
        var milestoneEntity = await _unitOfWork.ResearchMilestones.GetByIdAsync(milestoneId);
        var milestone = ResearchMilestoneValidator.ValidateMilestoneExists(milestoneEntity, milestoneId);

        await ResearchMilestoneValidator.EnsureCanMutateActivityLinkAsync(
            _unitOfWork,
            _claimsService,
            milestone.ModuleId);

        var link = await _unitOfWork.ResearchMilestoneActivities.FirstOrDefaultAsync(
            l => l.ResearchMilestoneId == milestoneId
                 && l.ActivityId == activityId
                 && !l.IsDeleted);

        if (link == null)
        {
            return false;
        }

        await _unitOfWork.ResearchMilestoneActivities.SoftRemove(link);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<StudentMilestoneProgressDto> GetStudentMilestoneProgress(Guid moduleEnrollmentId)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            ResearchMilestoneValidator.ViewProgressForbiddenMessage);

        var enrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
        if (enrollmentEntity == null || enrollmentEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module enrollment with id '{moduleEnrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollmentEntity.StudentId,
            ResearchMilestoneValidator.ViewProgressForbiddenMessage);

        var module = await _unitOfWork.Modules.GetByIdAsync(enrollmentEntity.ModuleId);
        ResearchMilestoneValidator.ValidateResearchModule(module, enrollmentEntity.ModuleId);

        var milestones = await _unitOfWork.ResearchMilestones.GetAllAsync(
            rm => rm.ModuleId == enrollmentEntity.ModuleId && !rm.IsDeleted);

        milestones = milestones.OrderBy(rm => rm.MilestoneOrder).ToList();

        if (milestones.Count == 0)
        {
            return new StudentMilestoneProgressDto
            {
                ModuleEnrollmentId = moduleEnrollmentId,
                ModuleId = enrollmentEntity.ModuleId,
                Milestones = []
            };
        }

        var milestoneIds = milestones.Select(rm => rm.Id).ToList();
        var assignmentIds = milestones.Select(rm => rm.AssignmentId).ToList();

        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => assignmentIds.Contains(a.Id) && !a.IsDeleted);
        var assignmentsById = assignments.ToDictionary(a => a.Id);

        var activityLinks = await _unitOfWork.ResearchMilestoneActivities.GetAllAsync(
            link => milestoneIds.Contains(link.ResearchMilestoneId) && !link.IsDeleted,
            link => link.Activity);
        var linksByMilestoneId = activityLinks
            .GroupBy(link => link.ResearchMilestoneId)
            .ToDictionary(group => group.Key, group => group.OrderBy(l => l.DisplayOrder).ToList());

        var activityProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollmentId && !ap.IsDeleted);
        var completedActivityIds = activityProgresses
            .Where(ap => ap.IsCompleted)
            .Select(ap => ap.ActivityId)
            .ToHashSet();

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.ModuleEnrollmentId == moduleEnrollmentId
                 && s.ResearchMilestoneId.HasValue
                 && milestoneIds.Contains(s.ResearchMilestoneId.Value)
                 && !s.IsDeleted);

        var submissionsByMilestoneId = submissions
            .GroupBy(s => s.ResearchMilestoneId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(s => s.AttemptNumber).ThenByDescending(s => s.CreatedAt).First());

        var now = DateTime.UtcNow;
        var milestoneItems = new List<StudentMilestoneItemProgressDto>();
        ResearchMilestone? previousMilestone = null;

        foreach (var milestone in milestones)
        {
            if (!assignmentsById.TryGetValue(milestone.AssignmentId, out var assignment))
            {
                continue;
            }

            linksByMilestoneId.TryGetValue(milestone.Id, out var links);
            links ??= [];

            var isUnlocked = previousMilestone == null
                || ResearchMilestoneValidator.HasPassedSubmission(previousMilestone, submissionsByMilestoneId, assignmentsById);

            string? unlockReason = null;
            if (!isUnlocked && previousMilestone != null)
            {
                unlockReason =
                    $"Complete milestone '{previousMilestone.Title}' with a passing grade to unlock this milestone.";
            }

            submissionsByMilestoneId.TryGetValue(milestone.Id, out var latestSubmission);

            var requiredActivities = links
                .Where(link => link.IsRequiredForSubmission && link.Activity != null && !link.Activity.IsDeleted)
                .Select(link => new StudentMilestoneActivityProgressDto
                {
                    ActivityId = link.ActivityId,
                    Title = link.Activity!.Name,
                    ActivityType = link.Activity.ActivityType,
                    IsRequiredForSubmission = true,
                    IsSatisfied = completedActivityIds.Contains(link.ActivityId)
                })
                .ToList();

            var activityBlockReasons = requiredActivities
                .Where(a => !a.IsSatisfied)
                .Select(a => $"Required activity '{a.Title}' is not completed.")
                .ToList();

            var (canSubmit, submitBlockReasons) = ResearchSubmissionValidator.EvaluateStudentSubmitEligibility(
                isUnlocked,
                activityBlockReasons,
                assignment,
                latestSubmission,
                now);

            var passed = latestSubmission != null
                && latestSubmission.Status == SubmissionStatus.Graded
                && latestSubmission.AssignedGrade.HasValue
                && latestSubmission.AssignedGrade.Value >= assignment.PassScore;

            milestoneItems.Add(new StudentMilestoneItemProgressDto
            {
                MilestoneId = milestone.Id,
                Code = milestone.Code,
                Title = milestone.Title,
                MilestoneOrder = milestone.MilestoneOrder,
                IsCapstone = milestone.IsCapstone,
                IsUnlocked = isUnlocked,
                UnlockReason = unlockReason,
                CanSubmit = canSubmit,
                SubmitBlockReasons = submitBlockReasons,
                AssignmentId = milestone.AssignmentId,
                SubmissionId = latestSubmission?.Id,
                SubmissionStatus = latestSubmission?.Status,
                AssignedGrade = latestSubmission?.AssignedGrade,
                Passed = latestSubmission?.Status == SubmissionStatus.Graded ? passed : null,
                RequiredActivities = requiredActivities
            });

            previousMilestone = milestone;
        }

        return new StudentMilestoneProgressDto
        {
            ModuleEnrollmentId = moduleEnrollmentId,
            ModuleId = enrollmentEntity.ModuleId,
            Milestones = milestoneItems
        };
    }
}
