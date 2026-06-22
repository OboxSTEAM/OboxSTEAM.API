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
    private async Task SeedProgramEnrollmentsAsync()
    {
        _loggerService.LogInformation("Starting seed program enrollments");
        var existingProgramEnrollments = await _unitOfWork.ProgramEnrollments.GetAllAsync();
        if (!existingProgramEnrollments.Any())
        {
            var student1 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-001");
            var student2 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-002");
            var student3 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-003");
            var student4 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-004");
            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");
            var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
            var enrollTime = DateTime.UtcNow;

            var programEnrollments = new List<ProgramEnrollment>();

            if (student1 != null && programRobotics != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student1.Id,
                    ProgramId = programRobotics.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-14),
                    StartedAt = enrollTime.AddDays(-10),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student2 != null && programWebDev != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student2.Id,
                    ProgramId = programWebDev.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-7),
                    StartedAt = enrollTime.AddDays(-5),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student3 != null && programSteam != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student3.Id,
                    ProgramId = programSteam.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 50m,
                    EnrolledAt = enrollTime.AddDays(-21),
                    StartedAt = enrollTime.AddDays(-18),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student4 != null && programIot != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student4.Id,
                    ProgramId = programIot.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-2),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (programEnrollments.Count > 0)
            {
                await _unitOfWork.ProgramEnrollments.AddRangeAsync(programEnrollments);
                await _unitOfWork.SaveChangesAsync();
                _loggerService.LogInformation("Finished seed program enrollments — {Count} record(s).", programEnrollments.Count);
            }
            else
            {
                _loggerService.LogWarning("No program enrollments seeded.");
            }
        }
        else
        {
            _loggerService.LogInformation("Program enrollments already exist, skipping seeding");
        }

    }
}

