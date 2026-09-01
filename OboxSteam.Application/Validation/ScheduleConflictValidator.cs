using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Shared calendar overlap rules for student enroll/transfer (and later redelivery / co-teach).
/// Two intervals overlap when <c>start1 &lt; end2 &amp;&amp; start2 &lt; end1</c>.
/// Cancelled sessions never block. Sessions without a real window cannot be stored on
/// <see cref="ClassSession"/> (SelfPaced activities have no session rows).
/// </summary>
public static class ScheduleConflictValidator
{
    public static bool Overlaps(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
        => start1 < end2 && start2 < end1;

    /// <summary>
    /// Returns the first busy session that overlaps any candidate, or null when there is no conflict.
    /// </summary>
    public static ClassSession? FindFirstOverlap(
        IReadOnlyList<ClassSession> busySessions,
        IReadOnlyList<ClassSession> candidateSessions)
    {
        foreach (var candidate in candidateSessions)
        {
            if (candidate.Status == ClassSessionStatus.Cancelled)
            {
                continue;
            }

            foreach (var busy in busySessions)
            {
                if (busy.Status == ClassSessionStatus.Cancelled)
                {
                    continue;
                }

                if (Overlaps(busy.StartTime, busy.EndTime, candidate.StartTime, candidate.EndTime))
                {
                    return busy;
                }
            }
        }

        return null;
    }

    public static async Task<List<ClassSession>> GetStudentBusySessionsAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid? excludeClassId = null)
    {
        var enrollments = await unitOfWork.ClassEnrollments.GetAllAsync(
            ce => ce.StudentId == studentId
                  && ce.Status == ClassEnrollmentStatus.Active
                  && !ce.IsDeleted);

        var classIds = enrollments
            .Select(ce => ce.ClassId)
            .Where(id => !excludeClassId.HasValue || id != excludeClassId.Value)
            .Distinct()
            .ToList();

        if (classIds.Count == 0)
        {
            return [];
        }

        return await unitOfWork.ClassSessions.GetAllAsync(
            cs => classIds.Contains(cs.ClassId)
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);
    }

    /// <summary>
    /// Returns the first busy session that overlaps any candidate of the given module, or null.
    /// Used for redelivery select-class (only module sessions of the target class matter).
    /// </summary>
    public static async Task ValidateStudentCanJoinModuleOnClassAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid targetClassId,
        Guid moduleId,
        Guid? excludeClassId = null)
    {
        var busySessions = await GetStudentBusySessionsAsync(unitOfWork, studentId, excludeClassId);
        var candidateSessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == targetClassId
                  && cs.ModuleId == moduleId
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

        var overlapping = FindFirstOverlap(busySessions, candidateSessions);
        if (overlapping == null)
        {
            return;
        }

        var classEntity = await unitOfWork.Classes.GetByIdAsync(overlapping.ClassId);
        var classCode = classEntity?.Code ?? overlapping.ClassId.ToString();

        throw ErrorHelper.Conflict(
            $"Your schedule overlaps with session '{overlapping.Title}' " +
            $"in class '{classCode}' " +
            $"({overlapping.StartTime:yyyy-MM-dd HH:mm} – {overlapping.EndTime:yyyy-MM-dd HH:mm} UTC).");
    }

    public static async Task ValidateStudentCanJoinClassAsync(
        IUnitOfWork unitOfWork,
        Guid studentId,
        Guid targetClassId,
        Guid? excludeClassId = null)
    {
        var busySessions = await GetStudentBusySessionsAsync(unitOfWork, studentId, excludeClassId);
        var candidateSessions = await unitOfWork.ClassSessions.GetAllAsync(
            cs => cs.ClassId == targetClassId
                  && cs.Status != ClassSessionStatus.Cancelled
                  && !cs.IsDeleted);

        var overlapping = FindFirstOverlap(busySessions, candidateSessions);
        if (overlapping == null)
        {
            return;
        }

        var classEntity = await unitOfWork.Classes.GetByIdAsync(overlapping.ClassId);
        var classCode = classEntity?.Code ?? overlapping.ClassId.ToString();

        throw ErrorHelper.Conflict(
            $"Your schedule overlaps with session '{overlapping.Title}' " +
            $"in class '{classCode}' " +
            $"({overlapping.StartTime:yyyy-MM-dd HH:mm} – {overlapping.EndTime:yyyy-MM-dd HH:mm} UTC).");
    }

    public static async Task<List<ClassSession>> GetExpertBusySessionsAsync(
        IUnitOfWork unitOfWork,
        Guid expertId,
        Guid? excludeSessionId = null)
    {
        var invitations = await unitOfWork.ClassSessionExperts.GetAllAsync(
            e => e.ExpertId == expertId
                 && e.Status == ClassSessionExpertStatus.Accepted
                 && !e.IsDeleted
                 && (!excludeSessionId.HasValue || e.ClassSessionId != excludeSessionId.Value));

        var sessionIds = invitations.Select(e => e.ClassSessionId).Distinct().ToList();
        if (sessionIds.Count == 0)
        {
            return [];
        }

        return await unitOfWork.ClassSessions.GetAllAsync(
            cs => sessionIds.Contains(cs.Id)
                  && cs.Status != ClassSessionStatus.Cancelled
                  && cs.SessionKind != SessionKind.AssignmentWindow
                  && !cs.IsDeleted);
    }

    public static async Task ValidateExpertSessionNoOverlapAsync(
        IUnitOfWork unitOfWork,
        Guid expertId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId = null)
    {
        var overlapping = await FindExpertOverlapAsync(
            unitOfWork, expertId, startTime, endTime, excludeSessionId);
        if (overlapping == null)
        {
            return;
        }

        var classEntity = await unitOfWork.Classes.GetByIdAsync(overlapping.ClassId);
        var classCode = classEntity?.Code ?? overlapping.ClassId.ToString();

        throw ErrorHelper.Conflict(
            $"Expert schedule overlaps with session '{overlapping.Title}' " +
            $"in class '{classCode}' " +
            $"({overlapping.StartTime:yyyy-MM-dd HH:mm} – {overlapping.EndTime:yyyy-MM-dd HH:mm} UTC).");
    }

    public static async Task<string?> BuildExpertOverlapWarningAsync(
        IUnitOfWork unitOfWork,
        Guid expertId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId = null)
    {
        var overlapping = await FindExpertOverlapAsync(
            unitOfWork, expertId, startTime, endTime, excludeSessionId);
        if (overlapping == null)
        {
            return null;
        }

        var classEntity = await unitOfWork.Classes.GetByIdAsync(overlapping.ClassId);
        var classCode = classEntity?.Code ?? overlapping.ClassId.ToString();

        return $"This invitation overlaps session '{overlapping.Title}' in class '{classCode}' "
            + $"({overlapping.StartTime:yyyy-MM-dd HH:mm} – {overlapping.EndTime:yyyy-MM-dd HH:mm} UTC). "
            + "Accept will be blocked unless the other session is cancelled or declined.";
    }

    private static async Task<ClassSession?> FindExpertOverlapAsync(
        IUnitOfWork unitOfWork,
        Guid expertId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId)
    {
        var busySessions = await GetExpertBusySessionsAsync(unitOfWork, expertId, excludeSessionId);
        foreach (var busy in busySessions)
        {
            if (Overlaps(busy.StartTime, busy.EndTime, startTime, endTime))
            {
                return busy;
            }
        }

        return null;
    }
}
