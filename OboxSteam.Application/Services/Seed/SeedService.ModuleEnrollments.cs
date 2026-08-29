using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Ensures every Active/Completed program enrollment has an Active/Completed
    /// <see cref="ModuleEnrollment"/> for every module in that program.
    /// </summary>
    private async Task SeedModuleEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed module enrollments (full program backfill)");

        var programEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active || pe.Status == EnrollmentStatus.Completed));

        if (programEnrollments.Count == 0)
        {
            _loggerService.LogWarning("No Active/Completed program enrollments found. Skipping module enrollment seeding.");
            return;
        }

        var programIds = programEnrollments.Select(pe => pe.ProgramId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => programIds.Contains(m.ProgramId) && !m.IsDeleted);
        var modulesByProgramId = modules
            .GroupBy(m => m.ProgramId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.ModuleOrder).ToList());

        var toAdd = new List<ModuleEnrollment>();
        var created = 0;
        var linked = 0;

        foreach (var pe in programEnrollments)
        {
            if (!modulesByProgramId.TryGetValue(pe.ProgramId, out var programModules) || programModules.Count == 0)
            {
                continue;
            }

            var existing = await _unitOfWork.ModuleEnrollments.GetAllAsync(
                me => me.StudentId == pe.StudentId
                      && me.ProgramEnrollmentId == pe.Id
                      && !me.IsDeleted);
            var existingModuleIds = existing.Select(me => me.ModuleId).ToHashSet();

            var isCompleted = pe.Status == EnrollmentStatus.Completed;
            var moduleIndex = 0;

            foreach (var module in programModules)
            {
                if (existingModuleIds.Contains(module.Id))
                {
                    moduleIndex++;
                    continue;
                }

                var anyExisting = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                    me => me.StudentId == pe.StudentId
                          && me.ModuleId == module.Id
                          && !me.IsDeleted);
                if (anyExisting != null)
                {
                    if (anyExisting.ProgramEnrollmentId == null)
                    {
                        anyExisting.ProgramEnrollmentId = pe.Id;
                        await _unitOfWork.ModuleEnrollments.Update(anyExisting);
                        linked++;
                    }

                    moduleIndex++;
                    continue;
                }

                var enrolledAt = pe.EnrolledAt ?? _seedNow.AddDays(-14);
                var startedAt = isCompleted
                    ? (pe.StartedAt ?? enrolledAt.AddDays(2))
                    : (pe.StartedAt ?? enrolledAt.AddDays(1));
                DateTime? completedAt = isCompleted
                    ? (pe.CompletedAt ?? _seedNow.AddDays(-moduleIndex - 1))
                    : null;
                var rowCreatedAt = enrolledAt.AddDays(moduleIndex);

                toAdd.Add(new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = pe.StudentId,
                    ModuleId = module.Id,
                    ProgramEnrollmentId = pe.Id,
                    Status = isCompleted ? EnrollmentStatus.Completed : EnrollmentStatus.Active,
                    ProgressPercent = isCompleted ? 100m : 0m,
                    FinalGrade = isCompleted ? 85m + (module.ModuleOrder % 10) : null,
                    AttemptNumber = 1,
                    EnrolledAt = rowCreatedAt,
                    StartedAt = startedAt.AddDays(moduleIndex),
                    CompletedAt = completedAt,
                    CreatedAt = rowCreatedAt,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                created++;
                moduleIndex++;
            }
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.ModuleEnrollments.AddRangeAsync(toAdd);
        }

        if (toAdd.Count > 0 || linked > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed module enrollments — {Created} new, {Linked} linked, across {PeCount} program enrollment(s).",
            created,
            linked,
            programEnrollments.Count);

        await SeedFailedProgramModuleEnrollmentsAsync();
    }

    /// <summary>
    /// Closed Failed purchases keep a Failed module row on <see cref="ProgramEnrollment.EndedModuleId"/>
    /// so curriculum history matches the close reason. Other modules are not backfilled.
    /// </summary>
    private async Task SeedFailedProgramModuleEnrollmentsAsync()
    {
        var failedEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && pe.Status == EnrollmentStatus.Failed
                  && pe.EndedModuleId != null);
        if (failedEnrollments.Count == 0)
        {
            return;
        }

        var toAdd = new List<ModuleEnrollment>();
        var updated = 0;
        foreach (var pe in failedEnrollments)
        {
            var existing = await _unitOfWork.ModuleEnrollments.FirstOrDefaultAsync(
                me => me.ProgramEnrollmentId == pe.Id
                      && me.ModuleId == pe.EndedModuleId!.Value
                      && !me.IsDeleted);
            if (existing != null)
            {
                if (existing.Status != EnrollmentStatus.Failed)
                {
                    existing.Status = EnrollmentStatus.Failed;
                    await _unitOfWork.ModuleEnrollments.Update(existing);
                    updated++;
                }

                continue;
            }

            var enrolledAt = pe.EnrolledAt ?? _seedNow.AddDays(-30);
            toAdd.Add(new ModuleEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = pe.StudentId,
                ModuleId = pe.EndedModuleId!.Value,
                ProgramEnrollmentId = pe.Id,
                Status = EnrollmentStatus.Failed,
                ProgressPercent = pe.ProgressPercent,
                AttemptNumber = 1,
                EnrolledAt = enrolledAt,
                StartedAt = pe.StartedAt ?? enrolledAt.AddDays(2),
                CreatedAt = enrolledAt,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (toAdd.Count > 0 || updated > 0)
        {
            if (toAdd.Count > 0)
            {
                await _unitOfWork.ModuleEnrollments.AddRangeAsync(toAdd);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed failed-module enrollments — {Added} new, {Updated} updated.",
            toAdd.Count,
            updated);
    }
}
