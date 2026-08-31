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
        var toAdd = new List<Submission>();
        var upgraded = 0;
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
                if (StudentHasLeftoverFailHold(existingSubmissions, seat.StudentId, assignment, _seedNow))
                {
                    continue;
                }

                var existing = LatestStudentAssignmentSubmission(
                    existingSubmissions,
                    seat.StudentId,
                    assignment.Id);
                if (existing != null)
                {
                    ApplySeededAssessmentHold(existing, assignment, moduleFullyTaught, _seedNow);
                    await _unitOfWork.Submissions.Update(existing);
                    upgraded++;
                    continue;
                }

                var created = CreateSeededAssessmentHold(
                    assignment,
                    seat.StudentId,
                    moduleEnrollment.Id,
                    moduleFullyTaught,
                    _seedNow);
                toAdd.Add(created);
                existingSubmissions.Add(created);
            }
        }

        if (toAdd.Count == 0 && upgraded == 0)
        {
            _loggerService.LogInformation("No taught-module assessment safety-net rows needed.");
            return;
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.Submissions.AddRangeAsync(toAdd);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded {Added} taught-module assessment safety-net submission(s), upgraded {Upgraded}.",
            toAdd.Count,
            upgraded);
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

        var toAdd = new List<Submission>();
        var upgraded = 0;
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

                if (StudentHasLeftoverFailHold(existingSubmissions, seat.StudentId, assignment, now))
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

                var existing = LatestStudentAssignmentSubmission(
                    existingSubmissions,
                    seat.StudentId,
                    assignment.Id);
                if (existing != null)
                {
                    ApplySeededAssessmentHold(existing, assignment, moduleFullyTaught: true, now);
                    existing.SubmittedAt = window.EndTime;
                    existing.GradedAt = window.EndTime;
                    existing.StartedAt = window.StartTime;
                    existing.ContentText = "Seeded pass for elapsed class work window.";
                    await _unitOfWork.Submissions.Update(existing);
                    upgraded++;
                    continue;
                }

                var created = CreateSeededAssessmentHold(
                    assignment,
                    seat.StudentId,
                    moduleEnrollment.Id,
                    moduleFullyTaught: true,
                    window.EndTime);
                created.SubmittedAt = window.EndTime;
                created.StartedAt = window.StartTime;
                created.ContentText = "Seeded pass for elapsed class work window.";
                toAdd.Add(created);
                existingSubmissions.Add(created);
            }
        }

        if (toAdd.Count == 0 && upgraded == 0)
        {
            _loggerService.LogInformation("No elapsed-window pass submissions needed.");
            return;
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.Submissions.AddRangeAsync(toAdd);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Seeded {Added} passing submission(s) for elapsed required windows, upgraded {Upgraded}.",
            toAdd.Count,
            upgraded);
    }

    /// <summary>
    /// Reopens in-progress purchases the leftover-fail scan closed during this seed run
    /// (EndedAt at/after <see cref="_seedNow"/>). Intentional FailRebuy snapshots use a past EndedAt.
    /// </summary>
    private async Task RestoreInProgressPurchasesClosedDuringSeedAsync()
    {
        var closed = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Failed
                  && pe.EndReason == ProgramPurchaseEndReason.AcademicFail
                  && pe.EndedAt != null
                  && pe.EndedAt >= _seedNow);
        if (closed.Count == 0)
        {
            return;
        }

        var restored = 0;
        foreach (var enrollment in closed)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.EndReason = null;
            enrollment.EndedModuleId = null;
            enrollment.EndedAt = null;
            await _unitOfWork.ProgramEnrollments.Update(enrollment);

            var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.ProgramEnrollmentId == enrollment.Id && !me.IsDeleted);
            foreach (var moduleEnrollment in moduleEnrollments)
            {
                if (moduleEnrollment.Status is EnrollmentStatus.Failed or EnrollmentStatus.Dropped)
                {
                    moduleEnrollment.Status = EnrollmentStatus.Active;
                    await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
                }
            }

            var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
                ce => ce.ProgramEnrollmentId == enrollment.Id
                      && !ce.IsDeleted
                      && ce.Status == ClassEnrollmentStatus.Withdrawn);
            foreach (var seat in seats)
            {
                seat.Status = ClassEnrollmentStatus.Active;
                await _unitOfWork.ClassEnrollments.Update(seat);
            }

            restored++;
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogWarning(
            "Reopened {Count} in-progress purchase(s) closed by leftover-fail during seed.",
            restored);
    }

    private static bool StudentHasLeftoverFailHold(
        IReadOnlyCollection<Submission> submissions,
        Guid studentId,
        Assignment assignment,
        DateTime now)
        => submissions.Any(s =>
            s.StudentId == studentId
            && s.AssignmentId == assignment.Id
            && !s.IsDeleted
            && BlocksLeftoverAcademicFail(s, assignment, now));

    private static Submission? LatestStudentAssignmentSubmission(
        IReadOnlyCollection<Submission> submissions,
        Guid studentId,
        Guid assignmentId)
        => submissions
            .Where(s => s.StudentId == studentId && s.AssignmentId == assignmentId && !s.IsDeleted)
            .OrderByDescending(s => s.AttemptNumber)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefault();

    private static bool BlocksLeftoverAcademicFail(Submission submission, Assignment assignment, DateTime now)
    {
        if (submission.Status == SubmissionStatus.TurnedIn)
        {
            return true;
        }

        if (submission.Status == SubmissionStatus.Graded
            && submission.AssignedGrade.HasValue
            && submission.AssignedGrade.Value >= assignment.PassScore)
        {
            return true;
        }

        return AssignmentWindowPolicy.IsBlockingInProgress(submission, now);
    }

    private static void ApplySeededAssessmentHold(
        Submission submission,
        Assignment assignment,
        bool moduleFullyTaught,
        DateTime at)
    {
        submission.Status = moduleFullyTaught ? SubmissionStatus.Graded : SubmissionStatus.TurnedIn;
        submission.AssignedGrade = moduleFullyTaught ? Math.Max(assignment.PassScore, 80m) : null;
        submission.ContentText = moduleFullyTaught
            ? "Seeded pass after the class finished teaching this module."
            : "Seeded in-progress work while the class is still teaching this module.";
        submission.SubmittedAt = at;
        submission.GradedAt = moduleFullyTaught ? at : null;
        submission.StartedAt ??= at.AddDays(-2);
        submission.UpdatedAt = at;
        submission.ExpiresAt = null;
    }

    private static Submission CreateSeededAssessmentHold(
        Assignment assignment,
        Guid studentId,
        Guid moduleEnrollmentId,
        bool moduleFullyTaught,
        DateTime at)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = ResearchSubmissionValidator.GenerateSubmissionCode(),
            AssignmentId = assignment.Id,
            StudentId = studentId,
            ModuleEnrollmentId = moduleEnrollmentId,
            AttemptNumber = 1,
            Status = moduleFullyTaught ? SubmissionStatus.Graded : SubmissionStatus.TurnedIn,
            AssignedGrade = moduleFullyTaught ? Math.Max(assignment.PassScore, 80m) : null,
            ContentText = moduleFullyTaught
                ? "Seeded pass after the class finished teaching this module."
                : "Seeded in-progress work while the class is still teaching this module.",
            SubmittedAt = at,
            GradedAt = moduleFullyTaught ? at : null,
            StartedAt = at.AddDays(-2),
            CreatedAt = at,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
}
