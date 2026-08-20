using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// For Completed program enrollments: mark all activities Done, then issue
    /// program certificates via <see cref="ICertificateService.EnsureProgramCertificateForSeedAsync"/>.
    /// </summary>
    private async Task SeedCompletedProgramCertificatesAsync()
    {
        _loggerService.LogInformation("Starting seed certificates for Completed program enrollments");

        var completedEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted && pe.Status == EnrollmentStatus.Completed);

        if (completedEnrollments.Count == 0)
        {
            _loggerService.LogInformation("No Completed program enrollments — skipping certificate seed.");
            return;
        }

        var issued = 0;
        var skipped = 0;

        foreach (var enrollment in completedEnrollments)
        {
            var prepared = await EnsureCompletedEnrollmentActivitiesDoneAsync(enrollment);
            if (!prepared)
            {
                skipped++;
                _loggerService.LogWarning(
                    "Skipping certificate for enrollment {EnrollmentId}: program has no activities.",
                    enrollment.Id);
                continue;
            }

            try
            {
                var cert = await _certificateService.EnsureProgramCertificateForSeedAsync(enrollment.Id);
                if (cert != null)
                {
                    issued++;
                }
                else
                {
                    skipped++;
                    _loggerService.LogWarning(
                        "Certificate ensure returned null for enrollment {EnrollmentId} (still ineligible).",
                        enrollment.Id);
                }
            }
            catch (Exception ex)
            {
                skipped++;
                _loggerService.LogError(
                    ex,
                    "Failed to ensure certificate for enrollment {EnrollmentId}.",
                    enrollment.Id);
            }
        }

        _loggerService.LogInformation(
            "Finished seed certificates — issued/ensured {Issued}, skipped {Skipped}.",
            issued,
            skipped);
    }

    /// <summary>
    /// Ensures module enrollments exist and every program activity has Done progress
    /// so <see cref="CertificateService"/> eligibility passes.
    /// </summary>
    private async Task<bool> EnsureCompletedEnrollmentActivitiesDoneAsync(ProgramEnrollment enrollment)
    {
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => m.ProgramId == enrollment.ProgramId && !m.IsDeleted);
        if (modules.Count == 0)
        {
            return false;
        }

        var seedTime = _seedNow;
        var allActivityIds = new List<(Guid ActivityId, Guid ModuleId)>();

        foreach (var module in modules)
        {
            var activityIds = await ActivityProgressCalculationHelper.GetModuleActivityIdsAsync(
                _unitOfWork,
                module.Id);
            foreach (var activityId in activityIds)
            {
                allActivityIds.Add((activityId, module.Id));
            }
        }

        if (allActivityIds.Count == 0)
        {
            return false;
        }

        var moduleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync(
            me => me.ProgramEnrollmentId == enrollment.Id
                  && me.StudentId == enrollment.StudentId
                  && !me.IsDeleted);

        var latestByModule = moduleEnrollments
            .GroupBy(me => me.ModuleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(me => me.AttemptNumber).First());

        var progressToAdd = new List<ActivityProgress>();
        var changed = false;

        foreach (var module in modules)
        {
            if (!latestByModule.TryGetValue(module.Id, out var moduleEnrollment))
            {
                moduleEnrollment = new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = enrollment.StudentId,
                    ModuleId = module.Id,
                    ProgramEnrollmentId = enrollment.Id,
                    Status = EnrollmentStatus.Completed,
                    ProgressPercent = 100m,
                    FinalGrade = 90m,
                    AttemptNumber = 1,
                    EnrolledAt = enrollment.EnrolledAt ?? seedTime.AddDays(-60),
                    StartedAt = enrollment.StartedAt ?? seedTime.AddDays(-55),
                    CompletedAt = enrollment.CompletedAt ?? seedTime.AddDays(-5),
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                };
                await _unitOfWork.ModuleEnrollments.AddAsync(moduleEnrollment);
                latestByModule[module.Id] = moduleEnrollment;
                changed = true;
            }
            else if (moduleEnrollment.Status != EnrollmentStatus.Completed
                     || moduleEnrollment.ProgressPercent != 100m)
            {
                moduleEnrollment.Status = EnrollmentStatus.Completed;
                moduleEnrollment.ProgressPercent = 100m;
                moduleEnrollment.FinalGrade ??= 90m;
                moduleEnrollment.CompletedAt ??= enrollment.CompletedAt ?? seedTime.AddDays(-5);
                await _unitOfWork.ModuleEnrollments.Update(moduleEnrollment);
                changed = true;
            }
        }

        var moduleEnrollmentIds = latestByModule.Values.Select(me => me.Id).ToList();
        var existingProgress = await _unitOfWork.ActivityProgresses.GetAllAsync(
            ap => moduleEnrollmentIds.Contains(ap.ModuleEnrollmentId) && !ap.IsDeleted);
        var progressByKey = existingProgress
            .GroupBy(ap => (ap.ModuleEnrollmentId, ap.ActivityId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(ap => ap.UpdatedAt ?? ap.CreatedAt).First());

        foreach (var (activityId, moduleId) in allActivityIds)
        {
            if (!latestByModule.TryGetValue(moduleId, out var moduleEnrollment))
            {
                continue;
            }

            var key = (moduleEnrollment.Id, activityId);
            if (progressByKey.TryGetValue(key, out var progress))
            {
                if (progress.ActivityStatus != ActivityStatus.Done || !progress.IsCompleted)
                {
                    progress.ActivityStatus = ActivityStatus.Done;
                    progress.IsCompleted = true;
                    progress.CompletedAt ??= enrollment.CompletedAt ?? seedTime.AddDays(-5);
                    progress.CompletionSource ??= CompletionSource.Manual;
                    await _unitOfWork.ActivityProgresses.Update(progress);
                    changed = true;
                }

                continue;
            }

            progressToAdd.Add(new ActivityProgress
            {
                Id = Guid.NewGuid(),
                StudentId = enrollment.StudentId,
                ActivityId = activityId,
                ModuleEnrollmentId = moduleEnrollment.Id,
                ActivityStatus = ActivityStatus.Done,
                IsCompleted = true,
                CompletedAt = enrollment.CompletedAt ?? seedTime.AddDays(-5),
                CompletionSource = CompletionSource.Manual,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (progressToAdd.Count > 0)
        {
            await _unitOfWork.ActivityProgresses.AddRangeAsync(progressToAdd);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return true;
    }
}
