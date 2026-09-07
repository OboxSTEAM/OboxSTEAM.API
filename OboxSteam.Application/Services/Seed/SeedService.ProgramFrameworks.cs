using Microsoft.Extensions.Logging;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    internal const string SeedFrameworkRoboticsName = "Robotics cơ bản";
    internal const string SeedFrameworkCsharpName = "Lập trình C#";
    internal const string SeedFrameworkOpenName = "Open family (no rubric)";
    internal const string SeedFrameworkOtherExpertName = "STEAM curriculum (Minh)";
    internal const string SeedFrameworkQaDraftName = "QA Robotics (Draft)";
    internal const string SeedFrameworkQaPendingName = "QA Robotics (PendingReview)";
    internal const string SeedFrameworkEmptyProgramCode = "PRG-FW-EMPTY";
    internal const string SeedFrameworkPendingProgramCode = "PRG-FW-PENDING";
    internal const string SeedFrameworkEditableProgramCode = "PRG-FW-EDIT";
    internal const string SeedFrameworkNoFrameworkProgramCode = "PRG-FW-NOFW";
    internal const string SeedFrameworkOpenProgramCode = "PRG-FW-OPEN";
    internal const string SeedFrameworkExp2PendingProgramCode = "PRG-FW-EXP2";

    /// <summary>
    /// Backfills specialization tags, degrees, and publications for every seed expert.
    /// There is no ExpertCertificate entity — professional certs live in Achievements text.
    /// Idempotent: only adds missing titles / empty specialization.
    /// </summary>
    private async Task SeedExpertCredentialsAsync()
    {
        _loggerService.LogInformation("Starting seed expert credentials (specialization / degrees / publications)");

        foreach (var profile in SeedExpertCredentialProfiles)
        {
            var expert = await _unitOfWork.Experts.FirstOrDefaultAsync(
                e => e.Code == profile.ExpertCode && !e.IsDeleted);
            if (expert == null)
            {
                _loggerService.LogWarning(
                    "{ExpertCode} missing. Skipping credential seed for that expert.",
                    profile.ExpertCode);
                continue;
            }

            var changed = false;
            if (expert.Specialization == null || expert.Specialization.Length == 0)
            {
                expert.Specialization = profile.Specialization;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(expert.Achievements)
                || !expert.Achievements.Contains(profile.PrimaryCertLabel, StringComparison.Ordinal))
            {
                expert.Achievements = string.IsNullOrWhiteSpace(expert.Achievements)
                    ? profile.Achievements
                    : $"{expert.Achievements}; {profile.Achievements}";
                changed = true;
            }

            if (changed)
            {
                await _unitOfWork.Experts.Update(expert);
            }

            foreach (var degree in profile.Degrees)
            {
                var exists = await _unitOfWork.ExpertDegrees.FirstOrDefaultAsync(
                    d => d.ExpertId == expert.Id && d.Title == degree.Title && !d.IsDeleted);
                if (exists != null)
                {
                    continue;
                }

                await _unitOfWork.ExpertDegrees.AddAsync(new ExpertDegree
                {
                    Id = Guid.NewGuid(),
                    ExpertId = expert.Id,
                    Title = degree.Title,
                    Institution = degree.Institution,
                    Year = degree.Year,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }

            foreach (var publication in profile.Publications)
            {
                var exists = await _unitOfWork.ExpertPublications.FirstOrDefaultAsync(
                    p => p.ExpertId == expert.Id && p.Title == publication.Title && !p.IsDeleted);
                if (exists != null)
                {
                    continue;
                }

                await _unitOfWork.ExpertPublications.AddAsync(new ExpertPublication
                {
                    Id = Guid.NewGuid(),
                    ExpertId = expert.Id,
                    Title = publication.Title,
                    Venue = publication.Venue,
                    Year = publication.Year,
                    Url = publication.Url,
                    CreatedAt = _seedNow,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed expert credentials");
    }

    private static readonly SeedExpertCredentialProfile[] SeedExpertCredentialProfiles =
    [
        new(
            "EXP-001",
            ["Robotics", "Maker education", "Hands-on STEM", "Curriculum review"],
            "Cert: Google for Education Certified Trainer; Cert: FIRST Robotics Mentor Coach",
            "National STEM Educator Award; Cert: Google for Education Certified Trainer; Cert: FIRST Robotics Mentor Coach",
            [
                ("PhD in Robotics Education", "Hanoi University of Science and Technology", 2016),
                ("MSc in Mechatronics", "Hanoi University of Science and Technology", 2012),
                ("BEng in Mechanical Engineering", "Da Nang University of Technology", 2010),
            ],
            [
                ("Hands-on robotics for ages 6-8", "STEAM Education Review", 2023,
                    "https://example.com/oboxsteam/robotics-6-8"),
                ("Maker lab safety scaffolds for primary cohorts", "Asia STEAM Journal", 2022,
                    "https://example.com/oboxsteam/maker-lab-safety"),
            ]),
        new(
            "EXP-002",
            ["STEAM curriculum", "Experiential learning", "Program frameworks"],
            "Cert: IB Educator Certificate (STEAM)",
            "Published 20+ STEAM research papers; Cert: IB Educator Certificate (STEAM)",
            [
                ("PhD in Curriculum Studies", "Vietnam National University", 2014),
                ("MA in Educational Leadership", "University of Education, HCMC", 2009),
            ],
            [
                ("Designing experiential STEAM frameworks", "Curriculum Inquiry Asia", 2024,
                    "https://example.com/oboxsteam/steam-frameworks"),
            ]),
        new(
            "EXP-003",
            ["Web development", "Full-stack", "Youth coding"],
            "Cert: AWS Certified Developer – Associate",
            "10+ years industry experience; Cert: AWS Certified Developer – Associate",
            [
                ("PhD in Computer Science", "Posts and Telecommunications Institute of Technology", 2018),
                ("BSc in Software Engineering", "University of Information Technology", 2011),
            ],
            [
                ("Teaching modern web stacks to young learners", "Tech Education Vietnam", 2023,
                    "https://example.com/oboxsteam/web-youth"),
            ]),
        new(
            "EXP-004",
            ["AI education", "Machine learning", "Data science for students"],
            "Cert: DeepLearning.AI TensorFlow Developer",
            "Led 5 national AI education initiatives; Cert: DeepLearning.AI TensorFlow Developer",
            [
                ("PhD in Artificial Intelligence", "Vietnam AI Institute", 2019),
                ("MSc in Applied Mathematics", "Hanoi University of Science", 2015),
            ],
            [
                ("Introductory AI pathways for secondary students", "AI & Society Education", 2024,
                    "https://example.com/oboxsteam/ai-pathways"),
            ]),
        new(
            "EXP-005",
            ["Mathematics education", "Problem solving", "Puzzle-based learning"],
            "Cert: Cambridge IGCSE Mathematics Trainer",
            "Author of 3 popular math textbooks; Cert: Cambridge IGCSE Mathematics Trainer",
            [
                ("PhD in Mathematics Education", "National University of Education", 2013),
                ("BSc in Mathematics", "Hue University", 2007),
            ],
            [
                ("Puzzle-first math for STEAM cohorts", "Math Teaching Today", 2021,
                    "https://example.com/oboxsteam/puzzle-math"),
            ]),
        new(
            "EXP-006",
            ["Digital arts", "Illustration", "Creative expression"],
            "Cert: Adobe Certified Professional (Illustrator)",
            "Award-winning digital artist; Cert: Adobe Certified Professional (Illustrator)",
            [
                ("MFA in Digital Arts", "University of Fine Arts, HCMC", 2016),
                ("BA in Graphic Design", "University of Fine Arts, Hanoi", 2012),
            ],
            [
                ("Studio critique loops in youth digital art labs", "Creative Minds Review", 2022,
                    "https://example.com/oboxsteam/digital-art-labs"),
            ]),
        new(
            "EXP-007",
            ["Environmental science", "Climate education", "Sustainability"],
            "Cert: UNESCO Climate Change Education Facilitator",
            "UN Youth Climate Ambassador 2023; Cert: UNESCO Climate Change Education Facilitator",
            [
                ("PhD in Environmental Science", "Can Tho University", 2017),
                ("MSc in Ecology", "Hue University of Agriculture and Forestry", 2012),
            ],
            [
                ("Field climate labs for STEAM middle school", "Green Earth Education", 2023,
                    "https://example.com/oboxsteam/climate-labs"),
            ]),
    ];

    private sealed record SeedExpertCredentialProfile(
        string ExpertCode,
        string[] Specialization,
        string PrimaryCertLabel,
        string Achievements,
        (string Title, string Institution, int Year)[] Degrees,
        (string Title, string Venue, int Year, string Url)[] Publications);

    private async Task SeedProgramFrameworksAsync()
    {
        _loggerService.LogInformation("Starting seed program frameworks");

        var expert001 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-001" && !e.IsDeleted);
        var expert002 = await _unitOfWork.Experts.FirstOrDefaultAsync(e => e.Code == "EXP-002" && !e.IsDeleted);
        if (expert001 == null)
        {
            _loggerService.LogWarning("EXP-001 missing. Skipping program framework seed.");
            return;
        }

        var robotics = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkRoboticsName,
            "Hands-on family: at least one Offline lab. Rubric is used at expert review.",
            ProgramCategory.Technology,
            minOfflineSessions: 1,
            requireCapstoneResearchMilestone: null,
            criteria:
            [
                ("Safety and workspace setup", "Students can set up and reset the kit safely.", 10, 1),
                ("Hands-on build quality", "Prototype matches the session goal.", 10, 2),
            ]);

        var qaDraft = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkQaDraftName,
            "Copy of Robotics rules for the Draft QA program. One framework per program.",
            ProgramCategory.Technology,
            minOfflineSessions: 1,
            requireCapstoneResearchMilestone: null,
            criteria:
            [
                ("Safety and workspace setup", "Students can set up and reset the kit safely.", 10, 1),
                ("Hands-on build quality", "Prototype matches the session goal.", 10, 2),
            ]);

        var qaPending = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkQaPendingName,
            "Copy of Robotics rules for the PendingReview QA program. One framework per program.",
            ProgramCategory.Technology,
            minOfflineSessions: 1,
            requireCapstoneResearchMilestone: null,
            criteria:
            [
                ("Safety and workspace setup", "Students can set up and reset the kit safely.", 10, 1),
                ("Hands-on build quality", "Prototype matches the session goal.", 10, 2),
            ]);

        var csharp = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkCsharpName,
            "Live coaching plus a capstone research milestone (IsCapstone).",
            ProgramCategory.Technology,
            minLiveSessions: 1,
            requireCapstoneResearchMilestone: true,
            criteria:
            [
                ("Live coaching quality", "LiveOnline sessions cover the learning outcomes.", 10, 1),
                ("Capstone completeness", "Final research milestone is present and required.", 10, 2),
            ]);

        var openFamily = await EnsureFrameworkAsync(
            expert001.Id,
            SeedFrameworkOpenName,
            "No numeric rules and no rubric — board experts still review after submit.",
            ProgramCategory.Technology,
            criteria: null);

        ProgramFramework? expert002Framework = null;
        if (expert002 != null)
        {
            expert002Framework = await EnsureFrameworkAsync(
                expert002.Id,
                SeedFrameworkOtherExpertName,
                "Owned by EXP-002 so Expert list isolation can be checked.",
                ProgramCategory.Technology,
                minModules: 1,
                criteria:
                [
                    ("Curriculum coverage", "Minimum module count is met.", 5, 1),
                ]);
        }

        await AttachFrameworkIfUnsetAsync("PRG-ROBOTICS", robotics.Id);

        var editProgram = await EnsureQaProgramAsync(
            SeedFrameworkEditableProgramCode,
            "QA — Draft, Robotics, ready to submit",
            "Has one Offline lab so Robotics MinOfflineSessions pre-check passes. Manager submit-review.",
            qaDraft.Id,
            ProgramStatus.Draft);
        var emptyProgram = await EnsureQaProgramAsync(
            SeedFrameworkEmptyProgramCode,
            "QA — Draft, C# framework, pre-check fail",
            "No LiveOnline and no capstone. Submit-review must 400.",
            csharp.Id,
            ProgramStatus.Draft);
        var pendingProgram = await EnsureQaProgramAsync(
            SeedFrameworkPendingProgramCode,
            "QA — PendingReview, Robotics",
            "Already in expert queue. EXP-001 (framework owner) approve-review (2 scores) or request-changes. Board members may view only.",
            qaPending.Id,
            ProgramStatus.PendingReview);
        var noFrameworkProgram = await EnsureQaProgramAsync(
            SeedFrameworkNoFrameworkProgramCode,
            "QA — Draft, no framework",
            "Submit-review still goes to PendingReview. No framework: EXP-001 on the board reviews free-form (any board expert with a login may decide).",
            frameworkId: null,
            status: ProgramStatus.Draft);
        var openProgram = await EnsureQaProgramAsync(
            SeedFrameworkOpenProgramCode,
            "QA — Draft, Open family (no rubric)",
            "Submit-review goes to PendingReview. EXP-001 (framework owner) approve-review with no scores. Board members may view only.",
            openFamily.Id,
            ProgramStatus.Draft);

        if (expert002Framework != null && expert002 != null)
        {
            var exp2Program = await EnsureQaProgramAsync(
                SeedFrameworkExp2PendingProgramCode,
                "QA — PendingReview, EXP-002 framework",
                "Owner expert2@oboxsteam.com decides. EXP-001 does not own this framework and is not on this board, so they should not see this queue item.",
                expert002Framework.Id,
                ProgramStatus.PendingReview);
            await EnsureQaTheoryModuleAsync(exp2Program, "EXP2");
            await EnsureExpertOnProgramBoardAsync(expert002, exp2Program.Id, "Reviewer");
        }

        await EnsureQaOfflineLabAsync(editProgram, "EDIT");
        await EnsureQaOfflineLabAsync(pendingProgram, "PEND");

        await EnsureExpertOnProgramBoardAsync(expert001, editProgram.Id, "Reviewer");
        await EnsureExpertOnProgramBoardAsync(expert001, emptyProgram.Id, "Reviewer");
        await EnsureExpertOnProgramBoardAsync(expert001, pendingProgram.Id, "Reviewer");
        await EnsureExpertOnProgramBoardAsync(expert001, noFrameworkProgram.Id, "Reviewer");
        await EnsureExpertOnProgramBoardAsync(expert001, openProgram.Id, "Reviewer");

        await _unitOfWork.SaveChangesAsync();
        _loggerService.LogInformation("Finished seed program frameworks");
    }

    private async Task<ProgramFramework> EnsureFrameworkAsync(
        Guid expertId,
        string name,
        string description,
        ProgramCategory category,
        int? minModules = null,
        int? minOfflineSessions = null,
        int? minLiveSessions = null,
        bool? requireCapstoneResearchMilestone = null,
        (string Name, string Description, int MaxScore, int DisplayOrder)[]? criteria = null)
    {
        var existing = await _unitOfWork.ProgramFrameworks.FirstOrDefaultAsync(
            f => f.ExpertId == expertId && f.Name == name && !f.IsDeleted);
        if (existing != null)
        {
            await EnsureFrameworkCriteriaAsync(existing.Id, criteria);
            return existing;
        }

        var framework = new ProgramFramework
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Name = name,
            Description = description,
            Category = category,
            MinModules = minModules,
            MinOfflineSessions = minOfflineSessions,
            MinLiveSessions = minLiveSessions,
            RequireCapstoneResearchMilestone = requireCapstoneResearchMilestone,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.ProgramFrameworks.AddAsync(framework);
        await EnsureFrameworkCriteriaAsync(framework.Id, criteria);
        return framework;
    }

    private async Task EnsureFrameworkCriteriaAsync(
        Guid frameworkId,
        (string Name, string Description, int MaxScore, int DisplayOrder)[]? criteria)
    {
        if (criteria == null)
        {
            return;
        }

        foreach (var item in criteria)
        {
            var existing = await _unitOfWork.FrameworkRubricCriteria.FirstOrDefaultAsync(
                c => c.FrameworkId == frameworkId && c.Name == item.Name && !c.IsDeleted);
            if (existing != null)
            {
                continue;
            }

            await _unitOfWork.FrameworkRubricCriteria.AddAsync(new FrameworkRubricCriterion
            {
                Id = Guid.NewGuid(),
                FrameworkId = frameworkId,
                Name = item.Name,
                Description = item.Description,
                MaxScore = item.MaxScore,
                DisplayOrder = item.DisplayOrder,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }
    }

    private async Task AttachFrameworkIfUnsetAsync(string programCode, Guid frameworkId)
    {
        var program = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == programCode && !p.IsDeleted);
        if (program == null || program.FrameworkId.HasValue)
        {
            return;
        }

        program.FrameworkId = frameworkId;
        await _unitOfWork.Programs.Update(program);
    }

    private async Task<Program> EnsureQaProgramAsync(
        string code,
        string name,
        string description,
        Guid? frameworkId,
        ProgramStatus status)
    {
        var existing = await _unitOfWork.Programs.FirstOrDefaultAsync(p => p.Code == code && !p.IsDeleted);
        if (existing != null)
        {
            if (existing.FrameworkId != frameworkId
                || existing.Status != status
                || existing.Name != name
                || existing.Description != description)
            {
                existing.Name = name;
                existing.Description = description;
                existing.FrameworkId = frameworkId;
                existing.Status = status;
                await _unitOfWork.Programs.Update(existing);
            }

            return existing;
        }

        var program = new Program
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            SeriesName = "Framework QA",
            Description = description,
            Level = DifficultyLevel.Beginner,
            Category = ProgramCategory.Technology,
            EstimatedDuration = "n/a",
            SkillsGained = "QA",
            Status = status,
            Price = 0m,
            FrameworkId = frameworkId,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Programs.AddAsync(program);
        return program;
    }

    private async Task EnsureQaOfflineLabAsync(Program program, string suffix)
    {
        var module = await EnsureQaTheoryModuleAsync(program, suffix, ModuleType.Experiential);
        var courseCode = $"CRS-FW-{suffix}";
        var activityCode = $"ACT-FW-{suffix}-OFF";

        var course = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == courseCode && !c.IsDeleted);
        if (course == null)
        {
            course = new Course
            {
                Id = Guid.NewGuid(),
                Code = courseCode,
                ModuleId = module.Id,
                Name = "QA lab",
                Description = "Offline session so MinOfflineSessions pre-check passes.",
                CourseOrder = 1,
                CreatedAt = _seedNow,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            };
            await _unitOfWork.Courses.AddAsync(course);
        }

        var activity = await _unitOfWork.Activities.FirstOrDefaultAsync(a => a.Code == activityCode && !a.IsDeleted);
        if (activity != null)
        {
            return;
        }

        await _unitOfWork.Activities.AddAsync(new Activity
        {
            Id = Guid.NewGuid(),
            Code = activityCode,
            CourseId = course.Id,
            Name = "QA offline lab",
            ActivityType = ActivityType.Offline,
            Description = "Seed Offline activity for Robotics framework pre-check.",
            ActivityOrder = 1,
            DurationMinutes = 90,
            RequireQrCheckin = true,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        });
    }

    private async Task<Module> EnsureQaTheoryModuleAsync(
        Program program,
        string suffix,
        ModuleType moduleType = ModuleType.Theory)
    {
        var moduleCode = $"MOD-FW-{suffix}";
        var existing = await _unitOfWork.Modules.FirstOrDefaultAsync(m => m.Code == moduleCode && !m.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var module = new Module
        {
            Id = Guid.NewGuid(),
            Code = moduleCode,
            ProgramId = program.Id,
            Name = $"QA module {suffix}",
            ModuleType = moduleType,
            ModuleOrder = 1,
            IsMandatory = true,
            CreatedAt = _seedNow,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };
        await _unitOfWork.Modules.AddAsync(module);
        return module;
    }
}
