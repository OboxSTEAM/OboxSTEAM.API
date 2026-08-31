using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// After live-session progress and elapsed-window passes exist, mark SelfPaced work
    /// done for courses/modules the student's class has already taught, then recalc
    /// module and program percent so roster progress matches the timetable.
    /// </summary>
    private async Task AlignInProgressCurriculumToClassTimetableAsync()
    {
        _loggerService.LogInformation("Aligning in-progress curriculum to class timetables");

        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => !ce.IsDeleted && ce.Status == ClassEnrollmentStatus.Active);
        if (seats.Count == 0)
        {
            return;
        }

        var demoProgramIds = await GetDemoProgramIdsAsync();
        var classIds = seats.Select(s => s.ClassId).Distinct().ToList();
        var classes = (await _unitOfWork.Classes.GetAllAsync(
                c => classIds.Contains(c.Id) && !c.IsDeleted && c.Status == ClassStatus.InProgress))
            .ToDictionary(c => c.Id);
        if (classes.Count == 0)
        {
            return;
        }

        var teachingSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId)
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && (cs.SessionKind == SessionKind.LiveOnline || cs.SessionKind == SessionKind.Offline));

        var taughtActivityIdsByClass = teachingSessions
            .GroupBy(cs => cs.ClassId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Where(cs => cs.Status == ClassSessionStatus.Completed
                                 || cs.EndTime <= _seedNow)
                    .Select(cs => cs.ActivityId!.Value)
                    .ToHashSet());

        var aligned = 0;
        foreach (var seat in seats)
        {
            if (!classes.TryGetValue(seat.ClassId, out var classEntity)
                || demoProgramIds.Contains(classEntity.ProgramId))
            {
                continue;
            }

            if (!taughtActivityIdsByClass.TryGetValue(seat.ClassId, out var taughtActivityIds)
                || taughtActivityIds.Count == 0)
            {
                continue;
            }

            var pe = await _unitOfWork.ProgramEnrollments.GetByIdAsync(seat.ProgramEnrollmentId);
            if (pe == null
                || pe.IsDeleted
                || pe.Status != EnrollmentStatus.Active)
            {
                continue;
            }

            var marked = await MarkTaughtSelfPacedAsync(pe, taughtActivityIds);
            if (marked)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.ProgramEnrollmentId == pe.Id && !me.IsDeleted);
            ModuleEnrollment? last = null;
            foreach (var me in moduleEnrollments)
            {
                last = me;
                await ActivityProgressCalculationHelper.RecalculateModuleProgressAsync(_unitOfWork, me);
            }

            if (last != null)
            {
                await ActivityProgressCalculationHelper.RecalculateProgramProgressAsync(_unitOfWork, pe.Id, last);
            }

            aligned++;
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Aligned in-progress curriculum for {Count} class enrollment(s).",
            aligned);
    }

    private async Task<bool> MarkTaughtSelfPacedAsync(
        ProgramEnrollment pe,
        HashSet<Guid> taughtLiveActivityIds)
    {
        var modules = (await _unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == pe.ProgramId && !m.IsDeleted))
            .OrderBy(m => m.ModuleOrder)
            .ToList();
        if (modules.Count == 0)
        {
            return false;
        }

        var moduleIds = modules.Select(m => m.Id).ToList();
        var courses = await _unitOfWork.Courses.GetAllAsync(
            c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);
        var courseIds = courses.Select(c => c.Id).ToHashSet();
        var activities = await _unitOfWork.Activities.GetAllAsync(
            a => courseIds.Contains(a.CourseId) && !a.IsDeleted);

        var moduleEnrollments = (await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.ProgramEnrollmentId == pe.Id && !me.IsDeleted))
            .ToDictionary(me => me.ModuleId);

        var marked = false;
        foreach (var module in modules)
        {
            if (!moduleEnrollments.TryGetValue(module.Id, out var moduleEnrollment)
                || moduleEnrollment.Status is EnrollmentStatus.Failed or EnrollmentStatus.Dropped)
            {
                continue;
            }

            var moduleCourses = courses
                .Where(c => c.ModuleId == module.Id)
                .OrderBy(c => c.CourseOrder)
                .ToList();

            foreach (var course in moduleCourses)
            {
                var courseActivities = activities.Where(a => a.CourseId == course.Id).ToList();
                var lives = courseActivities
                    .Where(a => a.ActivityType is ActivityType.LiveOnline or ActivityType.Offline)
                    .ToList();
                if (lives.Count == 0 || lives.Any(a => !taughtLiveActivityIds.Contains(a.Id)))
                {
                    continue;
                }

                foreach (var taught in courseActivities.Where(a =>
                             a.ActivityType == ActivityType.SelfPaced
                             || taughtLiveActivityIds.Contains(a.Id)))
                {
                    if (await EnsureSeedActivityDoneAsync(moduleEnrollment, taught.Id, _seedNow))
                    {
                        marked = true;
                    }
                }
            }
        }

        return marked;
    }

    private async Task<bool> EnsureSeedActivityDoneAsync(
        ModuleEnrollment moduleEnrollment,
        Guid activityId,
        DateTime completedAt)
    {
        var existing = await _unitOfWork.ActivityProgresses.FirstOrDefaultAsync(
            ap => ap.ModuleEnrollmentId == moduleEnrollment.Id
                  && ap.ActivityId == activityId
                  && !ap.IsDeleted);
        if (existing != null)
        {
            if (existing.IsCompleted && existing.ActivityStatus == ActivityStatus.Done)
            {
                return false;
            }

            existing.ActivityStatus = ActivityStatus.Done;
            existing.IsCompleted = true;
            existing.CompletedAt ??= completedAt;
            existing.CompletionSource = CompletionSource.Manual;
            existing.UpdatedAt = completedAt;
            await _unitOfWork.ActivityProgresses.Update(existing);
            return true;
        }

        await _unitOfWork.ActivityProgresses.AddAsync(new ActivityProgress
        {
            Id = Guid.NewGuid(),
            StudentId = moduleEnrollment.StudentId,
            ActivityId = activityId,
            ModuleEnrollmentId = moduleEnrollment.Id,
            ActivityStatus = ActivityStatus.Done,
            IsCompleted = true,
            CompletionSource = CompletionSource.Manual,
            CompletedAt = completedAt,
            LastAccessedAt = completedAt,
            CreatedAt = completedAt,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
        return true;
    }

    private async Task<bool> StudentClassHasStartedModuleAsync(Guid studentId, Guid moduleId)
    {
        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && !ce.IsDeleted
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Completed));
        if (seats.Count == 0)
        {
            return false;
        }

        var classIds = seats.Select(s => s.ClassId).Distinct().ToList();
        var sessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId)
                  && cs.ModuleId == moduleId
                  && !cs.IsDeleted
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.ActivityId != null
                  && (cs.SessionKind == SessionKind.LiveOnline || cs.SessionKind == SessionKind.Offline)
                  && (cs.Status == ClassSessionStatus.Completed
                      || cs.Status == ClassSessionStatus.InProgress
                      || cs.EndTime <= _seedNow));

        return sessions.Count > 0;
    }
}
