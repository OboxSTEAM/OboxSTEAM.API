using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.ClassStudentProgressDTO;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

/// <summary>
/// Mentor-facing roster-complete student progress for a single activity or assignment in a class.
/// </summary>
public sealed class ClassStudentProgressService : IClassStudentProgressService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClaimsService _claimsService;
    private readonly ILogger<ClassStudentProgressService> _logger;

    public ClassStudentProgressService(
        IUnitOfWork unitOfWork,
        IClaimsService claimsService,
        ILogger<ClassStudentProgressService> logger)
    {
        _unitOfWork = unitOfWork;
        _claimsService = claimsService;
        _logger = logger;
    }

    public async Task<ClassActivityStudentProgressDto> GetActivityStudentProgressAsync(
        Guid classId,
        Guid activityId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var classEntity = await MentorScopeValidator.EnsureMentorOwnsClassAsync(
            _unitOfWork,
            mentorId,
            classId);

        var (activity, moduleId) = await LoadActivityInClassProgramAsync(activityId, classEntity.ProgramId);

        var roster = await LoadActiveRosterAsync(classId);
        var studentIds = roster.Select(r => r.StudentId).Distinct().ToList();
        var studentsById = await LoadStudentsByIdAsync(studentIds);

        var latestModuleEnrollmentIds = await ResolveLatestModuleEnrollmentIdsAsync(
            roster.Select(r => r.ProgramEnrollmentId).Distinct().ToList(),
            moduleId);

        var progressByStudentId = await LoadLatestActivityProgressByStudentAsync(
            activityId,
            latestModuleEnrollmentIds);

        ClassSession? primarySession = null;
        Dictionary<Guid, SessionAttendance> attendanceByStudentId = new();
        if (activity.ActivityType is ActivityType.LiveOnline or ActivityType.Offline)
        {
            primarySession = await LoadPrimarySessionAsync(classId, activityId);
            if (primarySession != null)
            {
                attendanceByStudentId = await LoadAttendanceByStudentAsync(primarySession.Id);
            }
        }

        var items = new List<ClassActivityStudentProgressItemDto>(roster.Count);
        var completed = 0;
        var inProgress = 0;
        var notStarted = 0;

        foreach (var seat in roster.OrderBy(r => studentsById.GetValueOrDefault(r.StudentId)?.FullName)
                     .ThenBy(r => studentsById.GetValueOrDefault(r.StudentId)?.Code))
        {
            if (!studentsById.TryGetValue(seat.StudentId, out var student))
            {
                continue;
            }

            progressByStudentId.TryGetValue(seat.StudentId, out var progress);
            var status = progress?.ActivityStatus ?? ActivityStatus.NotStart;
            switch (status)
            {
                case ActivityStatus.Done:
                    completed++;
                    break;
                case ActivityStatus.InProgress:
                    inProgress++;
                    break;
                default:
                    notStarted++;
                    break;
            }

            attendanceByStudentId.TryGetValue(seat.StudentId, out var attendance);

            items.Add(new ClassActivityStudentProgressItemDto
            {
                StudentId = student.Id,
                StudentCode = student.Code,
                StudentName = student.FullName,
                Email = student.Email,
                AvatarUrl = student.AvatarUrl,
                ActivityStatus = status,
                CompletedAt = progress?.CompletedAt,
                LastAccessedAt = progress?.LastAccessedAt,
                CompletionSource = progress?.CompletionSource,
                AttendanceStatus = primarySession == null
                    ? null
                    : attendance?.Status ?? AttendanceStatus.Expected,
                CheckedInAt = attendance?.CheckedInAt,
                ParticipationMinutes = attendance?.ParticipationMinutes,
            });
        }

        _logger.LogInformation(
            "[GetActivityStudentProgressAsync] Mentor {MentorId} viewed activity {ActivityId} progress for class {ClassId} ({StudentCount} students).",
            mentorId,
            activityId,
            classId,
            items.Count);

        return new ClassActivityStudentProgressDto
        {
            ClassId = classId,
            ActivityId = activityId,
            ActivityType = activity.ActivityType,
            ClassSessionId = primarySession?.Id,
            SessionStatus = primarySession?.Status,
            TotalStudents = items.Count,
            CompletedCount = completed,
            InProgressCount = inProgress,
            NotStartedCount = notStarted,
            Students = items,
        };
    }

    public async Task<ClassAssignmentStudentProgressDto> GetAssignmentStudentProgressAsync(
        Guid classId,
        Guid assignmentId)
    {
        var mentorId = await GetCurrentMentorIdAsync();
        var assignment = await _unitOfWork.Assignments.GetByIdAsync(assignmentId);
        if (assignment == null || assignment.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Assignment with id '{assignmentId}' not found.");
        }

        await MentorScopeValidator.EnsureMentorOwnsClassForModuleAsync(
            _unitOfWork,
            mentorId,
            classId,
            assignment.ModuleId);

        var roster = await LoadActiveRosterAsync(classId);
        var studentIds = roster.Select(r => r.StudentId).Distinct().ToList();
        var studentsById = await LoadStudentsByIdAsync(studentIds);

        var moduleEnrollmentIds = await SubmissionEnrollmentScope.GetModuleEnrollmentIdsForClassAsync(
            _unitOfWork,
            classId);

        var submissionsByStudentId = await LoadLatestSubmissionsByStudentAsync(
            assignmentId,
            studentIds,
            moduleEnrollmentIds);

        var items = new List<ClassAssignmentStudentProgressItemDto>(roster.Count);
        var submitted = 0;
        var graded = 0;
        var notStarted = 0;
        var gradedScores = new List<double>();

        foreach (var seat in roster.OrderBy(r => studentsById.GetValueOrDefault(r.StudentId)?.FullName)
                     .ThenBy(r => studentsById.GetValueOrDefault(r.StudentId)?.Code))
        {
            if (!studentsById.TryGetValue(seat.StudentId, out var student))
            {
                continue;
            }

            submissionsByStudentId.TryGetValue(seat.StudentId, out var submission);
            if (submission == null)
            {
                notStarted++;
                items.Add(new ClassAssignmentStudentProgressItemDto
                {
                    StudentId = student.Id,
                    StudentCode = student.Code,
                    StudentName = student.FullName,
                    Email = student.Email,
                    AvatarUrl = student.AvatarUrl,
                });
                continue;
            }

            var isHandedIn = submission.Status is SubmissionStatus.TurnedIn
                or SubmissionStatus.Graded
                or SubmissionStatus.ReturnedForRevision;
            if (isHandedIn)
            {
                submitted++;
            }
            else
            {
                notStarted++;
            }

            bool? passed = null;
            if (submission.Status == SubmissionStatus.Graded)
            {
                graded++;
                if (submission.AssignedGrade.HasValue)
                {
                    gradedScores.Add((double)submission.AssignedGrade.Value);
                    passed = submission.AssignedGrade.Value >= assignment.PassScore;
                }
            }

            items.Add(new ClassAssignmentStudentProgressItemDto
            {
                StudentId = student.Id,
                StudentCode = student.Code,
                StudentName = student.FullName,
                Email = student.Email,
                AvatarUrl = student.AvatarUrl,
                SubmissionId = submission.Id,
                AttemptNumber = submission.AttemptNumber,
                SubmissionStatus = submission.Status,
                AssignedGrade = submission.AssignedGrade,
                Passed = passed,
                SubmittedAt = submission.SubmittedAt,
                GradedAt = submission.GradedAt,
            });
        }

        var status = ClassCurriculumNavStatusHelper.ResolveAssignmentStatus(
            items.Count,
            submitted,
            graded);

        _logger.LogInformation(
            "[GetAssignmentStudentProgressAsync] Mentor {MentorId} viewed assignment {AssignmentId} progress for class {ClassId} ({StudentCount} students).",
            mentorId,
            assignmentId,
            classId,
            items.Count);

        return new ClassAssignmentStudentProgressDto
        {
            ClassId = classId,
            AssignmentId = assignmentId,
            AssignmentType = assignment.AssignmentType,
            Status = status,
            TotalStudents = items.Count,
            SubmittedCount = submitted,
            GradedCount = graded,
            NotStartedCount = notStarted,
            AverageScore = gradedScores.Count > 0 ? gradedScores.Average() : null,
            Students = items,
        };
    }

    private async Task<(Activity Activity, Guid ModuleId)> LoadActivityInClassProgramAsync(
        Guid activityId,
        Guid programId)
    {
        var activity = await _unitOfWork.Activities.GetByIdAsync(activityId);
        if (activity == null || activity.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Activity with id '{activityId}' not found.");
        }

        var course = await _unitOfWork.Courses.GetByIdAsync(activity.CourseId);
        if (course == null || course.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Course with id '{activity.CourseId}' not found.");
        }

        var module = await _unitOfWork.Modules.GetByIdAsync(course.ModuleId);
        if (module == null || module.IsDeleted)
        {
            throw ErrorHelper.NotFound($"Module with id '{course.ModuleId}' not found.");
        }

        if (module.ProgramId != programId)
        {
            throw ErrorHelper.BadRequest(MentorScopeValidator.ClassProgramMismatchMessage);
        }

        return (activity, module.Id);
    }

    private async Task<List<ClassEnrollment>> LoadActiveRosterAsync(Guid classId)
    {
        return await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.ClassId == classId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);
    }

    private async Task<Dictionary<Guid, User>> LoadStudentsByIdAsync(List<Guid> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return new Dictionary<Guid, User>();
        }

        var students = await _unitOfWork.Users.GetAllAsync(
            u => studentIds.Contains(u.Id) && !u.IsDeleted);
        return students.ToDictionary(u => u.Id);
    }

    private async Task<List<Guid>> ResolveLatestModuleEnrollmentIdsAsync(
        List<Guid> programEnrollmentIds,
        Guid moduleId)
    {
        if (programEnrollmentIds.Count == 0)
        {
            return [];
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ModuleId == moduleId
                  && me.ProgramEnrollmentId.HasValue
                  && programEnrollmentIds.Contains(me.ProgramEnrollmentId.Value)
                  && !me.IsDeleted);

        return moduleEnrollments
            .GroupBy(me => me.StudentId)
            .Select(g => g.OrderByDescending(me => me.AttemptNumber).First().Id)
            .ToList();
    }

    private async Task<Dictionary<Guid, ActivityProgress>> LoadLatestActivityProgressByStudentAsync(
        Guid activityId,
        List<Guid> latestModuleEnrollmentIds)
    {
        if (latestModuleEnrollmentIds.Count == 0)
        {
            return new Dictionary<Guid, ActivityProgress>();
        }

        var progresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => ap.ActivityId == activityId
                  && latestModuleEnrollmentIds.Contains(ap.ModuleEnrollmentId)
                  && !ap.IsDeleted);

        return progresses
            .GroupBy(ap => ap.StudentId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<ClassSession?> LoadPrimarySessionAsync(Guid classId, Guid activityId)
    {
        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == classId
                  && cs.ActivityId == activityId
                  && !cs.IsDeleted);

        return ClassCurriculumNavStatusHelper.SelectPrimarySession(sessions);
    }

    private async Task<Dictionary<Guid, SessionAttendance>> LoadAttendanceByStudentAsync(Guid classSessionId)
    {
        var attendances = await _unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ClassSessionId == classSessionId && !sa.IsDeleted);

        return attendances
            .GroupBy(sa => sa.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(sa => sa.UpdatedAt ?? sa.CreatedAt).First());
    }

    private async Task<Dictionary<Guid, Submission>> LoadLatestSubmissionsByStudentAsync(
        Guid assignmentId,
        List<Guid> studentIds,
        List<Guid> moduleEnrollmentIds)
    {
        if (studentIds.Count == 0 || moduleEnrollmentIds.Count == 0)
        {
            return new Dictionary<Guid, Submission>();
        }

        var submissions = await _unitOfWork.Submissions.GetAllAsync(
            s => s.AssignmentId == assignmentId
                 && studentIds.Contains(s.StudentId)
                 && s.ModuleEnrollmentId.HasValue
                 && moduleEnrollmentIds.Contains(s.ModuleEnrollmentId.Value)
                 && !s.IsDeleted);

        return submissions
            .GroupBy(s => s.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.AttemptNumber)
                    .ThenByDescending(s => s.SubmittedAt ?? s.UpdatedAt ?? s.CreatedAt)
                    .First());
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
            throw ErrorHelper.Forbidden("Only mentors can view class student progress.");
        }

        return userId;
    }
}
