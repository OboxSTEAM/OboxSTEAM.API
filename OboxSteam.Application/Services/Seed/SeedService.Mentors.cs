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
    private async Task EnsureAdditionalMentorUsersAsync()
    {
        var additionalMentors = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "MNT-003",
                Email = "mentor3@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                FullName = "Michael Mentor",
                Phone = "0123456780",
                Role = RoleType.Mentor,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "MNT-004",
                Email = "mentor4@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                FullName = "Emily Mentor",
                Phone = "0123456779",
                Role = RoleType.Mentor,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "MNT-005",
                Email = "mentor5@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                FullName = "Chris Mentor",
                Phone = "0123456778",
                Role = RoleType.Mentor,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "MNT-006",
                Email = "mentor6@oboxsteam.com",
                PasswordHash = new PasswordHasher().HashPassword("Mentor@123")!,
                FullName = "Lisa Mentor",
                Phone = "0123456777",
                Role = RoleType.Mentor,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            }
        };

        var mentorsToAdd = new List<User>();
        foreach (var mentor in additionalMentors)
        {
            var exists = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == mentor.Code);
            if (exists == null)
            {
                mentorsToAdd.Add(mentor);
            }
        }

        if (mentorsToAdd.Count == 0)
        {
            return;
        }

        await _unitOfWork.Users.AddRangeAsync(mentorsToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Backfilled {Count} additional mentor user(s).",
            mentorsToAdd.Count);
    }

    private async Task SeedMentorClassesAsync()
    {
        _loggerService.LogInformation("Starting seed mentor classes");

        var seedTime = DateTime.UtcNow;
        var classDefinitions = new List<(string MentorCode, string ProgramCode, string Code, string Name, ClassStatus Status, int StartDaysOffset, int EndDaysOffset, int MaxCapacity, string ScheduleSummary)>
        {
            ("MNT-001", "PRG-ROBOTICS", "CLS-ROBOTICS-2026A", "Robotics Spring 2026 - Cohort A", ClassStatus.InProgress, -14, 84, 24, "Tuesday & Saturday 09:00-11:30"),
            ("MNT-001", "PRG-ROBOTICS", "CLS-ROBOTICS-2026B", "Robotics Summer 2026 - Cohort B", ClassStatus.Open, 7, 105, 20, "Wednesday & Saturday 14:00-16:30"),
            ("MNT-002", "PRG-ROBOTICS", "CLS-ROBOTICS-2026C", "Robotics Fall 2026 - Cohort C", ClassStatus.Open, 21, 119, 22, "Every Thursday 18:00-20:30"),
            ("MNT-003", "PRG-ROBOTICS", "CLS-ROBOTICS-2026D", "Robotics Winter 2026 - Cohort D", ClassStatus.Draft, 35, 133, 18, "Every Monday 09:00-11:30"),
            ("MNT-002", "PRG-IOT", "CLS-IOT-2026A", "IoT Sensors Spring 2026 - Cohort A", ClassStatus.InProgress, -10, 90, 22, "Every Tuesday 18:00-20:30"),
            ("MNT-002", "PRG-IOT", "CLS-IOT-2026B", "IoT Cloud Summer 2026 - Cohort B", ClassStatus.Open, 18, 115, 18, "Every Thursday 19:00-21:30"),
            ("MNT-003", "PRG-WEBDEV", "CLS-WEBDEV-2026A", "Web Foundations Spring 2026 - Cohort A", ClassStatus.InProgress, -12, 88, 24, "Every Monday 18:30-21:00"),
            ("MNT-003", "PRG-WEBDEV", "CLS-WEBDEV-2026B", "JavaScript Bootcamp Summer 2026 - Cohort B", ClassStatus.Open, 20, 118, 20, "Every Wednesday 18:30-21:00"),
            ("MNT-004", "PRG-GAMEDEV", "CLS-GAMEDEV-2026A", "Game Design Spring 2026 - Cohort A", ClassStatus.InProgress, -8, 92, 20, "Every Friday 15:00-18:00"),
            ("MNT-004", "PRG-GAMEDEV", "CLS-GAMEDEV-2026B", "Unity Prototype Summer 2026 - Cohort B", ClassStatus.Open, 25, 125, 16, "Every Saturday 13:00-16:00"),
            ("MNT-005", "PRG-AIBASIC", "CLS-AIBASIC-2026A", "AI Basics Spring 2026 - Cohort A", ClassStatus.InProgress, -6, 94, 26, "Every Tuesday 16:00-18:30"),
            ("MNT-005", "PRG-AIBASIC", "CLS-AIBASIC-2026B", "Machine Learning Intro Summer 2026 - Cohort B", ClassStatus.Open, 22, 122, 22, "Every Sunday 09:00-11:30"),
            ("MNT-006", "PRG-3DDESIGN", "CLS-3DDESIGN-2026A", "3D Modeling Spring 2026 - Cohort A", ClassStatus.InProgress, -9, 91, 18, "Every Thursday 14:00-17:00"),
            ("MNT-006", "PRG-3DDESIGN", "CLS-3DDESIGN-2026B", "3D Animation Summer 2026 - Cohort B", ClassStatus.Open, 19, 119, 15, "Every Saturday 10:00-13:00")
        };

        var classesToAdd = new List<Class>();

        foreach (var definition in classDefinitions)
        {
            var existingClass = await _unitOfWork.Classes.FirstOrDefaultAsync(c => c.Code == definition.Code);
            if (existingClass != null)
            {
                continue;
            }

            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == definition.MentorCode);
            var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == definition.ProgramCode);

            if (mentor == null || program == null)
            {
                _loggerService.LogWarning(
                    "Skipping class {ClassCode}: mentor {MentorCode} or program {ProgramCode} not found.",
                    definition.Code,
                    definition.MentorCode,
                    definition.ProgramCode);
                continue;
            }

            classesToAdd.Add(new Class
            {
                Id = Guid.NewGuid(),
                Code = definition.Code,
                Name = definition.Name,
                ProgramId = program.Id,
                MentorId = mentor.Id,
                StartDate = seedTime.AddDays(definition.StartDaysOffset),
                EndDate = seedTime.AddDays(definition.EndDaysOffset),
                MaxCapacity = definition.MaxCapacity,
                Status = definition.Status,
                MinHoursBeforeAssignmentJoin = 48,
                ScheduleSummary = definition.ScheduleSummary,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false
            });
        }

        if (classesToAdd.Count == 0)
        {
            _loggerService.LogInformation("Mentor classes already seeded, skipping");
            return;
        }

        await _unitOfWork.Classes.AddRangeAsync(classesToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed mentor classes — {Count} class(es) created.",
            classesToAdd.Count);
    }
}

