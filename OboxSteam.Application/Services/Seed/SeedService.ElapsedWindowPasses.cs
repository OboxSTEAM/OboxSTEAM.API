using Microsoft.Extensions.Logging;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Write assessment rows before AssignmentWindows exist so the close scan cannot
    /// AcademicFail an in-progress cohort mid-seed. Fully taught modules get a passing
    /// grade; modules the class has only started get a TurnedIn draft (waiting on mentor).
    /// </summary>
    private async Task SeedTaughtModuleAssessmentSafetyNetAsync()
    {
        _loggerService.LogInformation("Seeding taught-module assessment safety net");

        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => !ce.IsDeleted && ce.Status == ClassEnrollmentStatus.Active);
        if (seats.Count == 0)
        {
            return;
        }

        var demoProgramIds = await GetDemoProgramIdsAsync();
        var classIds = seats.Select(s => s.ClassId).Distinct().ToList();
        var classById = (await _unitOfWork.Classes.GetAllAsync(
                c => classIds.Contains(c.Id) && !c.IsDeleted && c.Status == ClassStatus.InProgress))
            .ToDictionary(c => c.Id);
        if (classById.Count == 0)
        {
            return;
        }

        var teachingSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId)
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && (cs.SessionKind == SessionKind.LiveOnline || cs.SessionKind == SessionKind.Offline));

        var taughtByClass = teachingSessions
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Where(cs => cs.Status == ClassSessionStatus.Completed || cs.EndTime <= _seedNow)
                    .Select(cs => cs.ActivityId!.Value)
                    .ToHashSet());

        var programIds = classById.Values.Select(c => c.ProgramId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => programIds.Contains(m.ProgramId) && !m.IsDeleted);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
        var courseIds = courses.Select(c => c.Id).ToHashSet();
        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId) && !a.IsDeleted);
        var assignments = await _unitOfWork.Assignments.GetAllAsync(
            a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted && a.IsRequiredForModulePass);

        var livesByModule = activities
            .Where(a => a.ActivityType is ActivityType.LiveOnline or ActivityType.Offline)
            .GroupBy(a => courses.First(c => c.Id == a.CourseId).ModuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var peIds = seats.Select(s => s.ProgramEnrollmentId).Distinct().ToList();
        var openPurchases = (await _unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => peIds.Contains(pe.Id)
                      && !pe.IsDeleted
                      && pe.Status == EnrollmentStatus.Active))
            .ToDictionary(pe => pe.Id);

        var studentIds = seats.Select(s => s.StudentId).Distinct().ToList();
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => studentIds.Contains(me.StudentId) && !me.IsDeleted);
        var existingSubmissions = await _unitOfWork.Submissions.GetAllAsync(
            s => studentIds.Contains(s.StudentId) && !s.IsDeleted);
        var submittedKeys = existingSubmissions
            .Select(s => (s.StudentId, s.AssignmentId))
            .ToHashSet();

        var toAdd = new List<Submission>();
        foreach (var seat in seats)
        {
            if (!classById.TryGetValue(seat.ClassId, out var classEntity)
                || demoProgramIds.Contains(classEntity.ProgramId)
                || !openPurchases.ContainsKey(seat.ProgramEnrollmentId))
            {
                continue;
            }

            if (!taughtByClass.TryGetValue(seat.ClassId, out var taughtIds) || taughtIds.Count == 0)
            {
                continue;
            }

            foreach (var assignment in assignments.Where(a =>
                         modules.Any(m => m.Id == a.ModuleId && m.ProgramId == classEntity.ProgramId)))
            {
                if (submittedKeys.Contains((seat.StudentId, assignment.Id)))
                {
                    continue;
                }

                if (!livesByModule.TryGetValue(assignment.ModuleId, out var lives) || lives.Count == 0)
                {
                    continue;
                }

                var taughtCount = lives.Count(a => taughtIds.Contains(a.Id));
                if (taughtCount == 0)
                {
                    continue;
                }

                var moduleEnrollment = moduleEnrollments.FirstOrDefault(
                    me => me.StudentId == seat.StudentId
                          && me.ModuleId == assignment.ModuleId
                          && me.ProgramEnrollmentId == seat.ProgramEnrollmentId);
                if (moduleEnrollment == null)
                {
                    continue;
                }

                var moduleFullyTaught = taughtCount == lives.Count;
                toAdd.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                    AssignmentId = assignment.Id,
                    StudentId = seat.StudentId,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    AttemptNumber = 1,
                    Status = moduleFullyTaught ? SubmissionStatus.Graded : SubmissionStatus.TurnedIn,
                    AssignedGrade = moduleFullyTaught ? Math.Max(assignment.PassScore, 80m) : null,
                    ContentText = moduleFullyTaught
                        ? "Seeded pass after the class finished teaching this module."
                        : "Seeded in-progress work while the class is still teaching this module.",
                    SubmittedAt = _seedNow,
                    GradedAt = moduleFullyTaught ? _seedNow : null,
                    StartedAt = _seedNow.AddDays(-2),
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                submittedKeys.Add((seat.StudentId, assignment.Id));
            }
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("No taught-module assessment safety-net rows needed.");
            return;
        }

        await _unitOfWork.Submissions.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded {Count} taught-module assessment safety-net submission(s).",
            toAdd.Count);
    }

    /// <summary>
    /// Active demo students who already sat through related teaching should not be
    /// AcademicFailed by the elapsed-window scan. Seed a passing grade when the
    /// class window has ended and they have no submission yet.
    /// </summary>
    private async Task SeedPassedSubmissionsForElapsedRequiredWindowsAsync()
    {
        _loggerService.LogInformation("Seeding passing submissions for elapsed required AssignmentWindows");

        var now = _seedNow;
        var windows = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.SessionKind == SessionKind.AssignmentWindow
                  && cs.AssignmentId != null
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted
                  && cs.EndTime < now);
        if (windows.Count == 0)
        {
            return;
        }

        var assignmentIds = windows.Select(w => w.AssignmentId!.Value).Distinct().ToList();
        var assignments = (await _unitOfWork.Assignments.GetAllAsync(
                a => assignmentIds.Contains(a.Id) && !a.IsDeleted && a.IsRequiredForModulePass))
            .ToDictionary(a => a.Id);
        if (assignments.Count == 0)
        {
            return;
        }

        var demoProgramIds = await GetDemoProgramIdsAsync();
        var classIds = windows.Select(w => w.ClassId).Distinct().ToList();
        var classById = (await _unitOfWork.Classes.GetAllAsync(
                c => classIds.Contains(c.Id) && !c.IsDeleted))
            .ToDictionary(c => c.Id);
        windows = windows
            .Where(w => classById.TryGetValue(w.ClassId, out var cls)
                        && !demoProgramIds.Contains(cls.ProgramId))
            .ToList();
        if (windows.Count == 0)
        {
            return;
        }

        classIds = windows.Select(w => w.ClassId).Distinct().ToList();
        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => classIds.Contains(ce.ClassId)
                  && !ce.IsDeleted
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Completed));
        if (seats.Count == 0)
        {
            return;
        }

        var peIds = seats.Select(s => s.ProgramEnrollmentId).Distinct().ToList();
        var openPurchases = (await _unitOfWork.ProgramEnrollments.GetAllAsync(
                pe => peIds.Contains(pe.Id)
                      && !pe.IsDeleted
                      && (pe.Status == EnrollmentStatus.Active
                          || pe.Status == EnrollmentStatus.Completed)))
            .ToDictionary(pe => pe.Id);

        var studentIds = seats.Select(s => s.StudentId).Distinct().ToList();
        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => studentIds.Contains(me.StudentId) && !me.IsDeleted);
        var existingSubmissions = await _unitOfWork.Submissions.GetAllAsync(
            s => studentIds.Contains(s.StudentId)
                 && assignmentIds.Contains(s.AssignmentId)
                 && !s.IsDeleted);
        var submittedKeys = existingSubmissions
            .Select(s => (s.StudentId, s.AssignmentId))
            .ToHashSet();

        var toAdd = new List<Submission>();
        foreach (var window in windows)
        {
            if (!assignments.TryGetValue(window.AssignmentId!.Value, out var assignment))
            {
                continue;
            }

            var classSeats = seats.Where(s => s.ClassId == window.ClassId);
            foreach (var seat in classSeats)
            {
                if (!openPurchases.ContainsKey(seat.ProgramEnrollmentId))
                {
                    continue;
                }

                if (submittedKeys.Contains((seat.StudentId, assignment.Id)))
                {
                    continue;
                }

                var moduleEnrollment = moduleEnrollments.FirstOrDefault(
                    me => me.StudentId == seat.StudentId
                          && me.ModuleId == assignment.ModuleId
                          && me.ProgramEnrollmentId == seat.ProgramEnrollmentId);
                if (moduleEnrollment == null)
                {
                    continue;
                }

                toAdd.Add(new Submission
                {
                    Id = Guid.NewGuid(),
                    Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
                    AssignmentId = assignment.Id,
                    StudentId = seat.StudentId,
                    ModuleEnrollmentId = moduleEnrollment.Id,
                    AttemptNumber = 1,
                    Status = SubmissionStatus.Graded,
                    AssignedGrade = Math.Max(assignment.PassScore, 80m),
                    ContentText = "Seeded pass for elapsed class work window.",
                    SubmittedAt = window.EndTime,
                    GradedAt = window.EndTime,
                    StartedAt = window.StartTime,
                    CreatedAt = window.EndTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                submittedKeys.Add((seat.StudentId, assignment.Id));
            }
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("No elapsed-window pass submissions needed.");
            return;
        }

        await _unitOfWork.Submissions.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded {Count} passing submission(s) for elapsed required windows.",
            toAdd.Count);
    }
}
