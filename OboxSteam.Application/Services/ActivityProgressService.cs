using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ActivityProgressService : IActivityProgressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClaimsService _claimsService;
        private readonly ICertificateService _certificateService;
        private readonly ILogger<ActivityProgressService> _logger;

        public ActivityProgressService(
            IUnitOfWork unitOfWork,
            IClaimsService claimsService,
            ICertificateService certificateService,
            ILogger<ActivityProgressService> logger)
        {
            _unitOfWork = unitOfWork;
            _claimsService = claimsService;
            _certificateService = certificateService;
            _logger = logger;
        }

    public async Task<ActivityProgressResponseDto> StartActivityProgressAsync(CreateActivityProgressRequestDto request)
    {
        ActivityProgressValidator.ValidateModuleEnrollmentIdRequired(request.ModuleEnrollmentId);
        ActivityProgressValidator.ValidateActivityIdRequired(request.ActivityId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ActivityProgressValidator.StartForbiddenMessage);

        var moduleEnrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId);
        var moduleEnrollment = ActivityProgressValidator.ValidateModuleEnrollmentExists(
            moduleEnrollmentEntity,
            request.ModuleEnrollmentId);
        ActivityProgressValidator.ValidateModuleEnrollmentBelongsToStudent(moduleEnrollment, student.Id);
        ActivityProgressValidator.ValidateModuleEnrollmentActive(moduleEnrollment);

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, request.ActivityId);

        var courseEntity = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (courseEntity == null || courseEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course for activity '{activity.Id}' not found.");
        }

        ActivityProgressValidator.ValidateActivityBelongsToModule(activity, courseEntity, moduleEnrollment.ModuleId);

        var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == request.ModuleEnrollmentId
                  && ap.ActivityId == request.ActivityId
                  && !ap.IsDeleted);
        ActivityProgressValidator.ValidateNoDuplicateProgress(existingProgress);

        var now = DateTime.UtcNow;

        if (!moduleEnrollment.StartedAt.HasValue)
        {
            moduleEnrollment.StartedAt = now;
            await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
        }

        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                moduleEnrollment.ProgramEnrollmentId.Value);
            if (programEnrollment != null
                && !programEnrollment.IsDeleted
                && !programEnrollment.StartedAt.HasValue)
            {
                programEnrollment.StartedAt = now;
                await _unitOfWork.ProgramEnrollments.Update(programEnrollment);
            }
        }

        var progress = new ActivityProgress
        {
            StudentId = student.Id,
            ActivityId = request.ActivityId,
            ModuleEnrollmentId = request.ModuleEnrollmentId,
            ActivityStatus = ActivityStatus.InProgress,
        };

        await _unitOfWork.ActivityProgresses.AddAsync(progress);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[StartActivityProgressAsync] Student {StudentId} started activity {ActivityId}, progress {ProgressId}.",
            student.Id,
            request.ActivityId,
            progress.Id);

        return new ActivityProgressResponseDto
        {
            Id = progress.Id,
            StudentId = progress.StudentId,
            ActivityId = progress.ActivityId,
            ModuleEnrollmentId = progress.ModuleEnrollmentId,
            ActivityStatus = progress.ActivityStatus,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            ActivityType = activity.ActivityType,
            ActivityOrder = activity.ActivityOrder,
        };
    }

    public async Task<ActivityProgressResponseDto> UpdateActivityProgressAsync(
        UpdateActivityProgressRequestDto request)
    {
        ActivityProgressValidator.ValidateModuleEnrollmentIdRequired(request.ModuleEnrollmentId);
        ActivityProgressValidator.ValidateActivityIdRequired(request.ActivityId);

        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ActivityProgressValidator.UpdateForbiddenMessage);

        var moduleEnrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(request.ModuleEnrollmentId);
        var moduleEnrollment = ActivityProgressValidator.ValidateModuleEnrollmentExists(
            moduleEnrollmentEntity,
            request.ModuleEnrollmentId);
        ActivityProgressValidator.ValidateModuleEnrollmentBelongsToStudent(moduleEnrollment, student.Id);
        ActivityProgressValidator.ValidateModuleEnrollmentActive(moduleEnrollment);

        var progressEntity = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == request.ModuleEnrollmentId
                  && ap.ActivityId == request.ActivityId
                  && !ap.IsDeleted);
        var progress = ActivityProgressValidator.ValidateActivityProgressForModuleEnrollment(
            progressEntity,
            request.ModuleEnrollmentId,
            request.ActivityId);

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, request.ActivityId);

        progress.ActivityStatus = ActivityStatus.Done;
        progress.IsCompleted = true;
        progress.CompletedAt = DateTime.UtcNow;

        await _unitOfWork.ActivityProgresses.Update(progress);
        await _unitOfWork.SaveChangesAsync();

        var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
            _unitOfWork,
            moduleEnrollment);

        decimal? programProgressPercent = null;
        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            programProgressPercent = await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
            await TryEnsureProgramCertificateAsync(moduleEnrollment.ProgramEnrollmentId.Value);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[UpdateActivityProgressAsync] Student {StudentId} marked activity {ActivityId} as Done, progress {ProgressId}.",
            student.Id,
            request.ActivityId,
            progress.Id);

        return new ActivityProgressResponseDto
        {
            Id = progress.Id,
            StudentId = progress.StudentId,
            ActivityId = progress.ActivityId,
            ModuleEnrollmentId = progress.ModuleEnrollmentId,
            ActivityStatus = progress.ActivityStatus,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            ActivityType = activity.ActivityType,
            ActivityOrder = activity.ActivityOrder,
            ModuleProgressPercent = moduleProgressPercent,
            ProgramProgressPercent = programProgressPercent,
        };
    }

    public async Task<ActivityProgressResponseDto> CompleteActivityForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId,
        CompletionSource? completionSource = null)
    {
        ActivityProgressValidator.ValidateModuleEnrollmentIdRequired(moduleEnrollmentId);
        ActivityProgressValidator.ValidateActivityIdRequired(activityId);

        var moduleEnrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
        var moduleEnrollment = ActivityProgressValidator.ValidateModuleEnrollmentExists(
            moduleEnrollmentEntity,
            moduleEnrollmentId);
        ActivityProgressValidator.ValidateModuleEnrollmentBelongsToStudent(moduleEnrollment, studentId);
        ActivityProgressValidator.ValidateModuleEnrollmentActive(moduleEnrollment);

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, activityId);

        var courseEntity = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (courseEntity == null || courseEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course for activity '{activity.Id}' not found.");
        }

        ActivityProgressValidator.ValidateActivityBelongsToModule(activity, courseEntity, moduleEnrollment.ModuleId);

        var progressEntity = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollmentId
                  && ap.ActivityId == activityId
                  && !ap.IsDeleted);

        var now = DateTime.UtcNow;
        ActivityProgress progress;

        if (progressEntity == null)
        {
            if (!moduleEnrollment.StartedAt.HasValue)
            {
                moduleEnrollment.StartedAt = now;
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            }

            if (moduleEnrollment.ProgramEnrollmentId.HasValue)
            {
                var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                    moduleEnrollment.ProgramEnrollmentId.Value);
                if (programEnrollment != null
                    && !programEnrollment.IsDeleted
                    && !programEnrollment.StartedAt.HasValue)
                {
                    programEnrollment.StartedAt = now;
                    await _unitOfWork.ProgramEnrollments.Update(programEnrollment);
                }
            }

            progress = new ActivityProgress
            {
                StudentId = studentId,
                ActivityId = activityId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = now,
                CompletionSource = completionSource,
                ResumeState = null,
                LastAccessedAt = now,
            };

            await _unitOfWork.ActivityProgresses.AddAsync(progress);
        }
        else
        {
            progress = progressEntity;
            progress.ActivityStatus = ActivityStatus.Done;
            progress.IsCompleted = true;
            progress.CompletedAt = now;
            progress.CompletionSource = completionSource;
            progress.ResumeState = null;
            progress.LastAccessedAt = now;
            await _unitOfWork.ActivityProgresses.Update(progress);
        }

        await _unitOfWork.SaveChangesAsync();

        var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
            _unitOfWork,
            moduleEnrollment);

        decimal? programProgressPercent = null;
        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            programProgressPercent = await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
            await TryEnsureProgramCertificateAsync(moduleEnrollment.ProgramEnrollmentId.Value);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[CompleteActivityForModuleEnrollmentAsync] Student {StudentId} completed activity {ActivityId}, progress {ProgressId}.",
            studentId,
            activityId,
            progress.Id);

        return new ActivityProgressResponseDto
        {
            Id = progress.Id,
            StudentId = progress.StudentId,
            ActivityId = progress.ActivityId,
            ModuleEnrollmentId = progress.ModuleEnrollmentId,
            ActivityStatus = progress.ActivityStatus,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            ActivityType = activity.ActivityType,
            ActivityOrder = activity.ActivityOrder,
            ModuleProgressPercent = moduleProgressPercent,
            ProgramProgressPercent = programProgressPercent,
        };
    }

    public async Task<ActivityProgressResponseDto> SaveCheckpointForModuleEnrollmentAsync(
        Guid moduleEnrollmentId,
        Guid activityId,
        Guid studentId,
        string resumeStateJson)
    {
        ActivityProgressValidator.ValidateModuleEnrollmentIdRequired(moduleEnrollmentId);
        ActivityProgressValidator.ValidateActivityIdRequired(activityId);

        var moduleEnrollmentEntity = await _unitOfWork.ModuleEnrollments.GetByIdAsync(moduleEnrollmentId);
        var moduleEnrollment = ActivityProgressValidator.ValidateModuleEnrollmentExists(
            moduleEnrollmentEntity,
            moduleEnrollmentId);
        ActivityProgressValidator.ValidateModuleEnrollmentBelongsToStudent(moduleEnrollment, studentId);
        ActivityProgressValidator.ValidateModuleEnrollmentActive(moduleEnrollment);

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, activityId);
        CurriculumAccessValidator.ValidateActivityTypeForManualComplete(activity);

        var courseEntity = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (courseEntity == null || courseEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course for activity '{activity.Id}' not found.");
        }

        ActivityProgressValidator.ValidateActivityBelongsToModule(activity, courseEntity, moduleEnrollment.ModuleId);

        var progressEntity = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollmentId
                  && ap.ActivityId == activityId
                  && !ap.IsDeleted);

        var now = DateTime.UtcNow;
        ActivityProgress progress;

        if (progressEntity == null)
        {
            if (!moduleEnrollment.StartedAt.HasValue)
            {
                moduleEnrollment.StartedAt = now;
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
            }

            if (moduleEnrollment.ProgramEnrollmentId.HasValue)
            {
                var programEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
                    moduleEnrollment.ProgramEnrollmentId.Value);
                if (programEnrollment != null
                    && !programEnrollment.IsDeleted
                    && !programEnrollment.StartedAt.HasValue)
                {
                    programEnrollment.StartedAt = now;
                    await _unitOfWork.ProgramEnrollments.Update(programEnrollment);
                }
            }

            progress = new ActivityProgress
            {
                StudentId = studentId,
                ActivityId = activityId,
                ModuleEnrollmentId = moduleEnrollmentId,
                ActivityStatus = ActivityStatus.InProgress,
                ResumeState = resumeStateJson,
                LastAccessedAt = now,
            };

            await _unitOfWork.ActivityProgresses.AddAsync(progress);
        }
        else
        {
            if (progressEntity.ActivityStatus == ActivityStatus.Done || progressEntity.IsCompleted)
            {
                throw ErrorHelper.BadRequest("Activity is already completed.");
            }

            progress = progressEntity;
            progress.ActivityStatus = ActivityStatus.InProgress;
            progress.ResumeState = resumeStateJson;
            progress.LastAccessedAt = now;
            await _unitOfWork.ActivityProgresses.Update(progress);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "[SaveCheckpointForModuleEnrollmentAsync] Student {StudentId} saved checkpoint for activity {ActivityId}.",
            studentId,
            activityId);

        return new ActivityProgressResponseDto
        {
            Id = progress.Id,
            StudentId = progress.StudentId,
            ActivityId = progress.ActivityId,
            ModuleEnrollmentId = progress.ModuleEnrollmentId,
            ActivityStatus = progress.ActivityStatus,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            ActivityType = activity.ActivityType,
            ActivityOrder = activity.ActivityOrder,
            ResumeState = ActivityResumeStateHelper.Deserialize(progress.ResumeState),
            LastAccessedAt = progress.LastAccessedAt,
        };
    }

    public async Task<ActivityProgressResponseDto> ForceCompleteActivityAsync(Guid studentId, Guid activityId)
    {
        ActivityProgressValidator.ValidateActivityIdRequired(activityId);

        if (studentId == Guid.Empty)
        {
            throw ErrorHelper.BadRequest("StudentId is required.");
        }

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, activityId);

        var courseEntity = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (courseEntity == null || courseEntity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course for activity '{activity.Id}' not found.");
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == courseEntity.ModuleId
                  && !me.IsDeleted);

        var moduleEnrollment = moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();

        if (moduleEnrollment == null)
        {
            throw ErrorHelper.NotFound(
                $"No module enrollment found for student '{studentId}' on module '{courseEntity.ModuleId}'.");
        }

        var now = DateTime.UtcNow;

        var progressEntity = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                  && ap.ActivityId == activityId
                  && !ap.IsDeleted);

        ActivityProgress progress;

        if (progressEntity == null)
        {
            progress = new ActivityProgress
            {
                StudentId = studentId,
                ActivityId = activityId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = now,
                CompletionSource = CompletionSource.Manual,
                ResumeState = null,
                LastAccessedAt = now,
            };

            await _unitOfWork.ActivityProgresses.AddAsync(progress);
        }
        else
        {
            progress = progressEntity;
            progress.ActivityStatus = ActivityStatus.Done;
            progress.IsCompleted = true;
            progress.CompletedAt = now;
            progress.CompletionSource = CompletionSource.Manual;
            progress.ResumeState = null;
            progress.LastAccessedAt = now;
            await _unitOfWork.ActivityProgresses.Update(progress);
        }

        await _unitOfWork.SaveChangesAsync();

        var moduleProgressPercent = await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(
            _unitOfWork,
            moduleEnrollment);

        decimal? programProgressPercent = null;
        if (moduleEnrollment.ProgramEnrollmentId.HasValue)
        {
            programProgressPercent = await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(
                _unitOfWork,
                moduleEnrollment.ProgramEnrollmentId.Value,
                moduleEnrollment);
            await TryEnsureProgramCertificateAsync(moduleEnrollment.ProgramEnrollmentId.Value);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogWarning(
            "[ForceCompleteActivityAsync] TEST bypass — activity {ActivityId} forced to Done for student {StudentId}, progress {ProgressId}.",
            activityId,
            studentId,
            progress.Id);

        return new ActivityProgressResponseDto
        {
            Id = progress.Id,
            StudentId = progress.StudentId,
            ActivityId = progress.ActivityId,
            ModuleEnrollmentId = progress.ModuleEnrollmentId,
            ActivityStatus = progress.ActivityStatus,
            IsCompleted = progress.IsCompleted,
            CompletedAt = progress.CompletedAt,
            CreatedAt = progress.CreatedAt,
            UpdatedAt = progress.UpdatedAt,
            ActivityCode = activity.Code,
            ActivityName = activity.Name,
            ActivityType = activity.ActivityType,
            ActivityOrder = activity.ActivityOrder,
            ModuleProgressPercent = moduleProgressPercent,
            ProgramProgressPercent = programProgressPercent,
        };
    }

    private async Task TryEnsureProgramCertificateAsync(Guid programEnrollmentId)
    {
        try
        {
            await _certificateService.EnsureProgramCertificateAsync(programEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureProgramCertificateAsync] Failed for enrollment {EnrollmentId}. Learning progress was not rolled back.",
                programEnrollmentId);
        }
    }
}
