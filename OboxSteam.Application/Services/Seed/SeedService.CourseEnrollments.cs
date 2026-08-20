using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    /// <summary>
    /// Ensures every Active/Completed program enrollment has matching
    /// <see cref="CourseEnrollment"/> rows for all courses under that program's modules.
    /// </summary>
    private async Task SeedCourseEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed course enrollments (full program backfill)");

        var programEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync(
            pe => !pe.IsDeleted
                  && (pe.Status == EnrollmentStatus.Active || pe.Status == EnrollmentStatus.Completed));

        if (programEnrollments.Count == 0)
        {
            _loggerService.LogWarning("No Active/Completed program enrollments found. Skipping course enrollment seeding.");
            return;
        }

        var programIds = programEnrollments.Select(pe => pe.ProgramId).Distinct().ToList();
        var modules = await _unitOfWork.Modules.GetAllAsync(
            m => programIds.Contains(m.ProgramId) && !m.IsDeleted);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var courses = moduleIds.Count == 0
            ? new List<Course>()
            : await _unitOfWork.Courses.GetAllAsync(c => moduleIds.Contains(c.ModuleId) && !c.IsDeleted);

        var coursesByProgramId = modules
            .GroupBy(m => m.ProgramId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ids = g.Select(m => m.Id).ToHashSet();
                    return courses.Where(c => ids.Contains(c.ModuleId)).ToList();
                });

        var toAdd = new List<CourseEnrollment>();
        var created = 0;
        var updated = 0;

        foreach (var pe in programEnrollments)
        {
            if (!coursesByProgramId.TryGetValue(pe.ProgramId, out var programCourses) || programCourses.Count == 0)
            {
                continue;
            }

            var isCompleted = pe.Status == EnrollmentStatus.Completed;
            var courseIndex = 0;

            foreach (var course in programCourses)
            {
                var existing = await _unitOfWork.CourseEnrollments.FirstOrDefaultAsync(
                    ce => ce.StudentId == pe.StudentId
                          && ce.CourseId == course.Id
                          && !ce.IsDeleted);

                if (existing != null)
                {
                    if (isCompleted && existing.Status != EnrollmentStatus.Completed)
                    {
                        existing.Status = EnrollmentStatus.Completed;
                        existing.StartedAt ??= pe.StartedAt ?? _seedNow.AddDays(-10);
                        existing.CompletedAt ??= pe.CompletedAt ?? _seedNow.AddDays(-1);
                        existing.JoinedAt ??= pe.EnrolledAt ?? _seedNow.AddDays(-14);
                        await _unitOfWork.CourseEnrollments.Update(existing);
                        updated++;
                    }

                    courseIndex++;
                    continue;
                }

                var joinedAt = pe.EnrolledAt ?? _seedNow.AddDays(-14);
                toAdd.Add(new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = pe.StudentId,
                    CourseId = course.Id,
                    Status = isCompleted ? EnrollmentStatus.Completed : EnrollmentStatus.Active,
                    JoinedAt = joinedAt.AddDays(courseIndex),
                    StartedAt = (pe.StartedAt ?? joinedAt.AddDays(1)).AddDays(courseIndex),
                    CompletedAt = isCompleted
                        ? (pe.CompletedAt ?? _seedNow.AddDays(-courseIndex - 1))
                        : null,
                    CreatedAt = joinedAt.AddDays(courseIndex),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
                created++;
                courseIndex++;
            }
        }

        if (toAdd.Count > 0)
        {
            await _unitOfWork.CourseEnrollments.AddRangeAsync(toAdd);
        }

        if (toAdd.Count > 0 || updated > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _loggerService.LogInformation(
            "Finished seed course enrollments — {Created} new, {Updated} upgraded, across {PeCount} program enrollment(s).",
            created,
            updated,
            programEnrollments.Count);
    }
}
