using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Ensures every Active/Completed program enrollment has an Active/Completed
    /// <see cref="ModuleEnrollment"/> for every module in that program (bypasses
    /// curriculum unlock / course-page provisioning).
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

        var seedTime = DateTime.UtcNow;
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

                // Also skip if an enrollment exists for this student+module under any PE link
                // (legacy rows may have null ProgramEnrollmentId).
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

                var enrolledAt = pe.EnrolledAt ?? seedTime.AddDays(-14);
                var startedAt = isCompleted
                    ? (pe.StartedAt ?? enrolledAt.AddDays(2))
                    : (pe.StartedAt ?? enrolledAt.AddDays(1));
                DateTime? completedAt = isCompleted
                    ? (pe.CompletedAt ?? seedTime.AddDays(-moduleIndex - 1))
                    : null;

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
                    AssignmentFailureCount = 0,
                    EnrolledAt = enrolledAt.AddDays(moduleIndex),
                    StartedAt = startedAt.AddDays(moduleIndex),
                    CompletedAt = completedAt,
                    CreatedAt = seedTime,
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
    }
}
