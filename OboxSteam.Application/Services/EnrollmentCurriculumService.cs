using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.EnrollmentDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class EnrollmentCurriculumService : IEnrollmentCurriculumService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly IActivityProgressService _activityProgressService;
    private readonly ILogger<EnrollmentCurriculumService> _logger;

    public EnrollmentCurriculumService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        IActivityProgressService activityProgressService,
        ILogger<EnrollmentCurriculumService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _activityProgressService = activityProgressService;
        _logger = logger;
    }

    public async Task<EnrollmentCurriculumDto> GetEnrollmentCurriculumAsync(Guid programEnrollmentId)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            CurriculumAccessValidator.CurriculumForbiddenMessage);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId, pe => pe.Program);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            CurriculumAccessValidator.CurriculumForbiddenMessage);

        CurriculumAccessValidator.ValidateProgramEnrollmentForCurriculum(enrollment);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
        var context = await BuildCurriculumContextAsync(enrollment, snapshot, provisionModuleEnrollments: true);

        var currentActivityId = CurriculumStatusHelper.FindCurrentActivityId(
            snapshot,
            activityId => IsActivityAccessible(activityId, snapshot, context),
            activityId => IsActivityCompleted(activityId, snapshot, context));

        var modules = snapshot.Modules.Select(module =>
            MapEnrollmentModule(module, snapshot, context, currentActivityId)).ToList();

        return new EnrollmentCurriculumDto
        {
            EnrollmentId = enrollment.Id,
            ProgramId = enrollment.ProgramId,
            ProgramName = snapshot.Program.Name,
            ProgressPercent = enrollment.ProgressPercent,
            CurrentActivityId = currentActivityId,
            Modules = modules,
        };
    }

    public async Task<CompleteActivityResponseDto> CompleteActivityAsync(
        Guid programEnrollmentId,
        Guid activityId,
        CompleteActivityRequestDto? request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ActivityProgressValidator.UpdateForbiddenMessage);

        var enrollment = await CurriculumAccessValidator.GetProgramEnrollmentForStudentActionAsync(
            _unitOfWork,
            programEnrollmentId,
            student.Id);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);

        if (!snapshot.ActivitiesById.TryGetValue(activityId, out var activity))
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId))
        {
            throw ErrorHelper.BadRequest("Activity does not belong to this program.");
        }

        CurriculumAccessValidator.ValidateActivityTypeForManualComplete(activity);

        var context = await BuildCurriculumContextAsync(enrollment, snapshot, provisionModuleEnrollments: true);

        if (!IsActivityAccessible(activityId, snapshot, context))
        {
            throw ErrorHelper.Forbidden(CurriculumAccessValidator.ActivityLockedMessage);
        }

        var moduleEnrollment = await CurriculumAccessValidator.ResolveModuleEnrollmentAsync(
            _unitOfWork,
            programEnrollmentId,
            student.Id,
            moduleId);

        var previouslyUnlockedModuleIds = snapshot.Modules
            .Where(m => CurriculumStatusHelper.IsModuleUnlocked(m, context.LatestEnrollmentByModuleId, context.ModulesById))
            .Select(m => m.Id)
            .ToHashSet();

        await _activityProgressService.CompleteActivityForModuleEnrollmentAsync(
            moduleEnrollment.Id,
            activityId,
            student.Id);

        var refreshedEnrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        var refreshedContext = await BuildCurriculumContextAsync(
            refreshedEnrollment!,
            snapshot,
            provisionModuleEnrollments: false);

        var nextActivityId = CurriculumStatusHelper.FindNextActivityId(
            snapshot,
            activityId,
            id => IsActivityAccessible(id, snapshot, refreshedContext),
            id => IsActivityCompleted(id, snapshot, refreshedContext));

        var unlockedModuleIds = snapshot.Modules
            .Where(m => CurriculumStatusHelper.IsModuleUnlocked(m, refreshedContext.LatestEnrollmentByModuleId, context.ModulesById))
            .Select(m => m.Id)
            .Where(id => !previouslyUnlockedModuleIds.Contains(id))
            .ToList();

        if (refreshedContext.LatestEnrollmentByModuleId.TryGetValue(moduleId, out var updatedModuleEnrollment)
            && updatedModuleEnrollment.ProgressPercent >= 100m)
        {
            unlockedModuleIds = unlockedModuleIds
                .Concat(CurriculumStatusHelper.FindNewlyUnlockedModuleIds(
                    snapshot,
                    moduleId,
                    refreshedContext.LatestEnrollmentByModuleId,
                    context.ModulesById))
                .Distinct()
                .ToList();
        }

        _logger.LogInformation(
            "[CompleteActivityAsync] Student {StudentId} completed activity {ActivityId} on enrollment {EnrollmentId}.",
            student.Id,
            activityId,
            programEnrollmentId);

        return new CompleteActivityResponseDto
        {
            ProgressPercent = refreshedEnrollment?.ProgressPercent ?? enrollment.ProgressPercent,
            NextActivityId = nextActivityId,
            UnlockedModuleIds = unlockedModuleIds,
            ActivityStatus = CurriculumStatusHelper.StatusCompleted,
        };
    }

    public async Task EnsureActivityAccessibleAsync(Guid programEnrollmentId, Guid activityId)
    {
        await EnrollmentAccessValidator.GetCurrentUserForGetAsync(
            _unitOfWork,
            _claimsService,
            CurriculumAccessValidator.CurriculumForbiddenMessage);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Program enrollment with id '{programEnrollmentId}' not found.");
        }

        await EnrollmentAccessValidator.EnsureCanViewEnrollmentAsync(
            _unitOfWork,
            _claimsService,
            enrollment.StudentId,
            CurriculumAccessValidator.CurriculumForbiddenMessage);

        CurriculumAccessValidator.ValidateProgramEnrollmentForCurriculum(enrollment);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
        if (!snapshot.ActivitiesById.ContainsKey(activityId))
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        var context = await BuildCurriculumContextAsync(enrollment, snapshot, provisionModuleEnrollments: false);
        if (!IsActivityAccessible(activityId, snapshot, context))
        {
            throw ErrorHelper.Forbidden(CurriculumAccessValidator.ActivityLockedMessage);
        }
    }

    public async Task EnsureStudentEnrolledInProgramAsync(Guid programId)
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            return;
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted || user.Role != RoleType.Student)
        {
            return;
        }

        var enrollment = await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
            pe => pe.StudentId == userId
                  && pe.ProgramId == programId
                  && !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Active);

        if (enrollment == null)
        {
            throw ErrorHelper.Forbidden(CurriculumAccessValidator.CurriculumForbiddenMessage);
        }
    }

    private async Task<EnrollmentCurriculumContext> BuildCurriculumContextAsync(
        ProgramEnrollment enrollment,
        ProgramCurriculumTreeSnapshot snapshot,
        bool provisionModuleEnrollments)
    {
        var modulesById = snapshot.Modules.ToDictionary(m => m.Id);
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.StudentId == enrollment.StudentId
                  && !me.IsDeleted);

        var latestEnrollmentByModuleId = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(me => me.AttemptNumber).First());

        if (provisionModuleEnrollments && enrollment.Status == EnrollmentStatus.Active)
        {
            var now = DateTime.UtcNow;
            var created = false;

            foreach (var module in snapshot.Modules)
            {
                if (latestEnrollmentByModuleId.ContainsKey(module.Id))
                {
                    continue;
                }

                if (!CurriculumStatusHelper.IsModuleUnlocked(module, latestEnrollmentByModuleId, modulesById))
                {
                    continue;
                }

                var moduleEnrollment = new ModuleEnrollment
                {
                    StudentId = enrollment.StudentId,
                    ModuleId = module.Id,
                    ProgramEnrollmentId = enrollment.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = now,
                };

                await _unitOfWork.ModuleEnrollments.AddAsync(moduleEnrollment);
                latestEnrollmentByModuleId[module.Id] = moduleEnrollment;
                created = true;
            }

            if (created)
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        var moduleEnrollmentIds = latestEnrollmentByModuleId.Values.Select(me => me.Id).ToList();

        var activityProgresses = moduleEnrollmentIds.Count > 0
            ? await _unitOfWork.ActivityProgresses.GetAllAsync(
                ap => moduleEnrollmentIds.Contains(ap.ModuleEnrollmentId) && !ap.IsDeleted)
            : [];

        var progressByActivityId = activityProgresses
            .GroupBy(ap => ap.ActivityId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(ap => ap.UpdatedAt ?? ap.CreatedAt).First());

        var activityIds = snapshot.GlobalActivityOrder;
        var bookings = activityIds.Count > 0
            ? await _unitOfWork.ActivityBookings.GetAllAsync(
                ab => ab.StudentId == enrollment.StudentId
                      && activityIds.Contains(ab.ActivityId)
                      && !ab.IsDeleted)
            : [];

        var checkedInActivityIds = bookings
            .Where(ab => ab.Status == BookingStatus.CheckedIn)
            .Select(ab => ab.ActivityId)
            .ToHashSet();

        return new EnrollmentCurriculumContext
        {
            LatestEnrollmentByModuleId = latestEnrollmentByModuleId,
            ModulesById = modulesById,
            ProgressByActivityId = progressByActivityId,
            CheckedInActivityIds = checkedInActivityIds,
        };
    }

    private static EnrollmentCurriculumModuleDto MapEnrollmentModule(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid? currentActivityId)
    {
        var isLocked = !CurriculumStatusHelper.IsModuleUnlocked(
            module,
            context.LatestEnrollmentByModuleId,
            context.ModulesById);

        context.LatestEnrollmentByModuleId.TryGetValue(module.Id, out var moduleEnrollment);

        var moduleDto = new EnrollmentCurriculumModuleDto
        {
            ModuleId = module.Id,
            ModuleName = module.Name,
            ModuleOrder = module.ModuleOrder,
            ModuleType = module.ModuleType,
            PrerequisiteModuleId = module.PrerequisiteModuleId,
            IsLocked = isLocked,
            LockReason = isLocked
                ? CurriculumStatusHelper.GetModuleLockReason(module, context.LatestEnrollmentByModuleId, context.ModulesById)
                : null,
            ModuleEnrollmentId = moduleEnrollment?.Id,
        };

        if (module.ModuleType == ModuleType.Research)
        {
            moduleDto.Milestones = snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var moduleMilestones)
                ? moduleMilestones.Select(milestone => new EnrollmentCurriculumMilestoneDto
                {
                    MilestoneId = milestone.Id,
                    MilestoneName = milestone.Title,
                    MilestoneOrder = milestone.MilestoneOrder,
                    Activities = snapshot.LinksByMilestoneId.TryGetValue(milestone.Id, out var links)
                        ? links
                            .Select(link => snapshot.ActivitiesById.GetValueOrDefault(link.ActivityId))
                            .Where(activity => activity != null)
                            .Select(activity => MapEnrollmentActivity(
                                activity!,
                                snapshot,
                                context,
                                currentActivityId,
                                isLocked))
                            .ToList()
                        : [],
                }).ToList()
                : [];
        }
        else if (snapshot.CoursesByModuleId.TryGetValue(module.Id, out var moduleCourses))
        {
            var courseOrder = 1;
            moduleDto.Courses = moduleCourses.Select(course => new EnrollmentCurriculumCourseDto
            {
                CourseId = course.Id,
                CourseName = course.Name,
                CourseOrder = courseOrder++,
                Activities = snapshot.ActivitiesByCourseId.TryGetValue(course.Id, out var moduleActivities)
                    ? moduleActivities
                        .Select(activity => MapEnrollmentActivity(
                            activity,
                            snapshot,
                            context,
                            currentActivityId,
                            isLocked))
                        .ToList()
                    : [],
            }).ToList();
        }

        return moduleDto;
    }

    private static EnrollmentCurriculumActivityDto MapEnrollmentActivity(
        Activity activity,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid? currentActivityId,
        bool moduleLocked)
    {
        snapshot.MaterialsByActivityId.TryGetValue(activity.Id, out var material);

        var status = ResolveActivityStatus(activity.Id, snapshot, context, currentActivityId, moduleLocked);

        return new EnrollmentCurriculumActivityDto
        {
            ActivityId = activity.Id,
            ActivityName = activity.Name,
            ActivityOrder = activity.ActivityOrder,
            ActivityType = activity.ActivityType,
            Status = status,
            Material = material == null
                ? null
                : new EnrollmentCurriculumMaterialDto
                {
                    MaterialId = material.Id,
                    MaterialName = material.Title,
                    MaterialType = material.MaterialType,
                },
        };
    }

    private static string ResolveActivityStatus(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid? currentActivityId,
        bool moduleLocked)
    {
        if (moduleLocked)
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        if (IsActivityCompleted(activityId, snapshot, context))
        {
            return CurriculumStatusHelper.StatusCompleted;
        }

        if (!IsActivitySequentiallyAccessible(activityId, snapshot, context))
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        if (currentActivityId.HasValue && currentActivityId.Value == activityId)
        {
            return CurriculumStatusHelper.StatusCurrent;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static bool IsActivityCompleted(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context)
    {
        if (!snapshot.ActivitiesById.TryGetValue(activityId, out var activity))
        {
            return false;
        }

        return CurriculumStatusHelper.IsActivityCompleted(
            activityId,
            activity,
            context.ProgressByActivityId,
            context.CheckedInActivityIds);
    }

    private static bool IsActivitySequentiallyAccessible(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context)
    {
        return CurriculumStatusHelper.IsActivitySequentiallyAccessible(
            activityId,
            snapshot,
            id => IsActivityCompleted(id, snapshot, context));
    }

    private static bool IsActivityAccessible(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context)
    {
        if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId)
            || !context.ModulesById.TryGetValue(moduleId, out var module))
        {
            return false;
        }

        if (!CurriculumStatusHelper.IsModuleUnlocked(module, context.LatestEnrollmentByModuleId, context.ModulesById))
        {
            return false;
        }

        return IsActivitySequentiallyAccessible(activityId, snapshot, context);
    }

    private sealed class EnrollmentCurriculumContext
    {
        public Dictionary<Guid, ModuleEnrollment> LatestEnrollmentByModuleId { get; init; } = new();

        public Dictionary<Guid, Module> ModulesById { get; init; } = new();

        public Dictionary<Guid, ActivityProgress> ProgressByActivityId { get; init; } = new();

        public HashSet<Guid> CheckedInActivityIds { get; init; } = [];
    }
}
