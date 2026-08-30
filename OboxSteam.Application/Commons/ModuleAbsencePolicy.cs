using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Commons;

/// <summary>
/// Absence-based module fail rule: when Absent marks reach
/// <see cref="MaxAbsencePercent"/> of the module's session-linked activities,
    /// the active module enrollment is failed and the student must chuyen ca.
/// Only <see cref="AttendanceStatus.Absent"/> counts; Excused and Expected never count.
/// </summary>
public static class ModuleAbsencePolicy
{
    public const decimal MaxAbsencePercent = 50m;

    public static async Task<int> CountMissedAsync(IUnitOfWork unitOfWork, Guid moduleEnrollmentId)
    {
        var absences = await unitOfWork.SessionAttendances.GetAllAsync(
            sa => sa.ModuleEnrollmentId == moduleEnrollmentId
                  && sa.Status == AttendanceStatus.Absent
                  && !sa.IsDeleted);

        if (absences.Count == 0)
        {
            return 0;
        }

        var sessionIds = absences.Select(sa => sa.ClassSessionId).Distinct().ToList();
        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => sessionIds.Contains(cs.Id)
                  && cs.ActivityId != null
                  && !cs.IsDeleted);

        return sessions
            .Where(cs => cs.ActivityId.HasValue)
            .Select(cs => cs.ActivityId!.Value)
            .Distinct()
            .Count();
    }

    public static async Task<int> CountSessionActivitiesAsync(IUnitOfWork unitOfWork, Guid moduleId)
    {
        var sessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ModuleId == moduleId
                  && cs.ActivityId != null
                  && !cs.IsDeleted);

        return sessions
            .Where(cs => cs.ActivityId.HasValue)
            .Select(cs => cs.ActivityId!.Value)
            .Distinct()
            .Count();
    }

    public static bool ShouldFail(int missed, int total)
        => total > 0 && missed / (decimal)total * 100m >= MaxAbsencePercent;
}
