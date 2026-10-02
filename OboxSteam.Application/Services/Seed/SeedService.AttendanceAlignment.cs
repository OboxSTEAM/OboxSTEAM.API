using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Every seeded LiveOnline / Offline activity that is Done on a Completed session must carry
    /// attended roster data, matching the mentor-complete rule (Present / Late / Excused).
    /// Missing, Expected, and Absent rows become Present; Late and Excused stay as-is.
    /// Runs after every fixture that writes ActivityProgress Done.
    /// </summary>
    private async Task AlignSeedAttendanceWithDoneSessionActivitiesAsync()
    {
        var sessionActivities = await _unitOfWork.Activities.GetAllAsync(
            a => !a.IsDeleted
                 && (a.ActivityType == ActivityType.LiveOnline || a.ActivityType == ActivityType.Offline));
        if (sessionActivities.Count == 0)
        {
            return;
        }

        var sessionActivityIds = sessionActivities.Select(a => a.Id).ToHashSet();
        var doneProgresses = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => !ap.IsDeleted
                  && sessionActivityIds.Contains(ap.ActivityId)
                  && (ap.ActivityStatus == ActivityStatus.Done || ap.IsCompleted));
        if (doneProgresses.Count == 0)
        {
            return;
        }

        var doneActivityIds = doneProgresses.Select(ap => ap.ActivityId).ToHashSet();
        var completedSessions = await _unitOfWork.ClassSessions.GetAllAsync(
            cs => !cs.IsDeleted
                  && cs.RequiresAttendance
                  && cs.Status == ClassSessionStatus.Completed
                  && cs.ActivityId != null
                  && doneActivityIds.Contains(cs.ActivityId.Value));
        if (completedSessions.Count == 0)
        {
            return;
        }

        var classIds = completedSessions.Select(cs => cs.ClassId).ToHashSet();
        var seats = await _unitOfWork.ClassEnrollments.GetAllAsync(
            ce => !ce.IsDeleted
                  && classIds.Contains(ce.ClassId)
                  && (ce.Status == ClassEnrollmentStatus.Active
                      || ce.Status == ClassEnrollmentStatus.Completed));
        var classIdsByStudent = seats
            .GroupBy(ce => ce.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(ce => ce.ClassId).ToHashSet());

        var sessionIds = completedSessions.Select(cs => cs.Id).ToHashSet();
        var attendanceByKey = (await _unitOfWork.SessionAttendances.GetAllAsync(
                sa => !sa.IsDeleted && sessionIds.Contains(sa.ClassSessionId)))
            .GroupBy(sa => (sa.ClassSessionId, sa.StudentId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(sa => sa.UpdatedAt ?? sa.CreatedAt).First());

        var sessionsByActivity = completedSessions
            .GroupBy(cs => cs.ActivityId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var attendancesToAdd = new List<SessionAttendance>();
        var updated = 0;

        foreach (var progress in doneProgresses)
        {
            if (!classIdsByStudent.TryGetValue(progress.StudentId, out var studentClassIds)
                || !sessionsByActivity.TryGetValue(progress.ActivityId, out var activitySessions))
            {
                continue;
            }

            foreach (var session in activitySessions.Where(cs => studentClassIds.Contains(cs.ClassId)))
            {
                var checkedInAt = session.StartTime.AddMinutes(2);

                if (!attendanceByKey.TryGetValue((session.Id, progress.StudentId), out var attendance))
                {
                    var created = new SessionAttendance
                    {
                        Id = Guid.NewGuid(),
                        ClassSessionId = session.Id,
                        StudentId = progress.StudentId,
                        ModuleEnrollmentId = progress.ModuleEnrollmentId,
                        Status = AttendanceStatus.Present,
                        CheckedInAt = checkedInAt,
                        CreatedAt = session.StartTime,
                        CreatedBy = Guid.Empty,
                        IsDeleted = false,
                    };
                    attendancesToAdd.Add(created);
                    attendanceByKey[(session.Id, progress.StudentId)] = created;
                    continue;
                }

                if (attendance.Status is not (AttendanceStatus.Expected or AttendanceStatus.Absent))
                {
                    continue;
                }

                attendance.Status = AttendanceStatus.Present;
                attendance.CheckedInAt ??= checkedInAt;
                attendance.UpdatedAt = _seedNow;
                attendance.UpdatedBy = Guid.Empty;
                await _unitOfWork.SessionAttendances.Update(attendance);
                updated++;
            }
        }

        if (attendancesToAdd.Count == 0 && updated == 0)
        {
            return;
        }

        if (attendancesToAdd.Count > 0)
        {
            await _unitOfWork.SessionAttendances.AddRangeAsync(attendancesToAdd);
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Aligned seed attendance with Done session activities — {Added} added, {Updated} updated.",
            attendancesToAdd.Count,
            updated);
    }
}
