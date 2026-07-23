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

    /// <summary>
    /// Seeds mentor skill tags aligned with the programs/classes each mentor teaches.
    /// Runs after <see cref="SeedSkillsAsync"/> and <see cref="SeedMentorClassesAsync"/>.
    /// </summary>
    private async Task SeedMentorSkillsAsync()
    {
        _loggerService.LogInformation("Starting seed mentor skills");

        // Mentor → skills tied to the programs they teach in SeedMentorClassesAsync.
        var skillDefinitions = new List<(
            string MentorCode,
            string SkillCode,
            SkillProficiencyLevel Level,
            string Notes)>
        {
            // MNT-001 — Robotics (CLS-ROBOTICS-2026A/B)
            ("MNT-001", "SKL-TECH-ROBOTICS-IOT", SkillProficiencyLevel.Expert,
                "Primary skill for Robotics Spring/Summer cohorts (CLS-ROBOTICS-2026A/B)."),
            ("MNT-001", "SKL-TECH-PROG-PYTHON", SkillProficiencyLevel.Advanced,
                "Robot control scripts and autonomous challenge logic."),
            ("MNT-001", "SKL-ENG-PROTOTYPE", SkillProficiencyLevel.Expert,
                "Physical robot builds and chassis prototyping in Robotics Lab."),
            ("MNT-001", "SKL-ENG-DESIGN", SkillProficiencyLevel.Advanced,
                "Engineering design cycles for competition robots."),
            ("MNT-001", "SKL-ENG-TEST-ITERATE", SkillProficiencyLevel.Advanced,
                "Competition prep: test, tune, and iterate robot behavior."),
            ("MNT-001", "SKL-SOFT-COLLAB", SkillProficiencyLevel.Advanced,
                "Team roles and pit-crew collaboration during robotics cohorts."),

            // MNT-002 — Robotics + IoT (CLS-ROBOTICS-2026C, CLS-IOT-2026A/B)
            ("MNT-002", "SKL-TECH-ROBOTICS-IOT", SkillProficiencyLevel.Expert,
                "Core for Robotics Fall and IoT Sensors/Cloud cohorts."),
            ("MNT-002", "SKL-TECH-PROG-PYTHON", SkillProficiencyLevel.Advanced,
                "Sensor pipelines and embedded Python for IoT projects."),
            ("MNT-002", "SKL-TECH-DATA-DB", SkillProficiencyLevel.Advanced,
                "Cloud IoT data handling in CLS-IOT-2026B."),
            ("MNT-002", "SKL-ENG-SYSTEMS", SkillProficiencyLevel.Expert,
                "Sensor–microcontroller–cloud system architecture."),
            ("MNT-002", "SKL-SCI-DATA", SkillProficiencyLevel.Advanced,
                "Collecting and analyzing IoT sensor telemetry."),
            ("MNT-002", "SKL-SOFT-CRITICAL", SkillProficiencyLevel.Advanced,
                "Debugging connected-device failures with students."),

            // MNT-003 — Robotics + WebDev (CLS-ROBOTICS-2026D, CLS-WEBDEV-2026A/B)
            ("MNT-003", "SKL-TECH-PROG-JS", SkillProficiencyLevel.Expert,
                "JavaScript Bootcamp (CLS-WEBDEV-2026B) and interactive web labs."),
            ("MNT-003", "SKL-ART-UXUI", SkillProficiencyLevel.Advanced,
                "UI/UX basics in Web Foundations (CLS-WEBDEV-2026A)."),
            ("MNT-003", "SKL-TECH-COMP-THINK", SkillProficiencyLevel.Expert,
                "Computational thinking across web and robotics draft cohorts."),
            ("MNT-003", "SKL-TECH-DIGITAL-LIT", SkillProficiencyLevel.Advanced,
                "Safe, effective use of web tooling in student projects."),
            ("MNT-003", "SKL-SOFT-COMM", SkillProficiencyLevel.Advanced,
                "Demo days and portfolio walkthroughs for web cohorts."),
            ("MNT-003", "SKL-TECH-ROBOTICS-IOT", SkillProficiencyLevel.Intermediate,
                "Supporting mentor for Robotics Winter draft cohort (CLS-ROBOTICS-2026D)."),

            // MNT-004 — GameDev (CLS-GAMEDEV-2026A/B)
            ("MNT-004", "SKL-TECH-SOFTWARE", SkillProficiencyLevel.Expert,
                "Unity and game-tooling proficiency for Game Lab cohorts."),
            ("MNT-004", "SKL-ART-VISUAL", SkillProficiencyLevel.Advanced,
                "Visual design for Game Design Spring (CLS-GAMEDEV-2026A)."),
            ("MNT-004", "SKL-ART-STORY", SkillProficiencyLevel.Advanced,
                "Narrative and level storytelling in game projects."),
            ("MNT-004", "SKL-ENG-PROTOTYPE", SkillProficiencyLevel.Expert,
                "Unity Prototype Summer (CLS-GAMEDEV-2026B) rapid iteration."),
            ("MNT-004", "SKL-SOFT-CREATIVE", SkillProficiencyLevel.Expert,
                "Creative direction and playtest feedback loops."),
            ("MNT-004", "SKL-MATH-LOGIC", SkillProficiencyLevel.Advanced,
                "Game mechanics, state machines, and scoring logic."),

            // MNT-005 — AI Basics (CLS-AIBASIC-2026A/B)
            ("MNT-005", "SKL-TECH-PROG-PYTHON", SkillProficiencyLevel.Expert,
                "Python notebooks for AI Basics and ML Intro cohorts."),
            ("MNT-005", "SKL-TECH-DATA-DB", SkillProficiencyLevel.Advanced,
                "Dataset prep and feature handling in ML Intro (CLS-AIBASIC-2026B)."),
            ("MNT-005", "SKL-MATH-STATS", SkillProficiencyLevel.Expert,
                "Probability and evaluation metrics for beginner ML."),
            ("MNT-005", "SKL-MATH-MODEL", SkillProficiencyLevel.Advanced,
                "Simple predictive models mapped to real student problems."),
            ("MNT-005", "SKL-SCI-REASONING", SkillProficiencyLevel.Advanced,
                "Interpreting model results with scientific reasoning."),
            ("MNT-005", "SKL-SOFT-CRITICAL", SkillProficiencyLevel.Expert,
                "Ethics and critical evaluation of AI outputs with teens."),

            // MNT-006 — 3D Design (CLS-3DDESIGN-2026A/B)
            ("MNT-006", "SKL-TECH-SOFTWARE", SkillProficiencyLevel.Expert,
                "3D tooling for Modeling and Animation cohorts."),
            ("MNT-006", "SKL-ART-VISUAL", SkillProficiencyLevel.Expert,
                "Composition, lighting, and visual polish in 3D Studio."),
            ("MNT-006", "SKL-ART-AESTHETIC", SkillProficiencyLevel.Advanced,
                "Aesthetic critique for student animation showcases."),
            ("MNT-006", "SKL-ENG-DRAWING", SkillProficiencyLevel.Advanced,
                "Technical drawing literacy before modeling (CLS-3DDESIGN-2026A)."),
            ("MNT-006", "SKL-MATH-MEASURE", SkillProficiencyLevel.Advanced,
                "Scale, proportion, and spatial geometry in 3D scenes."),
            ("MNT-006", "SKL-SOFT-CREATIVE", SkillProficiencyLevel.Expert,
                "Creative storytelling through 3D animation (CLS-3DDESIGN-2026B)."),
        };

        var skills = await _unitOfWork.Skills.GetAllAsync(s => !s.IsDeleted);
        var skillsByCode = skills.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        var mentors = await _unitOfWork.Users.GetAllAsync(
            u => u.Role == RoleType.Mentor && !u.IsDeleted);
        var mentorsByCode = mentors.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

        var existingMentorSkills = await _unitOfWork.MentorSkills.GetAllAsync(ms => !ms.IsDeleted);
        var existingPairs = existingMentorSkills
            .Select(ms => (ms.MentorId, ms.SkillId))
            .ToHashSet();

        var toAdd = new List<MentorSkill>();
        var seedTime = DateTime.UtcNow;

        foreach (var definition in skillDefinitions)
        {
            if (!mentorsByCode.TryGetValue(definition.MentorCode, out var mentor))
            {
                _loggerService.LogWarning(
                    "Skipping mentor skill: mentor {MentorCode} not found.",
                    definition.MentorCode);
                continue;
            }

            if (!skillsByCode.TryGetValue(definition.SkillCode, out var skill))
            {
                _loggerService.LogWarning(
                    "Skipping mentor skill: skill {SkillCode} not found for {MentorCode}.",
                    definition.SkillCode,
                    definition.MentorCode);
                continue;
            }

            if (existingPairs.Contains((mentor.Id, skill.Id)))
            {
                continue;
            }

            toAdd.Add(new MentorSkill
            {
                Id = Guid.NewGuid(),
                MentorId = mentor.Id,
                SkillId = skill.Id,
                ProficiencyLevel = definition.Level,
                Notes = definition.Notes,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
            existingPairs.Add((mentor.Id, skill.Id));
        }

        if (toAdd.Count == 0)
        {
            _loggerService.LogInformation("Mentor skills already seeded, skipping");
            return;
        }

        await _unitOfWork.MentorSkills.AddRangeAsync(toAdd);
        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation(
            "Finished seed mentor skills — {Count} skill link(s) created.",
            toAdd.Count);
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

