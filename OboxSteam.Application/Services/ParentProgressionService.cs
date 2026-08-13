using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ParentProgressionDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public sealed class ParentProgressionService : IParentProgressionService
{
    private const int MaxRecentMilestones = 10;
    private const string StatusOverdue = "overdue";
    private const decimal ExcellentGradeThreshold = 90m;
    private const decimal PassGradeThreshold = 70m;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ParentProgressionService> _logger;

    public ParentProgressionService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ParentProgressionService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<ParentChildProgressionDto> GetChildProgressionAsync(Guid studentId)
    {
        var (student, link) = await EnrollmentAccessValidator.EnsureVerifiedParentOfAsync(
            _unitOfWork,
            _claimsService,
            studentId);

        var enrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => pe.StudentId == studentId && !pe.IsDeleted,
            pe => pe.Program);

        var orderedEnrollments = enrollments
            .OrderByDescending(pe => pe.Status == EnrollmentStatus.Active)
            .ThenByDescending(pe => pe.EnrolledAt ?? pe.CreatedAt)
            .ToList();

        var briefs = new List<ParentEnrollmentBriefDto>();
        var allEvents = new List<ParentProgressEventDto>();
        DateTime? globalLastAccessed = null;

        foreach (var enrollment in orderedEnrollments)
        {
            var program = enrollment.Program
                ?? await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId);
            if (program == null || program.IsDeleted)
            {
                continue;
            }

            var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
            var context = await BuildContextAsync(enrollment, snapshot);
            var brief = MapEnrollmentBrief(enrollment, program, snapshot, context);
            briefs.Add(brief);

            if (brief.LastAccessedAt.HasValue
                && (!globalLastAccessed.HasValue || brief.LastAccessedAt > globalLastAccessed))
            {
                globalLastAccessed = brief.LastAccessedAt;
            }

            allEvents.AddRange(BuildMilestonesForEnrollment(enrollment, program, snapshot, context));
        }

        var recentMilestones = allEvents
            .OrderByDescending(e => e.OccurredAt)
            .Take(MaxRecentMilestones)
            .ToList();

        _logger.LogInformation(
            "[GetChildProgressionAsync] Parent viewed progression for student {StudentId} ({EnrollmentCount} enrollments).",
            studentId,
            briefs.Count);

        return new ParentChildProgressionDto
        {
            Student = new ParentLinkedStudentDto
            {
                LinkedUserId = student.Id,
                Code = student.Code,
                FullName = student.FullName,
                Email = student.Email,
                Phone = student.Phone,
                AvatarUrl = student.AvatarUrl,
                IsVerified = link.IsVerified,
                LinkedAt = link.CreatedAt,
            },
            Summary = new ParentProgressionSummaryDto
            {
                ActiveEnrollmentCount = briefs.Count(e => e.Status == EnrollmentStatus.Active),
                CompletedEnrollmentCount = briefs.Count(e => e.Status == EnrollmentStatus.Completed),
                LastAccessedAt = globalLastAccessed,
            },
            Enrollments = briefs,
            RecentMilestones = recentMilestones,
        };
    }

    public async Task<ParentEnrollmentProgressionDto> GetEnrollmentProgressionAsync(
        Guid studentId,
        Guid enrollmentId)
    {
        await EnrollmentAccessValidator.EnsureVerifiedParentOfAsync(
            _unitOfWork,
            _claimsService,
            studentId);

        var enrollment = await _unitOfWork.ProgramEnrollments.GetByIdAsync(
            enrollmentId,
            pe => pe.Program);
        if (enrollment == null || enrollment.IsDeleted || enrollment.StudentId != studentId)
        {
            throw ErrorHelper.NotFound($"Program enrollment '{enrollmentId}' not found.");
        }

        var program = enrollment.Program
            ?? await _unitOfWork.Programs.GetByIdAsync(enrollment.ProgramId)
            ?? throw ErrorHelper.NotFound($"Program '{enrollment.ProgramId}' not found.");

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, enrollment.ProgramId);
        var context = await BuildContextAsync(enrollment, snapshot);
        var lastAccessedAt = ResolveLastAccessedAt(context);

        var modules = snapshot.Modules
            .Select(module => MapModuleProgress(module, snapshot, context))
            .ToList();

        _logger.LogInformation(
            "[GetEnrollmentProgressionAsync] Parent viewed enrollment {EnrollmentId} for student {StudentId}.",
            enrollmentId,
            studentId);

        return new ParentEnrollmentProgressionDto
        {
            StudentId = studentId,
            Enrollment = new ParentEnrollmentHeaderDto
            {
                EnrollmentId = enrollment.Id,
                ProgramId = enrollment.ProgramId,
                ProgramName = program.Name,
                ProgramCode = program.Code,
                ThumbnailUrl = program.ThumbnailUrl,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                EnrolledAt = enrollment.EnrolledAt,
                StartedAt = enrollment.StartedAt,
                CompletedAt = enrollment.CompletedAt,
                LastAccessedAt = lastAccessedAt,
            },
            ClassInfo = await ResolveClassInfoAsync(enrollment.Id),
            Modules = modules,
        };
    }

    private async Task<ParentClassInfoDto?> ResolveClassInfoAsync(Guid programEnrollmentId)
    {
        var classEnrollment = await _unitOfWork.ClassEnrollments.FirstOrDefaultAsync(
            ce => ce.ProgramEnrollmentId == programEnrollmentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        if (classEnrollment == null)
        {
            return null;
        }

        var classEntity = await _unitOfWork.Classes.GetByIdAsync(classEnrollment.ClassId);
        if (classEntity == null || classEntity.IsDeleted)
        {
            return null;
        }

        string? mentorName = null;
        if (classEntity.MentorId.HasValue)
        {
            var mentor = await _unitOfWork.Users.GetByIdAsync(classEntity.MentorId.Value);
            mentorName = mentor?.FullName;
        }

        return new ParentClassInfoDto
        {
            ClassId = classEnrollment.ClassId,
            ClassName = classEntity.Name,
            MentorName = mentorName,
        };
    }

    private ParentEnrollmentBriefDto MapEnrollmentBrief(
        ProgramEnrollment enrollment,
        Program program,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        var currentActivityId = CurriculumStatusHelper.FindCurrentActivityId(
            snapshot,
            activityId => IsActivityAccessible(activityId, snapshot, context),
            activityId => IsActivityCompleted(activityId, snapshot, context));

        ParentCurrentModuleDto? currentModule = null;
        ParentCurrentActivityDto? currentActivity = null;

        if (currentActivityId.HasValue
            && snapshot.ActivitiesById.TryGetValue(currentActivityId.Value, out var activity)
            && snapshot.ActivityModuleMap.TryGetValue(currentActivityId.Value, out var moduleId)
            && context.ModulesById.TryGetValue(moduleId, out var module))
        {
            context.LatestEnrollmentByModuleId.TryGetValue(moduleId, out var moduleEnrollment);
            currentModule = new ParentCurrentModuleDto
            {
                ModuleId = module.Id,
                ModuleEnrollmentId = moduleEnrollment?.Id,
                ModuleName = module.Name,
                ModuleOrder = module.ModuleOrder,
                ModuleType = module.ModuleType,
                Status = moduleEnrollment?.Status,
                ProgressPercent = moduleEnrollment?.ProgressPercent,
            };
            currentActivity = new ParentCurrentActivityDto
            {
                ActivityId = activity.Id,
                ActivityName = activity.Name,
                ActivityType = activity.ActivityType,
            };
        }
        else
        {
            // Fallback: first unlocked incomplete module
            var fallbackModule = snapshot.Modules.FirstOrDefault(m =>
            {
                if (!CurriculumStatusHelper.IsModuleUnlocked(
                        m,
                        context.LatestEnrollmentByModuleId,
                        context.ModulesById))
                {
                    return false;
                }

                if (!context.LatestEnrollmentByModuleId.TryGetValue(m.Id, out var me))
                {
                    return true;
                }

                return me.Status != EnrollmentStatus.Completed && me.ProgressPercent < 100m;
            });

            if (fallbackModule != null)
            {
                context.LatestEnrollmentByModuleId.TryGetValue(fallbackModule.Id, out var moduleEnrollment);
                currentModule = new ParentCurrentModuleDto
                {
                    ModuleId = fallbackModule.Id,
                    ModuleEnrollmentId = moduleEnrollment?.Id,
                    ModuleName = fallbackModule.Name,
                    ModuleOrder = fallbackModule.ModuleOrder,
                    ModuleType = fallbackModule.ModuleType,
                    Status = moduleEnrollment?.Status,
                    ProgressPercent = moduleEnrollment?.ProgressPercent,
                };
            }
        }

        return new ParentEnrollmentBriefDto
        {
            EnrollmentId = enrollment.Id,
            ProgramId = enrollment.ProgramId,
            ProgramName = program.Name,
            ProgramCode = program.Code,
            ThumbnailUrl = program.ThumbnailUrl,
            Level = program.Level,
            Status = enrollment.Status,
            ProgressPercent = enrollment.ProgressPercent,
            EnrolledAt = enrollment.EnrolledAt,
            StartedAt = enrollment.StartedAt,
            CompletedAt = enrollment.CompletedAt,
            CurrentModule = currentModule,
            CurrentActivity = currentActivity,
            LastAccessedAt = ResolveLastAccessedAt(context),
            Blockers = BuildBlockers(enrollment, snapshot, context),
        };
    }

    private ParentModuleProgressDto MapModuleProgress(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        var isLocked = !CurriculumStatusHelper.IsModuleUnlocked(
            module,
            context.LatestEnrollmentByModuleId,
            context.ModulesById);
        context.LatestEnrollmentByModuleId.TryGetValue(module.Id, out var moduleEnrollment);

        var moduleActivityIds = snapshot.ActivityModuleMap
            .Where(kvp => kvp.Value == module.Id)
            .Select(kvp => kvp.Key)
            .ToList();

        var completedCount = moduleActivityIds.Count(id => IsActivityCompleted(id, snapshot, context));

        var assignments = CollectModuleAssignments(module, snapshot)
            .Select(item => MapAssignmentOutcome(
                item.Assignment,
                snapshot,
                context,
                module.Id,
                isLocked,
                item.Milestone,
                item.PreviousMilestone))
            .ToList();

        return new ParentModuleProgressDto
        {
            ModuleId = module.Id,
            ModuleEnrollmentId = moduleEnrollment?.Id,
            ModuleName = module.Name,
            ModuleOrder = module.ModuleOrder,
            ModuleType = module.ModuleType,
            IsLocked = isLocked,
            LockReason = isLocked
                ? CurriculumStatusHelper.GetModuleLockReason(
                    module,
                    context.LatestEnrollmentByModuleId,
                    context.ModulesById)
                : null,
            Status = moduleEnrollment?.Status,
            ProgressPercent = moduleEnrollment?.ProgressPercent ?? 0m,
            AttemptNumber = moduleEnrollment?.AttemptNumber,
            FinalGrade = moduleEnrollment?.FinalGrade,
            OutcomeLabel = ResolveOutcomeLabel(moduleEnrollment),
            StartedAt = moduleEnrollment?.StartedAt,
            CompletedAt = moduleEnrollment?.CompletedAt,
            ActivityStats = new ParentActivityStatsDto
            {
                Total = moduleActivityIds.Count,
                Completed = completedCount,
            },
            Assignments = assignments,
        };
    }

    private static List<(
        Assignment Assignment,
        ResearchMilestone? Milestone,
        ResearchMilestone? PreviousMilestone)> CollectModuleAssignments(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        var result = new List<(Assignment, ResearchMilestone?, ResearchMilestone?)>();

        if (module.ModuleType == ModuleType.Research
            && snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var milestones))
        {
            ResearchMilestone? previous = null;
            foreach (var milestone in milestones)
            {
                if (snapshot.AssignmentsById.TryGetValue(milestone.AssignmentId, out var assignment))
                {
                    result.Add((assignment, milestone, previous));
                }

                previous = milestone;
            }

            return result;
        }

        if (snapshot.ModuleScopedAssignmentsByModuleId.TryGetValue(module.Id, out var moduleAssignments))
        {
            foreach (var assignment in moduleAssignments)
            {
                result.Add((assignment, null, null));
            }
        }

        if (snapshot.CoursesByModuleId.TryGetValue(module.Id, out var courses))
        {
            foreach (var course in courses)
            {
                if (!snapshot.AssignmentsByCourseId.TryGetValue(course.Id, out var courseAssignments))
                {
                    continue;
                }

                foreach (var assignment in courseAssignments)
                {
                    result.Add((assignment, null, null));
                }
            }
        }

        return result;
    }

    private ParentAssignmentOutcomeDto MapAssignmentOutcome(
        Assignment assignment,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context,
        Guid moduleId,
        bool moduleLocked,
        ResearchMilestone? researchMilestone,
        ResearchMilestone? previousResearchMilestone)
    {
        var status = ResolveAssignmentStatus(
            assignment,
            snapshot,
            context,
            moduleId,
            moduleLocked,
            researchMilestone,
            previousResearchMilestone);

        if (status == CurriculumStatusHelper.StatusAvailable
            && assignment.DueDate.HasValue
            && assignment.DueDate.Value < DateTime.UtcNow)
        {
            status = StatusOverdue;
        }

        Submission? latest = null;
        bool? passed = null;
        if (context.SubmissionsByAssignmentId.TryGetValue(assignment.Id, out var submissions)
            && submissions.Count > 0)
        {
            latest = submissions
                .OrderByDescending(s => s.AttemptNumber)
                .ThenByDescending(s => s.CreatedAt)
                .First();

            passed = submissions.Any(s =>
                s.Status == SubmissionStatus.Graded
                && s.AssignedGrade.HasValue
                && s.AssignedGrade.Value >= assignment.PassScore);
        }

        return new ParentAssignmentOutcomeDto
        {
            AssignmentId = assignment.Id,
            Title = assignment.Title,
            AssignmentType = assignment.AssignmentType,
            IsRequiredForModulePass = assignment.IsRequiredForModulePass,
            DueDate = assignment.DueDate,
            Status = status,
            Score = latest?.AssignedGrade,
            MaxPoints = assignment.MaxPoints,
            PassScore = assignment.PassScore,
            Passed = passed,
            SubmittedAt = latest?.SubmittedAt,
            GradedAt = latest?.GradedAt,
            AttemptUsed = latest?.AttemptNumber,
            MaxAttempts = assignment.MaxAttempts,
        };
    }

    private static string ResolveAssignmentStatus(
        Assignment assignment,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context,
        Guid moduleId,
        bool moduleLocked,
        ResearchMilestone? researchMilestone,
        ResearchMilestone? previousResearchMilestone)
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

        if (!CurriculumStatusHelper.IsAssignmentAccessible(
                assignment,
                moduleId,
                snapshot,
                activityId => IsActivityCompleted(activityId, snapshot, context),
                researchMilestone,
                previousResearchMilestone,
                context.SubmissionsByMilestoneId))
        {
            return CurriculumStatusHelper.StatusLocked;
        }

        return CurriculumStatusHelper.StatusAvailable;
    }

    private static ParentModuleOutcomeLabel? ResolveOutcomeLabel(ModuleEnrollment? moduleEnrollment)
    {
        if (moduleEnrollment == null)
        {
            return ParentModuleOutcomeLabel.NotStarted;
        }

        return moduleEnrollment.Status switch
        {
            EnrollmentStatus.Failed => ParentModuleOutcomeLabel.Failed,
            EnrollmentStatus.Completed => ResolveCompletedOutcome(moduleEnrollment.FinalGrade),
            EnrollmentStatus.Active
                or EnrollmentStatus.PendingPayment
                or EnrollmentStatus.Deferred => ParentModuleOutcomeLabel.InProgress,
            EnrollmentStatus.Dropped => ParentModuleOutcomeLabel.NotStarted,
            _ => ParentModuleOutcomeLabel.NotStarted,
        };
    }

    private static ParentModuleOutcomeLabel ResolveCompletedOutcome(decimal? finalGrade)
    {
        if (!finalGrade.HasValue)
        {
            return ParentModuleOutcomeLabel.Pass;
        }

        if (finalGrade.Value >= ExcellentGradeThreshold)
        {
            return ParentModuleOutcomeLabel.Excellent;
        }

        if (finalGrade.Value >= PassGradeThreshold)
        {
            return ParentModuleOutcomeLabel.Pass;
        }

        return ParentModuleOutcomeLabel.NeedsImprovement;
    }

    private static List<ParentBlockerDto> BuildBlockers(
        ProgramEnrollment enrollment,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        var blockers = new List<ParentBlockerDto>();

        if (enrollment.Status == EnrollmentStatus.PendingPayment)
        {
            blockers.Add(new ParentBlockerDto
            {
                Code = ParentBlockerCode.PendingPayment,
                Message = "Đang chờ thanh toán để tiếp tục học.",
                EnrollmentId = enrollment.Id,
            });
        }

        foreach (var module in snapshot.Modules)
        {
            context.LatestEnrollmentByModuleId.TryGetValue(module.Id, out var moduleEnrollment);

            if (moduleEnrollment?.Status == EnrollmentStatus.Failed)
            {
                blockers.Add(new ParentBlockerDto
                {
                    Code = ParentBlockerCode.ModuleFailed,
                    Message = $"Học phần '{module.Name}' chưa đạt yêu cầu.",
                    ModuleId = module.Id,
                    EnrollmentId = enrollment.Id,
                });
            }

            if (module.PrerequisiteModuleId.HasValue
                && context.LatestEnrollmentByModuleId.TryGetValue(
                    module.PrerequisiteModuleId.Value,
                    out var prereq)
                && prereq.Status == EnrollmentStatus.Failed)
            {
                blockers.Add(new ParentBlockerDto
                {
                    Code = ParentBlockerCode.PrerequisiteFailed,
                    Message = $"Học phần tiên quyết của '{module.Name}' chưa đạt.",
                    ModuleId = module.Id,
                    EnrollmentId = enrollment.Id,
                });
            }

            if (IsNextLockedModule(module, snapshot, context))
            {
                blockers.Add(new ParentBlockerDto
                {
                    Code = ParentBlockerCode.ModuleLocked,
                    Message = CurriculumStatusHelper.GetModuleLockReason(
                                   module,
                                   context.LatestEnrollmentByModuleId,
                                   context.ModulesById)
                               ?? $"Học phần '{module.Name}' đang bị khóa.",
                    ModuleId = module.Id,
                    EnrollmentId = enrollment.Id,
                });
            }
        }

        // Overdue required assignments (cap at a few)
        var overdueCount = 0;
        foreach (var module in snapshot.Modules)
        {
            var isLocked = !CurriculumStatusHelper.IsModuleUnlocked(
                module,
                context.LatestEnrollmentByModuleId,
                context.ModulesById);
            foreach (var item in CollectModuleAssignments(module, snapshot))
            {
                if (!item.Assignment.IsRequiredForModulePass || !item.Assignment.DueDate.HasValue)
                {
                    continue;
                }

                var status = ResolveAssignmentStatus(
                    item.Assignment,
                    snapshot,
                    context,
                    module.Id,
                    isLocked,
                    item.Milestone,
                    item.PreviousMilestone);

                if (status is CurriculumStatusHelper.StatusCompleted or CurriculumStatusHelper.StatusSubmitted)
                {
                    continue;
                }

                if (item.Assignment.DueDate.Value >= DateTime.UtcNow)
                {
                    continue;
                }

                blockers.Add(new ParentBlockerDto
                {
                    Code = ParentBlockerCode.AssignmentOverdue,
                    Message = $"Bài tập '{item.Assignment.Title}' đã quá hạn.",
                    ModuleId = module.Id,
                    EnrollmentId = enrollment.Id,
                });
                overdueCount++;
                if (overdueCount >= 3)
                {
                    break;
                }
            }

            if (overdueCount >= 3)
            {
                break;
            }
        }

        return blockers
            .GroupBy(b => new { b.Code, b.ModuleId, b.EnrollmentId, b.Message })
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsNextLockedModule(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        foreach (var candidate in snapshot.Modules.OrderBy(m => m.ModuleOrder))
        {
            var unlocked = CurriculumStatusHelper.IsModuleUnlocked(
                candidate,
                context.LatestEnrollmentByModuleId,
                context.ModulesById);
            if (!unlocked)
            {
                return candidate.Id == module.Id;
            }

            if (!context.LatestEnrollmentByModuleId.TryGetValue(candidate.Id, out var me)
                || (me.Status != EnrollmentStatus.Completed && me.ProgressPercent < 100m))
            {
                return false;
            }
        }

        return false;
    }

    private static List<ParentProgressEventDto> BuildMilestonesForEnrollment(
        ProgramEnrollment enrollment,
        Program program,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        var events = new List<ParentProgressEventDto>();
        var programName = program.Name;

        if (enrollment.Status == EnrollmentStatus.Completed && enrollment.CompletedAt.HasValue)
        {
            events.Add(new ParentProgressEventDto
            {
                Id = $"enrollment-completed-{enrollment.Id}",
                OccurredAt = enrollment.CompletedAt.Value,
                Type = ParentProgressEventType.EnrollmentCompleted,
                Title = "Hoàn thành chương trình",
                Subtitle = programName,
                EnrollmentId = enrollment.Id,
            });
        }

        foreach (var (moduleId, moduleEnrollment) in context.LatestEnrollmentByModuleId)
        {
            if (!context.ModulesById.TryGetValue(moduleId, out var module))
            {
                continue;
            }

            if (moduleEnrollment.Status == EnrollmentStatus.Completed && moduleEnrollment.CompletedAt.HasValue)
            {
                events.Add(new ParentProgressEventDto
                {
                    Id = $"module-completed-{moduleEnrollment.Id}",
                    OccurredAt = moduleEnrollment.CompletedAt.Value,
                    Type = ParentProgressEventType.ModuleCompleted,
                    Title = $"Hoàn thành {module.Name}",
                    Subtitle = programName,
                    EnrollmentId = enrollment.Id,
                    ModuleId = moduleId,
                });
            }
            else if (moduleEnrollment.Status == EnrollmentStatus.Failed)
            {
                var occurredAt = moduleEnrollment.UpdatedAt ?? moduleEnrollment.CreatedAt;
                events.Add(new ParentProgressEventDto
                {
                    Id = $"module-failed-{moduleEnrollment.Id}",
                    OccurredAt = occurredAt,
                    Type = ParentProgressEventType.ModuleFailed,
                    Title = $"Chưa đạt {module.Name}",
                    Subtitle = programName,
                    EnrollmentId = enrollment.Id,
                    ModuleId = moduleId,
                });
            }
        }

        foreach (var (activityId, progress) in context.ProgressByActivityId)
        {
            if (progress.ActivityStatus != ActivityStatus.Done || !progress.CompletedAt.HasValue)
            {
                continue;
            }

            snapshot.ActivitiesById.TryGetValue(activityId, out var activity);
            snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId);
            events.Add(new ParentProgressEventDto
            {
                Id = $"activity-completed-{progress.Id}",
                OccurredAt = progress.CompletedAt.Value,
                Type = ParentProgressEventType.ActivityCompleted,
                Title = activity != null ? $"Hoàn thành {activity.Name}" : "Hoàn thành hoạt động",
                Subtitle = programName,
                EnrollmentId = enrollment.Id,
                ModuleId = moduleId == Guid.Empty ? null : moduleId,
            });
        }

        foreach (var (assignmentId, submissions) in context.SubmissionsByAssignmentId)
        {
            if (!snapshot.AssignmentsById.TryGetValue(assignmentId, out var assignment))
            {
                continue;
            }

            Guid? moduleId = assignment.ModuleId;
            var latestGraded = submissions
                .Where(s => s.Status == SubmissionStatus.Graded && s.AssignedGrade.HasValue)
                .OrderByDescending(s => s.GradedAt ?? s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefault();

            if (latestGraded != null)
            {
                var passed = latestGraded.AssignedGrade!.Value >= assignment.PassScore;
                events.Add(new ParentProgressEventDto
                {
                    Id = passed
                        ? $"asg-pass-{latestGraded.Id}"
                        : $"asg-fail-{latestGraded.Id}",
                    OccurredAt = latestGraded.GradedAt ?? latestGraded.UpdatedAt ?? latestGraded.CreatedAt,
                    Type = passed
                        ? ParentProgressEventType.AssignmentPassed
                        : ParentProgressEventType.AssignmentFailed,
                    Title = passed
                        ? $"Đạt bài kiểm tra: {assignment.Title}"
                        : $"Chưa đạt bài kiểm tra: {assignment.Title}",
                    Subtitle = programName,
                    EnrollmentId = enrollment.Id,
                    ModuleId = moduleId,
                });
                continue;
            }

            var latestSubmitted = submissions
                .Where(s => s.SubmittedAt.HasValue)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefault();
            if (latestSubmitted?.SubmittedAt != null)
            {
                events.Add(new ParentProgressEventDto
                {
                    Id = $"asg-submit-{latestSubmitted.Id}",
                    OccurredAt = latestSubmitted.SubmittedAt.Value,
                    Type = ParentProgressEventType.AssignmentSubmitted,
                    Title = $"Đã nộp: {assignment.Title}",
                    Subtitle = programName,
                    EnrollmentId = enrollment.Id,
                    ModuleId = moduleId,
                });
            }
        }

        return events;
    }

    private async Task<ProgressContext> BuildContextAsync(
        ProgramEnrollment enrollment,
        ProgramCurriculumTreeSnapshot snapshot)
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

        return new ProgressContext
        {
            LatestEnrollmentByModuleId = latestEnrollmentByModuleId,
            ModulesById = modulesById,
            ProgressByActivityId = progressByActivityId,
            CheckedInActivityIds = checkedInActivityIds,
            SubmissionsByAssignmentId = submissionsByAssignmentId,
            SubmissionsByMilestoneId = submissionsByMilestoneId,
        };
    }

    private static DateTime? ResolveLastAccessedAt(ProgressContext context)
    {
        var timestamps = context.ProgressByActivityId.Values
            .Select(p => p.LastAccessedAt ?? p.CompletedAt ?? p.UpdatedAt)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        return timestamps.Count == 0 ? null : timestamps.Max();
    }

    private static bool IsActivityCompleted(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
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

    private static bool IsActivityAccessible(
        Guid activityId,
        ProgramCurriculumTreeSnapshot snapshot,
        ProgressContext context)
    {
        if (!snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId)
            || !context.ModulesById.TryGetValue(moduleId, out var module))
        {
            return false;
        }

        if (!CurriculumStatusHelper.IsModuleUnlocked(
                module,
                context.LatestEnrollmentByModuleId,
                context.ModulesById))
        {
            return false;
        }

        return CurriculumStatusHelper.IsActivitySequentiallyAccessible(
            activityId,
            snapshot,
            id => IsActivityCompleted(id, snapshot, context));
    }

    private sealed class ProgressContext
    {
        public Dictionary<Guid, ModuleEnrollment> LatestEnrollmentByModuleId { get; init; } = new();

        public Dictionary<Guid, Module> ModulesById { get; init; } = new();

        public Dictionary<Guid, ActivityProgress> ProgressByActivityId { get; init; } = new();

        public HashSet<Guid> CheckedInActivityIds { get; init; } = [];

        public Dictionary<Guid, List<Submission>> SubmissionsByAssignmentId { get; init; } = new();

        public Dictionary<Guid, Submission> SubmissionsByMilestoneId { get; init; } = new();
    }
}
