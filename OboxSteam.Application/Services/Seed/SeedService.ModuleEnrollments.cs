using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    private async Task SeedModuleEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed module enrollments");
        var existingModuleEnrollments = await _unitOfWork.ModuleEnrollments.GetAllAsync();
        if (!existingModuleEnrollments.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var moduleRobotics1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-ROBOTICS-01");
            var moduleWebDev1 = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == "MOD-WEBDEV-01");
            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programEnrollmentStudent1 = student1 != null && programRobotics != null
                ? await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student1.Id && pe.ProgramId == programRobotics.Id && !pe.IsDeleted)
                : null;
            var programEnrollmentStudent2 = student2 != null && programWebDev != null
                ? await _unitOfWork.ProgramEnrollments.FirstOrDefaultAsync(
                    pe => pe.StudentId == student2.Id && pe.ProgramId == programWebDev.Id && !pe.IsDeleted)
                : null;
            var enrollTime = DateTime.UtcNow;

            var moduleEnrollments = new List<ModuleEnrollment>();

            if (student1 != null && moduleRobotics1 != null)
            {
                moduleEnrollments.Add(new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student1.Id,
                    ModuleId = moduleRobotics1.Id,
                    ProgramEnrollmentId = programEnrollmentStudent1?.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    FinalGrade = null,
                    EnrolledAt = enrollTime.AddDays(-10),
                    StartedAt = enrollTime.AddDays(-8),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student2 != null && moduleWebDev1 != null)
            {
                moduleEnrollments.Add(new ModuleEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student2.Id,
                    ModuleId = moduleWebDev1.Id,
                    ProgramEnrollmentId = programEnrollmentStudent2?.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-5),
                    StartedAt = enrollTime.AddDays(-4),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (moduleEnrollments.Count > 0)
            {
                await _unitOfWork.ModuleEnrollments.AddRangeAsync(moduleEnrollments);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed module enrollments");
            }
        }
        else
        {
            _loggerService.LogInformation("Module enrollments already exist, skipping seeding");
        }

    }
}

