using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassCurriculumProgressDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Mentor-facing class rollup of activity progress and assignment submission/grading counts.
/// Aggregates over active class enrollments only; no per-student PII is returned.
/// Also exposes class-scoped nav statuses for the mentor curriculum tree.
/// </summary>
public sealed class ClassCurriculumProgressService : IClassCurriculumProgressService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassCurriculumProgressService> _logger;

    public ClassCurriculumProgressService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassCurriculumProgressService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<ClassCurriculumProgressDto> GetCurriculumProgressAsync(Guid classId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var classEntity = await MentorScopeValidator.EnsureMentorOwnsClassAsync(
            _unitOfWork,
            mentorId,
            classId);

        var activeEnrollments = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var studentIds = activeEnrollments
            .Select(ce => ce.StudentId)
            .Distinct()
            .ToList();
        var programEnrollmentIds = activeEnrollments
            .Select(ce => ce.ProgramEnrollmentId)
            .Distinct()
            .ToList();

        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(_unitOfWork, classEntity.ProgramId);

        var latestModuleEnrollmentIds = await ResolveLatestModuleEnrollmentIdsAsync(programEnrollmentIds);
        var classModuleEnrollmentIds = await SubmissionEnrollmentScope.GetModuleEnrollmentIdsForClassAsync(
            _unitOfWork,
            classId);
        var activityCountsById = AggregateActivityCounts(latestModuleEnrollmentIds);
        var assignmentCountsById = AggregateAssignmentCounts(
            studentIds,
            classModuleEnrollmentIds,
            snapshot.AssignmentsById.Keys);

        var sessionsByActivityId = await LoadPrimarySessionsByActivityIdAsync(classId, snapshot);
        var orderedNavInputs = BuildOrderedNavInputs(snapshot, activityCountsById, sessionsByActivityId);
        var (navByActivityId, currentActivityId) = ClassCurriculumNavStatusHelper.ResolveActivityStatuses(
            orderedNavInputs,
            studentIds.Count);

        var modules = snapshot.Modules
            .Select(module => MapModule(
                module,
                snapshot,
                activityCountsById,
                assignmentCountsById,
                navByActivityId,
                studentIds.Count))
            .ToList();

        _logger.LogInformation(
            "[GetCurriculumProgressAsync] Mentor {MentorId} viewed curriculum progress for class {ClassId} ({StudentCount} active students, {ModuleCount} modules).",
            mentorId,
            classId,
            studentIds.Count,
            modules.Count);

        return new ClassCurriculumProgressDto
        {
            ClassId = classId,
            TotalStudents = studentIds.Count,
            CurrentActivityId = currentActivityId,
            Modules = modules,
        };
    }

    private async Task<List<Guid>> ResolveLatestModuleEnrollmentIdsAsync(List<Guid> programEnrollmentIds)
    {
        if (programEnrollmentIds.Count == 0)
        {
            return [];
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId.HasValue
                  && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value)
                  && !me.IsDeleted);

        return moduleEnrollments
            .GroupBy(me => new { me.StudentId, me.ModuleId })
            .Select(g => g.OrderByDescending(me => me.AttemptNumber).First().Id)
            .ToList();
    }

    private Dictionary<Guid, (int Completed, int InProgress)> AggregateActivityCounts(
        List<Guid> latestModuleEnrollmentIds)
    {
        if (latestModuleEnrollmentIds.Count == 0)
        {
            return new Dictionary<Guid, (int, int)>();
        }

        return _unitOfWork.ActivityProgresses
            .GetQueryable()
            .Where(ap => latestModuleEnrollmentIds.Contains(ap.ModuleEnrollmentId) && !ap.IsDeleted)
            .GroupBy(ap => ap.ActivityId)
            .Select(g => new
            {
                ActivityId = g.Key,
                Completed = g.Count(ap => ap.ActivityStatus == ActivityStatus.Done),
                InProgress = g.Count(ap => ap.ActivityStatus == ActivityStatus.InProgress),
            })
            .ToList()
            .ToDictionary(x => x.ActivityId, x => (x.Completed, x.InProgress));
    }

    private Dictionary<Guid, (int Submitted, int Graded, double? AverageScore)> AggregateAssignmentCounts(
        List<Guid> studentIds,
        List<Guid> moduleEnrollmentIds,
        IEnumerable<Guid> assignmentIds)
    {
        var assignmentIdList = assignmentIds.ToList();
        if (studentIds.Count == 0 || assignmentIdList.Count == 0 || moduleEnrollmentIds.Count == 0)
        {
            return new Dictionary<Guid, (int, int, double?)>();
        }

        var submissions = _unitOfWork.Submissions
            .GetQueryable()
            .Where(s => studentIds.Contains(s.StudentId)
                        && assignmentIdList.Contains(s.AssignmentId)
                        && s.ModuleEnrollmentId.HasValue
                        && moduleEnrollmentIds.Contains(s.ModuleEnrollmentId.Value)
                        && !s.IsDeleted
                        && (s.Status == SubmissionStatus.TurnedIn
                            || s.Status == SubmissionStatus.Graded
                            || s.Status == SubmissionStatus.ReturnedForRevision))
            .Select(s => new
            {
                s.AssignmentId,
                s.StudentId,
                s.Status,
                s.AssignedGrade,
            })
            .ToList();

        return submissions
            .GroupBy(s => s.AssignmentId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var submitted = g.Select(x => x.StudentId).Distinct().Count();
                    var gradedRows = g
                        .Where(x => x.Status == SubmissionStatus.Graded)
                        .GroupBy(x => x.StudentId)
                        .Select(sg => sg.OrderByDescending(x => x.AssignedGrade ?? 0m).First())
                        .ToList();
                    var graded = gradedRows.Count;
                    double? average = null;
                    if (gradedRows.Count > 0)
                    {
                        var grades = gradedRows
                            .Where(x => x.AssignedGrade.HasValue)
                            .Select(x => (double)x.AssignedGrade!.Value)
                            .ToList();
                        if (grades.Count > 0)
                        {
                            average = grades.Average();
                        }
                    }

                    return (submitted, graded, average);
                });
    }

    private async Task<Dictionary<Guid, ClassSession>> LoadPrimarySessionsByActivityIdAsync(
        Guid classId,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        var liveActivityIds = snapshot.ActivitiesById
            .Where(kvp => kvp.Value.ActivityType is ActivityType.LiveOnline or ActivityType.Offline)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        if (liveActivityIds.Count == 0)
        {
            return new Dictionary<Guid, ClassSession>();
        }

        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.ActivityId.HasValue
                  && liveActivityIds.Contains(cs.ActivityId.Value)
                  && !cs.IsDeleted);

        return sessions
            .GroupBy(cs => cs.ActivityId!.Value)
            .Select(g => (
                ActivityId: g.Key,
                Session: ClassCurriculumNavStatusHelper.SelectPrimarySession(g)))
            .Where(x => x.Session != null)
            .ToDictionary(x => x.ActivityId, x => x.Session!);
    }

    private static List<ClassCurriculumNavStatusHelper.ActivityNavInput> BuildOrderedNavInputs(
        ProgramCurriculumTreeSnapshot snapshot,
        Dictionary<Guid, (int Completed, int InProgress)> activityCountsById,
        Dictionary<Guid, ClassSession> sessionsByActivityId)
    {
        var result = new List<ClassCurriculumNavStatusHelper.ActivityNavInput>(
            snapshot.GlobalActivityOrder.Count);

        foreach (var activityId in snapshot.GlobalActivityOrder)
        {
            if (!snapshot.ActivitiesById.TryGetValue(activityId, out var activity))
            {
                continue;
            }

            activityCountsById.TryGetValue(activityId, out var counts);
            sessionsByActivityId.TryGetValue(activityId, out var session);

            result.Add(new ClassCurriculumNavStatusHelper.ActivityNavInput(
                activityId,
                activity.ActivityType,
                counts.Completed,
                session));
        }

        return result;
    }

    private static ClassCurriculumModuleProgressDto MapModule(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot,
        Dictionary<Guid, (int Completed, int InProgress)> activityCountsById,
        Dictionary<Guid, (int Submitted, int Graded, double? AverageScore)> assignmentCountsById,
        IReadOnlyDictionary<Guid, ClassCurriculumNavStatusHelper.ActivityNavResult> navByActivityId,
        int totalStudents)
    {
        var activityIds = snapshot.ActivityModuleMap
            .Where(kvp => kvp.Value == module.Id)
            .Select(kvp => kvp.Key)
            .ToList();

        var activities = activityIds
            .Select(activityId =>
            {
                activityCountsById.TryGetValue(activityId, out var counts);
                navByActivityId.TryGetValue(activityId, out var nav);

                return new ClassCurriculumActivityProgressDto
                {
                    ActivityId = activityId,
                    Status = nav?.Status ?? CurriculumStatusHelper.StatusAvailable,
                    ClassSessionId = nav?.ClassSessionId,
                    SessionStatus = nav?.SessionStatus,
                    CompletedCount = counts.Completed,
                    InProgressCount = counts.InProgress,
                };
            })
            .ToList();

        var assignments = CollectModuleAssignments(module, snapshot)
            .Select(assignment =>
            {
                assignmentCountsById.TryGetValue(assignment.Id, out var counts);
                return new ClassCurriculumAssignmentProgressDto
                {
                    AssignmentId = assignment.Id,
                    Status = ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(
                        totalStudents,
                        counts.Submitted,
                        counts.Graded),
                    SubmittedCount = counts.Submitted,
                    GradedCount = counts.Graded,
                    AverageScore = counts.AverageScore,
                };
            })
            .ToList();

        return new ClassCurriculumModuleProgressDto
        {
            ModuleId = module.Id,
            Activities = activities,
            Assignments = assignments,
        };
    }

    private static List<Assignment> CollectModuleAssignments(
        Module module,
        ProgramCurriculumTreeSnapshot snapshot)
    {
        var result = new List<Assignment>();

        if (module.ModuleType == ModuleType.Research
            && snapshot.MilestonesByModuleId.TryGetValue(module.Id, out var milestones))
        {
            foreach (var milestone in milestones)
            {
                if (snapshot.AssignmentsById.TryGetValue(milestone.AssignmentId, out var assignment))
                {
                    result.Add(assignment);
                }
            }

            return result;
        }

        if (snapshot.ModuleScopedAssignmentsByModuleId.TryGetValue(module.Id, out var moduleAssignments))
        {
            result.AddRange(moduleAssignments);
        }

        if (snapshot.CoursesByModuleId.TryGetValue(module.Id, out var courses))
        {
            foreach (var course in courses)
            {
                if (snapshot.AssignmentsByCourseId.TryGetValue(course.Id, out var courseAssignments))
                {
                    result.AddRange(courseAssignments);
                }
            }
        }

        return result;
    }

    private async Task<Guid> GetCurrentMentorIdAsync()
    {
        var userId = _claimsService.GetCurrentUserId;
        if (userId == Guid.Empty)
        {
            throw ErrorHelper.Unauthorized("Unauthorized access.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            throw ErrorHelper.NotFound("Current user not found.");
        }

        if (user.Role != RoleType.Mentor)
        {
            throw ErrorHelper.Forbidden("Only mentors can view class curriculum progress.");
        }

        return userId;
    }
}
