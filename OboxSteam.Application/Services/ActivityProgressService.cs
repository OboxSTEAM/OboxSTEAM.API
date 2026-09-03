using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ActivityProgressDTO;
using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Notifications;
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
        private readonly INotificationPublisher _notificationPublisher;
        private readonly ILogger<ActivityProgressService> _logger;

        public ActivityProgressService(
            IUnitOfWork unitOfWork,
            IClaimsService claimsService,
            ICertificateService certificateService,
            INotificationPublisher notificationPublisher,
            ILogger<ActivityProgressService> logger)
        {
            _unitOfWork = unitOfWork;
            _claimsService = claimsService;
            _certificateService = certificateService;
            _notificationPublisher = notificationPublisher;
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

        var previousModuleStatus = moduleEnrollment.Status;

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

        await PublishActivityCompletionNotificationsAsync(
            moduleEnrollment,
            previousModuleStatus,
            activity,
            student.Id);

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

        var previousModuleStatus = moduleEnrollment.Status;

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

        await PublishActivityCompletionNotificationsAsync(
            moduleEnrollment,
            previousModuleStatus,
            activity,
            studentId);

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

        var previousModuleStatus = moduleEnrollment.Status;

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

        await PublishActivityCompletionNotificationsAsync(
            moduleEnrollment,
            previousModuleStatus,
            activity,
            studentId);

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

    public async Task<MentorCompleteBulkResponseDto> MentorCompleteClassSessionAsync(
        MentorCompleteBulkRequestDto request)
    {
        MentorCompleteValidator.ValidateRequest(request);

        var classSession = await _unitOfWork.ClassSessions.GetByIdAsync(request.ClassSessionId);
        ClassSessionValidator.ValidateClassSessionExists(classSession, request.ClassSessionId);

        await SessionAttendanceValidator.EnsureCanUpdateSessionAttendanceAsync(
            _unitOfWork,
            _claimsService,
            classSession!);

        MentorCompleteValidator.ValidateSessionLinkedToActivity(classSession!, request.ActivityId);

        var activityEntity = await _unitOfWork.Activities.GetByIdAsync(request.ActivityId);
        var activity = ActivityProgressValidator.ValidateActivityExists(activityEntity, request.ActivityId);
        CurriculumAccessValidator.ValidateActivityTypeForMentorComplete(activity);

        if (activity.RequireMediaEvidence)
        {
            var sessionMedia = await _unitOfWork.MediaAssets.GetAllAsync(
                m => m.ClassSessionId == request.ClassSessionId
                     && !m.IsDeleted
                     && m.FileType == "image");
            MentorCompleteValidator.EnsureMediaEvidencePresent(
                activity,
                SessionEvidenceService.HasSessionImageEvidence(sessionMedia, request.ClassSessionId));
        }

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classSession!.ClassId);
        ClassValidator.ValidateClassExists(classEntity, classSession.ClassId);

        var module = await _unitOfWork.Modules.GetByIdAsync(classSession.ModuleId);
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{classSession.ModuleId}' not found.");
        }

        var courseActivities = await _unitOfWork.Activities.GetAllAsync(
            a => a.CourseId == activity.CourseId && !a.IsDeleted);
        var orderedCourseActivities = courseActivities
            .OrderBy(a => a.ActivityOrder)
            .ToList();

        var roster = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classSession.ClassId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var attendances = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ClassSessionId == request.ClassSessionId && !sa.IsDeleted);
        var attendanceByStudentId = attendances
            .GroupBy(sa => sa.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(sa => sa.UpdatedAt ?? sa.CreatedAt).First());

        var results = new List<MentorCompleteStudentResultDto>();

        foreach (var classEnrollment in roster)
        {
            results.Add(await TryMentorCompleteStudentAsync(
                classEnrollment,
                classSession,
                activity,
                module,
                orderedCourseActivities,
                attendanceByStudentId));
        }

        _logger.LogInformation(
            "[MentorCompleteClassSessionAsync] Session {SessionId} activity {ActivityId}: {Completed} completed, {AlreadyDone} already done, {Skipped} skipped of {Total} roster.",
            request.ClassSessionId,
            request.ActivityId,
            results.Count(r => r.Outcome == MentorCompleteOutcome.Completed),
            results.Count(r => r.Outcome == MentorCompleteOutcome.AlreadyDone),
            results.Count(r => r.Outcome == MentorCompleteOutcome.Skipped),
            results.Count);

        return new MentorCompleteBulkResponseDto
        {
            ClassSessionId = request.ClassSessionId,
            ActivityId = request.ActivityId,
            Results = results,
        };
    }

    private async Task<MentorCompleteStudentResultDto> TryMentorCompleteStudentAsync(
        ClassEnrollment classEnrollment,
        ClassSession classSession,
        Activity activity,
        Module module,
        IReadOnlyList<Activity> orderedCourseActivities,
        IReadOnlyDictionary<Guid, SessionAttendance> attendanceByStudentId)
    {
        var studentId = classEnrollment.StudentId;

        try
        {
            attendanceByStudentId.TryGetValue(studentId, out var attendance);
            var attendanceSkipReason = MentorCompleteValidator.GetAttendanceSkipReason(attendance);
            if (attendanceSkipReason != null)
            {
                return Skipped(studentId, attendanceSkipReason);
            }

            var moduleEnrollment = await ResolveActiveModuleEnrollmentForSessionAsync(
                studentId,
                classSession.ModuleId,
                classEnrollment.ProgramEnrollmentId);

            if (moduleEnrollment == null)
            {
                return Skipped(
                    studentId,
                    "Student does not have an active module enrollment for this session.");
            }

            if (!await IsModulePrerequisiteMetAsync(module, studentId, classEnrollment.ProgramEnrollmentId))
            {
                return Skipped(studentId, CurriculumAccessValidator.ActivityLockedMessage);
            }

            if (!await IsCourseSequenceUnlockedAsync(
                    activity.Id,
                    moduleEnrollment.Id,
                    orderedCourseActivities))
            {
                return Skipped(studentId, CurriculumAccessValidator.ActivityLockedMessage);
            }

            var existingProgress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
                ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                      && ap.ActivityId == activity.Id
                      && !ap.IsDeleted);

            if (existingProgress != null
                && (existingProgress.ActivityStatus == ActivityStatus.Done || existingProgress.IsCompleted))
            {
                return new MentorCompleteStudentResultDto
                {
                    StudentId = studentId,
                    Outcome = MentorCompleteOutcome.AlreadyDone,
                };
            }

            var progress = await CompleteActivityForModuleEnrollmentAsync(
                moduleEnrollment.Id,
                activity.Id,
                studentId,
                CompletionSource.Mentor);

            return new MentorCompleteStudentResultDto
            {
                StudentId = studentId,
                Outcome = MentorCompleteOutcome.Completed,
                Progress = progress,
            };
        }
        catch (AppException ex)
        {
            return Skipped(studentId, ex.Message);
        }
    }

    private static MentorCompleteStudentResultDto Skipped(Guid studentId, string reason)
        => new()
        {
            StudentId = studentId,
            Outcome = MentorCompleteOutcome.Skipped,
            Reason = reason,
        };

    private async Task<ModuleEnrollment?> ResolveActiveModuleEnrollmentForSessionAsync(
        Guid studentId,
        Guid moduleId,
        Guid programEnrollmentId)
    {
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == moduleId
                  && me.ProgramEnrollmentId == programEnrollmentId
                  && me.Status == EnrollmentStatus.Active
                  && !me.IsDeleted);

        return moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();
    }

    private async Task<bool> IsModulePrerequisiteMetAsync(
        Module module,
        Guid studentId,
        Guid programEnrollmentId)
    {
        if (!module.PrerequisiteModuleId.HasValue)
        {
            return true;
        }

        var prerequisiteEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.StudentId == studentId
                  && me.ModuleId == module.PrerequisiteModuleId.Value
                  && me.ProgramEnrollmentId == programEnrollmentId
                  && !me.IsDeleted);

        var latest = prerequisiteEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();

        return latest != null && latest.ProgressPercent >= 100m;
    }

    private async Task<bool> IsCourseSequenceUnlockedAsync(
        Guid activityId,
        Guid moduleEnrollmentId,
        IReadOnlyList<Activity> orderedCourseActivities)
    {
        var index = -1;
        for (var i = 0; i < orderedCourseActivities.Count; i++)
        {
            if (orderedCourseActivities[i].Id == activityId)
            {
                index = i;
                break;
            }
        }

        if (index <= 0)
        {
            return true;
        }

        var priorGateIds = orderedCourseActivities
            .Take(index)
            .Where(CurriculumStatusHelper.CompletionGatesUnlock)
            .Select(a => a.Id)
            .ToList();

        if (priorGateIds.Count == 0)
        {
            return true;
        }

        var doneProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollmentId
                  && priorGateIds.Contains(ap.ActivityId)
                  && ap.ActivityStatus == ActivityStatus.Done
                  && !ap.IsDeleted);

        var doneIds = doneProgresses.Select(ap => ap.ActivityId).ToHashSet();
        return priorGateIds.All(id => doneIds.Contains(id));
    }

    private async Task TryEnsureProgramCertificateAsync(Guid programEnrollmentId)
    {
        try
        {
            // Internal: mentor session-complete / force-complete must still be able to issue.
            await _certificateService.EnsureProgramCertificateInternalAsync(programEnrollmentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TryEnsureProgramCertificateAsync] Failed for enrollment {EnrollmentId}. Learning progress was not rolled back.",
                programEnrollmentId);
        }
    }

    /// <summary>
    /// Publishes <see cref="NotificationCatalog.ActivityCompleted"/> for the just-completed
    /// activity, plus <see cref="NotificationCatalog.ModuleCompleted"/> and any
    /// <see cref="NotificationCatalog.ModuleUnlocked"/> notifications when the recalculated
    /// module progress (already persisted by the caller) transitioned the enrollment to Completed.
    /// </summary>
    private async Task PublishActivityCompletionNotificationsAsync(
        ModuleEnrollment moduleEnrollment,
        EnrollmentStatus previousModuleStatus,
        Activity activity,
        Guid studentId)
    {
        var module = await _unitOfWork.Modules.GetByIdAsync(moduleEnrollment.ModuleId);
        var programId = module?.ProgramId;
        var programEnrollmentId = moduleEnrollment.ProgramEnrollmentId;

        Guid? nextActivityId = null;
        if (programId.HasValue && programEnrollmentId.HasValue)
        {
            nextActivityId = await NotificationDeeplinkResolver.ResolveNextActivityIdAsync(
                _unitOfWork,
                programId.Value,
                programEnrollmentId.Value,
                activity.Id);
        }

        await _notificationPublisher.PublishAsync(NotificationCatalog.ActivityCompleted(
            studentId,
            activity.Id,
            moduleEnrollment.ModuleId,
            programId,
            activity.Name,
            programEnrollmentId,
            nextActivityId,
            activity.CourseId));

        var justCompletedModule = moduleEnrollment.Status == EnrollmentStatus.Completed
            && previousModuleStatus != EnrollmentStatus.Completed;

        if (!justCompletedModule)
        {
            return;
        }

        await _notificationPublisher.PublishAsync(NotificationCatalog.ModuleCompleted(
            studentId,
            moduleEnrollment.ModuleId,
            moduleEnrollment.Id,
            programId,
            module?.Name,
            programEnrollmentId,
            nextActivityId));

        if (module == null || !programId.HasValue)
        {
            return;
        }

        var unlockedModules = await _unitOfWork.Modules.GetAllAsync(
            m => m.PrerequisiteModuleId == module.Id && m.ProgramId == module.ProgramId && !m.IsDeleted);

        foreach (var unlockedModule in unlockedModules)
        {
            var firstActivityId = await NotificationDeeplinkResolver.ResolveFirstActivityInModuleAsync(
                _unitOfWork,
                unlockedModule.ProgramId,
                unlockedModule.Id);

            await _notificationPublisher.PublishAsync(NotificationCatalog.ModuleUnlocked(
                studentId,
                unlockedModule.Id,
                unlockedModule.ProgramId,
                unlockedModule.Name,
                programEnrollmentId,
                firstActivityId ?? nextActivityId));
        }
    }
}