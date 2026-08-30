using Microsoft.Extensions.Logging;
using OboxSteam.Application.Validation;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
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
