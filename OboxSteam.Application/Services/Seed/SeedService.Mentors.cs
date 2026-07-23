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

    private async Task SeedMentorProfilesAsync()
    {
        _loggerService.LogInformation("Starting seed mentor profiles");

        var profileDefinitions = new List<(
            string MentorCode,
            string Title,
            string Organization,
            string Bio,
            string Achievements,
            string LinkedInUrl)>
        {
            (
                "MNT-001",
                "Senior Robotics Coach",
                "OboxSTEAM Robotics Lab",
                "Hands-on robotics mentor helping students build, code, and compete with autonomous robots.",
                "Led multiple regional robotics competition teams; National STEM Educator Award nominee.",
                "https://www.linkedin.com/in/john-mentor-oboxsteam"
            ),
            (
                "MNT-002",
                "IoT & Embedded Systems Mentor",
                "OboxSTEAM Connected Devices Studio",
                "Guides students through sensor networks, microcontrollers, and cloud-connected IoT projects.",
                "Built production IoT curricula used across OboxSTEAM cohorts; 8+ years industry experience.",
                "https://www.linkedin.com/in/sarah-mentor-oboxsteam"
            ),
            (
                "MNT-003",
                "Full-Stack Web Development Mentor",
                "OboxSTEAM Web Academy",
                "Teaches modern web foundations and JavaScript bootcamps with project-based learning.",
                "Shipped 30+ student portfolio sites; former frontend engineer turned STEAM educator.",
                "https://www.linkedin.com/in/michael-mentor-oboxsteam"
            ),
            (
                "MNT-004",
                "Game Design & Unity Mentor",
                "OboxSTEAM Game Lab",
                "Mentors students from game design principles through Unity prototyping and playtesting.",
                "Published indie prototypes with student teams; Unity Certified Instructor.",
                "https://www.linkedin.com/in/emily-mentor-oboxsteam"
            ),
            (
                "MNT-005",
                "AI & Machine Learning Mentor",
                "OboxSTEAM AI Studio",
                "Introduces students to AI basics and practical machine learning with accessible tools.",
                "Designed introductory ML pathways for teens; research collaborator on education AI.",
                "https://www.linkedin.com/in/chris-mentor-oboxsteam"
            ),
            (
                "MNT-006",
                "3D Modeling & Animation Mentor",
                "OboxSTEAM Creative Studio",
                "Helps students master 3D modeling, texturing, and animation for STEAM storytelling projects.",
                "Portfolio of student animation showcases; industry experience in digital content creation.",
                "https://www.linkedin.com/in/lisa-mentor-oboxsteam"
            ),
        };

        var profilesToAdd = new List<MentorProfile>();
        var seedTime = DateTime.UtcNow;

        foreach (var definition in profileDefinitions)
        {
            var mentor = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Code == definition.MentorCode);
            if (mentor == null || mentor.Role != RoleType.Mentor)
            {
                _loggerService.LogWarning(
                    "Skipping mentor profile for {MentorCode}: mentor user not found.",
                    definition.MentorCode);
                continue;
            }

            var existing = await _unitOfWork.MentorProfiles.FirstOrDefaultAsync(
                mp => mp.MentorId == mentor.Id && !mp.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            profilesToAdd.Add(new MentorProfile
            {
                Id = Guid.NewGuid(),
                MentorId = mentor.Id,
                Title = definition.Title,
                Organization = definition.Organization,
                Bio = definition.Bio,
                Achievements = definition.Achievements,
                LinkedInUrl = definition.LinkedInUrl,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        if (profilesToAdd.Count == 0)
        {
            _loggerService.LogInformation("Mentor profiles already seeded, skipping");
            return;
        }

        await _unitOfWork.MentorProfiles.AddRangeAsync(profilesToAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed mentor profiles — {Count} profile(s) created.",
            profilesToAdd.Count);
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

