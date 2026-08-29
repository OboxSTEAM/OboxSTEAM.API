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
    private const string StatusInProgress = "in_progress";

    private const string NodeTypeProgram = "program";
    private const string NodeTypeModule = "module";
    private const string NodeTypeCourse = "course";
    private const string NodeTypeMilestone = "milestone";
    private const string NodeTypeActivity = "activity";
    private const string NodeTypeAssignment = "assignment";

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

    public async Task<EnrollmentCurriculumMindMapDto> GetEnrollmentCurriculumMindMapAsync(
        Guid programEnrollmentId)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            CurriculumAccessValidator.CurriculumForbiddenMessage);

        var enrollment = await CurriculumAccessValidator.GetProgramEnrollmentForStudentActionAsync(
            _unitOfWork,
            programEnrollmentId,
            student.Id);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
        var context = await BuildCurriculumContextAsync(
            enrollment,
            snapshot,
            provisionModuleEnrollments: false);

        var currentActivityIdSet = CollectCurrentActivityIds(snapshot, context);
        var modules = snapshot.Modules
            .Select(module => MapMindMapModule(module, snapshot, context, currentActivityIdSet))
            .ToList();

        var completedModuleCount = modules.Count(m =>
            m.Learning.Status == CurriculumStatusHelper.StatusCompleted);
        var hubStatus = ResolveHubStatus(enrollment.ProgressPercent, completedModuleCount, modules.Count);

        return new EnrollmentCurriculumMindMapDto
        {
            EnrollmentId = enrollment.Id,
            Hub = new EnrollmentCurriculumMindMapHubDto
            {
                ProgramId = enrollment.ProgramId,
                ProgramName = snapshot.Program.Name,
                ProgressPercent = enrollment.ProgressPercent,
                Status = hubStatus,
                CompletedModuleCount = completedModuleCount,
                TotalModuleCount = modules.Count,
                Navigation = BuildMindMapNavigation(NodeTypeProgram, enrollment.ProgramId, null),
            },
            CurrentPaths = BuildMindMapCurrentPaths(modules, currentActivityIdSet, enrollment.ProgramId),
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

        var completionSource = ActivityResumeStateHelper.ParseCompletionSource(request?.Source);

        await _activityProgressService.CompleteActivityForModuleEnrollmentAsync(
            moduleEnrollment.Id,
            activityId,
            student.Id,
            completionSource);

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

    public async Task<SaveActivityCheckpointResponseDto> SaveActivityCheckpointAsync(
        Guid programEnrollmentId,
        Guid activityId,
        SaveActivityCheckpointRequestDto request)
    {
        var student = await EnrollmentAccessValidator.GetCurrentStudentForEnrollAsync(
            _unitOfWork,
            _claimsService,
            ActivityProgressValidator.UpdateForbiddenMessage);

        var enrollment = await CurriculumAccessValidator.GetProgramEnrollmentForStudentActionAsync(
            _unitOfWork,
            programEnrollmentId,
            student.Id);

        ActivityResumeStateHelper.ValidateResumeState(request.ResumeState);

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);

        if (!snapshot.ActivitiesById.ContainsKey(activityId))
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId))
        {
            throw ErrorHelper.BadRequest("Activity does not belong to this program.");
        }

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

        var resumeStateJson = ActivityResumeStateHelper.Serialize(request.ResumeState);

        var progress = await _activityProgressService.SaveCheckpointForModuleEnrollmentAsync(
            moduleEnrollment.Id,
            activityId,
            student.Id,
            resumeStateJson);

        return new SaveActivityCheckpointResponseDto
        {
            ActivityId = activityId,
            ActivityStatus = progress.ActivityStatus.ToString(),
            ResumeState = progress.ResumeState,
            LastAccessedAt = progress.LastAccessedAt,
        };
    }

    public async Task<ActivityLearningProgressDto?> GetActivityLearningProgressAsync(
        Guid programEnrollmentId,
        Guid activityId)
    {
        await EnsureActivityAccessibleAsync(programEnrollmentId, activityId);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(programEnrollmentId);
        if (enrollment == null || enrollment.IsDeleted)
        {
            return null;
        }

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
        if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId))
        {
            return null;
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId
                  && me.StudentId == enrollment.StudentId
                  && me.ModuleId == moduleId
                  && !me.IsDeleted);

        var moduleEnrollment = moduleEnrollments
            .OrderByDescending(me => me.AttemptNumber)
            .FirstOrDefault();

        if (moduleEnrollment == null)
        {
            return null;
        }

        var progress = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                  && ap.ActivityId == activityId
                  && !ap.IsDeleted);

        return progress == null ? null : MapLearningProgress(progress);
    }

    private static ActivityLearningProgressDto MapLearningProgress(ActivityProgress progress)
    {
        return new ActivityLearningProgressDto
        {
            ActivityStatus = progress.ActivityStatus.ToString(),
            ResumeState = ActivityResumeStateHelper.Deserialize(progress.ResumeState),
            LastAccessedAt = progress.LastAccessedAt,
            CompletedAt = progress.CompletedAt,
            CompletionSource = ActivityResumeStateHelper.ToApiString(progress.CompletionSource),
        };
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

            var studentWideAttempts = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.StudentId == enrollment.StudentId && !me.IsDeleted);
            var nextAttemptByModuleId = studentWideAttempts
                .GroupBy(me => me.ModuleId)
                .ToDictionary(g => g.Key, g => g.Max(me => me.AttemptNumber) + 1);

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
                    AttemptNumber = nextAttemptByModuleId.TryGetValue(module.Id, out var nextAttempt) ? nextAttempt : 1,
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

        var assignmentIds = snapshot.AssignmentsById.Keys.ToList();
        var submissions = assignmentIds.Count > 0
            ? await _unitOfWork.Submissions.GetAllAsync(
                s => s.StudentId == enrollment.StudentId
                     && assignmentIds.Contains(s.AssignmentId)
                     && !s.IsDeleted)
            : new List<Submission>();

        var submissionsByAssignmentId = submissions
            .GroupBy(s => s.AssignmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var submissionsByMilestoneId = submissions
            .Where(s => s.ResearchMilestoneId.HasValue)
            .GroupBy(s => s.ResearchMilestoneId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.AttemptNumber).ThenByDescending(s => s.CreatedAt).First());

        return new EnrollmentCurriculumContext
        {
            LatestEnrollmentByModuleId = latestEnrollmentByModuleId,
            ModulesById = modulesById,
            ProgressByActivityId = progressByActivityId,
            SubmissionsByAssignmentId = submissionsByAssignmentId,
            SubmissionsByMilestoneId = submissionsByMilestoneId,
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

        moduleDto.Assignments = snapshot.ModuleScopedAssignmentsByModuleId.TryGetValue(module.Id, out var moduleAssignments)
            ? moduleAssignments.Select(assignment => MapEnrollmentAssignment(
                assignment,
                snapshot,
                context,
                module.Id,
                isLocked)).ToList()
            : [];

        if (module.ModuleType == ModuleType.Research)
        {
            if (snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var moduleMilestones))
            {
                var milestoneDtos = new List<EnrollmentCurriculumMilestoneDto>();
                ResearchMilestone? previousMilestone = null;

                foreach (var milestone in moduleMilestones)
                {
                    milestoneDtos.Add(new EnrollmentCurriculumMilestoneDto
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
                        Assignment = snapshot.AssignmentsById.TryGetValue(milestone.AssignmentId, out var milestoneAssignment)
                            ? MapEnrollmentAssignment(
                                milestoneAssignment,
                                snapshot,
                                context,
                                module.Id,
                                isLocked,
                                milestone,
                                previousMilestone)
                            : null,
                    });

                    previousMilestone = milestone;
                }

                moduleDto.Milestones = milestoneDtos;
            }
            else
            {
                moduleDto.Milestones = [];
            }
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
                Assignments = snapshot.AssignmentsByCourseId.TryGetValue(course.Id, out var courseAssignments)
                    ? courseAssignments
                        .Select(assignment => MapEnrollmentAssignment(
                            assignment,
                            snapshot,
                            context,
                            module.Id,
                            isLocked))
                        .ToList()
                    : [],
            }).ToList();
        }

        return moduleDto;
    }

    private static EnrollmentCurriculumAssignmentDto MapEnrollmentAssignment(
        Assignment assignment,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid moduleId,
        bool moduleLocked,
        ResearchMilestone? researchMilestone = null,
        ResearchMilestone? previousResearchMilestone = null)
    {
        return new EnrollmentCurriculumAssignmentDto
        {
            AssignmentId = assignment.Id,
            AssignmentCode = assignment.Code,
            Title = assignment.Title,
            AssignmentType = assignment.AssignmentType,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            DueDate = assignment.DueDate,
            Status = ResolveAssignmentStatus(
                assignment,
                snapshot,
                context,
                moduleId,
                moduleLocked,
                researchMilestone,
                previousResearchMilestone),
        };
    }

    private static string ResolveAssignmentStatus(
        Assignment assignment,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid moduleId,
        bool moduleLocked,
        ResearchMilestone? researchMilestone = null,
        ResearchMilestone? previousResearchMilestone = null)
    {
        if (moduleLocked)
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        if (context.SubmissionsByAssignmentId.TryGetValue(assignment.Id, out var submissions)
            && submissions.Count > 0)
        {
            var passed = submissions.Any(s =>
                s.Status == SubmissionStatus.Graded
                && s.AssignedGrade.HasValue
                && s.AssignedGrade.Value >= assignment.PassScore);

            if (passed)
            {
                return CurriculumStatusHelper.StatusCompleted;
            }

            var inProgress = submissions.Any(s =>
                s.Status is SubmissionStatus.Pending or SubmissionStatus.ReturnedForRevision);

            if (!inProgress)
            {
                return CurriculumStatusHelper.StatusSubmitted;
            }
        }

        if (!IsAssignmentAccessible(
                assignment,
                moduleId,
                snapshot,
                context,
                researchMilestone,
                previousResearchMilestone))
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static bool IsAssignmentAccessible(
        Assignment assignment,
        Guid moduleId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        ResearchMilestone? researchMilestone,
        ResearchMilestone? previousResearchMilestone)
    {
        return CurriculumStatusHelper.IsAssignmentAccessible(
            assignment,
            moduleId,
            snapshot,
            activityId => IsActivityCompleted(activityId, snapshot, context),
            researchMilestone,
            previousResearchMilestone,
            context.SubmissionsByMilestoneId);
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

        var activityDto = new EnrollmentCurriculumActivityDto
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

        ApplyResumeFields(activityDto, activity.Id, context, status);
        return activityDto;
    }

    private static HashSet<Guid> CollectCurrentActivityIds(
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context)
    {
        var currentActivityIdSet = new HashSet<Guid>();

        foreach (var module in snapshot.Modules)
        {
            if (!CurriculumStatusHelper.IsModuleUnlocked(
                    module,
                    context.LatestEnrollmentByModuleId,
                    context.ModulesById))
            {
                continue;
            }

            if (context.LatestEnrollmentByModuleId.TryGetValue(module.Id, out var latestModuleEnrollment)
                && (latestModuleEnrollment.Status == EnrollmentStatus.Completed
                    || latestModuleEnrollment.ProgressPercent >= 100m))
            {
                continue;
            }

            foreach (var activityId in snapshot.GlobalActivityOrder)
            {
                if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var owningModuleId)
                    || owningModuleId != module.Id)
                {
                    continue;
                }

                if (!IsActivityAccessible(activityId, snapshot, context)
                    || IsActivityCompleted(activityId, snapshot, context))
                {
                    continue;
                }

                currentActivityIdSet.Add(activityId);
                break;
            }
        }

        return currentActivityIdSet;
    }

    private static EnrollmentCurriculumMindMapModuleDto MapMindMapModule(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        IReadOnlySet<Guid> currentActivityIds)
    {
        var isLocked = !CurriculumStatusHelper.IsModuleUnlocked(
            module,
            context.LatestEnrollmentByModuleId,
            context.ModulesById);

        context.LatestEnrollmentByModuleId.TryGetValue(module.Id, out var moduleEnrollment);
        var lockReason = isLocked
            ? CurriculumStatusHelper.GetModuleLockReason(
                module,
                context.LatestEnrollmentByModuleId,
                context.ModulesById)
            : null;

        var moduleDto = new EnrollmentCurriculumMindMapModuleDto
        {
            ModuleInfo = new EnrollmentCurriculumMindMapModuleInfoDto
            {
                ModuleId = module.Id,
                ModuleName = module.Name,
                ModuleCode = module.Code,
                ModuleOrder = module.ModuleOrder,
                ModuleType = module.ModuleType,
                PrerequisiteModuleId = module.PrerequisiteModuleId,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                IsMandatory = module.IsMandatory,
                LearningOutcomes = module.LearningOutcomes,
            },
            Navigation = BuildMindMapNavigation(NodeTypeModule, module.Id, moduleEnrollment?.Id),
        };

        moduleDto.Assignments = snapshot.ModuleScopedAssignmentsByModuleId.TryGetValue(module.Id, out var moduleAssignments)
            ? moduleAssignments.Select(assignment => BuildMindMapAssignment(
                assignment,
                snapshot,
                context,
                module.Id,
                isLocked,
                lockReason,
                moduleEnrollment?.Id)).ToList()
            : [];

        if (module.ModuleType == ModuleType.Research)
        {
            if (snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var moduleMilestones))
            {
                ResearchMilestone? previousMilestone = null;

                foreach (var milestone in moduleMilestones)
                {
                    moduleDto.Milestones.Add(MapMindMapMilestone(
                        milestone,
                        previousMilestone,
                        module,
                        snapshot,
                        context,
                        currentActivityIds,
                        isLocked,
                        lockReason,
                        moduleEnrollment?.Id));
                    previousMilestone = milestone;
                }
            }
        }
        else if (snapshot.CoursesByModuleId.TryGetValue(module.Id, out var moduleCourses))
        {
            var courseOrder = 1;
            foreach (var course in moduleCourses)
            {
                moduleDto.Courses.Add(MapMindMapCourse(
                    course,
                    courseOrder++,
                    module,
                    snapshot,
                    context,
                    currentActivityIds,
                    isLocked,
                    lockReason,
                    moduleEnrollment?.Id));
            }
        }

        var childStatuses = moduleDto.Courses.Select(c => c.Learning.Status)
            .Concat(moduleDto.Milestones.Select(m => m.Learning.Status))
            .Concat(moduleDto.Assignments.Select(a => a.Learning.Status))
            .ToList();

        if (childStatuses.Count == 0)
        {
            childStatuses = CollectLeafStatuses(moduleDto);
        }

        var moduleStatus = ResolveMindMapModuleStatus(
            isLocked,
            moduleEnrollment,
            childStatuses);

        moduleDto.Learning = new EnrollmentCurriculumMindMapModuleLearningDto
        {
            Status = moduleStatus,
            ProgressPercent = moduleEnrollment?.ProgressPercent ?? 0m,
            IsLocked = isLocked,
            LockReason = lockReason,
        };

        moduleDto.ChildProgress = BuildChildProgressFromLeaves(CollectLeafStatuses(moduleDto));

        return moduleDto;
    }

    private static EnrollmentCurriculumMindMapCourseDto MapMindMapCourse(
        Course course,
        int courseOrder,
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        IReadOnlySet<Guid> currentActivityIds,
        bool moduleLocked,
        string? moduleLockReason,
        Guid? moduleEnrollmentId)
    {
        var activities = snapshot.ActivitiesByCourseId.TryGetValue(course.Id, out var courseActivities)
            ? courseActivities.Select(activity => BuildMindMapActivity(
                activity,
                snapshot,
                context,
                currentActivityIds,
                moduleLocked,
                moduleLockReason,
                moduleEnrollmentId)).ToList()
            : [];

        var assignments = snapshot.AssignmentsByCourseId.TryGetValue(course.Id, out var courseAssignments)
            ? courseAssignments.Select(assignment => BuildMindMapAssignment(
                assignment,
                snapshot,
                context,
                module.Id,
                moduleLocked,
                moduleLockReason,
                moduleEnrollmentId)).ToList()
            : [];

        var childStatuses = activities.Select(a => a.Learning.Status)
            .Concat(assignments.Select(a => a.Learning.Status))
            .ToList();
        var childProgress = BuildChildProgressFromLeaves(childStatuses);
        var status = ResolveMindMapContainerStatus(moduleLocked, childStatuses);

        return new EnrollmentCurriculumMindMapCourseDto
        {
            CourseInfo = new EnrollmentCurriculumMindMapCourseInfoDto
            {
                CourseId = course.Id,
                CourseName = course.Name,
                CourseOrder = courseOrder,
            },
            Learning = new EnrollmentCurriculumMindMapContainerLearningDto
            {
                Status = status,
                ProgressPercent = childProgress.ProgressPercent,
                IsLocked = moduleLocked || status == CurriculumStatusHelper.StatusLocked,
                LockReason = moduleLocked ? moduleLockReason : null,
            },
            ChildProgress = childProgress,
            Navigation = BuildMindMapNavigation(NodeTypeCourse, course.Id, moduleEnrollmentId),
            Activities = activities,
            Assignments = assignments,
        };
    }

    private static EnrollmentCurriculumMindMapMilestoneDto MapMindMapMilestone(
        ResearchMilestone milestone,
        ResearchMilestone? previousMilestone,
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        IReadOnlySet<Guid> currentActivityIds,
        bool moduleLocked,
        string? moduleLockReason,
        Guid? moduleEnrollmentId)
    {
        var activities = new List<EnrollmentCurriculumMindMapActivityDto>();
        if (snapshot.LinksByMilestoneId.TryGetValue(milestone.Id, out var links))
        {
            foreach (var link in links)
            {
                if (!snapshot.ActivitiesById.TryGetValue(link.ActivityId, out var activity))
                {
                    continue;
                }

                activities.Add(BuildMindMapActivity(
                    activity,
                    snapshot,
                    context,
                    currentActivityIds,
                    moduleLocked,
                    moduleLockReason,
                    moduleEnrollmentId));
            }
        }

        EnrollmentCurriculumMindMapAssignmentDto? assignmentDto = null;
        if (snapshot.AssignmentsById.TryGetValue(milestone.AssignmentId, out var milestoneAssignment))
        {
            assignmentDto = BuildMindMapAssignment(
                milestoneAssignment,
                snapshot,
                context,
                module.Id,
                moduleLocked,
                moduleLockReason,
                moduleEnrollmentId,
                milestone,
                previousMilestone);
        }

        var childStatuses = activities.Select(a => a.Learning.Status).ToList();
        if (assignmentDto != null)
        {
            childStatuses.Add(assignmentDto.Learning.Status);
        }

        var childProgress = BuildChildProgressFromLeaves(childStatuses);
        var status = ResolveMindMapContainerStatus(moduleLocked, childStatuses);

        return new EnrollmentCurriculumMindMapMilestoneDto
        {
            MilestoneInfo = new EnrollmentCurriculumMindMapMilestoneInfoDto
            {
                MilestoneId = milestone.Id,
                MilestoneName = milestone.Title,
                MilestoneOrder = milestone.MilestoneOrder,
                IsCapstone = milestone.IsCapstone,
            },
            Learning = new EnrollmentCurriculumMindMapContainerLearningDto
            {
                Status = status,
                ProgressPercent = childProgress.ProgressPercent,
                IsLocked = moduleLocked || status == CurriculumStatusHelper.StatusLocked,
                LockReason = moduleLocked ? moduleLockReason : null,
            },
            ChildProgress = childProgress,
            Navigation = BuildMindMapNavigation(NodeTypeMilestone, milestone.Id, moduleEnrollmentId),
            Activities = activities,
            Assignment = assignmentDto,
        };
    }

    private static EnrollmentCurriculumMindMapActivityDto BuildMindMapActivity(
        Activity activity,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        IReadOnlySet<Guid> currentActivityIds,
        bool moduleLocked,
        string? moduleLockReason,
        Guid? moduleEnrollmentId)
    {
        snapshot.MaterialsByActivityId.TryGetValue(activity.Id, out var material);

        var status = ResolveMindMapActivityStatus(
            activity.Id,
            snapshot,
            context,
            currentActivityIds,
            moduleLocked);

        var isLocked = status == CurriculumStatusHelper.StatusLocked;
        var learning = new EnrollmentCurriculumMindMapActivityLearningDto
        {
            Status = status,
            IsLocked = isLocked,
            LockReason = isLocked
                ? (moduleLocked
                    ? moduleLockReason
                    : "Complete previous activities in this section to unlock.")
                : null,
        };

        if (status is not (CurriculumStatusHelper.StatusLocked or CurriculumStatusHelper.StatusCompleted)
            && context.ProgressByActivityId.TryGetValue(activity.Id, out var progress))
        {
            learning.ResumeState = ActivityResumeStateHelper.Deserialize(progress.ResumeState);
            learning.LastAccessedAt = progress.LastAccessedAt;
        }

        return new EnrollmentCurriculumMindMapActivityDto
        {
            ActivityInfo = new EnrollmentCurriculumMindMapActivityInfoDto
            {
                ActivityId = activity.Id,
                ActivityName = activity.Name,
                ActivityCode = activity.Code,
                ActivityOrder = activity.ActivityOrder,
                ActivityType = activity.ActivityType,
                Description = activity.Description,
                Material = material == null
                    ? null
                    : new EnrollmentCurriculumMaterialDto
                    {
                        MaterialId = material.Id,
                        MaterialName = material.Title,
                        MaterialType = material.MaterialType,
                    },
            },
            Learning = learning,
            Navigation = BuildMindMapNavigation(NodeTypeActivity, activity.Id, moduleEnrollmentId),
        };
    }

    private static EnrollmentCurriculumMindMapAssignmentDto BuildMindMapAssignment(
        Assignment assignment,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        Guid moduleId,
        bool moduleLocked,
        string? moduleLockReason,
        Guid? moduleEnrollmentId,
        ResearchMilestone? researchMilestone = null,
        ResearchMilestone? previousResearchMilestone = null)
    {
        var status = ResolveAssignmentStatus(
            assignment,
            snapshot,
            context,
            moduleId,
            moduleLocked,
            researchMilestone,
            previousResearchMilestone);

        var isLocked = status == CurriculumStatusHelper.StatusLocked;

        return new EnrollmentCurriculumMindMapAssignmentDto
        {
            AssignmentInfo = new EnrollmentCurriculumMindMapAssignmentInfoDto
            {
                AssignmentId = assignment.Id,
                AssignmentCode = assignment.Code,
                Title = assignment.Title,
                AssignmentType = assignment.AssignmentType,
                MaxPoints = assignment.MaxPoints,
                PassScore = assignment.PassScore,
                IsRequiredForModulePass = assignment.IsRequiredForModulePass,
                DueDate = assignment.DueDate,
            },
            Learning = new EnrollmentCurriculumMindMapAssignmentLearningDto
            {
                Status = status,
                IsLocked = isLocked,
                LockReason = isLocked
                    ? (moduleLocked
                        ? moduleLockReason
                        : "Complete required activities before this assignment unlocks.")
                    : null,
            },
            Navigation = BuildMindMapNavigation(NodeTypeAssignment, assignment.Id, moduleEnrollmentId),
        };
    }

    private static EnrollmentCurriculumMindMapNavigationDto BuildMindMapNavigation(
        string targetType,
        Guid targetId,
        Guid? moduleEnrollmentId)
    {
        return new EnrollmentCurriculumMindMapNavigationDto
        {
            TargetType = targetType,
            TargetId = targetId,
            ModuleEnrollmentId = moduleEnrollmentId,
        };
    }

    private static List<EnrollmentCurriculumMindMapPathDto> BuildMindMapCurrentPaths(
        IReadOnlyList<EnrollmentCurriculumMindMapModuleDto> modules,
        IReadOnlySet<Guid> currentActivityIds,
        Guid programId)
    {
        var paths = new List<EnrollmentCurriculumMindMapPathDto>();

        foreach (var module in modules)
        {
            foreach (var course in module.Courses)
            {
                foreach (var activity in course.Activities)
                {
                    if (!currentActivityIds.Contains(activity.ActivityInfo.ActivityId))
                    {
                        continue;
                    }

                    paths.Add(new EnrollmentCurriculumMindMapPathDto
                    {
                        Nodes =
                        [
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeProgram,
                                NodeId = programId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeModule,
                                NodeId = module.ModuleInfo.ModuleId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeCourse,
                                NodeId = course.CourseInfo.CourseId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeActivity,
                                NodeId = activity.ActivityInfo.ActivityId,
                            },
                        ],
                    });
                }
            }

            foreach (var milestone in module.Milestones)
            {
                foreach (var activity in milestone.Activities)
                {
                    if (!currentActivityIds.Contains(activity.ActivityInfo.ActivityId))
                    {
                        continue;
                    }

                    paths.Add(new EnrollmentCurriculumMindMapPathDto
                    {
                        Nodes =
                        [
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeProgram,
                                NodeId = programId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeModule,
                                NodeId = module.ModuleInfo.ModuleId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeMilestone,
                                NodeId = milestone.MilestoneInfo.MilestoneId,
                            },
                            new EnrollmentCurriculumMindMapPathNodeDto
                            {
                                NodeType = NodeTypeActivity,
                                NodeId = activity.ActivityInfo.ActivityId,
                            },
                        ],
                    });
                }
            }
        }

        return paths;
    }

    private static List<string> CollectLeafStatuses(EnrollmentCurriculumMindMapModuleDto module)
    {
        var statuses = new List<string>();

        foreach (var course in module.Courses)
        {
            statuses.AddRange(course.Activities.Select(a => a.Learning.Status));
            statuses.AddRange(course.Assignments.Select(a => a.Learning.Status));
        }

        foreach (var milestone in module.Milestones)
        {
            statuses.AddRange(milestone.Activities.Select(a => a.Learning.Status));
            if (milestone.Assignment != null)
            {
                statuses.Add(milestone.Assignment.Learning.Status);
            }
        }

        statuses.AddRange(module.Assignments.Select(a => a.Learning.Status));
        return statuses;
    }

    private static EnrollmentCurriculumMindMapChildProgressDto BuildChildProgressFromLeaves(
        IReadOnlyList<string> leafStatuses)
    {
        var total = leafStatuses.Count;
        var completed = leafStatuses.Count(IsMindMapLeafCompleted);
        var progressPercent = total == 0
            ? 0m
            : Math.Round((decimal)completed / total * 100m, 2);

        return new EnrollmentCurriculumMindMapChildProgressDto
        {
            TotalCount = total,
            CompletedCount = completed,
            ProgressPercent = progressPercent,
        };
    }

    private static bool IsMindMapLeafCompleted(string status) =>
        status is CurriculumStatusHelper.StatusCompleted or CurriculumStatusHelper.StatusSubmitted;

    private static string ResolveHubStatus(
        decimal progressPercent,
        int completedModuleCount,
        int totalModuleCount)
    {
        if (totalModuleCount > 0 && completedModuleCount >= totalModuleCount)
        {
            return CurriculumStatusHelper.StatusCompleted;
        }

        if (progressPercent > 0m || completedModuleCount > 0)
        {
            return StatusInProgress;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static string ResolveMindMapModuleStatus(
        bool isLocked,
        ModuleEnrollment? moduleEnrollment,
        IReadOnlyList<string> childStatuses)
    {
        if (isLocked)
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        if (moduleEnrollment?.Status == EnrollmentStatus.Completed
            || moduleEnrollment?.ProgressPercent >= 100m)
        {
            return CurriculumStatusHelper.StatusCompleted;
        }

        if (childStatuses.Any(s => s == CurriculumStatusHelper.StatusCurrent))
        {
            return CurriculumStatusHelper.StatusCurrent;
        }

        if (moduleEnrollment?.ProgressPercent > 0m
            || childStatuses.Any(s => s is StatusInProgress or CurriculumStatusHelper.StatusSubmitted))
        {
            return StatusInProgress;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static string ResolveMindMapContainerStatus(
        bool moduleLocked,
        IReadOnlyList<string> childStatuses)
    {
        if (moduleLocked)
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        if (childStatuses.Count == 0)
        {
            return CurriculumStatusHelper.StatusAvailable;
        }

        if (childStatuses.All(IsMindMapLeafCompleted))
        {
            return CurriculumStatusHelper.StatusCompleted;
        }

        if (childStatuses.Any(s => s == CurriculumStatusHelper.StatusCurrent))
        {
            return CurriculumStatusHelper.StatusCurrent;
        }

        if (childStatuses.Any(s =>
                s is StatusInProgress
                    or CurriculumStatusHelper.StatusSubmitted
                    or CurriculumStatusHelper.StatusCompleted
                    or CurriculumStatusHelper.StatusAvailable))
        {
            if (childStatuses.Any(s =>
                    s is StatusInProgress
                        or CurriculumStatusHelper.StatusSubmitted
                        or CurriculumStatusHelper.StatusCompleted))
            {
                return StatusInProgress;
            }

            return CurriculumStatusHelper.StatusAvailable;
        }

        return CurriculumStatusHelper.StatusLocked;
    }

    private static string ResolveMindMapActivityStatus(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        EnrollmentCurriculumContext context,
        IReadOnlySet<Guid> currentActivityIds,
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

        if (currentActivityIds.Contains(activityId))
        {
            return CurriculumStatusHelper.StatusCurrent;
        }

        if (IsActivityInProgress(activityId, context))
        {
            return StatusInProgress;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static bool IsActivityInProgress(Guid activityId, EnrollmentCurriculumContext context)
    {
        if (!context.ProgressByActivityId.TryGetValue(activityId, out var progress))
        {
            return false;
        }

        if (progress.ActivityStatus == ActivityStatus.Done)
        {
            return false;
        }

        return progress.ActivityStatus == ActivityStatus.InProgress
               || progress.LastAccessedAt.HasValue
               || !string.IsNullOrWhiteSpace(progress.ResumeState);
    }

    private static void ApplyResumeFields(
        EnrollmentCurriculumActivityDto dto,
        Guid activityId,
        EnrollmentCurriculumContext context,
        string status)
    {
        if (status is CurriculumStatusHelper.StatusLocked or CurriculumStatusHelper.StatusCompleted)
        {
            return;
        }

        if (!context.ProgressByActivityId.TryGetValue(activityId, out var progress))
        {
            return;
        }

        dto.ResumeState = ActivityResumeStateHelper.Deserialize(progress.ResumeState);
        dto.LastAccessedAt = progress.LastAccessedAt;
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
        if (!snapshot.ActivitiesById.ContainsKey(activityId))
        {
            return false;
        }

        return CurriculumStatusHelper.IsActivityCompleted(
            activityId,
            context.ProgressByActivityId);
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

        public Dictionary<Guid, List<Submission>> SubmissionsByAssignmentId { get; init; } = new();

        public Dictionary<Guid, Submission> SubmissionsByMilestoneId { get; init; } = new();
    }
}
