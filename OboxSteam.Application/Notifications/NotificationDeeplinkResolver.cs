using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Notifications;

/// <summary>
/// Resolves curriculum deep-link targets for notification payloads
/// (aligned with <c>CompleteActivityResponseDto.NextActivityId</c>).
/// Failures return <c>null</c> so notification publish never blocks learning flows.
/// </summary>
public static class NotificationDeeplinkResolver
{
    /// <summary>
    /// Next incomplete activity after <paramref name="afterActivityId"/> in program order,
    /// using module unlock + sequential accessibility.
    /// </summary>
    public static async Task<Guid?> ResolveNextActivityIdAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        Guid programEnrollmentId,
        Guid afterActivityId)
    {
        try
        {
            var context = await LoadContextAsync(unitOfWork, programId, programEnrollmentId);

            return CurriculumStatusHelper.FindNextActivityId(
                context.Snapshot,
                afterActivityId,
                id => IsAccessible(id, context),
                id => context.CompletedActivityIds.Contains(id));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// First incomplete, accessible activity in the program
    /// (for activate / enroll / payment-succeeded).
    /// </summary>
    public static async Task<Guid?> ResolveCurrentActivityIdAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        Guid programEnrollmentId)
    {
        try
        {
            var context = await LoadContextAsync(unitOfWork, programId, programEnrollmentId);

            return CurriculumStatusHelper.FindCurrentActivityId(
                context.Snapshot,
                id => IsAccessible(id, context),
                id => context.CompletedActivityIds.Contains(id));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>First activity in a module's global order (for module unlocked).</summary>
    public static async Task<Guid?> ResolveFirstActivityInModuleAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        Guid moduleId)
    {
        try
        {
            var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(unitOfWork, programId);
            foreach (var activityId in snapshot.GlobalActivityOrder)
            {
                if (snapshot.ActivityModuleMap.TryGetValue(activityId, out var mapped)
                    && mapped == moduleId)
                {
                    return activityId;
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsAccessible(Guid activityId, DeeplinkContext context)
    {
        if (!context.Snapshot.ActivityModuleMap.TryGetValue(activityId, out var moduleId)
            || !context.ModulesById.TryGetValue(moduleId, out var module))
        {
            return false;
        }

        if (!CurriculumStatusHelper.IsModuleUnlocked(
                module,
                context.LatestEnrollmentByModuleId,
                context.ModulesById))
        {
            return false;
        }

        return CurriculumStatusHelper.IsActivitySequentiallyAccessible(
            activityId,
            context.Snapshot,
            id => context.CompletedActivityIds.Contains(id));
    }

    private static async Task<DeeplinkContext> LoadContextAsync(
        IUnitOfWork unitOfWork,
        Guid programId,
        Guid programEnrollmentId)
    {
        var snapshot = await ProgramCurriculumTreeLoader.LoadAsync(unitOfWork, programId);
        var modulesById = snapshot.Modules.ToDictionary(m => m.Id);

        var moduleEnrollments = await unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == programEnrollmentId && !me.IsDeleted);

        var latestByModule = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(me => me.AttemptNumber).First());

        var moduleEnrollmentIds = moduleEnrollments.Select(me => me.Id).ToList();
        var completed = new HashSet<Guid>();
        if (moduleEnrollmentIds.Count > 0)
        {
            var progresses = await unitOfWork.ActivityProgresses.GetAllAsync(
                ap => moduleEnrollmentIds.Contains(ap.ModuleEnrollmentId)
                      && !ap.IsDeleted);

            foreach (var progress in progresses)
            {
                if (progress.ActivityStatus == ActivityStatus.Done || progress.IsCompleted)
                {
                    completed.Add(progress.ActivityId);
                }
            }
        }

        return new DeeplinkContext(snapshot, modulesById, latestByModule, completed);
    }

    private sealed record DeeplinkContext(
        ProgramCurriculumTreeSnapshot Snapshot,
        IReadOnlyDictionary<Guid, Module> ModulesById,
        IReadOnlyDictionary<Guid, ModuleEnrollment> LatestEnrollmentByModuleId,
        HashSet<Guid> CompletedActivityIds);
}
