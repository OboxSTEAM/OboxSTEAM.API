using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Validation;

/// <summary>
/// Counts active class sessions against LiveOnline/Offline activities plus assignments.
/// </summary>
public static class ClassScheduleCoverage
{
    public static int CountActiveSessions(IUnitOfWork unitOfWork, Guid classId)
        => unitOfWork.ClassSessions
            .GetQueryable()
            .Count(s => s.ClassId == classId
                        && !s.IsDeleted
                        && s.Status != ClassSessionStatus.Cancelled);

    public static async Task<int> CountSchedulableItemsAsync(IUnitOfWork unitOfWork, Guid programId)
    {
        var moduleIds = (await unitOfWork.Modules.GetAllAsync(
                m => m.ProgramId == programId && !m.IsDeleted))
            .Select(m => m.Id)
            .ToList();

        if (moduleIds.Count == 0)
        {
            return 0;
        }

        var courseIds = (await unitOfWork.Courses.GetAllAsync(
                c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted))
            .Select(c => c.Id)
            .ToList();

        var activityCount = (await unitOfWork.Activities.GetAllAsync(
                a => courseIds.Contains(a.CourseId)
                     && !a.IsDeleted
                     && (a.ActivityType == ActivityType.LiveOnline || a.ActivityType == ActivityType.Offline)))
            .Count;

        var assignmentCount = (await unitOfWork.Assignments.GetAllAsync(
                a => moduleIds.Contains(a.ModuleId) && !a.IsDeleted))
            .Count;

        return activityCount + assignmentCount;
    }

    public static bool CoversCurriculum(int activeSessionCount, int schedulableItemCount)
        => activeSessionCount > 0 && activeSessionCount == schedulableItemCount;
}
