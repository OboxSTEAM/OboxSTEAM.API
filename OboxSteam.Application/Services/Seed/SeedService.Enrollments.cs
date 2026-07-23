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
            var studentCertIncomplete = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-024");
            var studentCertComplete = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-025");
            var programRobotics = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-ROBOTICS");
            var programWebDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-WEBDEV");
            var programSteam = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-STEAM-01");
            var programIot = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-IOT");
            var programCertTest = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-CERT-TEST");
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

            // Certificate test program: STD-024 has no progress; STD-025 completes all activities.
            if (studentCertIncomplete != null && programCertTest != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentCertIncomplete.Id,
                    ProgramId = programCertTest.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-3),
                    StartedAt = enrollTime.AddDays(-2),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (studentCertComplete != null && programCertTest != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentCertComplete.Id,
                    ProgramId = programCertTest.Id,
                    Status = EnrollmentStatus.Active,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddDays(-5),
                    StartedAt = enrollTime.AddDays(-4),
                    CreatedAt = enrollTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            // Extra status / date diversity for Manager dashboard enrollment charts.
            var student5 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-005");
            var student6 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-006");
            var student7 = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == "STD-007");
            var programGameDev = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-GAMEDEV");
            var programAi = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == "PRG-AIBASIC");

            if (student5 != null && programRobotics != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student5.Id,
                    ProgramId = programRobotics.Id,
                    Status = EnrollmentStatus.Completed,
                    ProgressPercent = 100m,
                    EnrolledAt = enrollTime.AddMonths(-8),
                    StartedAt = enrollTime.AddMonths(-8).AddDays(3),
                    CompletedAt = enrollTime.AddMonths(-2),
                    CreatedAt = enrollTime.AddMonths(-8),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student6 != null && programGameDev != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student6.Id,
                    ProgramId = programGameDev.Id,
                    Status = EnrollmentStatus.PendingPayment,
                    ProgressPercent = 0m,
                    EnrolledAt = enrollTime.AddMonths(-1),
                    CreatedAt = enrollTime.AddMonths(-1),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student7 != null && programAi != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student7.Id,
                    ProgramId = programAi.Id,
                    Status = EnrollmentStatus.Dropped,
                    ProgressPercent = 15m,
                    EnrolledAt = enrollTime.AddMonths(-5),
                    StartedAt = enrollTime.AddMonths(-5).AddDays(2),
                    CreatedAt = enrollTime.AddMonths(-5),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student3 != null && programWebDev != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student3.Id,
                    ProgramId = programWebDev.Id,
                    Status = EnrollmentStatus.Completed,
                    ProgressPercent = 100m,
                    EnrolledAt = enrollTime.AddMonths(-10),
                    StartedAt = enrollTime.AddMonths(-10).AddDays(5),
                    CompletedAt = enrollTime.AddMonths(-3),
                    CreatedAt = enrollTime.AddMonths(-10),
                    CreatedBy = Guid.Empty,
                    IsDeleted = false
                });
            }

            if (student4 != null && programSteam != null)
            {
                programEnrollments.Add(new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = student4.Id,
                    ProgramId = programSteam.Id,
                    Status = EnrollmentStatus.Deferred,
                    ProgressPercent = 20m,
                    EnrolledAt = enrollTime.AddMonths(-3),
                    StartedAt = enrollTime.AddMonths(-3).AddDays(1),
                    CreatedAt = enrollTime.AddMonths(-3),
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

